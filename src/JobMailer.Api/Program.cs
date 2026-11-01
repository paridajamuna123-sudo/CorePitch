using JobMailer.Api.Middlewares;
using JobMailer.Application;
using JobMailer.Infrastructure;
using JobMailer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Cloud Port Binding: bind to PORT environment variable (e.g., Render, Railway, Fly.io, Azure App Service)
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Ensure App_Data and App_Data/Resumes directories exist
var appDataPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
var resumesPath = Path.Combine(appDataPath, "Resumes");
Directory.CreateDirectory(appDataPath);
Directory.CreateDirectory(resumesPath);

// Add Application and Infrastructure services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add API Controllers and ProblemDetails
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// Swagger / OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "CorePitch (JobMailer) API",
        Version = "v1",
        Description = "Automated Cold-Email Job Application Platform"
    });
});

// Configure CORS
const string CorsPolicy = "CorePitchCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Database EnsureCreated & Master Profile Startup Seeding
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        logger.LogInformation("Ensuring database schema exists...");
        
        // EnsureCreated works seamlessly with SQLite and SQL Server without needing migrations installed
        await db.Database.EnsureCreatedAsync();
        logger.LogInformation("Database ready.");

        // Remove any legacy placeholder data
        var oldProfiles = db.CandidateProfiles
            .Where(p => p.FullName.Contains("Alex") || p.FullName.Contains("Rivera"))
            .ToList();
        if (oldProfiles.Any())
        {
            db.CandidateProfiles.RemoveRange(oldProfiles);
            await db.SaveChangesAsync();
            logger.LogInformation("Removed legacy candidate profile records.");
        }

        // Seed Rajesh Parida as Master Profile
        var rajeshProfile = await db.CandidateProfiles.FirstOrDefaultAsync(p => p.FullName == "Rajesh Parida");
        if (rajeshProfile == null)
        {
            rajeshProfile = new JobMailer.Domain.Entities.CandidateProfile
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                FullName = "Rajesh Parida",
                Title = "Full-Stack .NET Core & Angular Engineer",
                YearsOfExperience = 2.6,
                CoreSkillsCsv = "C#, ASP.NET Core 8, ASP.NET MVC 5, Web API 2, REST APIs, Entity Framework Core, EF6, AutoMapper, JWT, RBAC, Angular 17/19, TypeScript, RxJS, Reactive Forms, SQL Server, Clean Architecture, SOLID, 3-Tier, Multi-tenant Data Isolation, CQRS (MediatR), RabbitMQ, Redis, Docker, SignalR, Hangfire, Twilio, xUnit, Git, Azure",
                KeyAchievements = "Software Engineer at Verve Systems Pvt Ltd (May 2024–Present). Built CottageCare Suite (multi-tenant SaaS with EF6 DynamicDbContext, 25+ REST APIs, SignalR, Hangfire/Twilio, 20+ Angular 17 modules) and VIS5 (Enterprise HR/Payroll SaaS with ASP.NET Core 8, EF Core, SQL Server query optimization, JWT/RBAC, Angular 19). Built DSA Code Execution Platform with .NET 8, CQRS/MediatR, RabbitMQ, Redis, Docker.",
                LinkedInUrl = "https://linkedin.com/in/rajeshparida",
                GitHubUrl = "https://github.com/rajeshparida",
                PortfolioUrl = "paridarajesh265@gmail.com | +91-7855891330 | Ahmedabad, Gujarat",
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.CandidateProfiles.Add(rajeshProfile);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded master profile for Rajesh Parida.");
        }

        // Ensure Rajesh_Parida_Resume.pdf exists on disk
        var resumeFilePath = Path.Combine(resumesPath, "Rajesh_Parida_Resume.pdf");
        if (!File.Exists(resumeFilePath))
        {
            var fallbackResumeSource = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "Sample_Resume.pdf");
            if (File.Exists(fallbackResumeSource))
            {
                File.Copy(fallbackResumeSource, resumeFilePath, true);
            }
        }

        // Seed default ResumeDocument
        var defaultResume = await db.ResumeDocuments.FirstOrDefaultAsync(r => r.DisplayFileName == "Rajesh_Parida_Resume.pdf");
        if (defaultResume == null)
        {
            var existingDefaults = await db.ResumeDocuments.Where(r => r.IsDefault).ToListAsync();
            foreach (var d in existingDefaults) d.IsDefault = false;

            db.ResumeDocuments.Add(new JobMailer.Domain.Entities.ResumeDocument
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                VersionName = "Full-Stack .NET Core & Angular (2.6+ YOE)",
                DisplayFileName = "Rajesh_Parida_Resume.pdf",
                StoragePath = resumeFilePath,
                IsDefault = true,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            logger.LogInformation("Registered Rajesh_Parida_Resume.pdf as default resume.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization/seeding encountered an error.");
    }
}

// Global Exception Handling
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Enable Swagger in all environments for easy remote testing
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CorePitch API v1");
    c.RoutePrefix = "swagger";
});

// Serve Resume PDF files statically
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(resumesPath),
    RequestPath = "/App_Data/Resumes"
});

// Serve Single Unified Angular Application from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors(CorsPolicy);

app.UseAuthorization();

app.MapControllers();

// Fallback all non-API routing to Angular index.html for client-side routing
app.MapFallbackToFile("index.html");

app.Run();
