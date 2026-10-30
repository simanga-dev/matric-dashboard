using MatricDasbhoard.Application.Features.PastPapers.Parsing;

namespace MatricDasbhoard.Unit.Tests.Application.PastPapers;

public class PastPaperIdTests
{
    [Fact]
    public void FromKey_IsStableLowercaseHexOf32Characters()
    {
        const string key = "exam_papers/2024 November NSC Examination Papers/Mathematics/Mathematics P1 Nov 2024 Eng.pdf";

        var first = PastPaperId.FromKey(key);
        var second = PastPaperId.FromKey(key);

        Assert.Equal(first, second);
        Assert.Equal(32, first.Length);
        Assert.True(PastPaperId.IsValid(first));
        Assert.NotEqual(first, PastPaperId.FromKey(key + "x"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0123456789abcdef0123456789abcde")]
    [InlineData("0123456789abcdef0123456789abcdef0")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("0123456789abcdef0123456789abcdeg")]
    [InlineData("../../../../etc/passwd/aaaaaaaaaa")]
    public void IsValid_MalformedValue_ReturnsFalse(string? value)
    {
        Assert.False(PastPaperId.IsValid(value));
    }

    [Fact]
    public void IsValid_WellFormedValue_ReturnsTrue()
    {
        Assert.True(PastPaperId.IsValid("0123456789abcdef0123456789abcdef"));
    }
}
