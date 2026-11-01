using JobMailer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobMailer.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<CandidateProfile> CandidateProfiles { get; }
    DbSet<ResumeDocument> ResumeDocuments { get; }
    DbSet<JobApplication> JobApplications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
