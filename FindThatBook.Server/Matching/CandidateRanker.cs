namespace FindThatBook.Server.Matching;

public sealed class CandidateRanker : ICandidateRanker {

    private static HashSet<string> StopWords { get; } = new(StringComparer.Ordinal) {
        "a", "an", "and", "by", "for", "in", "of", "on", "or", "the", "to", "with"
    };

    private static HashSet<string> CommonSurnames { get; } = new(StringComparer.Ordinal) {
        "adams", "allen", "anderson", "baker", "brown", "clark", "davis", "garcia", "green", "hall",
        "harris", "hill", "jackson", "johnson", "jones", "king", "lee", "lewis", "martin", "martinez",
        "miller", "moore", "nelson", "robinson", "scott", "smith", "taylor", "thomas", "thompson",
        "walker", "white", "williams", "wilson", "wright", "young"
    };

    public IReadOnlyList<RankedCandidate> Rank(
        QueryIntent intent,
        IEnumerable<CandidateRankingInput> candidates) {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Select(candidate => Evaluate(intent, candidate))
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => EvidenceTier(candidate.Evidence))
            .ThenByDescending(candidate => candidate.Evidence.IsCanonicalPrimaryAuthorMatch)
            .ThenByDescending(candidate => candidate.Evidence.AuthorScore)
            .ThenByDescending(candidate => candidate.Evidence.TitleScore)
            .ThenByDescending(candidate => candidate.Evidence.YearKind == YearMatchKind.Exact)
            .ThenByDescending(candidate =>
                candidate.Candidate.RankingMetadata.EditionCount ??
                candidate.Candidate.SearchDocument.EditionCount ??
                0)
            .ThenBy(candidate => candidate.Candidate.SearchDocument.Key ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }

    private static RankedCandidate Evaluate(QueryIntent intent, CandidateRankingInput candidate) {
        QueryField<string>? titleQuery = intent.Title;
        QueryField<string>? authorQuery = intent.Author;
        bool usesAmbiguousFallbackAuthor = false;

        if (ShouldEvaluateFallbackAsSparseEvidence(intent)) {
            QueryField<string> fallbackField = new(intent.OriginalQuery, QueryFieldProvenance.Extracted);
            titleQuery ??= fallbackField;

            if (authorQuery is null) {
                authorQuery = fallbackField;
                usesAmbiguousFallbackAuthor = true;
            }
        }

        string candidateTitle = candidate.RankingMetadata.CanonicalTitle ?? candidate.SearchDocument.Title;
        TitleEvidence title = EvaluateTitle(titleQuery, candidateTitle);

        AuthorEvidence author = EvaluateAuthor(
            authorQuery,
            candidate,
            allowCanonicalConflict: !usesAmbiguousFallbackAuthor);

        KeywordEvidence keywords = EvaluateKeywords(intent.KeywordFields, candidate);
        YearEvidence year = EvaluateYear(intent.Year, candidate.SearchDocument.FirstPublishYear);

        MatchEvidence evidence = new(
            title.Kind,
            title.Score,
            title.IsMeaningful,
            author.Kind,
            author.Score,
            author.IsMeaningful,
            author.IsPrimary,
            author.HasCanonicalConflict,
            author.Source,
            keywords.Matches,
            keywords.Score,
            keywords.HasDistinctiveMatch,
            year.Kind,
            year.Score);

        return new RankedCandidate(candidate, evidence, IsUseful(intent, evidence));
    }

    private static bool ShouldEvaluateFallbackAsSparseEvidence(QueryIntent intent) {
        if (!intent.UsedFallback) {
            return false;
        }

        IReadOnlyList<string> significantTokens = SignificantTokens(
            TextNormalizer.Normalize(intent.OriginalQuery).LooseTokens);

        return significantTokens.Count <= 3;
    }

