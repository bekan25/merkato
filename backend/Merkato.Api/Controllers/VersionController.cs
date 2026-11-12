using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Merkato.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/version")]
public sealed class VersionController(IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(VersionResponse), StatusCodes.Status200OK)]
    public ActionResult<VersionResponse> Get()
    {
        var assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3);
        var version = FirstConfigured(configuration["MERKATO_VERSION"], ReadTextFile("VERSION"), assemblyVersion, "unknown");
        var commitSha = FirstConfigured(
            configuration["RENDER_GIT_COMMIT"],
            configuration["MERKATO_COMMIT_SHA"],
            "unknown");
        var branch = FirstConfigured(
            configuration["RENDER_GIT_BRANCH"],
            configuration["MERKATO_BRANCH"],
            "unknown");
        var buildTimeUtc = FirstConfigured(
            configuration["MERKATO_BUILD_TIME_UTC"],
            ReadTextFile("merkato-build-time.txt"),
            "unknown");

        return Ok(new VersionResponse(version, commitSha, buildTimeUtc, branch));
    }

    private static string FirstConfigured(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "unknown";

    private static string? ReadTextFile(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        return System.IO.File.Exists(path)
            ? System.IO.File.ReadAllText(path).Trim()
            : null;
    }
}

public sealed record VersionResponse(
    string Version,
    string CommitSha,
    string BuildTimeUtc,
    string Branch);
