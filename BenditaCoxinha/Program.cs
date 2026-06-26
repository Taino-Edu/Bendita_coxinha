using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using BenditaCoxinha.Configuration;
using BenditaCoxinha.Data;
using BenditaCoxinha.HealthChecks;
using BenditaCoxinha.Hubs;
using BenditaCoxinha.Middleware;
using BenditaCoxinha.Services.Implementations;
using BenditaCoxinha.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. CONFIGURAÇÕES
// ---------------------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

// ---------------------------------------------------------------------------
// 2. BANCO RELACIONAL — SQLite (dev local) ou PostgreSQL (produção/Docker)
// ---------------------------------------------------------------------------
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL");
var useSqlite = string.IsNullOrWhiteSpace(pgConnStr);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqlite)
    {
        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "benditacoxinha.db");
        options.UseSqlite($"Data Source={dbPath}");
    }
    else
    {
        options.UseNpgsql(
            pgConnStr,
            npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5)
        );
    }
});

// ---------------------------------------------------------------------------
// 3. AUTENTICAÇÃO — JWT Bearer Token
// ---------------------------------------------------------------------------
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSettings.Issuer,
            ValidAudience            = jwtSettings.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)
            ),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var cookieToken = context.Request.Cookies["accessToken"];
                if (!string.IsNullOrEmpty(cookieToken))
                {
                    context.Token = cookieToken;
                    return Task.CompletedTask;
                }

                var accessToken = context.Request.Query["access_token"];
                var path        = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            }
        };
    });

// ---------------------------------------------------------------------------
// 4. AUTORIZAÇÃO — RBAC com políticas por perfil
// ---------------------------------------------------------------------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly",       policy => policy.RequireRole("Admin", "Operator"));
    options.AddPolicy("CustomerOrAdmin", policy => policy.RequireRole("Admin", "Customer", "Operator"));
});

// ---------------------------------------------------------------------------
// 5. RATE LIMITING
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    static string GetClientIp(HttpContext ctx) =>
        ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
        ?? ctx.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(
        context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            GetClientIp(context),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit          = 300,
                Window               = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            }));

    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit          = 5;
        opt.Window               = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 0;
    });

    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.PermitLimit          = 200;
        opt.Window               = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 10;
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers["Retry-After"] = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { Message = "Muitas requisições. Aguarde 1 minuto antes de tentar novamente." },
            cancellationToken: token);
    };
});

// ---------------------------------------------------------------------------
// 6. TIMEOUT DE REQUISIÇÃO
// ---------------------------------------------------------------------------
builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    options.AddPolicy("long", TimeSpan.FromSeconds(60));
});

// ---------------------------------------------------------------------------
// 7. SIGNALR — Comunicação em tempo real (comandas → dashboard)
// ---------------------------------------------------------------------------
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors      = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 32 * 1024;
    options.KeepAliveInterval         = TimeSpan.FromSeconds(10);
    options.ClientTimeoutInterval     = TimeSpan.FromSeconds(20);
});

