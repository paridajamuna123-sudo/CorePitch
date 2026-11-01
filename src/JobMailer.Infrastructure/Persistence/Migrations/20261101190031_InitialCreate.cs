using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobMailer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    YearsOfExperience = table.Column<double>(type: "float", nullable: false),
                    CoreSkillsCsv = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    KeyAchievements = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    GitHubUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PortfolioUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResumeDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DisplayFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecruiterName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RecruiterEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RawJobDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmailSubject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EmailBodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResumeDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SmtpMessageId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledSendUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobApplications_ResumeDocuments_ResumeDocumentId",
                        column: x => x.ResumeDocumentId,
                        principalTable: "ResumeDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CandidateProfiles",
                columns: new[] { "Id", "CoreSkillsCsv", "FullName", "GitHubUrl", "KeyAchievements", "LinkedInUrl", "PortfolioUrl", "Title", "UpdatedAtUtc", "YearsOfExperience" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), ".NET 8, C#, ASP.NET Core Web API, Entity Framework Core, SQL Server, Clean Architecture, CQRS, MediatR, Angular 17+, TypeScript, Tailwind CSS, Docker, Microservices, Azure", "Rajesh Parida", "https://github.com/alex-rivera-dev", "Engineered high-throughput transactional outbox message dispatchers; built scalable enterprise SaaS microservices handling 2M+ daily requests; accelerated Angular enterprise UI rendering by 45% using Signals and standalone architecture.", "https://linkedin.com/in/alex-rivera-dev", "https://alexrivera.dev", "Senior Full-Stack .NET & Angular Engineer", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 6.5 });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_RecruiterEmail_CompanyName",
                table: "JobApplications",
                columns: new[] { "RecruiterEmail", "CompanyName" });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_ResumeDocumentId",
                table: "JobApplications",
                column: "ResumeDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_Status_ScheduledSendUtc",
                table: "JobApplications",
                columns: new[] { "Status", "ScheduledSendUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateProfiles");

            migrationBuilder.DropTable(
                name: "JobApplications");

            migrationBuilder.DropTable(
                name: "ResumeDocuments");
        }
    }
}
