namespace FindThatBook.Server.Matching;

// these are essentially arbitrary; an educated "gut feeling"
public static class RankingScores {
    public const int TitleExactFull = 60;
    public const int TitleExactMain = 54;
    public const int TitleContainsCompleteQuery = 48;
    public const int TitleStrongTokenMinimum = 25;
    public const int TitleStrongTokenMaximum = 45;
    public const int TitleFuzzyTokenMinimum = 10;
    public const int TitleFuzzyTokenMaximum = 30;

    public const int AuthorExactFull = 35;
    public const int AuthorInitialsAndSurname = 32;
    public const int AuthorExactSurname = 24;
    public const int AuthorMultiTokenMinimum = 15;
    public const int AuthorMultiTokenMaximum = 25;
    public const int AuthorFuzzySurnameMinimum = 5;
    public const int AuthorFuzzySurnameMaximum = 12;
    public const int AuthorCanonicalConflict = -50;

    public const int KeywordMaximum = 10;
    public const int YearExact = 5;
    public const int YearWithinOne = 3;
    public const int YearWithinThree = 1;

    public const double InferredEvidenceWeight = 0.70;
}

public static class RankingThresholds {
    public const double MeaningfulExactTitleTokenCoverage = 0.60;
    public const double MeaningfulFuzzyTitleTokenCoverage = 0.60;
    public const int MeaningfulFuzzyTitleTokenCount = 2;
    public const int KeywordOnlyMatchCount = 2;
    public const int DistinctiveKeywordLength = 8;
    public const int FullyInferredCategoryCount = 2;
    public const int FullyInferredMinimumScore = 35;
}