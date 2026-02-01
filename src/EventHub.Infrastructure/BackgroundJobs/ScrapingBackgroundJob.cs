using EventHub.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventHub.Infrastructure.BackgroundJobs;

public class ScrapingBackgroundJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScrapingBackgroundJob> _logger;

    public ScrapingBackgroundJob(
        IServiceProvider serviceProvider,
        ILogger<ScrapingBackgroundJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting Hangfire scraping job...");

        using (var scope = _serviceProvider.CreateScope())
        {
            try
            {
                var scraperService = scope.ServiceProvider.GetRequiredService<ScraperService>();

                await scraperService.ScrapeAllSourcesAsync();

                _logger.LogInformation("✅ Hangfire scraping job completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hangfire scraping job failed: {Message}", ex.Message);
                throw; 
            }
        }
    }
}