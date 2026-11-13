using System.Net;
using System.Text.Json;
using Xunit;

namespace Merkato.Tests;

[Collection(MerkatoHttpCollection.Name)]
public sealed class VersionEndpointTests(MerkatoHttpHostFixture factory)
{
    [Fact]
    public async Task GetVersion_ReturnsAnonymousJsonWithRequiredMetadataShape()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        AssertStringProperty(root, "version");
        AssertStringProperty(root, "commitSha");
        AssertStringProperty(root, "buildTimeUtc");
        AssertStringProperty(root, "branch");
    }

    private static void AssertStringProperty(JsonElement root, string name)
    {
        Assert.True(root.TryGetProperty(name, out var value), $"Missing '{name}' in /api/version response.");
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(value.GetString()));
    }
}
