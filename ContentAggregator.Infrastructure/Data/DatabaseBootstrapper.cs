using Microsoft.EntityFrameworkCore;

namespace ContentAggregator.Infrastructure.Data
{
    public sealed class DatabaseBootstrapper
    {
        private readonly DatabaseContext _databaseContext;

        public DatabaseBootstrapper(DatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
        }

        public async Task<int> ApplyMigrationsAsync(CancellationToken cancellationToken = default)
        {
            var pendingMigrations = await _databaseContext.Database
                .GetPendingMigrationsAsync(cancellationToken);

            var migrationCount = pendingMigrations.Count();
            await DbInitializer.ApplyMigrationsAsync(_databaseContext, cancellationToken);

            return migrationCount;
        }

        public Task SeedDevelopmentDataAsync(CancellationToken cancellationToken = default)
        {
            return DbInitializer.SeedDevelopmentDataAsync(_databaseContext, cancellationToken);
        }
    }
}
