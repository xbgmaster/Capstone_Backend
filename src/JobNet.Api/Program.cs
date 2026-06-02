using System.Text.Json.Serialization;
using JobNet.Api.Middleware;
using JobNet.Infrastructure;
using JobNet.Infrastructure.Persistence;
using JobNet.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ----
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
{
    // camelCase property names AND camelCase enum values so the wire format
    // matches what the React frontend already expects ('worker', 'open', etc.).
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
});

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

// ---- Infrastructure (SQL Server, Mongo, JWT, services) ----
builder.Services.AddJobNetInfrastructure(builder.Configuration);
builder.Services.AddJobNetJwtAuth(builder.Configuration);

// ---- CORS for the React frontend (Vite dev + preview, any local port) ----
const string CorsPolicy = "JobNetFrontend";
builder.Services.AddCors(o =>
{
    o.AddPolicy(CorsPolicy, p => p
        .SetIsOriginAllowed(origin =>
        {
            // Allow any localhost / 127.0.0.1 origin so Vite is free to fall
            // back to 5174, 5175, etc. when 5173 is already in use.
            if (string.IsNullOrEmpty(origin)) return false;
            if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                return uri.Host is "localhost" or "127.0.0.1";
            }
            return false;
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// ---- Swagger / OpenAPI with JWT bearer support ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Jobnet API",
        Version = "v1",
        Description = "Backend for the Jobnet job marketplace (construction & general services - Canada).",
    });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT obtained from /api/auth/login. Format: Bearer {token}",
        Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme },
    };
    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [jwtScheme] = Array.Empty<string>() });
});

var app = builder.Build();

// ---- Pipeline ----
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Jobnet API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.MapControllers();

// ---- Apply migrations / seed in development ----
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JobNetDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await db.Database.MigrateAsync();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migrate/seed failed. The API will still start; fix DB connectivity and restart.");
    }
}

app.Run();
