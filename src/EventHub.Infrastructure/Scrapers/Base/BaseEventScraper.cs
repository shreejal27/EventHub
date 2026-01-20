using EventHub.Domain.Entities;
using EventHub.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EventHub.Infrastructure.Scrapers.Base;

public abstract class BaseEventScraper : IEventScraper
{
    protected readonly ILogger Logger;
    protected readonly HttpClient HttpClient;

    protected BaseEventScraper(ILogger logger, HttpClient httpClient)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public abstract string SourceName { get; }

    public async Task<List<Event>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Starting scrape for {Source}", SourceName);
        var startTime = DateTime.UtcNow;

        try
        {
            var events = await ScrapeEventsInternalAsync(cancellationToken);

            var validEvents = ValidateAndFilterEvents(events);

            var duration = DateTime.UtcNow - startTime;
            Logger.LogInformation(
                "Scrape completed for {Source}. Found {Total} events, {Valid} valid. Duration: {Duration}ms",
                SourceName,
                events.Count,
                validEvents.Count,
                duration.TotalMilliseconds
            );

            return validEvents;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Scrape cancelled for {Source}", SourceName);
            return new List<Event>();
        }
        catch (Exception ex)
        {
            // Log error but don't throw - we want other scrapers to continue
            Logger.LogError(ex, "Error scraping {Source}: {Message}", SourceName, ex.Message);
            return new List<Event>();
        }
    }


    protected abstract Task<List<Event>> ScrapeEventsInternalAsync(CancellationToken cancellationToken);

    public virtual async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await HttpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, HttpClient.BaseAddress),
                cancellationToken
            );

            var isHealthy = response.IsSuccessStatusCode;

            if (!isHealthy)
            {
                Logger.LogWarning(
                    "{Source} health check failed with status {StatusCode}",
                    SourceName,
                    response.StatusCode
                );
            }

            return isHealthy;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Source} health check failed: {Message}", SourceName, ex.Message);
            return false;
        }
    }
    protected virtual List<Event> ValidateAndFilterEvents(List<Event> events)
    {
        var validEvents = new List<Event>();

        foreach (var @event in events)
        {
            if (IsEventValid(@event))
            {
                validEvents.Add(@event);
            }
            else
            {
                Logger.LogWarning(
                    "Invalid event filtered out from {Source}: {Title}",
                    SourceName,
                    @event?.Title ?? "Unknown"
                );
            }
        }

        return validEvents;
    }

    protected virtual bool IsEventValid(Event @event)
    {
        if (@event == null)
            return false;

        // Basic validation - Event constructor already validates most things
        // But we can add scraper-specific checks here

        // Check if event is too far in the past
        if (@event.EndDate < DateTime.UtcNow.AddDays(-7))
        {
            Logger.LogDebug("Event is too old: {Title}", @event.Title);
            return false;
        }

        // Check if event is too far in the future (spam/test events)
        if (@event.StartDate > DateTime.UtcNow.AddYears(2))
        {
            Logger.LogDebug("Event is too far in future: {Title}", @event.Title);
            return false;
        }

        // All good!
        return true;
    }

    protected DateTime? TryParseDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        // Try ISO 8601 format first (most APIs use this)
        if (DateTime.TryParse(dateString, out var date))
            return date;

        // Log if we couldn't parse
        Logger.LogDebug("Could not parse date: {DateString}", dateString);
        return null;
    }
}