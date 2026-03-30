using System.Text;
using BankingApi.Application.Auth.Commands;
using BankingApi.Application.Common.Behaviors;
using BankingApi.Application.Common.Exceptions;
using BankingApi.Infrastructure.Persistence;
using BankingApi.Infrastructure.Seed;
using BankingApi.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Filters;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

// ══════════════════════════════════════════════════════════════════════════════
// 1. DATABASE — EF Core + Pomelo MySQL
// ══════════════════════════════════════════════════════════════════════════════

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<BankingDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mysqlOptions =>
        {
            mysqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            mysqlOptions.CommandTimeout(60);
        }));

// ══════════════════════════════════════════════════════════════════════════════
// 2. INFRASTRUCTURE SERVICES
// ══════════════════════════════════════════════════════════════════════════════

// JWT token service
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Account number generator — scoped because it queries the DB
builder.Services.AddScoped<IAccountNumberGenerator, AccountNumberGenerator>();

// Fee calculator — singleton because it reads from static config
builder.Services.Configure<List<FeeTier>>(
    builder.Configuration.GetSection("FeeSchedule"));
builder.Services.AddSingleton<IFeeCalculator, FeeCalculator>();

// Database seeder
builder.Services.AddScoped<DatabaseSeeder>();

// ══════════════════════════════════════════════════════════════════════════════
// 3. FLUENTVALIDATION
// ══════════════════════════════════════════════════════════════════════════════

// Register all validators from the Application assembly
builder.Services.AddValidatorsFromAssemblyContaining<RegisterUserCommandValidator>(
    lifetime: ServiceLifetime.Scoped);

// ══════════════════════════════════════════════════════════════════════════════
// 4. WOLVERINE CQRS
// ══════════════════════════════════════════════════════════════════════════════

builder.Host.UseWolverine(opts =>
{
    // Scan Application assembly for all handlers
    opts.Discovery.IncludeAssembly(
        typeof(RegisterUserHandler).Assembly);

    // Apply ValidationMiddleware to every handler chain
    opts.Policies.Add<ValidationPolicy>();

    // Use inline (synchronous) message processing for HTTP-triggered commands
    opts.DefaultLocalQueue.UseDurableInbox();
});

// ══════════════════════════════════════════════════════════════════════════════
// 5. JWT AUTHENTICATION
// ══════════════════════════════════════════════════════════════════════════════

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // set true in production
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtIssuer,
            IssuerSigningKey = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero   // no grace period
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";
                var body = """
                    {
                      "type":   "https://tools.ietf.org/html/rfc7807",
                      "title":  "Unauthorized",
                      "status": 401,
                      "detail": "A valid Bearer token is required."
                    }
                    """;
                return context.Response.WriteAsync(body);
            }
        };
    });

builder.Services.AddAuthorization();

// ══════════════════════════════════════════════════════════════════════════════
// 6. GLOBAL EXCEPTION HANDLER
// ══════════════════════════════════════════════════════════════════════════════

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ══════════════════════════════════════════════════════════════════════════════
// 7. CONTROLLERS + SWAGGER
// ══════════════════════════════════════════════════════════════════════════════

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Banking API",
        Version = "v1",
        Description = "Production-grade Banking REST API — CQRS with Wolverine, " +
                      "EF Core 8, MySQL, JWT Authentication.",
        Contact = new OpenApiContact { Name = "BankingApi Team" }
    });

    // Enable XML doc comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);

    // JWT Bearer security definition — shows lock icon on protected endpoints
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. Example: Bearer {token}"
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme),
            new List<string>()
        }
    });

    // Swashbuckle filters for example request/response schemas
    options.ExampleFilters();

    // Group endpoints by controller tag
    options.TagActionsBy(api =>
        new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"]! });
});

// Register Swashbuckle example filters from this assembly
builder.Services.AddSwaggerExamplesFromAssemblyOf<Program>();

// ══════════════════════════════════════════════════════════════════════════════
// BUILD
// ══════════════════════════════════════════════════════════════════════════════

var app = builder.Build();

// ══════════════════════════════════════════════════════════════════════════════
// 8. SEED DATABASE (Development only)
// ══════════════════════════════════════════════════════════════════════════════

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

// ══════════════════════════════════════════════════════════════════════════════
// 9. MIDDLEWARE PIPELINE
// ══════════════════════════════════════════════════════════════════════════════

// Global exception handler must be first
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Banking API v1");
        options.RoutePrefix = string.Empty; // Swagger at root "/"
        options.DisplayRequestDuration();
        options.EnableDeepLinking();
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();

// Expose Program class for WebApplicationFactory in integration tests
public partial class Program { }