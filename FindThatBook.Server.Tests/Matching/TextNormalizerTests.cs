using FindThatBook.Server.Matching;
using Xunit;

namespace FindThatBook.Server.Tests.Matching;

public class TextNormalizerTests {
    [Fact]
    public void Normalize_LowercasesRemovesPunctuationAndCollapsesWhitespace() {
        NormalizedText result = TextNormalizer.Normalize("  The   SONG,\tof Achilles!  ");

        Assert.Equal("the song of achilles", result.Strict);
        Assert.Equal(["the", "song", "of", "achilles"], result.StrictTokens);
    }

    [Fact]
    public void Normalize_KeepsDiacriticsStrictlyAndRemovesThemLoosely() {
        NormalizedText result = TextNormalizer.Normalize("L’Étranger");

        Assert.Equal("létranger", result.Strict);
        Assert.Equal("letranger", result.Loose);
        Assert.Equal("L’Étranger", result.Original);
    }

    [Fact]
    public void Normalize_ReplacesHyphensWithoutJoiningWords() {
        NormalizedText result = TextNormalizer.Normalize("Catch-22");

        Assert.Equal("catch 22", result.Strict);
    }

    [Theory]
    [InlineData("J. R. R. Tolkien")]
    [InlineData("J.R.R. Tolkien")]
    [InlineData("JRR Tolkien")]
    public void NormalizeAuthor_ProducesTheSameLooseFormForInitialVariants(string author) {
        NormalizedText result = TextNormalizer.NormalizeAuthor(author);

        Assert.Equal("jrr tolkien", result.Loose);
    }

    [Theory]
    [InlineData("jk rowling")]
    [InlineData("JK rowling")]
    [InlineData("JK. Rowling")]
    [InlineData("J K Rowling")]
    [InlineData("J. K. Rowling")]
    [InlineData("j.k. rowling")]
    public void EquivalentAuthorQuery_ReturnsTheCanonicalCandidate(string query) {
        string[] candidates = ["Robert Galbraith", "J.K. Rowling", "Joanne Rowling"];

        string? result = candidates.FirstOrDefault(candidate =>
            TextNormalizer.AreEquivalentAuthors(query, candidate));

        Assert.Equal("J.K. Rowling", result);
    }

    [Theory]
    [InlineData("Gabriel García Márquez", "gabriel garcia marquez")]
    [InlineData("URSULA K. LE GUIN", "ursula k le guin")]
    [InlineData("J.R.R. Tolkien", "j r r tolkien")]
    public void AreEquivalentAuthors_HandlesDiacriticsCaseAndInitials(string left, string right) {
        Assert.True(TextNormalizer.AreEquivalentAuthors(left, right));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(null, "J.K. Rowling")]
    [InlineData("J.K. Rowling", null)]
    [InlineData("Joanne Rowling", "J.K. Rowling")]
    [InlineData("J.R.R. Tolkien", "J.K. Rowling")]
    public void AreEquivalentAuthors_RejectsMissingOrDifferentNames(string? left, string? right) {
        Assert.False(TextNormalizer.AreEquivalentAuthors(left, right));
    }

    [Fact]
    public void NormalizeTitle_SeparatesMainTitleAndSubtitle() {
        NormalizedTitle result = TextNormalizer.NormalizeTitle("The Hobbit: There and Back Again");

        Assert.Equal("the hobbit there and back again", result.Full.Strict);
        Assert.Equal("the hobbit", result.MainTitle.Strict);
        Assert.Equal("there and back again", result.Subtitle?.Strict);
    }

    [Fact]
    public void NormalizeTitle_RecognizesADashSubtitle() {
        NormalizedTitle result = TextNormalizer.NormalizeTitle("Dune — Deluxe Edition");

        Assert.Equal("dune", result.MainTitle.Strict);
        Assert.Equal("deluxe edition", result.Subtitle?.Strict);
    }

    [Fact]
    public void Normalize_HandlesMissingText() {
        NormalizedText result = TextNormalizer.Normalize(null);

        Assert.Equal(string.Empty, result.Strict);
        Assert.Equal(string.Empty, result.Loose);
        Assert.Empty(result.StrictTokens);
    }
}