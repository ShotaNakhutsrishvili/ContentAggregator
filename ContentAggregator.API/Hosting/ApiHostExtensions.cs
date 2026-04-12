using ContentAggregator.API.Services;
using ContentAggregator.API.Services.Middlewares;
using ContentAggregator.Application.DependencyInjection;
using ContentAggregator.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography.X509Certificates;

namespace ContentAggregator.API.Hosting
{
    public static class ApiHostExtensions
    {
        private const string CorsPolicyName = "AllowSpecificOrigin";

        public static WebApplicationBuilder AddApiHostServices(this WebApplicationBuilder builder)
        {
            builder.ConfigureHttpsCertificate();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(CorsPolicyName, policy =>
                {
                    policy.WithOrigins("https://localhost:7084")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            builder.Services.AddControllers(options =>
                {
                    options.Filters.Add<OperationCanceledExceptionFilter>();
                })
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context =>
                    {
                        var problemDetails = new CustomValidationProblemDetails(context, builder.Environment);
                        return new BadRequestObjectResult(problemDetails);
                    };
                });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GeneralErrorHandler>();
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();
            builder.Services.AddApplicationServices();
            builder.Services.AddInfrastructureServices(builder.Configuration);

            return builder;
        }

        public static WebApplication UseApiPipeline(this WebApplication app)
        {
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors(CorsPolicyName);
            app.UseExceptionHandler();
            app.UseMiddleware<ResponseTimerMiddleware>();
            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            return app;
        }

        private static void ConfigureHttpsCertificate(this WebApplicationBuilder builder)
        {
            if (builder.Environment.IsDevOrQA())
            {
                return;
            }

            var certificateOptions = builder.Configuration
                .GetSection(HttpsCertificateOptions.SectionName)
                .Get<HttpsCertificateOptions>()
                ?? new HttpsCertificateOptions();

            if (string.IsNullOrWhiteSpace(certificateOptions.Path))
            {
                throw new InvalidOperationException(
                    $"Configuration section '{HttpsCertificateOptions.SectionName}' must define '{nameof(HttpsCertificateOptions.Path)}' outside development.");
            }

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ConfigureHttpsDefaults(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = X509CertificateLoader.LoadPkcs12FromFile(
                        certificateOptions.Path,
                        certificateOptions.Password,
                        X509KeyStorageFlags.DefaultKeySet,
                        Pkcs12LoaderLimits.Defaults);
                });
            });
        }
    }
}