// ---------------------------------------------------------------------------
// 8. HTTP CLIENTS — APIs externas
// ---------------------------------------------------------------------------
builder.Services.AddHttpClient("gemini", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ---------------------------------------------------------------------------
// 9. HEALTH CHECKS — apenas Postgres
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddCheck<DbHealthCheck>("postgres", tags: ["db", "postgres"]);

// ---------------------------------------------------------------------------
// 10. SERVIÇOS DE APLICAÇÃO
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IAuthService,         AuthService>();
builder.Services.AddScoped<IComandaService,       ComandaService>();
builder.Services.AddScoped<IProductService,       ProductService>();
builder.Services.AddScoped<ICategoryService,      CategoryService>();
builder.Services.AddScoped<IChampionshipService,  ChampionshipService>();
builder.Services.AddScoped<IUserService,          UserService>();
builder.Services.AddScoped<IVendaAvulsaService,   VendaAvulsaService>();
builder.Services.AddScoped<IAnnouncementService,  AnnouncementService>();
builder.Services.AddScoped<IEmailService,         EmailService>();
builder.Services.AddScoped<IAiChatService,        GeminiChatService>();
builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// 11. CORS
// ---------------------------------------------------------------------------
var corsOrigins = (builder.Configuration["CorsSettings:AllowedOrigins"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ---------------------------------------------------------------------------
// 12. SWAGGER
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Bendita Coxinha API",
        Version     = "v1",
        Description = "API para gestão do restaurante Bendita Coxinha"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Informe: Bearer {seu_token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddControllers();

// ---------------------------------------------------------------------------
// 13. BUILD
// ---------------------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------------------
// 14. BANCO DE DADOS — EnsureCreated
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Inicializando banco de dados...");
        await db.Database.EnsureCreatedAsync();
        logger.LogInformation("Banco pronto.");

        if (!useSqlite)
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS perfis (
                    id                  UUID         NOT NULL DEFAULT gen_random_uuid(),
                    nome                VARCHAR(100) NOT NULL,
                    permissoes_json     TEXT         NOT NULL DEFAULT '[]',
                    criado_por_admin_id UUID         NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                    criado_em           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    atualizado_em       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    CONSTRAINT pk_perfis PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS ix_perfis_nome ON perfis (nome);
                ALTER TABLE users ADD COLUMN IF NOT EXISTS perfil_id UUID REFERENCES perfis(id) ON DELETE SET NULL;
            ");
        }

        if (!db.Users.Any(u => u.Email == "admin@benditacoxinha.com.br"))
        {
            var adminPassword = Environment.GetEnvironmentVariable("ADMIN_SEED_PASSWORD") ?? "SenhaForte@123";
            if (adminPassword == "SenhaForte@123")
                logger.LogWarning("ATENÇÃO: admin criado com senha padrão. Defina ADMIN_SEED_PASSWORD no ambiente de produção!");

            db.Users.Add(new BenditaCoxinha.Models.PostgreSQL.User
            {
                Id           = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name         = "Admin",
                Email        = "admin@benditacoxinha.com.br",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role         = BenditaCoxinha.Models.PostgreSQL.UserRole.Admin,
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            logger.LogInformation("Usuário admin criado com sucesso.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erro ao inicializar o banco: {Msg}", ex.Message);
        throw;
    }
}

// ---------------------------------------------------------------------------
// 15. MIDDLEWARE PIPELINE
// ---------------------------------------------------------------------------
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
forwardedOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
forwardedOptions.KnownProxies.Add(IPAddress.Loopback);
forwardedOptions.KnownProxies.Add(IPAddress.IPv6Loopback);
app.UseForwardedHeaders(forwardedOptions);

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"]  = "nosniff";
    context.Response.Headers["X-Frame-Options"]         = "DENY";
    context.Response.Headers["X-XSS-Protection"]        = "1; mode=block";
    context.Response.Headers["Referrer-Policy"]         = "no-referrer";
    context.Response.Headers["Permissions-Policy"]      = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bendita Coxinha API v1");
        c.RoutePrefix   = "swagger";
        c.DocumentTitle = "Bendita Coxinha — API";
    });
}

app.UseStaticFiles();
app.UseCors("FrontendPolicy");
app.UseRateLimiter();
app.UseRequestTimeouts();
app.UseAuthentication();
app.UseAuthorization();
app.UseOperatorPermissions();

app.MapControllers();
app.MapHub<ComandaHub>("/hubs/comanda");

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = new
        {
            Status    = report.Status.ToString(),
            Timestamp = DateTime.UtcNow,
            Checks    = report.Entries.Select(e => new
            {
                Name    = e.Key,
                Status  = e.Value.Status.ToString(),
                Message = e.Value.Description,
            })
        };
        await ctx.Response.WriteAsJsonAsync(result);
    }
})
.AllowAnonymous()
.DisableRateLimiting();

app.Run();
