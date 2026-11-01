using JobMailer.Application.Common.Interfaces;
using JobMailer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobMailer.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<ResumeDocument> ResumeDocuments => Set<ResumeDocument>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CandidateProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FullName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(150).IsRequired();
            entity.Property(e => e.CoreSkillsCsv).HasMaxLength(1500);
            entity.Property(e => e.KeyAchievements).HasMaxLength(4000);
            entity.Property(e => e.LinkedInUrl).HasMaxLength(300);
            entity.Property(e => e.GitHubUrl).HasMaxLength(300);
            entity.Property(e => e.PortfolioUrl).HasMaxLength(300);
        });

        modelBuilder.Entity<ResumeDocument>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VersionName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.DisplayFileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.StoragePath).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<JobApplication>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.RecruiterName).HasMaxLength(150);
            entity.Property(e => e.RecruiterEmail).HasMaxLength(200).IsRequired();
            entity.Property(e => e.JobTitle).HasMaxLength(200).IsRequired();
            entity.Property(e => e.EmailSubject).HasMaxLength(300).IsRequired();
            entity.Property(e => e.SmtpMessageId).HasMaxLength(255);

            entity.HasIndex(e => new { e.RecruiterEmail, e.CompanyName });
            entity.HasIndex(e => new { e.Status, e.ScheduledSendUtc });

            entity.HasOne(e => e.ResumeDocument)
                .WithMany()
                .HasForeignKey(e => e.ResumeDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Seed Rajesh Parida as the permanent Master Candidate Profile
        var defaultProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        modelBuilder.Entity<CandidateProfile>().HasData(new CandidateProfile
        {
            Id = defaultProfileId,
            FullName = "Rajesh Parida",
            Title = "Full-Stack .NET Core & Angular Engineer",
            YearsOfExperience = 2.6,
            CoreSkillsCsv = "C#, ASP.NET Core 8, ASP.NET MVC 5, Web API 2, REST APIs, Entity Framework Core, EF6, AutoMapper, JWT, RBAC, Angular 17/19, TypeScript, RxJS, Reactive Forms, HTTP Interceptors, ApexCharts, SQL Server (T-SQL, Stored Procedures, Query Optimization), Clean Architecture, SOLID, 3-Tier (API/BAL/DAL), Multi-tenant Data Isolation, CQRS (MediatR), RabbitMQ, Redis, Docker, SignalR, Hangfire, Twilio, xUnit, Git, HiQPdf, EPPlus, Azure Fundamentals (AZ-900)",
            KeyAchievements = "Software Engineer (Full-Stack .NET & Angular) at Verve Systems Pvt Ltd (May 2024 – Present; promoted within 6 mos in Nov 2024); CottageCare Suite (Multi-tenant commercial SaaS): Architected tenant-isolated EF6 DynamicDbContext, delivered 25+ REST API modules (.NET 4.8 / MVC 5) for room quoting, pricing, crew scheduling; automated SMS/email follow-ups with Hangfire and Twilio; built 20+ Angular 17 modules (Kanban CRM, ApexCharts dashboards, HiQPdf/EPPlus exports); VIS5 (Enterprise HR & Payroll SaaS): Engineered ASP.NET Core 8 APIs with EF Core & SQL Server, optimized heavy stored procedures to reduce peak latency, secured endpoints with JWT/RBAC, and built Angular 19 components with Reactive Forms, RxJS, and Clean Architecture.",
            LinkedInUrl = "https://linkedin.com/in/rajeshparida",
            GitHubUrl = "https://github.com/rajeshparida",
            PortfolioUrl = "paridarajesh265@gmail.com | +91-7855891330 | Ahmedabad, Gujarat",
            UpdatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // Seed default ResumeDocument
        var defaultResumeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        modelBuilder.Entity<ResumeDocument>().HasData(new ResumeDocument
        {
            Id = defaultResumeId,
            VersionName = "Full-Stack .NET Core & Angular (2.6+ YOE)",
            DisplayFileName = "Rajesh_Parida_Resume.pdf",
            StoragePath = "App_Data/Resumes/Rajesh_Parida_Resume.pdf",
            IsDefault = true,
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
