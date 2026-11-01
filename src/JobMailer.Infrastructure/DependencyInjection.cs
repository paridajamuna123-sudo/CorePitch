using JobMailer.Application.Common.Interfaces;
using JobMailer.Infrastructure.BackgroundWorkers;
using JobMailer.Infrastructure.Configuration;
using JobMailer.Infrastructure.Persistence;
using JobMailer.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace JobMailer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // If connection string contains "Data Source=" and ends with .db, or no connection string is set, use SQLite
        if (string.IsNullOrWhiteSpace(connectionString) || 
            connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase) || 
            connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) && !connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
        {
            var sqliteDbPath = string.IsNullOrWhiteSpace(connectionString) 
                ? "Data Source=App_Data/corepitch.db" 
                : connectionString;

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(sqliteDbPath, b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));
        }
        else
        {
            // Fallback to SQL Server if an explicit server connection string is supplied
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));
        }

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // File Storage
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddSingleton<IResumePdfParserService, ResumePdfParserService>();

        // SMTP MailKit Dispatcher
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        services.AddTransient<IEmailSmtpDispatcher, MailKitSmtpDispatcher>();

        // AI Engine with Polly Resilience Policy
        services.Configure<AiSettings>(configuration.GetSection(AiSettings.SectionName));
        var aiBaseUrl = configuration.GetValue<string>("AiSettings:BaseUrl") ?? "https://generativelanguage.googleapis.com/v1beta/";

        services.AddHttpClient<IColdEmailAiService, ColdEmailAiService>(client =>
        {
            client.BaseAddress = new Uri(aiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(45);
        })
        .AddPolicyHandler(GetRetryPolicy());

        // Background Outbox Worker
        services.AddHostedService<EmailOutboxWorker>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
