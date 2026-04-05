using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Services.Features;
using ContentAggregator.Application.Services.Subtitles;
using ContentAggregator.Application.Services.Summarization;
using ContentAggregator.Application.Services.Youtube;
using ContentAggregator.Application.Services.YoutubeContents;
using ContentAggregator.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using ContentAggregator.Infrastructure.Data;
using ContentAggregator.API.Services;
using Microsoft.AspNetCore.Mvc;
using ContentAggregator.API.Services.Middlewares;
using ContentAggregator.Core.Services;
using ContentAggregator.API.Services.BackgroundServices;
using Hangfire;
using Hangfire.PostgreSql;
using System.Security.Cryptography.X509Certificates;
using dotenv.net;
using Microsoft.Extensions.Options;
using ContentAggregator.Infrastructure.Services.Subtitles;
using ContentAggregator.Infrastructure.Services.Summarization;
using ContentAggregator.Infrastructure.Services.Youtube;

namespace ContentAggregator.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            LoadSecretsForDevelopment();
            var builder = WebApplication.CreateBuilder(args);

            ConfigureKestrel(builder, builder.Environment);

            // Add services to the container.

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigin",
                    builder =>
                    {
                        builder.WithOrigins("https://localhost:7084")
                               .AllowAnyHeader()
                               .AllowAnyMethod();
                    });
            });

            builder.Services.AddControllers(options =>
                {
                    //options.Filters.Add<ValidateModelFilter>();
                    options.Filters.Add<OperationCanceledExceptionFilter>();
                })
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context =>
                    {
                        var problemDetails = new CustomValidationProblemDetails(context, builder.Environment); return new BadRequestObjectResult(problemDetails);
                    };
                })
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                });
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GeneralErrorHandler>();

            var configuration = builder.Configuration;
            builder.Services.AddDbContext<DatabaseContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("PostgreSQLConnection")));

            builder.Services.AddScoped<IFeatureRepository, FeatureRepository>();
            builder.Services.AddScoped<IYTChannelRepository, YTChannelRepository>();
            builder.Services.AddScoped<IYoutubeContentRepository, YoutubeContentRepository>();
            builder.Services.AddScoped<IFeatureService, FeatureService>();
            builder.Services.AddScoped<ISubtitleWorkflow, SubtitleWorkflow>();
            builder.Services.AddScoped<ISubtitleDownloader, YtDlpSubtitleDownloader>();
            builder.Services.AddScoped<ISummarizationWorkflow, SummarizationWorkflow>();
            builder.Services.AddScoped<IYoutubeChannelService, YoutubeChannelService>();
            builder.Services.AddScoped<IYoutubeDiscoveryWorkflow, YoutubeDiscoveryWorkflow>();
            builder.Services.AddScoped<IYoutubeContentQueryService, YoutubeContentQueryService>();
            builder.Services
                .AddOptions<YtDlpOptions>()
                .Bind(configuration.GetSection(YtDlpOptions.SectionName));
            builder.Services
                .AddOptions<LmStudioOptions>()
                .Bind(configuration.GetSection(LmStudioOptions.SectionName))
                .PostConfigure(options =>
                {
                    if (string.IsNullOrWhiteSpace(options.BaseUrl))
                    {
                        options.BaseUrl = configuration["LMStudioApiURL"] ?? configuration["LMSTUDIO_API_URL"] ?? string.Empty;
                    }
                });
            builder.Services
                .AddOptions<YoutubeApiOptions>()
                .Bind(configuration.GetSection(YoutubeApiOptions.SectionName))
                .PostConfigure(options =>
                {
                    if (string.IsNullOrWhiteSpace(options.ApiKey))
                    {
                        options.ApiKey = configuration["YoutubeAccessToken"] ?? string.Empty;
                    }
                });

            builder.Services.AddHttpClient(HttpClientNames.Default);
            builder.Services.AddHttpClient(HttpClientNames.LongTimeout, client =>
            {
                client.Timeout = TimeSpan.FromMinutes(40);
            });
            builder.Services.AddHttpClient(nameof(YoutubeCommentService), client =>
            {
                client.Timeout = TimeSpan.FromMinutes(2);
            });
            builder.Services.AddHttpClient<ISummaryGenerator, LmStudioSummaryGenerator>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<LmStudioOptions>>().Value;
                client.Timeout = TimeSpan.FromMinutes(40);

                if (!string.IsNullOrWhiteSpace(options.BaseUrl)
                    && Uri.TryCreate(EnsureTrailingSlash(options.BaseUrl), UriKind.Absolute, out var baseAddress))
                {
                    client.BaseAddress = baseAddress;
                }
            });
            builder.Services.AddHttpClient<IYoutubeMetadataClient, YoutubeMetadataClient>(client =>
            {
                client.BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/");
                client.Timeout = TimeSpan.FromMinutes(2);
            });

            builder.Services.AddSingleton<FbPoster>(provider =>
            {
                var accessToken = builder.Configuration["FacebookAccessToken"] ?? string.Empty;
                var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
                return new FbPoster(accessToken, httpClientFactory.CreateClient(HttpClientNames.Default));
            });

            builder.Services.AddScoped<YoutubeService>();
            builder.Services.AddScoped<SubtitleService>();
            builder.Services.AddScoped<SummarizerJob>();
            builder.Services.AddScoped<FacebookService>();
            builder.Services.AddScoped<YoutubeCommentService>();

            builder.Services.AddHangfire(config =>
            {
                config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UsePostgreSqlStorage(storage => storage.UseNpgsqlConnection(configuration.GetConnectionString("PostgreSQLConnection")));
            });
            builder.Services.AddHangfireServer();

            builder.Services.AddDatabaseDeveloperPageExceptionFilter();
            // Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            var app = builder.Build();

            CreateDbIfNotExists(app.Services);
            RegisterRecurringJobs(app.Services);

            if (app.Environment.IsDevelopment())
            {
                app.Lifetime.ApplicationStarted.Register(() =>
                {
                    EnqueueStartupPipeline(app.Services);
                });

                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors("AllowSpecificOrigin");

            app.UseHangfireDashboard("/hangfire");
            //app.UseMiddleware<ErrorHandlerMiddleware>();
            app.UseExceptionHandler();
            app.UseMiddleware<ResponseTimerMiddleware>();
            //app.UseMiddleware<CancellationTokenMiddleware>();

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }

        private static void RegisterRecurringJobs(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

            recurringJobs.AddOrUpdate<YoutubeService>(
                "pipeline:youtube-discovery",
                job => job.ProcessOnceAsync(),
                "0 */2 * * *");

            recurringJobs.AddOrUpdate<SubtitleService>(
                "pipeline:subtitle-fetch",
                job => job.ProcessOnceAsync(),
                "*/30 * * * *");

            recurringJobs.AddOrUpdate<SummarizerJob>(
                "pipeline:georgian-summary",
                job => job.ProcessOnceAsync(),
                "*/15 * * * *");

            recurringJobs.AddOrUpdate<FacebookService>(
                "pipeline:facebook-publish",
                job => job.ProcessOnceAsync(),
                "*/5 * * * *");

            recurringJobs.AddOrUpdate<YoutubeCommentService>(
                "pipeline:youtube-comment-publish",
                job => job.ProcessOnceAsync(),
                "*/10 * * * *");
        }

        private static void EnqueueStartupPipeline(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var backgroundJobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();

            var discoveryJobId = backgroundJobs.Enqueue<YoutubeService>(job => job.ProcessOnceAsync());
            var subtitleJobId = backgroundJobs.ContinueJobWith<SubtitleService>(discoveryJobId, job => job.ProcessOnceAsync());
            var summaryJobId = backgroundJobs.ContinueJobWith<SummarizerJob>(subtitleJobId, job => job.ProcessOnceAsync());

            backgroundJobs.ContinueJobWith<FacebookService>(summaryJobId, job => job.ProcessOnceAsync());
            backgroundJobs.ContinueJobWith<YoutubeCommentService>(summaryJobId, job => job.ProcessOnceAsync());
        }

        private static void CreateDbIfNotExists(IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<DatabaseContext>();
                    DbInitializer.Initialize(context);
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred creating the DB.");
                }
            }
        }

        private static void ConfigureKestrel(WebApplicationBuilder builder, IWebHostEnvironment env)
        {
            if (!env.IsDevOrQA())
            {
                var certPath = "/etc/ssl/certs/dev-cert.pfx";
                var certPassword = Environment.GetEnvironmentVariable("CERT_PASSWORD");

                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.ConfigureHttpsDefaults(httpsOptions =>
                    {
                        httpsOptions.ServerCertificate = new X509Certificate2(certPath, certPassword);
                    });
                });
            }
        }

        public static class HttpClientNames
        {
            public const string Default = "default";
            public const string LongTimeout = "longTimeout";
        }

        private static string EnsureTrailingSlash(string value)
        {
            return value.TrimEnd('/') + "/";
        }

        /// <summary>
        /// Use this method before WebApplication.CreateBuilder(args) to load secrets from .env file in development environment.
        /// Otherwise the secrets won't be available for calls like builder.Configuration.GetConnectionString("PostgreSqlConnection").
        /// </summary>
        /// <param name="prefix">location prefix for projects nested with different depth.</param>
        public static void LoadSecretsForDevelopment(string prefix = "..")
        {
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                      ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            // The if check is alternative to using builder.Environment.IsDevelopment().
            // We can't use that here because WebApplication.CreateBuilder(args) returns builder after this method is used.
            if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
            {
                DotEnv.Load(options: new DotEnvOptions(envFilePaths: new[] { $"{prefix}/.env" }));
            }
        }
    }
}
