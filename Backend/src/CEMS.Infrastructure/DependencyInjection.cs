using CEMS.Application.Analytics;
using CEMS.Application.Common.Interfaces;
using CEMS.Application.Exams;
using CEMS.Application.Payroll;
using CEMS.Infrastructure.Auth;
using CEMS.Infrastructure.Identity;
using CEMS.Infrastructure.Persistence;
using CEMS.Infrastructure.Reporting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace CEMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // QuestPDF's Community license is free for organizations under its revenue threshold,
        // which covers personal/portfolio use.
        QuestPDF.Settings.License = LicenseType.Community;

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireDigit = false;
            options.Password.RequiredUniqueChars = 1;

            // Five wrong passwords lock the account for 15 minutes. The trade-off is deliberate: someone who
            // knows an address can also lock that account out, so the lock is kept short.
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        });

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IReportCardGenerator, QuestPdfReportCardGenerator>();
        services.AddSingleton<IDashboardReportGenerator, DashboardReportGenerator>();
        services.AddSingleton<IPayStubGenerator, QuestPdfPayStubGenerator>();

        return services;
    }
}
