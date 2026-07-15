using System.Text;
using JobNet.Infrastructure.Auditing;
using JobNet.Infrastructure.Auth;
using JobNet.Infrastructure.Email;
using JobNet.Infrastructure.Mongo;
using JobNet.Infrastructure.Persistence;
using JobNet.Infrastructure.Persistence.Seeding;
using JobNet.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace JobNet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddJobNetInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // ---- Relational database (EF Core) - provider selectable by config ----
        // "Database:Provider" = "SqlServer" (default) or "Postgres".
        //   SqlServer -> ConnectionStrings:SqlServer; migrations in JobNet.Infrastructure
        //   Postgres  -> ConnectionStrings:Postgres;  migrations in JobNet.Migrations.Postgres
        var dbProvider = (config["Database:Provider"] ?? "SqlServer").Trim();

        if (dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
            dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            var pgConn = config.GetConnectionString("Postgres")
                         ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres");
            services.AddDbContext<JobNetDbContext>(opts =>
                opts.UseNpgsql(pgConn, npg =>
                    npg.MigrationsAssembly("JobNet.Migrations.Postgres")));
        }
        else
        {
            var sqlConn = config.GetConnectionString("SqlServer")
                          ?? throw new InvalidOperationException("Missing ConnectionStrings:SqlServer");
            // SQL Server migrations stay in this assembly (JobNet.Infrastructure),
            // the default MigrationsAssembly, so existing databases keep working.
            services.AddDbContext<JobNetDbContext>(opts => opts.UseSqlServer(sqlConn));
        }

        // ---- MongoDB ----
        services.Configure<MongoSettings>(config.GetSection("Mongo"));
        services.AddSingleton<MongoContext>();
        services.AddScoped<IAuditLogger, MongoAuditLogger>();

        // ---- JWT ----
        services.Configure<JwtSettings>(config.GetSection("Jwt"));
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        // ---- Email (Brevo transactional API) ----
        services.Configure<BrevoSettings>(config.GetSection("Brevo"));
        services.AddHttpClient<IEmailSender, BrevoEmailSender>();

        // ---- HTTP context / current user ----
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        // ---- Domain services ----
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IWorkerProfileService, WorkerProfileService>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAdminReportsService, AdminReportsService>();

        // ---- Seeder ----
        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    public static IServiceCollection AddJobNetJwtAuth(this IServiceCollection services, IConfiguration config)
    {
        var jwt = config.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(o =>
        {
            o.RequireHttpsMetadata = false;
            o.SaveToken = true;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            };
        });

        services.AddAuthorization();
        return services;
    }
}
