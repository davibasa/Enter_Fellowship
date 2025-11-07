
using Enter_Extractor_Api.Middleware;
using Enter_Extractor_Api.Services;
using Scalar.AspNetCore;
using Enter_Extractor_Api.Models.Cache;
using Enter_Extractor_Api.Models.Redis;
using Enter_Extractor_Api.Services.Cache;
using Enter_Extractor_Api.Services.Redis;
using StackExchange.Redis;
using Enter_Extractor_Api.Services.Extractor;
using Enter_Extractor_Api.Services.Template;

namespace Enter_Extractor_Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.Configure<RedisConfig>(
                builder.Configuration.GetSection("Redis")
            );


            var redisCacheConnectionString = builder.Configuration["Redis:Cache:ConnectionString"] ?? "localhost:6379";
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var config = ConfigurationOptions.Parse(redisCacheConnectionString);
                config.AbortOnConnectFail = false;
                config.ConnectTimeout = 5000;
                config.SyncTimeout = 5000;
                return ConnectionMultiplexer.Connect(config);
            });

            builder.Services.Configure<RedisConfiguration>(
                builder.Configuration.GetSection("Redis")
            );

            builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();
            builder.Services.AddSingleton<IRedisPersistenceService, RedisPersistenceService>();
            builder.Services.AddScoped<IMetricsCacheService, MetricsCacheService>();
            builder.Services.AddScoped<IExtractionCacheService, ExtractionCacheService>();

            builder.Services.AddScoped<Services.Template.ITemplateService, Services.Template.TemplateService>();

            var pythonApiUrl = builder.Configuration["ExtractionV2:PythonApiUrl"] ?? "http://localhost:5001";


            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins(
                            "http://localhost:3000",
                            "http://localhost:3001",
                            "http://localhost:5173"
                          )
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
            });
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<ITemplateStore, TemplateStore>();
            builder.Services.AddScoped<IPdfTextExtractor, PdfTextExtractor>();

            builder.Services.AddScoped<Services.SmartExtraction.IEnumParser, Services.SmartExtraction.EnumParser>();
            builder.Services.AddScoped<Services.SmartExtraction.IFieldTypeClassifier, Services.SmartExtraction.FieldTypeClassifier>();
            builder.Services.AddScoped<ITokenExtractor, TokenExtractor>();
            builder.Services.AddScoped<IExtractorService, ExtractorService>();

            builder.Services.AddScoped<Services.LabelDetection.ILabelDetectionService, Services.LabelDetection.LabelDetectionService>();

            builder.Services.AddSingleton<IBatchJobService, BatchJobService>();

            builder.Services.AddHttpClient<Services.SmartExtraction.IZeroShotClassifier, Services.SmartExtraction.ZeroShotClassifierClient>(client =>
            {
                var pythonZeroShotUrl = builder.Configuration["ZeroShot:PythonApiUrl"] ?? "http://localhost:5056";
                client.BaseAddress = new Uri(pythonZeroShotUrl);
                client.Timeout = TimeSpan.FromSeconds(300);
            });

            builder.Services.AddHttpClient<Services.SmartExtraction.IPythonExtractorClient, Services.SmartExtraction.PythonExtractorClient>(client =>
            {
                var pythonApiUrl = builder.Configuration["SmartExtraction:PythonApiUrl"]
                    ?? builder.Configuration["ZeroShot:PythonApiUrl"]
                    ?? "http://localhost:5056";
                client.BaseAddress = new Uri(pythonApiUrl);
                client.Timeout = TimeSpan.FromSeconds(300);
            });


            builder.Services.AddHttpClient<IPdfTextExtractor, PdfTextExtractor>(client =>
            {
                var pythonApiUrl = builder.Configuration["PythonApi:BaseUrl"] ?? "http://pdf-extractor:5000";
                client.BaseAddress = new Uri(pythonApiUrl);
                client.Timeout = TimeSpan.FromMinutes(2);
            });

            var app = builder.Build();
            app.UseMiddleware<TraceMiddleware>();

            app.MapScalarApiReference();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseCors("AllowFrontend");

            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