    private static TitleEvidence EvaluateTitle(QueryField<string>? queryField, string? candidateTitle) {
        if (queryField is null || string.IsNullOrWhiteSpace(queryField.Value) ||
            string.IsNullOrWhiteSpace(candidateTitle)) {
            return TitleEvidence.None;
        }

        NormalizedTitle query = TextNormalizer.NormalizeTitle(queryField.Value);
        NormalizedTitle candidate = TextNormalizer.NormalizeTitle(candidateTitle);

        if (query.Full.Loose.Length == 0 || candidate.Full.Loose.Length == 0) {
            return TitleEvidence.None;
        }

        if (query.Full.Loose == candidate.Full.Loose) {
            return new TitleEvidence(TitleMatchKind.ExactFullTitle,
                ApplyProvenance(RankingScores.TitleExactFull, queryField.Provenance), true);
        }

        if (query.MainTitle.Loose == candidate.MainTitle.Loose) {
            return new TitleEvidence(TitleMatchKind.ExactMainTitle,
                ApplyProvenance(RankingScores.TitleExactMain, queryField.Provenance), true);
        }

        IReadOnlyList<string> queryTokens = SignificantTokens(query.Full.LooseTokens);
        IReadOnlyList<string> candidateTokens = SignificantTokens(candidate.Full.LooseTokens);

        if (ContainsSequence(candidate.Full.LooseTokens, query.Full.LooseTokens)) {
            return new TitleEvidence(TitleMatchKind.ContainsCompleteQuery,
                ApplyProvenance(RankingScores.TitleContainsCompleteQuery, queryField.Provenance), true);
        }

        TokenMatchResult tokenMatch = MatchTokens(queryTokens, candidateTokens);

        if (tokenMatch.ExactMatches > 0) {
            double effectiveMatches = tokenMatch.ExactMatches + tokenMatch.FuzzyMatches * 0.65;
            double overlap = WeightedOverlap(effectiveMatches, queryTokens.Count, candidateTokens.Count);

            int score = Interpolate(RankingScores.TitleStrongTokenMinimum,
                RankingScores.TitleStrongTokenMaximum, overlap);

            bool meaningful = tokenMatch.QueryCoverage >= RankingThresholds.MeaningfulExactTitleTokenCoverage;

            return new TitleEvidence(TitleMatchKind.SignificantTokenOverlap,
                ApplyProvenance(score, queryField.Provenance), meaningful);
        }

        if (tokenMatch.FuzzyMatches > 0) {
            double overlap = WeightedOverlap(tokenMatch.FuzzyMatches, queryTokens.Count, candidateTokens.Count);

            int score = Interpolate(RankingScores.TitleFuzzyTokenMinimum,
                RankingScores.TitleFuzzyTokenMaximum, overlap);

            bool meaningful = tokenMatch.FuzzyMatches >= RankingThresholds.MeaningfulFuzzyTitleTokenCount &&
                              tokenMatch.QueryCoverage >= RankingThresholds.MeaningfulFuzzyTitleTokenCoverage ||
                              queryTokens.Count == 1 && tokenMatch.QueryCoverage == 1;

            return new TitleEvidence(TitleMatchKind.FuzzyTokens,
                ApplyProvenance(score, queryField.Provenance), meaningful);
        }

        return TitleEvidence.None;
    }

    private static AuthorEvidence EvaluateAuthor(
        QueryField<string>? queryField,
        CandidateRankingInput candidate,
        bool allowCanonicalConflict = true) {
        if (queryField is null || string.IsNullOrWhiteSpace(queryField.Value)) {
            return AuthorEvidence.None;
        }

        CandidateRankingMetadata metadata = candidate.RankingMetadata;

        IReadOnlyList<string> authors = metadata.HasCanonicalAuthorData
            ? metadata.CanonicalAuthors
            : candidate.SearchDocument.AuthorName ?? [];

        AuthorEvidenceSource source = metadata.HasCanonicalAuthorData
            ? AuthorEvidenceSource.CanonicalWork
            : AuthorEvidenceSource.SearchResponse;

        AuthorEvidence best = AuthorEvidence.None;

        for (int index = 0; index < authors.Count; index++) {
            AuthorEvidence current = CompareAuthor(
                queryField,
                authors[index],
                index == 0 && metadata.HasCanonicalAuthorData,
                source);

            if (current.Score > best.Score) {
                best = current;
            }
        }

        bool canConflict = allowCanonicalConflict && queryField.Provenance != QueryFieldProvenance.Inferred;

        bool hasNamedCanonicalAuthor = metadata.CanonicalAuthors.Any(author => !string.IsNullOrWhiteSpace(author));

        if (metadata.HasCanonicalAuthorData && metadata.HasCompleteCanonicalAuthorData &&
            hasNamedCanonicalAuthor && !best.IsMeaningful &&
            canConflict) {
            return new AuthorEvidence(AuthorMatchKind.CanonicalConflict,
                RankingScores.AuthorCanonicalConflict,
                false,
                false,
                true,
                AuthorEvidenceSource.CanonicalWork);
        }

        return best;
    }

