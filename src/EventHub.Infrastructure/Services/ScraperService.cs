using EventHub.Domain.Entities;
using EventHub.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EventHub.Infrastructure.Services;
public class ScraperService
{
    private readonly IEnumerable<IEventScraper> _scrapers;
    private readonly IEventRepository _repository;
    private readonly IDeduplicationService _deduplicationService;
    private readonly ILogger<ScraperService> _logger;

    public ScraperService(
        IEnumerable<IEventScraper> scrapers,
        IEventRepository repository,
        IDeduplicationService deduplicationService,
        ILogger<ScraperService> logger)
    {
        _scrapers = scrapers ?? throw new ArgumentNullException(nameof(scrapers));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _deduplicationService = deduplicationService ?? throw new ArgumentNullException(nameof(deduplicationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ScrapeAllSourcesAsync()
    {
        _logger.LogInformation("=== Starting scraping job for all sources ===");
        var startTime = DateTime.UtcNow;
        var totalEventsScraped = 0;
        var totalEventsSaved = 0;
        var successfulScrapers = 0;
        var failedScrapers = 0;

        var scrapersList = _scrapers.ToList();

        if (!scrapersList.Any())
        {
            _logger.LogWarning("No scrapers registered! Check DI configuration.");
            return;
        }

        _logger.LogInformation("Found {Count} registered scrapers", scrapersList.Count);

        // Run each scraper
        foreach (var scraper in scrapersList)
        {
            try
            {
                _logger.LogInformation("Processing scraper: {Source}", scraper.SourceName);

                // Check if scraper is healthy before running
                var isHealthy = await scraper.IsHealthyAsync();
                if (!isHealthy)
                {
                    _logger.LogWarning(
                        "Scraper {Source} is unhealthy, skipping",
                        scraper.SourceName
                    );
                    failedScrapers++;
                    continue;
                }

                // Scrape events from this source
                var events = await scraper.ScrapeAsync();

                if (events.Any())
                {
                    // Activate all events (make them visible)
                    foreach (var @event in events)
                    {
                        try
                        {
                            @event.Activate();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "Could not activate event {Title} from {Source}",
                                @event.Title,
                                scraper.SourceName
                            );
                        }
                    }

                    // 🆕 DEDUPLICATION - Process events to find duplicates
                    var deduplicationResult = await _deduplicationService.ProcessEventsAsync(events);

                    // Save new events only
                    if (deduplicationResult.NewEvents.Any())
                    {
                        await _repository.AddRangeAsync(deduplicationResult.NewEvents);
                        _logger.LogInformation(
                            "Added {Count} new events from {Source}",
                            deduplicationResult.NewEvents.Count,
                            scraper.SourceName
                        );
                    }

                    // Update existing events with better data
                    foreach (var pair in deduplicationResult.ExistingEvents)
                    {
                        MergeEventData(pair.ExistingEvent, pair.ScrapedEvent);
                        await _repository.UpdateAsync(pair.ExistingEvent);
                    }

                    if (deduplicationResult.ExistingEvents.Any())
                    {
                        _logger.LogInformation(
                            "Updated {Count} existing events from {Source}",
                            deduplicationResult.ExistingEvents.Count,
                            scraper.SourceName
                        );
                    }

                    if (deduplicationResult.SkippedEvents.Any())
                    {
                        _logger.LogInformation(
                            "Skipped {Count} duplicate events from {Source}",
                            deduplicationResult.SkippedEvents.Count,
                            scraper.SourceName
                        );
                    }

                    totalEventsScraped += events.Count;
                    totalEventsSaved += deduplicationResult.NewEvents.Count;

                    _logger.LogInformation(
                        "Processed {Source}: {Total} scraped, {New} new, {Updated} updated, {Skipped} skipped",
                        scraper.SourceName,
                        events.Count,
                        deduplicationResult.NewEvents.Count,
                        deduplicationResult.ExistingEvents.Count,
                        deduplicationResult.SkippedEvents.Count
                    );

                    successfulScrapers++;
                }
                else
                {
                    _logger.LogInformation(
                        "No events found from {Source}",
                        scraper.SourceName
                    );
                    successfulScrapers++;
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with other scrapers
                _logger.LogError(
                    ex,
                    "Error processing scraper {Source}: {Message}",
                    scraper.SourceName,
                    ex.Message
                );
                failedScrapers++;
            }
        }

        // Log summary
        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation(
            "=== Scraping job completed ===" +
            "\n  Duration: {Duration}ms" +
            "\n  Total Scrapers: {Total}" +
            "\n  Successful: {Success}" +
            "\n  Failed: {Failed}" +
            "\n  Events Scraped: {Scraped}" +
            "\n  Events Saved: {Saved}",
            duration.TotalMilliseconds,
            scrapersList.Count,
            successfulScrapers,
            failedScrapers,
            totalEventsScraped,
            totalEventsSaved
        );
    }

    public async Task ScrapeSourceAsync(string sourceName)
    {
        _logger.LogInformation("Starting targeted scrape for source: {Source}", sourceName);

        var scraper = _scrapers.FirstOrDefault(s =>
            s.SourceName.Equals(sourceName, StringComparison.OrdinalIgnoreCase));

        if (scraper == null)
        {
            _logger.LogWarning("Scraper not found for source: {Source}", sourceName);
            return;
        }

        try
        {
            var isHealthy = await scraper.IsHealthyAsync();
            if (!isHealthy)
            {
                _logger.LogWarning("Scraper {Source} is unhealthy", sourceName);
                return;
            }

            var events = await scraper.ScrapeAsync();

            if (events.Any())
            {
                foreach (var @event in events)
                {
                    @event.Activate();
                }

                await _repository.AddRangeAsync(events);

                _logger.LogInformation(
                    "Scraped and saved {Count} events from {Source}",
                    events.Count,
                    sourceName
                );
            }
            else
            {
                _logger.LogInformation("No events found from {Source}", sourceName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error scraping {Source}: {Message}",
                sourceName,
                ex.Message
            );
        }
    }

    public async Task<ScraperStatistics> GetStatisticsAsync()
    {
        var stats = new ScraperStatistics
        {
            TotalScrapers = _scrapers.Count(),
            Scrapers = new List<ScraperInfo>()
        };

        foreach (var scraper in _scrapers)
        {
            var isHealthy = await scraper.IsHealthyAsync();

            stats.Scrapers.Add(new ScraperInfo
            {
                Name = scraper.SourceName,
                IsHealthy = isHealthy
            });

            if (isHealthy)
                stats.HealthyScrapers++;
            else
                stats.UnhealthyScrapers++;
        }

        return stats;
    }

    private void MergeEventData(Event existing, Event scraped)
    {
        // Update description if scraped version is longer/better
        if (scraped.Description.Length > existing.Description.Length)
        {
            existing.UpdateDescription(scraped.Description);
        }

        // Update title if it's more detailed
        if (scraped.Title.Length > existing.Title.Length &&
            !scraped.Title.Equals(existing.Title, StringComparison.OrdinalIgnoreCase))
        {
            existing.UpdateTitle(scraped.Title);
        }

        // Update dates if scraped version is more precise
        if (scraped.StartDate.TimeOfDay != TimeSpan.Zero &&
            existing.StartDate.TimeOfDay == TimeSpan.Zero)
        {
            existing.Reschedule(scraped.StartDate, scraped.EndDate);
        }

        _logger.LogDebug(
            "Merged data for event '{Title}' (ID: {Id})",
            existing.Title,
            existing.Id
        );
    }
}

public class ScraperStatistics
{
    public int TotalScrapers { get; set; }
    public int HealthyScrapers { get; set; }
    public int UnhealthyScrapers { get; set; }
    public List<ScraperInfo> Scrapers { get; set; } = new();
}

public class ScraperInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
}

