using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Merkato.Application.Interfaces;
using Merkato.Infrastructure.Persistence;
using Merkato.Infrastructure.Services;
using Scalar.AspNetCore;
using System.Text;
using System.Text.Json.Serialization;

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

if (!File.Exists(envPath))
{
    throw new FileNotFoundException(
        $"Merkato .env file was not found at: {envPath}"
    );
}

Env.Load(envPath);

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

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        }
    );
});

// ============================================================
// SWAGGER METADATA
// ============================================================



// ============================================================
// CORS
// ============================================================

var allowedOrigins = (
    Environment.GetEnvironmentVariable("MERKATO_ALLOWED_ORIGINS")
    ?? "http://localhost:4200;https://merkato.et;https://www.merkato.et"
).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
        var exceptionHandler =
            context.Features.Get<
                Microsoft.AspNetCore.Diagnostics
                    .IExceptionHandlerFeature
            >();

        var exception = exceptionHandler?.Error;

        context.Response.ContentType =
            "application/json";

        var statusCode = exception switch
        {
            KeyNotFoundException => 404,
            UnauthorizedAccessException => 403,
            InvalidOperationException => 409,
            ArgumentException => 400,
            _ => 500
        };

        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(
            new
            {
                status = statusCode,

                message =
                    exception?.Message
                    ?? "An unexpected error occurred.",

                traceId =
                    context.TraceIdentifier
            }
        );
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

app.UseCors("MerkatoFrontend");

// ============================================================
// AUTHENTICATION
// ============================================================

app.UseAuthentication();

// ============================================================
// AUTHORIZATION
// ============================================================

app.UseStaticFiles();

app.UseAuthorization();;

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

app.MapGet(
    "/health/db",
    async (MerkatoDbContext db) =>
    {
        try
        {
            var connected =
                await db.Database.CanConnectAsync();

            return Results.Ok(
                new
                {
                    status =
                        connected
                            ? "healthy"
                            : "unhealthy",

                    database =
                        connected
                            ? "PostgreSQL connected"
                            : "PostgreSQL not connected",

                    timestamp = DateTime.UtcNow
                }
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: 500,
                title: "Database connection failed"
            );
        }
    }
);

// ============================================================
// RUN
// ============================================================


// ============================================================
// ADMIN SEED
// ============================================================

var adminPhone = Environment.GetEnvironmentVariable("MERKATO_ADMIN_PHONE");
var adminPin = Environment.GetEnvironmentVariable("MERKATO_ADMIN_PIN");
var adminName = Environment.GetEnvironmentVariable("MERKATO_ADMIN_NAME");

if (!string.IsNullOrWhiteSpace(adminPhone) &&
    !string.IsNullOrWhiteSpace(adminPin) &&
    !string.IsNullOrWhiteSpace(adminName))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MerkatoDbContext>();
    var admin = await db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == adminPhone);

    if (admin == null)
    {
        admin = new Merkato.Domain.Entities.User
        {
            PhoneNumber = adminPhone.Trim(),
            FullName = adminName.Trim(),
            PinHash = Merkato.Api.Services.PinHasher.Hash(adminPin),
            Role = Merkato.Domain.Enums.UserRole.ADMIN,
            TrustScore = 100,
            IsFaydaVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(admin);
    }
    else if (admin.Role != Merkato.Domain.Enums.UserRole.ADMIN)
    {
        throw new InvalidOperationException(
            "Configured MERKATO_ADMIN_PHONE belongs to a non-admin account. Resolve this manually.");
    }

    await db.SaveChangesAsync();
}
app.Run();




