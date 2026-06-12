using Api.Middlewares;
using Application.Interfaces;
using Application.Services;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using System.Text.Encodings.Web;

// Bootstrap logger captures startup errors before full Serilog config loads.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration)
                     .ReadFrom.Services(services)
                     .Enrich.FromLogContext());

    var connectionString = builder.Configuration.GetConnectionString("PostgresConnection");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));

    builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

    // Repositories
    builder.Services.AddScoped<IAccountRepository, AccountRepository>();
    builder.Services.AddScoped<ITransferRepository, TransferRepository>();
    builder.Services.AddScoped<IReportRepository, ReportRepository>();

    // Services
    builder.Services.AddScoped<IAccountService, AccountService>();
    builder.Services.AddScoped<ITransferService, TransferService>();
    builder.Services.AddScoped<IReportService, ReportService>();

    // UC7: exchange rate — typed HttpClient with 10s timeout; caches internally for 1h
    builder.Services.AddHttpClient<IExchangeRateService, ExchangeRateService>(client =>
        client.Timeout = TimeSpan.FromSeconds(10));

    builder.Services.AddMemoryCache();

    builder.Services.AddControllers()
        .AddJsonOptions(o => o.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
    builder.Services.AddOpenApi();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
        app.MapGet("/", () => Results.Redirect("/scalar"));
    }

    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "La aplicación falló al iniciar.");
}
finally
{
    Log.CloseAndFlush();
}
