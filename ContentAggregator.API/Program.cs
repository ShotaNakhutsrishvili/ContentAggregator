using ContentAggregator.API.Hosting;
using ContentAggregator.Infrastructure.Hosting;

DevelopmentEnvironmentBootstrap.LoadSecretsForDevelopment();

var builder = WebApplication.CreateBuilder(args);
builder.AddApiHostServices();

var app = builder.Build();
app.UseApiPipeline();
app.Run();

public partial class Program;
