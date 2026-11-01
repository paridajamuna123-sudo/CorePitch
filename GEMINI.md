# CorePitch (JobMailer) - Architectural Rules & System Specification

## Overview
**CorePitch** (internal code name / project namespaces `JobMailer`) is a Full-Stack Automated Cold-Email Job Application Platform designed to streamline, personalize, and track job applications with AI-assisted outreach.

---

## Strict Architectural Rules

### 1. Backend Architecture
- **Framework**: ASP.NET Core 8 Web API.
- **Style**: Strict **Clean Architecture** with separation of concerns:
  - `JobMailer.Domain`: Entities, Value Objects, Domain Events, Enums, Interfaces. Zero external framework dependencies.
  - `JobMailer.Application`: CQRS Handlers (Commands/Queries), MediatR behaviors, FluentValidation validators, DTOs, Application interfaces.
  - `JobMailer.Infrastructure`: EF Core DbContext, Migrations, External API Clients (AI Engine), SMTP Email Services (MailKit), Outbox Background Services.
  - `JobMailer.Api`: Controllers, Middlewares, Dependency Injection registration, Swagger/OpenAPI configuration.

### 2. Design Patterns & Messaging
- **CQRS**: Command Query Responsibility Segregation implemented using **MediatR**.
- **Validation**: Strict pipeline validation using **FluentValidation** via MediatR Pipeline Behaviors.
- **Reliable Email Dispatch**: **Transactional Outbox Pattern** with a dedicated `BackgroundService` polling or processing outbox email messages asynchronously to guarantee at-least-once delivery without blocking HTTP requests.

### 3. Database & Persistence
- **RDBMS**: Microsoft SQL Server.
- **ORM**: Entity Framework Core 8 (`Microsoft.EntityFrameworkCore.SqlServer`).
- **Migrations**: Code-first migrations managed with `Microsoft.EntityFrameworkCore.Tools` / `.Design`.

### 4. Email Engine
- **Libraries**: **MailKit** and **MimeKit**.
- **Transport**: Personal Gmail SMTP (`smtp.gmail.com`, Port 587, StartTLS).
- **Attachments**: Support for binary/PDF resume attachments and inline HTML formatting.

### 5. AI Engine
- **Integration**: OpenAI / Gemini compatible HTTP JSON-mode client.
- **Responsibilities**: Generate tailored subject lines and high-converting, personalized HTML cold emails given target Job Descriptions (JD) and applicant profile/resume metadata.
- **Resilience**: Configured with Polly retry and backoff policies via `Microsoft.Extensions.Http.Polly`.

### 6. Frontend
- **Framework**: Angular 17+ / 18+ (inside the `/client` directory).
- **Architecture**: Standalone Components, Reactive Forms, Signals state management, and modern responsive design using **Tailwind CSS**.

### 7. Continuous Verification Rule
- **Mandatory**: After every implementation phase or change, run `dotnet build` in the terminal and resolve any compilation or dependency errors before concluding the turn.
