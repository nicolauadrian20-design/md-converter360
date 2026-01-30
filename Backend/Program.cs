using MDConverter360.Services;
using QuestPDF.Infrastructure;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "MD.converter360")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("Logs/mdconverter360-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting MD.converter360...");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog();

    // Configure Kestrel to listen on port 5294
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenLocalhost(5294);
    });

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Register converter services
    // Pandoc converter (high quality) - registered first so it can be injected into main converter
    builder.Services.AddSingleton<IPandocConverterService, PandocConverterService>();
    // Main converter service (uses Pandoc when available, falls back to C# implementation)
    builder.Services.AddScoped<IConverterService, ConverterService>();

    // Configure CORS for frontend
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5172",
                    "http://127.0.0.1:5172"
                )
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });

    // Configure QuestPDF license
    QuestPDF.Settings.License = LicenseType.Community;

    var app = builder.Build();

    // Configure the HTTP request pipeline
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "MD.converter360 API v1");
        options.RoutePrefix = "swagger";
    });

    app.UseCors("AllowFrontend");

    // Health check endpoint at root
    app.MapGet("/api/health", () => Results.Ok(new
    {
        status = "healthy",
        service = "MD.converter360",
        version = "1.0.0",
        timestamp = DateTime.UtcNow
    }));

    app.MapControllers();

    Log.Information("MD.converter360 started successfully");
    Log.Information("Backend:  http://localhost:5294");
    Log.Information("Swagger:  http://localhost:5294/swagger");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
