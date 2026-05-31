using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MatricDasbhoard.Infrastructure.Persistence;

namespace MatricDasbhoard.Component.Tests.Fixtures;

internal static class TestDbContextFactory
{
    public static MatricDasbhoardDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<MatricDasbhoardDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new MatricDasbhoardDbContext(options);
    }
}
