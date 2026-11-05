using System.Net;
using System.Text;
using Merkato.Api.Controllers;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merkato.Tests;

public sealed class AssistantControllerTests
{
    private static MerkatoDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase($"assistant-{Guid.NewGuid():N}")
            .Options;
        return new MerkatoDbContext(options);
    }

    [Fact]
    public async Task Chat_RejectsEmptyOrOversizedMessages()
    {
        using var db = CreateDb();
        var controller = new AssistantController(db, new StubHttpClientFactory(), NullLogger<AssistantController>.Instance);

        var empty = await controller.Chat(new ShoppingAssistantRequest(" "), CancellationToken.None);
        var oversized = await controller.Chat(new ShoppingAssistantRequest(new string('x', 1001)), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(empty);
        Assert.IsType<BadRequestObjectResult>(oversized);
    }

    [Fact]
    public async Task Chat_ReturnsServiceUnavailableWhenProviderKeyIsMissing()
    {
        var previousKey = Environment.GetEnvironmentVariable("MERKATO_AI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("MERKATO_AI_API_KEY", null);
            using var db = CreateDb();
            var controller = new AssistantController(db, new StubHttpClientFactory(), NullLogger<AssistantController>.Instance);

            var result = await controller.Chat(new ShoppingAssistantRequest("Find me a phone"), CancellationToken.None);

            var unavailable = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MERKATO_AI_API_KEY", previousKey);
        }
    }

    [Fact]
    public async Task Chat_UsesConfiguredProviderAndReturnsAssistantMessage()
    {
        var previousKey = Environment.GetEnvironmentVariable("MERKATO_AI_API_KEY");
        var previousEndpoint = Environment.GetEnvironmentVariable("MERKATO_AI_ENDPOINT");
        var previousModel = Environment.GetEnvironmentVariable("MERKATO_AI_MODEL");
        try
        {
            Environment.SetEnvironmentVariable("MERKATO_AI_API_KEY", "unit-test-key");
            Environment.SetEnvironmentVariable("MERKATO_AI_ENDPOINT", "https://assistant.test/v1/chat/completions");
            Environment.SetEnvironmentVariable("MERKATO_AI_MODEL", "test-model");

            using var db = CreateDb();
            var factory = new StubHttpClientFactory(new StubHttpMessageHandler());
            var controller = new AssistantController(db, factory, NullLogger<AssistantController>.Instance);

            var result = await controller.Chat(new ShoppingAssistantRequest("Find a product"), CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
            Assert.Contains("MERKATO", ok.Value!.ToString() ?? string.Empty);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MERKATO_AI_API_KEY", previousKey);
            Environment.SetEnvironmentVariable("MERKATO_AI_ENDPOINT", previousEndpoint);
            Environment.SetEnvironmentVariable("MERKATO_AI_MODEL", previousModel);
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubHttpClientFactory() : this(new StubHttpMessageHandler()) { }
        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"Welcome to MERKATO. I can help you find products.\"}}]}",
                    Encoding.UTF8,
                    "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
