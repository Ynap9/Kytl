using Asp.Versioning;
using ktl.be.applications.Document.Implements;
using ktl.be.applications.Document.Interfaces;
using ktl.be.infrastructure.external.Documents.Implements;
using ktl.be.infrastructure.external.Documents.Interfaces;
using ktl.be.shared.Constants;
using NLog;
using NLog.Web;
using System.Globalization;

namespace ktl.be.api
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            try
            {
                logger.Info("Starting application...");
                BuildApp(args).Run();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Application stopped because of an exception");
                throw;
            }
            finally
            {
                LogManager.Shutdown();
            }
        }

        public static WebApplication BuildApp(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Logging.ClearProviders();
            builder.Host.UseNLog();

            var origins = (builder.Configuration.GetSection("AllowedHosts").Value ?? string.Empty)
                .Split(';')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(
                    ProgramExtensions.CorsPolicy,
                    policy => policy
                        .WithOrigins(origins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .WithExposedHeaders("Content-Disposition"));
            });

            builder.Services.AddScoped<ISignService, SignService>();
            builder.Services.AddScoped<IPdfSignatureReader, PdfSignatureReader>();
            builder.Services.AddScoped<ISignatureVerifier, SignatureVerifier>();
            builder.Services.AddScoped<ICertificateProvider, CertificateProvider>();
            builder.Services.AddScoped<ISigningKey, StoreSigningKey>();
            builder.Services.AddScoped<ITimestampClient, TimestampClient>();
            builder.Services.AddScoped<IPdfSigner, PdfSigner>();
            builder.Services.AddScoped<ISignaturePlacementResolver, SignaturePlacementResolver>();
            builder.Services.AddScoped<IPdfPageInspector, PdfPageInspector>();
            builder.Services.AddScoped<IDocumentMergeService, DocumentMergeService>();
            builder.Services.AddScoped<IExcelReader, ExcelReader>();
            builder.Services.AddScoped<IWordTemplateEngine, WordTemplateEngine>();
            builder.Services.AddScoped<IWordPdfConverter, WordPdfConverter>();
            builder.Services.AddScoped<IPdfImageStamper, PdfImageStamper>();
            builder.Services.AddScoped<IFileUploadService, FileUploadService>();
            builder.Services.AddScoped<IStampPlanner, StampPlanner>();

            builder.Services.AddControllers()
                .AddApplicationPart(typeof(Program).Assembly);
            builder.Services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(ApiVersionConstants.DefaultMajor, ApiVersionConstants.DefaultMinor);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
                .AddMvc()
                .AddApiExplorer(options =>
                {
                    options.GroupNameFormat = ApiVersionConstants.GroupNameFormat;
                    options.SubstituteApiVersionInUrl = true;
                });
            builder.Services.AddHealthChecks();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new() { Title = "Sao Mai - Ký số API", Version = "v1" });
            });

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseRequestLocalization(new RequestLocalizationOptions()
                .SetDefaultCulture(CultureInfo.InvariantCulture.Name)
                .AddSupportedCultures(CultureInfo.InvariantCulture.Name)
                .AddSupportedUICultures(CultureInfo.InvariantCulture.Name));

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseCors(ProgramExtensions.CorsPolicy);

            app.MapControllers();
            app.MapHealthChecks("/health");
            app.MapFallbackToFile("index.html");

            return app;
        }
    }
}
