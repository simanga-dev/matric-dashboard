using MatricDasbhoard.Domain.Entities;
using MatricDasbhoard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MatricDasbhoard.Component.Tests.Fixtures;

internal static class TestDbContextFactory
{
    /// <summary>Creates an in-memory database context for component tests.</summary>
    public static MatricDasbhoardDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<MatricDasbhoardDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new MatricDasbhoardDbContext(options);
    }

    extension(MatricDasbhoardDbContext db)
    {
        /// <summary>Adds an NSC subject and returns it for wiring into school results.</summary>
        public Subject AddSubject(string code, string name, bool isGateway = true)
        {
            var subject = new Subject(code, name, isGateway);
            db.Subjects.Add(subject);
            return subject;
        }
    }

    extension(School school)
    {
        /// <summary>
        /// Creates a subject result and adds it to both the school and the subject
        /// navigations so foreign keys are fixed up before the context is saved.
        /// </summary>
        public SchoolSubjectPerformance AddSubjectResult(
            Subject subject,
            int year,
            int totalWrote,
            int? achieved = null,
            decimal? percentAchieved = null)
        {
            var performance = new SchoolSubjectPerformance(year, totalWrote, achieved, percentAchieved);
            school.AddSubjectPerformance(performance);
            subject.AddPerformance(performance);
            return performance;
        }
    }
}