    private static AuthorEvidence CompareAuthor(
        QueryField<string> queryField,
        string? candidateAuthor,
        bool isPrimary,
        AuthorEvidenceSource source) {
        if (string.IsNullOrWhiteSpace(candidateAuthor)) {
            return AuthorEvidence.None;
        }

        NormalizedText query = TextNormalizer.NormalizeAuthor(queryField.Value);
        NormalizedText candidate = TextNormalizer.NormalizeAuthor(candidateAuthor);

        if (query.Loose.Length == 0 || candidate.Loose.Length == 0) {
            return AuthorEvidence.None;
        }

        if (query.Loose == candidate.Loose) {
            return Author(AuthorMatchKind.ExactFullName, RankingScores.AuthorExactFull, true);
        }

        AuthorParts queryParts = AuthorParts.Create(query.LooseTokens);
        AuthorParts candidateParts = AuthorParts.Create(candidate.LooseTokens);

        if (queryParts.Surname == candidateParts.Surname &&
            queryParts.Initials.Length > 0 &&
            queryParts.Initials == candidateParts.Initials &&
            (queryParts.UsesInitialStyle || candidateParts.UsesInitialStyle)) {
            return Author(AuthorMatchKind.InitialsAndSurname, RankingScores.AuthorInitialsAndSurname, true);
        }

        TokenMatchResult tokenMatch = MatchTokens(query.LooseTokens, candidate.LooseTokens);

        if (tokenMatch.ExactMatches >= 2) {
            double overlap = WeightedOverlap(tokenMatch.ExactMatches, query.LooseTokens.Count,
                candidate.LooseTokens.Count);

            int score = Interpolate(RankingScores.AuthorMultiTokenMinimum,
                RankingScores.AuthorMultiTokenMaximum, overlap);

            return Author(AuthorMatchKind.MultiToken, score, tokenMatch.QueryCoverage >= 0.5);
        }

        if (queryParts.Surname == candidateParts.Surname) {
            bool oneSideUsesSurnameOnly = queryParts.GivenNames.Count == 0 || candidateParts.GivenNames.Count == 0;

            if (!oneSideUsesSurnameOnly) {
                return AuthorEvidence.None;
            }

            bool meaningful = !CommonSurnames.Contains(queryParts.Surname);

            return Author(AuthorMatchKind.ExactSurname, RankingScores.AuthorExactSurname, meaningful);
        }

        if (IsFuzzyTokenMatch(queryParts.Surname, candidateParts.Surname)) {
            int maximumLength = Math.Max(queryParts.Surname.Length, candidateParts.Surname.Length);

            double similarity = 1 - (double)DamerauLevenshtein.Distance(queryParts.Surname, candidateParts.Surname) /
                maximumLength;

            int score = Interpolate(RankingScores.AuthorFuzzySurnameMinimum,
                RankingScores.AuthorFuzzySurnameMaximum, similarity);

            bool oneSideUsesSurnameOnly = queryParts.GivenNames.Count == 0 || candidateParts.GivenNames.Count == 0;
            bool givenNameCorroboration = queryParts.GivenNames.Intersect(candidateParts.GivenNames).Any();

            bool meaningful = !CommonSurnames.Contains(candidateParts.Surname) &&
                              (oneSideUsesSurnameOnly || givenNameCorroboration);

            return Author(AuthorMatchKind.FuzzySurname, score, meaningful);
        }

        return AuthorEvidence.None;

        AuthorEvidence Author(AuthorMatchKind kind, int score, bool meaningful) =>
            new(kind, ApplyProvenance(score, queryField.Provenance), meaningful, isPrimary, false, source);
    }

