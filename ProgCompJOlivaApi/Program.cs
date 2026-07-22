using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.JudgeClients.CodeforcesClient;
using ProgCompJOlivaApi.JudgeClients.CsesClient;
using ProgCompJOlivaApi.Models;
using ProgCompJOlivaApi.Services;

namespace ProgCompJOlivaApi;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Bare "ADDCODEFORCES" flag toggles the one-shot gym import. Strip it before building
        // configuration so the command-line provider doesn't reject the unkeyed token.
        var addCodeforcesFlag = args.Any(a => string.Equals(a.TrimStart('-'), "ADDCODEFORCES", StringComparison.OrdinalIgnoreCase));
        var builderArgs = args.Where(a => !string.Equals(a.TrimStart('-'), "ADDCODEFORCES", StringComparison.OrdinalIgnoreCase)).ToArray();

        var builder = WebApplication.CreateBuilder(builderArgs);

        // Also honour the toggle via env/config (e.g. ADDCODEFORCES=true), e.g. for Docker.
        // Parsed leniently: docker-compose passes an empty string when the host var is unset,
        // which GetValue<bool> would reject with an unhandled exception.
        var addCodeforces = addCodeforcesFlag
            || (bool.TryParse(builder.Configuration["ADDCODEFORCES"], out var addCfFlag) && addCfFlag);

        builder.Services.AddControllers();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("Frontend", policy =>
            {
                policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        builder.Services.AddHostedService<PeriodicWorker>();

        builder.Services.AddHostedService<CsesProblemImportService>();

        // CSES "rating" = number of solved problems, refreshed periodically.
        builder.Services.AddHostedService<CsesWorker>();

        // Turn SeedData/standings/*.dat into stored ICPC standings JSON at startup.
        builder.Services.AddHostedService<IcpcStandingsSeedService>();

        // Load SeedData/oci-standings/*.json into stored OCI standings at startup.
        builder.Services.AddHostedService<OciStandingsSeedService>();

        // Load SeedData/icpc-events/*.json (LATAM regional, PdA, …) into stored standings at startup.
        builder.Services.AddHostedService<IcpcEventsSeedService>();

        // Single owner of all Codeforces API access (ratings + gym solve sync, and the one-shot
        // ADDCODEFORCES import) so calls from this server's IP share one coordinated rate budget.
        builder.Services.AddHostedService(sp => new CodeforcesWorker(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<ILogger<CodeforcesWorker>>(),
            importOnStartup: addCodeforces));

        builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddScoped<PasswordService>();

        builder.Services.AddScoped<JwtTokenService>();

        builder.Services.AddScoped<CodeforcesGymImporter>();

        builder.Services.AddSingleton<CsesSolvedScraper>();

        var jwtSection = builder.Configuration.GetSection("Jwt");
        var keyBytes = Encoding.UTF8.GetBytes(jwtSection["Key"]!);

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSection["Issuer"],

                    ValidateAudience = true,
                    ValidAudience = jwtSection["Audience"],

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                // Only access tokens may be used as bearer credentials. Refresh tokens are valid
                // signed JWTs with the same issuer/audience, so without this they would also
                // authenticate — and they live far longer. Reject anything that isn't an access token.
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.Principal?.FindFirst("token_use")?.Value != "access")
                            context.Fail("Not an access token.");

                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization();

        builder.Services.AddOpenApi();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        app.UseCors("Frontend");

        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        // In local dev the SPA talks to the plain-HTTP endpoint. Redirecting to HTTPS (whose dev
        // cert is often untrusted) breaks cross-origin fetches with net::ERR_EMPTY_RESPONSE.
        // TLS is terminated by the reverse proxy in production, so only redirect outside dev.
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseStaticFiles();

        app.MapControllers();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var passwordService = scope.ServiceProvider.GetRequiredService<PasswordService>();
            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

            await DbDevSeeder.SeedAsync(db, passwordService, env);
        }

        app.Run();
    }
}
