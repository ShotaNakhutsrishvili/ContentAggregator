using ContentAggregator.Core.Interfaces;
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

namespace ContentAggregator.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
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

            builder.Services.AddHttpClient(HttpClientNames.Default);
            builder.Services.AddHttpClient(HttpClientNames.LongTimeout, client =>
            {
                client.Timeout = TimeSpan.FromMinutes(40);
            });
            builder.Services.AddHttpClient(nameof(YoutubeCommentService), client =>
            {
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
            builder.Services.AddScoped<SummarizerService>();
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

            app.UseCors("AllowSpecificOrigin");

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

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

            recurringJobs.AddOrUpdate<SummarizerService>(
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
    }
}