    private static KeywordEvidence EvaluateKeywords(
        IReadOnlyList<QueryField<string>> keywordFields,
        CandidateRankingInput candidate) {
        if (keywordFields.Count == 0) {
            return KeywordEvidence.None;
        }

        string candidateTitle = candidate.RankingMetadata.CanonicalTitle ?? candidate.SearchDocument.Title;

        HashSet<string> titleTokens = TextNormalizer.NormalizeTitle(candidateTitle).Full.LooseTokens
            .ToHashSet(StringComparer.Ordinal);

        HashSet<string> subjectTokens = candidate.SearchDocument.Subjects
            .Concat(candidate.RankingMetadata.SubjectValues)
            .SelectMany(subject => TextNormalizer.Normalize(subject).LooseTokens)
            .ToHashSet(StringComparer.Ordinal);

        HashSet<string> seen = new(StringComparer.Ordinal);
        List<string> matches = [];
        int score = 0;
        bool distinctive = false;

        foreach (QueryField<string> field in keywordFields) {
            IEnumerable<string> keywords = TextNormalizer.Normalize(field.Value).LooseTokens
                .Where(keyword => !StopWords.Contains(keyword));

            foreach (string keyword in keywords) {
                if (!seen.Add(keyword)) {
                    continue;
                }

                bool titleMatch = titleTokens.Contains(keyword);
                bool subjectMatch = subjectTokens.Contains(keyword);

                if (!titleMatch && !subjectMatch) {
                    continue;
                }

                matches.Add(keyword);
                int baseScore = keyword.Length >= RankingThresholds.DistinctiveKeywordLength ? 3 : 2;
                score += ApplyProvenance(baseScore, field.Provenance);
                distinctive |= keyword.Length >= RankingThresholds.DistinctiveKeywordLength;
            }
        }

        return new KeywordEvidence(matches, Math.Min(score, RankingScores.KeywordMaximum), distinctive);
    }

    private static YearEvidence EvaluateYear(QueryField<long>? queryField, long? candidateYear) {
        if (queryField is null || candidateYear is null or <= 0) {
            return YearEvidence.None;
        }

        long difference = Math.Abs(queryField.Value - candidateYear.Value);

        return difference switch {
            0 => new YearEvidence(YearMatchKind.Exact,
                ApplyProvenance(RankingScores.YearExact, queryField.Provenance)),
            1 => new YearEvidence(YearMatchKind.WithinOneYear,
                ApplyProvenance(RankingScores.YearWithinOne, queryField.Provenance)),
            <= 3 => new YearEvidence(YearMatchKind.WithinThreeYears,
                ApplyProvenance(RankingScores.YearWithinThree, queryField.Provenance)),
            _ => new YearEvidence(YearMatchKind.OutsideTolerance, 0)
        };
    }

