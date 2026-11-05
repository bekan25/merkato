using Merkato.Api.Services;
using Xunit;

namespace Merkato.Tests;

public sealed class EthiopianPhoneNumberTests
{
    [Theory]
    [InlineData("0911000000", "+251911000000")]
    [InlineData("0911 000 000", "+251911000000")]
    [InlineData("+251911000000", "+251911000000")]
    [InlineData("251911000000", "+251911000000")]
    [InlineData("0711000000", "+251711000000")]
    [InlineData("+251711000000", "+251711000000")]
    public void TryNormalize_ConvertsEthiopianMobileNumbersToCanonicalFormat(
        string input, string expected)
    {
        var valid = EthiopianPhoneNumber.TryNormalize(input, out var normalized);

        Assert.True(valid);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789")]
    [InlineData("091100000")]
    [InlineData("+251811000000")]
    [InlineData("not-a-phone")]
    public void TryNormalize_RejectsInvalidOrUnsupportedNumbers(string input)
    {
        Assert.False(EthiopianPhoneNumber.TryNormalize(input, out var normalized));
        Assert.Equal(string.Empty, normalized);
    }
}
