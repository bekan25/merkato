using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Merkato.Application.Interfaces;
using Merkato.Infrastructure.Persistence;
using Merkato.Infrastructure.Services;
using Scalar.AspNetCore;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

// ============================================================
// LOAD .ENV
// ============================================================

var envPath = Path.GetFullPath(
    Path.Combine(
        Directory.GetCurrentDirectory(),
        "..",
        "..",
        ".env"
    )
);

// Local development may use a repository-root .env file. Production should inject
// environment variables through the hosting platform's secret manager.
if (File.Exists(envPath))
{
    Env.Load(envPath);
}

// ============================================================
// CREATE BUILDER
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// DATABASE
// ============================================================

var connectionString =
    Environment.GetEnvironmentVariable(
        "ConnectionStrings__PostgreSql"
    )
    ?? builder.Configuration.GetConnectionString("PostgreSql");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "PostgreSQL connection string is not configured."
    );
}

builder.Services.AddDbContext<MerkatoDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// ============================================================
// JWT
// ============================================================

var jwtKey =
    Environment.GetEnvironmentVariable("Jwt__Key")
    ?? builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "JWT key must be configured and contain at least 32 UTF-8 bytes."
    );
}

var jwtIssuer =
    Environment.GetEnvironmentVariable("Jwt__Issuer")
    ?? builder.Configuration["Jwt:Issuer"]
    ?? "https://api.merkato.et";

var jwtAudience =
    Environment.GetEnvironmentVariable("Jwt__Audience")
    ?? builder.Configuration["Jwt:Audience"]
    ?? "https://merkato.et";

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme
    )
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)
                    ),

                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,

                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();

// ============================================================
// APPLICATION SERVICES
// ============================================================

builder.Services.AddScoped<IEscrowService, EscrowService>();
builder.Services.AddScoped<ILedgerEngine, LedgerEngine>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IDeliveryService, DeliveryService>();
builder.Services.AddScoped<IInspectionService, InspectionService>();
builder.Services.AddScoped<IKycService, KycService>();
builder.Services.AddScoped<ITrustScoreService, TrustScoreService>();
builder.Services.AddScoped<IRiskAssessmentService, RiskAssessmentService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// ============================================================
// CONTROLLERS
// ============================================================

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// ============================================================
// OPENAPI
// ============================================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "Merkato API",
            Version = "v1",
            Description = "Secure Ethiopian Marketplace Backend API"
        }
    );

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter JWT token as: Bearer {token}"
        }
    );

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// ============================================================
// SWAGGER METADATA
// ============================================================



// ============================================================
// CORS
// ============================================================

var configuredOrigins =
    Environment.GetEnvironmentVariable("MERKATO_ALLOWED_ORIGINS")
    ?? builder.Configuration["MERKATO_ALLOWED_ORIGINS"];

if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(configuredOrigins))
{
    throw new InvalidOperationException("MERKATO_ALLOWED_ORIGINS must be configured in production.");
}

var allowedOrigins = (configuredOrigins ??
    "http://localhost:4200")
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

if (allowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var parsed) ||
    (parsed.Scheme != Uri.UriSchemeHttps && !builder.Environment.IsDevelopment())))
{
    throw new InvalidOperationException("Production CORS origins must be valid HTTPS origins.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "MerkatoFrontend",
        policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
    );
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;
    // The API is private to the Docker network; only the reverse proxy can reach it.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 6,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// ============================================================
// BUILD
// ============================================================

var app = builder.Build();

// ============================================================
// GLOBAL ERROR HANDLING
// ============================================================

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;

        app.Logger.LogError(exception, "Unhandled request failure. TraceId: {TraceId}",
            context.TraceIdentifier);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://httpstatuses.com/500",
            title = "An unexpected error occurred.",
            status = StatusCodes.Status500InternalServerError,
            traceId = context.TraceIdentifier
        });
    });
});

// ============================================================
// OPENAPI + SCALAR
// ============================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.MapScalarApiReference(options =>
{
    options
        .WithTitle("Merkato API")
        .WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json");
});
}

// ============================================================
// CORS
// ============================================================

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseCors("MerkatoFrontend");
app.UseRateLimiter();

// ============================================================
// AUTHENTICATION
// ============================================================

app.UseAuthentication();

// ============================================================
// AUTHORIZATION
// ============================================================

app.UseStaticFiles();

app.UseAuthorization();

// ============================================================
// CONTROLLERS
// ============================================================

app.MapControllers();

// ============================================================
// BASIC HEALTH CHECK
// ============================================================

app.MapGet(
    "/health",
    () =>
        Results.Ok(
            new
            {
                status = "OK",
                service = "Merkato API",
                timestamp = DateTime.UtcNow
            }
        )
);

// ============================================================
// DATABASE HEALTH CHECK
// ============================================================