    private static bool IsUseful(QueryIntent intent, MatchEvidence evidence) {
        bool hasTitle = HasValue(intent.Title);
        bool hasAuthor = HasValue(intent.Author);
        bool hasKeywords = intent.KeywordFields.Any(keyword => !string.IsNullOrWhiteSpace(keyword.Value));

        bool keywordMatch = evidence.MatchedKeywords.Count >= RankingThresholds.KeywordOnlyMatchCount ||
                            evidence.HasDistinctiveKeywordMatch;

        if (intent.UsedFallback) {
            return evidence.HasMeaningfulTitleMatch ||
                   evidence.HasMeaningfulAuthorMatch ||
                   keywordMatch;
        }

        bool useful = (hasTitle, hasAuthor, hasKeywords) switch {
            (true, true, _) => evidence.HasMeaningfulTitleMatch && !evidence.HasCanonicalAuthorConflict,
            (true, false, _) => evidence.HasMeaningfulTitleMatch,
            (false, true, _) => evidence.HasMeaningfulAuthorMatch && !evidence.HasCanonicalAuthorConflict,
            (false, false, true) => keywordMatch,
            _ => false
        };

        if (!useful || !AllPopulatedFieldsAreInferred(intent)) {
            return useful;
        }

        int meaningfulCategories = (evidence.HasMeaningfulTitleMatch ? 1 : 0) +
                                   (evidence.HasMeaningfulAuthorMatch ? 1 : 0) +
                                   (evidence.MatchedKeywords.Count > 0 ? 1 : 0) +
                                   (evidence.YearScore > 0 ? 1 : 0);

        return meaningfulCategories >= RankingThresholds.FullyInferredCategoryCount &&
               evidence.TotalScore >= RankingThresholds.FullyInferredMinimumScore;
    }

    private static bool AllPopulatedFieldsAreInferred(QueryIntent intent) {
        List<QueryFieldProvenance> values = [];

        if (intent.Title is not null) {
            values.Add(intent.Title.Provenance);
        }

        if (intent.Author is not null) {
            values.Add(intent.Author.Provenance);
        }

        values.AddRange(intent.KeywordFields.Select(keyword => keyword.Provenance));

        if (intent.Year is not null) {
            values.Add(intent.Year.Provenance);
        }

        return values.Count > 0 && values.All(value => value == QueryFieldProvenance.Inferred);
    }

    private static bool HasValue(QueryField<string>? field) =>
        field is not null && !string.IsNullOrWhiteSpace(field.Value);

    private static int EvidenceTier(MatchEvidence evidence) {
        int titleTier = evidence.TitleKind switch {
            TitleMatchKind.ExactFullTitle => 5,
            TitleMatchKind.ExactMainTitle => 4,
            TitleMatchKind.ContainsCompleteQuery => 3,
            TitleMatchKind.SignificantTokenOverlap => 2,
            TitleMatchKind.FuzzyTokens => 1,
            _ => 0
        };

        int authorTier = evidence.AuthorKind switch {
            AuthorMatchKind.ExactFullName => 5,
            AuthorMatchKind.InitialsAndSurname => 4,
            AuthorMatchKind.ExactSurname => 3,
            AuthorMatchKind.MultiToken => 2,
            AuthorMatchKind.FuzzySurname => 1,
            _ => 0
        };

        return Math.Max(titleTier, authorTier);
    }

    private static IReadOnlyList<string> SignificantTokens(IReadOnlyList<string> tokens) {
        string[] significant = tokens.Where(token => !StopWords.Contains(token)).ToArray();

        return significant.Length > 0 ? significant : tokens;
    }

    private static bool ContainsSequence(IReadOnlyList<string> candidate, IReadOnlyList<string> query) {
        if (query.Count == 0 || query.Count > candidate.Count) {
            return false;
        }

        for (int start = 0; start <= candidate.Count - query.Count; start++) {
            bool matches = true;

            for (int offset = 0; offset < query.Count; offset++) {
                if (candidate[start + offset] != query[offset]) {
                    matches = false;

                    break;
                }
            }

            if (matches) {
                return true;
            }
        }

        return false;
    }

