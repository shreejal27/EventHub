using EventHub.Domain.Interfaces;
using EventHub.Infrastructure.Persistence;
using EventHub.Infrastructure.Persistence.Repositories;
using EventHub.Infrastructure.Scrapers.Eventbrite;
using EventHub.Infrastructure.Services;
using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers
builder.Services.AddControllers();

// Add Swagger/OpenAPI (for testing API)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register DbContext with SQLite
builder.Services.AddDbContext<EventHubDbContext>(options =>
{
    // SQLite connection string - creates eventhub.db in API project folder
    options.UseSqlite("Data Source=eventhub.db");

    // Enable detailed errors in development (helpful for debugging)
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Configure Hangfire to use SQLite storage
builder.Services.AddHangfire(config =>
{
    config.UseSQLiteStorage("Data Source=hangfire.db");
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180);
    config.UseSimpleAssemblyNameTypeSerializer();
    config.UseRecommendedSerializerSettings();
});

// Add Hangfire server (processes background jobs)
builder.Services.AddHangfireServer();


// Register repository with Dependency Injection
// When someone asks for IEventRepository, give them EventRepository
builder.Services.AddScoped<IEventRepository, EventRepository>();


// Register HttpClient for scrapers
builder.Services.AddHttpClient();

// Register scraper options
builder.Services.AddSingleton(new EventbriteScraperOptions
{
    ApiKey = "", // Would come from configuration in production
    Location = "Kathmandu, Nepal",
    DefaultCity = "Kathmandu",
    DefaultCountry = "Nepal",
    DefaultLatitude = 27.7172,
    DefaultLongitude = 85.3240
});

// Register Eventbrite scraper
builder.Services.AddScoped<IEventScraper, EventbriteScraper>();

// Register scraper service
builder.Services.AddScoped<ScraperService>();

// Add more scrapers here as you build them:
// builder.Services.AddScoped<IEventScraper, MeetupScraper>();
// builder.Services.AddScoped<IEventScraper, FacebookScraper>();

var app = builder.Build();


// Enable Swagger in development (API documentation UI)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = Array.Empty<Hangfire.Dashboard.IDashboardAuthorizationFilter>()
});

// Schedule recurring job to scrape all sources every hour
RecurringJob.AddOrUpdate<ScraperService>(
    "scrape-all-events",
    service => service.ScrapeAllSourcesAsync(),
    Cron.Hourly  // Run every hour at minute 0
    //Cron.Minutely
);

// Map controller routes
app.MapControllers();

app.Run();