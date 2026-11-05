using System.Net.Http.Headers;
using System.Text.Json;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

public sealed record ShoppingAssistantRequest(string Message);

[ApiController]
[Route("api/assistant")]
[EnableRateLimiting("assistant")]
public sealed class AssistantController : ControllerBase
{
    private readonly MerkatoDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AssistantController> _logger;

    public AssistantController(
        MerkatoDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<AssistantController> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(
        [FromBody] ShoppingAssistantRequest request,
        CancellationToken cancellationToken)
    {
        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message) || message.Length > 1000)
        {
            return BadRequest(new
            {
                message = "Enter a shopping question between 1 and 1000 characters."
            });
        }

        var apiKey = Environment.GetEnvironmentVariable("MERKATO_AI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "The shopping assistant is not configured yet."
            });
        }

        var endpointValue = Environment.GetEnvironmentVariable("MERKATO_AI_ENDPOINT")
            ?? "https://api.openai.com/v1/chat/completions";
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "The shopping assistant endpoint configuration is invalid."
            });
        }

        var model = Environment.GetEnvironmentVariable("MERKATO_AI_MODEL") ?? "gpt-4o-mini";
        var term = message.ToLowerInvariant();
        var products = await _db.Products
            .AsNoTracking()
            .Where(product => product.IsActive &&
                (product.Name.ToLower().Contains(term) ||
                 product.Category.ToLower().Contains(term) ||
                 product.Location.ToLower().Contains(term)))
            .OrderByDescending(product => product.CreatedAt)
            .Take(8)
            .Select(product => new
            {
                product.Id,
                product.Name,
                product.Category,
                product.Location,
                product.PriceEtb,
                product.Emoji
            })
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            products = await _db.Products
                .AsNoTracking()
                .Where(product => product.IsActive)
                .OrderByDescending(product => product.CreatedAt)
                .Take(8)
                .Select(product => new
                {
                    product.Id,
                    product.Name,
                    product.Category,
                    product.Location,
                    product.PriceEtb,
                    product.Emoji
                })
                .ToListAsync(cancellationToken);
        }

        var catalogueContext = JsonSerializer.Serialize(products);
        var systemPrompt = """
            You are MERKATO's shopping assistant for an Ethiopian marketplace.
            Help customers compare products, understand prices in ETB, and discover
            local merchants. Use only the supplied catalogue for product names,
            prices, categories and locations. Never invent stock, discounts, delivery
            promises, seller ratings or payment status. If the catalogue does not
            contain a suitable item, say so clearly. Reply in the language used by
            the customer (including Afaan Oromoo, Amharic or English). Keep answers
            concise, helpful and transparent.
            """;

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = JsonContent.Create(new
        {
            model,
            temperature = 0.2,
            max_tokens = 350,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"Customer question: {message}\n\nCurrent public catalogue: {catalogueContext}" }
            }
        });

        try
        {
            using var response = await client.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Shopping AI provider returned HTTP {StatusCode}.", (int)response.StatusCode);
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    message = "The shopping assistant is temporarily unavailable. Please try again."
                });
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var answer = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(answer))
            {
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    message = "The shopping assistant returned an empty answer."
                });
            }

            return Ok(new
            {
                message = answer.Trim(),
                suggestedProducts = products
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                message = "The shopping assistant took too long to respond. Please try again."
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Shopping AI provider request failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "The shopping assistant is temporarily unavailable. Please try again."
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Shopping AI provider returned an invalid response.");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "The shopping assistant could not read the provider response."
            });
        }
    }
}
