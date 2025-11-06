
using Enter_Extractor_Api.Middleware;
using Enter_Extractor_Api.Services;
using Enter_Extractor_Api.Services.V2.PythonClients;
using Enter_Extractor_Api.Services.V2;
using Enter_Extractor_Api.Services.Proximity;
using Scalar.AspNetCore;
using Enter_Extractor_Api.Models.V2;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Enter_Extractor_Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Configuração da seção ExtractionV2
            builder.Services.Configure<ExtractionV2Config>(
                builder.Configuration.GetSection("ExtractionV2")
            );

            // 2. Middleware de Trace
            //builder.Services.AddTransient<TraceMiddleware>();

            // 3. HttpClient Factory configurado com Polly Policies
            var pythonApiUrl = builder.Configuration["ExtractionV2:PythonApiUrl"] ?? "http://localhost:5001";

            // 3.1. NLI Client (Timeout: 10s, Retry: 3x, Circuit Breaker: 5 falhas)
            builder.Services.AddHttpClient<IPythonNliClient, PythonNliClient>(client =>
            {
                client.BaseAddress = new Uri(pythonApiUrl);
                client.Timeout = TimeSpan.FromSeconds(300);
            });

            // 3.2. Smart Extract Client (Timeout: 20s, Retry: 3x, Circuit Breaker: 5 falhas)
            builder.Services.AddHttpClient<IPythonSmartExtractClient, PythonSmartExtractClient>(client =>
            {
                client.BaseAddress = new Uri(pythonApiUrl);
                client.Timeout = TimeSpan.FromSeconds(300);
            });

            // 3.3. Fallback Client (Timeout: 30s, Retry: 2x, Circuit Breaker: 3 falhas)
            builder.Services.AddHttpClient<IPythonFallbackClient, PythonFallbackClient>(client =>
            {
                client.BaseAddress = new Uri(pythonApiUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // 3.4. Metrics Client (Timeout: 1.5s, sem retry - usa fallback)
            builder.Services.AddHttpClient<IPythonMetricsClient, PythonMetricsClient>(client =>
            {
                client.BaseAddress = new Uri(pythonApiUrl);
                client.Timeout = TimeSpan.FromMilliseconds(1500);
            });

            // 4. Serviços de Extração V2
            builder.Services.AddScoped<FinalOptimizerService>();
            builder.Services.AddScoped<ExtractionOrchestratorService>();


            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            // Register custom services
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
            builder.Services.AddSingleton<ITemplateStore, TemplateStore>();
            builder.Services.AddScoped<IPdfTextExtractor, PdfTextExtractor>();
            builder.Services.AddScoped<IHeuristicExtractor, HeuristicExtractor>();
            builder.Services.AddScoped<ILLMService, OpenAIService>();
            builder.Services.AddScoped<IExtractionService, ExtractionService>();

            // Smart Extraction services (Fase 1 - sem ML)
            builder.Services.AddScoped<Services.SmartExtraction.IEnumParser, Services.SmartExtraction.EnumParser>();
            builder.Services.AddScoped<Services.SmartExtraction.IFieldTypeClassifier, Services.SmartExtraction.FieldTypeClassifier>();
            builder.Services.AddScoped<Services.Proximity.ILabelDetectorProximity, Services.Proximity.LabelDetectorProximity>();
            builder.Services.AddScoped<ILabelDetector, LabelDetector>();
            builder.Services.AddScoped<ITextTokenizer, TextTokenizer>();
            //builder.Services.AddScoped<Services.SmartExtraction.ISimpleTokenExtractor, Services.SmartExtraction.SimpleTokenExtractor>();
            //builder.Services.AddScoped<Services.SmartExtraction.IAdaptiveMultiLineExtractor, Services.SmartExtraction.AdaptiveMultiLineExtractor>();
            //builder.Services.AddScoped<Services.SmartExtraction.ISequentialExtractor, Services.SmartExtraction.SequentialExtractor>();
            builder.Services.AddScoped<ITokenExtractor, TokenExtractor>();
            builder.Services.AddScoped<IExtractorService, ExtractorService>();

            // ⭐ FASE 2: Zero-Shot Classification (Python API via HTTP)
            builder.Services.AddHttpClient<Services.SmartExtraction.IZeroShotClassifier, Services.SmartExtraction.ZeroShotClassifierClient>(client =>
            {
                var pythonZeroShotUrl = builder.Configuration["ZeroShot:PythonApiUrl"] ?? "http://localhost:5000";
                client.BaseAddress = new Uri(pythonZeroShotUrl);
                client.Timeout = TimeSpan.FromSeconds(300);
            });
            builder.Services.AddScoped<INLIValidator, NLIValidator>();

            // ⭐ FASE 2.5: Python Extractor Client (NLI + Smart Extract)
            builder.Services.AddHttpClient<Services.SmartExtraction.IPythonExtractorClient, Services.SmartExtraction.PythonExtractorClient>(client =>
            {
                var pythonApiUrl = builder.Configuration["SmartExtraction:PythonApiUrl"]
                    ?? builder.Configuration["ZeroShot:PythonApiUrl"]
                    ?? "http://localhost:5000";
                client.BaseAddress = new Uri(pythonApiUrl);
                client.Timeout = TimeSpan.FromSeconds(300); // Timeout maior para Smart Extract + GPT
            });

            // ⭐ FASE 2.5: Smart Extractor Client (NER + Embeddings + Cache Redis)
            builder.Services.AddHttpClient<Services.SmartExtraction.ISmartExtractorClient, Services.SmartExtraction.SmartExtractorClient>(client =>
            {
                var pythonApiUrl = builder.Configuration["SmartExtraction:PythonApiUrl"]
                    ?? builder.Configuration["ZeroShot:PythonApiUrl"]
                    ?? "http://localhost:5001";
                client.BaseAddress = new Uri(pythonApiUrl);
                client.Timeout = TimeSpan.FromSeconds(300); // Timeout para NER + Embeddings
            });

            // Add HttpClient for OpenAI
            builder.Services.AddHttpClient<ILLMService, OpenAIService>();

            // Add HttpClient for PdfTextExtractor (Python API)
            builder.Services.AddHttpClient<IPdfTextExtractor, PdfTextExtractor>(client =>
            {
                var pythonApiUrl = builder.Configuration["PythonApi:BaseUrl"] ?? "http://pdf-extractor:5000";
                client.BaseAddress = new Uri(pythonApiUrl);
                client.Timeout = TimeSpan.FromMinutes(2); // Timeout para PDFs grandes
            });

            var app = builder.Build();
            app.UseMiddleware<TraceMiddleware>();

            app.MapScalarApiReference();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
