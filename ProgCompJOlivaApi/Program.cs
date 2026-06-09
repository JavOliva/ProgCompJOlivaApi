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
        var addCodeforces = args.Any(a => string.Equals(a.TrimStart('-'), "ADDCODEFORCES", StringComparison.OrdinalIgnoreCase));
        var builderArgs = args.Where(a => !string.Equals(a.TrimStart('-'), "ADDCODEFORCES", StringComparison.OrdinalIgnoreCase)).ToArray();

        var builder = WebApplication.CreateBuilder(builderArgs);

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

        // Periodically sync solved problems from the registered Codeforces gyms.
        builder.Services.AddHostedService<CodeforcesSolveSyncService>();

        // One-shot gym import, only when started with the ADDCODEFORCES flag.
        if (addCodeforces)
            builder.Services.AddHostedService<CodeforcesContestImportService>();

        builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddScoped<PasswordService>();

        builder.Services.AddScoped<JwtTokenService>();

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

        app.UseHttpsRedirection();

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
