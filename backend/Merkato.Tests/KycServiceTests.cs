using Merkato.Infrastructure.Services;
using Xunit;

namespace Merkato.Tests;

public sealed class KycServiceTests
{
    [Fact]
    public async Task FaydaVerification_FailsClosed_WithoutAuthenticatedProvider()
    {
        var service = new KycService();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            service.VerifyFaydaAsync(42, "FAN-1234-5678-ABC"));

        Assert.Contains("authenticated Fayda provider", exception.Message);
    }
}