    private static TokenMatchResult MatchTokens(
        IReadOnlyList<string> queryTokens,
        IReadOnlyList<string> candidateTokens) {
        List<string> unmatchedCandidates = candidateTokens.ToList();
        int exact = 0;
        List<string> unmatchedQueries = [];

        foreach (string queryToken in queryTokens) {
            int index = unmatchedCandidates.FindIndex(candidate => candidate == queryToken);

            if (index >= 0) {
                exact++;
                unmatchedCandidates.RemoveAt(index);
            }
            else {
                unmatchedQueries.Add(queryToken);
            }
        }

        int fuzzy = 0;

        foreach (string queryToken in unmatchedQueries) {
            int bestIndex = -1;
            int bestDistance = int.MaxValue;

            for (int index = 0; index < unmatchedCandidates.Count; index++) {
                string candidateToken = unmatchedCandidates[index];
                int distance = DamerauLevenshtein.Distance(queryToken, candidateToken);

                if (distance < bestDistance && IsFuzzyTokenMatch(queryToken, candidateToken)) {
                    bestIndex = index;
                    bestDistance = distance;
                }
            }

            if (bestIndex >= 0) {
                fuzzy++;
                unmatchedCandidates.RemoveAt(bestIndex);
            }
        }

        int totalMatches = exact + fuzzy;
        double queryCoverage = queryTokens.Count == 0 ? 0 : (double)totalMatches / queryTokens.Count;

        return new TokenMatchResult(exact, fuzzy, queryCoverage);
    }

    private static bool IsFuzzyTokenMatch(string left, string right) {
        int maximumLength = Math.Max(left.Length, right.Length);

        int allowedDistance = maximumLength switch {
            <= 3 => 0,
            <= 7 => 1,
            _ => 2
        };

        return DamerauLevenshtein.Distance(left, right) <= allowedDistance;
    }

    private static double WeightedOverlap(double matches, int queryCount, int candidateCount) {
        if (matches == 0 || queryCount == 0 || candidateCount == 0) {
            return 0;
        }

        double queryCoverage = matches / queryCount;
        double candidatePrecision = matches / candidateCount;

        return queryCoverage * 0.75 + candidatePrecision * 0.25;
    }

    private static int Interpolate(int minimum, int maximum, double ratio) =>
        minimum + (int)Math.Round((maximum - minimum) * Math.Clamp(ratio, 0, 1),
            MidpointRounding.AwayFromZero);

    private static int ApplyProvenance(int score, QueryFieldProvenance provenance) =>
        provenance == QueryFieldProvenance.Inferred
            ? (int)Math.Round(score * RankingScores.InferredEvidenceWeight, MidpointRounding.AwayFromZero)
            : score;

    private sealed record TitleEvidence(TitleMatchKind Kind, int Score, bool IsMeaningful) {
        public static TitleEvidence None { get; } = new(TitleMatchKind.None, 0, false);
    }

    private sealed record AuthorEvidence(
        AuthorMatchKind Kind,
        int Score,
        bool IsMeaningful,
        bool IsPrimary,
        bool HasCanonicalConflict,
        AuthorEvidenceSource Source) {
        public static AuthorEvidence None { get; } = new(
            AuthorMatchKind.None,
            0,
            false,
            false,
            false,
            AuthorEvidenceSource.None);
    }

    private sealed record KeywordEvidence(
        IReadOnlyList<string> Matches,
        int Score,
        bool HasDistinctiveMatch) {
        public static KeywordEvidence None { get; } = new([], 0, false);
    }

    private sealed record YearEvidence(YearMatchKind Kind, int Score) {
        public static YearEvidence None { get; } = new(YearMatchKind.None, 0);
    }

    private sealed record TokenMatchResult(int ExactMatches, int FuzzyMatches, double QueryCoverage);

    private sealed record AuthorParts(
        IReadOnlyList<string> GivenNames,
        string Surname,
        string Initials,
        bool UsesInitialStyle) {
        public static AuthorParts Create(IReadOnlyList<string> tokens) {
            if (tokens.Count == 0) {
                return new AuthorParts([], string.Empty, string.Empty, false);
            }

            string[] givenNames = tokens.Take(tokens.Count - 1).ToArray();

            string initials = givenNames.Length == 1 && givenNames[0].Length <= 3
                ? givenNames[0]
                : string.Concat(givenNames.Select(name => name[0]));

            bool usesInitialStyle = givenNames.Length > 0 &&
                                    (givenNames.All(name => name.Length == 1) ||
                                     givenNames.Length == 1 && givenNames[0].Length <= 3);

            return new AuthorParts(givenNames, tokens[^1], initials, usesInitialStyle);
        }
    }
}