app.MapGet("/health/ready", async (MerkatoDbContext db, CancellationToken ct) =>
{
    try
    {
        return await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Readiness check failed.");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

// Serve Angular client-side routes from the same origin as the API.
app.MapFallbackToFile("index.html");

// ============================================================
// RUN
// ============================================================

// Apply EF Core migrations only when explicitly enabled by the deployment environment.
// Disable MERKATO_APPLY_MIGRATIONS after the first successful schema deployment.
if (string.Equals(
    Environment.GetEnvironmentVariable("MERKATO_APPLY_MIGRATIONS"),
    "true",
    StringComparison.OrdinalIgnoreCase))
{
    app.Logger.LogInformation("Applying pending MERKATO database migrations.");
    using var migrationScope = app.Services.CreateScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<MerkatoDbContext>();
    await migrationDb.Database.MigrateAsync();
    app.Logger.LogInformation("MERKATO database migrations completed.");
}

// ============================================================
// ADMIN SEED
// ============================================================

var adminPhone = Environment.GetEnvironmentVariable("MERKATO_ADMIN_PHONE");
var adminPin = Environment.GetEnvironmentVariable("MERKATO_ADMIN_PIN");
var adminName = Environment.GetEnvironmentVariable("MERKATO_ADMIN_NAME");

var adminBootstrapRequested = new[] { adminPhone, adminPin, adminName }
    .Any(value => !string.IsNullOrWhiteSpace(value));

if (adminBootstrapRequested)
{
    if (string.IsNullOrWhiteSpace(adminPhone) ||
        string.IsNullOrWhiteSpace(adminPin) ||
        string.IsNullOrWhiteSpace(adminName))
    {
        throw new InvalidOperationException(
            "Set MERKATO_ADMIN_PHONE, MERKATO_ADMIN_PIN and MERKATO_ADMIN_NAME together.");
    }

    if (adminPin.Length < 6 || adminPin.Length > 8 ||
        adminPin.Any(character => !char.IsDigit(character)))
    {
        throw new InvalidOperationException(
            "MERKATO_ADMIN_PIN must contain 6 to 8 digits.");
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MerkatoDbContext>();
    var normalizedAdminPhone = adminPhone.Trim();
    var admin = await db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == normalizedAdminPhone);

    if (admin == null)
    {
        admin = new Merkato.Domain.Entities.User
        {
            PhoneNumber = normalizedAdminPhone,
            FullName = adminName.Trim(),
            PinHash = Merkato.Api.Services.PinHasher.Hash(adminPin),
            Role = Merkato.Domain.Enums.UserRole.ADMIN,
            TrustScore = 100,
            IsFaydaVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
    else if (admin.Role != Merkato.Domain.Enums.UserRole.ADMIN)
    {
        throw new InvalidOperationException(
            "Configured MERKATO_ADMIN_PHONE belongs to a non-admin account. Resolve this manually.");
    }
}
// ============================================================
// PRODUCT DISCOVERY
// ============================================================
// A bounded, database-backed discovery endpoint for storefront search.
// It deliberately returns public catalogue fields only.
app.MapGet("/api/v1/discovery/products", async Task<IResult> (
    MerkatoDbContext db,
    string? q,
    string? category,
    decimal? maxPriceEtb,
    int? limit,
    CancellationToken ct) =>
{
    if (maxPriceEtb is < 0)
    {
        return Results.BadRequest(new { message = "maxPriceEtb must be zero or greater." });
    }

    var take = Math.Clamp(limit ?? 12, 1, 24);
    var products = db.Products
        .AsNoTracking()
        .Where(product => product.IsActive);

    if (!string.IsNullOrWhiteSpace(q))
    {
        var term = q.Trim().ToLowerInvariant();
        products = products.Where(product =>
            product.Name.ToLower().Contains(term) ||
            product.Category.ToLower().Contains(term) ||
            product.Location.ToLower().Contains(term));
    }

    if (!string.IsNullOrWhiteSpace(category))
    {
        var selectedCategory = category.Trim().ToLowerInvariant();
        products = products.Where(product =>
            product.Category.ToLower() == selectedCategory);
    }

    if (maxPriceEtb.HasValue)
    {
        products = products.Where(product => product.PriceEtb <= maxPriceEtb.Value);
    }

    var results = await products
        .OrderByDescending(product => product.CreatedAt)
        .ThenBy(product => product.Id)
        .Take(take)
        .Select(product => new
        {
            product.Id,
            product.Name,
            product.PriceEtb,
            product.Location,
            product.Category,
            product.Emoji,
            product.ImageUrl,
            product.SellerId
        })
        .ToListAsync(ct);

    return Results.Ok(new
    {
        items = results,
        count = results.Count,
        limit = take
    });
}).AllowAnonymous();

app.Run();




