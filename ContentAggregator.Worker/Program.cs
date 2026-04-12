using ContentAggregator.Infrastructure.Data;
using ContentAggregator.Infrastructure.Hosting;
using ContentAggregator.Worker.Hosting;

DevelopmentEnvironmentBootstrap.LoadSecretsForDevelopment();

if (WorkerBootstrapCommand.TryParse(args, out var bootstrapCommand))
{
    var builder = Host.CreateApplicationBuilder(bootstrapCommand!.HostArguments);
    builder.AddWorkerBootstrapServices();

    using var host = builder.Build();
    using var scope = host.Services.CreateScope();

    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("ContentAggregator.Worker.DatabaseBootstrap");
    var bootstrapper = scope.ServiceProvider.GetRequiredService<DatabaseBootstrapper>();

    var appliedMigrationCount = await bootstrapper.ApplyMigrationsAsync();
    logger.LogInformation("Database bootstrap applied {MigrationCount} pending migrations.", appliedMigrationCount);

    if (bootstrapCommand.SeedDevelopmentData)
    {
        if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Development seed data can only be applied when DOTNET_ENVIRONMENT or ASPNETCORE_ENVIRONMENT is Development.");
        }

        await bootstrapper.SeedDevelopmentDataAsync();
        logger.LogInformation("Development seed data bootstrap completed.");
    }

    return;
}

var workerBuilder = Host.CreateApplicationBuilder(args);
workerBuilder.AddWorkerHostServices();

using var workerHost = workerBuilder.Build();
await workerHost.RunAsync();
