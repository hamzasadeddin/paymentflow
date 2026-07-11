using PaymentFlow.Domain.Common;
using Xunit;

namespace PaymentFlow.Domain.Tests;

public class MaskingUtilitiesTests
{
    [Theory]
    [InlineData("12345678", "****5678")]
    [InlineData("GB29NWBK60161331926819", "******************6819")]
    [InlineData("1234", "****")]
    [InlineData("12", "**")]
    public void MaskAccountNumber_masks_all_but_last_four(string input, string expected)
        => Assert.Equal(expected, MaskingUtilities.MaskAccountNumber(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskAccountNumber_returns_empty_for_blank(string? input)
        => Assert.Equal(string.Empty, MaskingUtilities.MaskAccountNumber(input));
}
