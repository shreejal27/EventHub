using EventHub.Domain.Entities;
using EventHub.Domain.Interfaces;
using EventHub.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace EventHub.Infrastructure.Services;

public class DeduplicationService : IDeduplicationService
{
    private readonly IEventRepository _repository;
    private readonly ILogger<DeduplicationService> _logger;

    private const double DUPLICATE_THRESHOLD = 0.85;

    public DeduplicationService(
        IEventRepository repository,
        ILogger<DeduplicationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Event?> FindDuplicateAsync(Event @event, CancellationToken cancellationToken = default)
    {
        // Strategy 1: Try exact hash match first (fastest)
        var hash = CalculateEventHash(@event);
        var candidates = await GetPotentialDuplicatesAsync(@event);

        if (!candidates.Any())
            return null;

        // Strategy 2: Calculate similarity scores for all candidates
        var scoredCandidates = candidates
            .Select(candidate => new
            {
                Event = candidate,
                Score = CalculateSimilarity(@event, candidate),
                Hash = CalculateEventHash(candidate)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        // Strategy 3: Return best match if above threshold
        var bestMatch = scoredCandidates.FirstOrDefault();

        if (bestMatch != null && bestMatch.Score >= DUPLICATE_THRESHOLD)
        {
            _logger.LogDebug(
                "Duplicate found for '{Title}': matched '{ExistingTitle}' with {Score}% similarity",
                @event.Title,
                bestMatch.Event.Title,
                (bestMatch.Score * 100).ToString("F1")
            );

            return bestMatch.Event;
        }

        return null;
    }

    public double CalculateSimilarity(Event event1, Event event2)
    {
        // Title similarity (most important - 50% weight)
        double titleSimilarity = CalculateStringSimilarity(
            event1.Title.ToLowerInvariant(),
            event2.Title.ToLowerInvariant()
        );

        // Date similarity (25% weight)
        // Events within 24 hours are considered same event
        double dateSimilarity = CalculateDateSimilarity(
            event1.StartDate,
            event2.StartDate
        );

        // Location similarity (20% weight)
        double locationSimilarity = CalculateLocationSimilarity(
            event1.Location,
            event2.Location
        );

        // Category match (5% weight)
        double categorySimilarity = event1.Category == event2.Category ? 1.0 : 0.0;

        // Weighted average
        double totalScore =
            (titleSimilarity * 0.50) +
            (dateSimilarity * 0.25) +
            (locationSimilarity * 0.20) +
            (categorySimilarity * 0.05);

        return totalScore;
    }

    public async Task<DeduplicationResult> ProcessEventsAsync(
        List<Event> scrapedEvents,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new DeduplicationResult();

        _logger.LogInformation("Starting deduplication of {Count} scraped events", scrapedEvents.Count);

        foreach (var scrapedEvent in scrapedEvents)
        {
            try
            {
                var duplicate = await FindDuplicateAsync(scrapedEvent, cancellationToken);

                if (duplicate == null)
                {
                    // No duplicate found - this is a new event
                    result.NewEvents.Add(scrapedEvent);
                }
                else
                {
                    // Duplicate found - decide if we should update or skip
                    var similarity = CalculateSimilarity(scrapedEvent, duplicate);

                    if (ShouldUpdateEvent(duplicate, scrapedEvent))
                    {
                        result.ExistingEvents.Add(new EventUpdatePair
                        {
                            ScrapedEvent = scrapedEvent,
                            ExistingEvent = duplicate,
                            SimilarityScore = similarity
                        });
                    }
                    else
                    {
                        result.SkippedEvents.Add(scrapedEvent);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error processing event '{Title}' during deduplication: {Message}",
                    scrapedEvent.Title,
                    ex.Message
                );
                // On error, treat as new event (safe default)
                result.NewEvents.Add(scrapedEvent);
            }
        }

        // Calculate statistics
        result.Statistics = new DeduplicationStatistics
        {
            TotalProcessed = scrapedEvents.Count,
            NewEventsCount = result.NewEvents.Count,
            DuplicatesFound = result.ExistingEvents.Count + result.SkippedEvents.Count,
            UpdatedEvents = result.ExistingEvents.Count,
            SkippedEvents = result.SkippedEvents.Count,
            ProcessingTime = DateTime.UtcNow - startTime
        };

        _logger.LogInformation(
            "Deduplication complete: {New} new, {Duplicates} duplicates ({Updated} to update, {Skipped} skipped) in {Time}ms",
            result.Statistics.NewEventsCount,
            result.Statistics.DuplicatesFound,
            result.Statistics.UpdatedEvents,
            result.Statistics.SkippedEvents,
            result.Statistics.ProcessingTime.TotalMilliseconds
        );

        return result;
    }

    private async Task<List<Event>> GetPotentialDuplicatesAsync(Event @event)
    {
        // Get events in same time window (±3 days) and category
        var events = await _repository.GetAllAsync();

        return events.Where(e =>
            e.Category == @event.Category &&
            Math.Abs((e.StartDate - @event.StartDate).TotalDays) <= 3 &&
            e.Location.City.Equals(@event.Location.City, StringComparison.OrdinalIgnoreCase)
        ).ToList();
    }

    private string CalculateEventHash(Event @event)
    {
        // Combine key fields for hashing
        var hashInput = $"{@event.Title.ToLowerInvariant().Trim()}" +
                       $"|{@event.StartDate:yyyyMMddHHmm}" +
                       $"|{@event.Location.City.ToLowerInvariant()}";

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashInput));
        return Convert.ToBase64String(hashBytes);
    }

    private double CalculateStringSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) && string.IsNullOrEmpty(str2))
            return 1.0;

        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0.0;

        int distance = LevenshteinDistance(str1, str2);
        int maxLength = Math.Max(str1.Length, str2.Length);

        return 1.0 - ((double)distance / maxLength);
    }

    private int LevenshteinDistance(string source, string target)
    {
        if (source == target) return 0;
        if (source.Length == 0) return target.Length;
        if (target.Length == 0) return source.Length;

        int[,] distance = new int[source.Length + 1, target.Length + 1];

        // Initialize first row and column
        for (int i = 0; i <= source.Length; i++)
            distance[i, 0] = i;
        for (int j = 0; j <= target.Length; j++)
            distance[0, j] = j;

        // Calculate distances
        for (int i = 1; i <= source.Length; i++)
        {
            for (int j = 1; j <= target.Length; j++)
            {
                int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                distance[i, j] = Math.Min(
                    Math.Min(
                        distance[i - 1, j] + 1,      // Deletion
                        distance[i, j - 1] + 1),     // Insertion
                    distance[i - 1, j - 1] + cost);  // Substitution
            }
        }

        return distance[source.Length, target.Length];
    }

    private double CalculateDateSimilarity(DateTime date1, DateTime date2)
    {
        var hoursDifference = Math.Abs((date1 - date2).TotalHours);

        if (hoursDifference == 0) return 1.0;
        if (hoursDifference <= 1) return 0.95;    // Within 1 hour
        if (hoursDifference <= 6) return 0.90;    // Within 6 hours
        if (hoursDifference <= 24) return 0.80;   // Same day
        if (hoursDifference <= 48) return 0.50;   // Within 2 days

        return 0.0; // More than 2 days apart
    }

    private double CalculateLocationSimilarity(Location loc1, Location loc2)
    {
        // City match is most important
        double citySimilarity = CalculateStringSimilarity(
            loc1.City.ToLowerInvariant(),
            loc2.City.ToLowerInvariant()
        );

        // Country should match
        double countrySimilarity = loc1.Country.Equals(
            loc2.Country,
            StringComparison.OrdinalIgnoreCase
        ) ? 1.0 : 0.0;

        // Weighted average (city more important)
        return (citySimilarity * 0.8) + (countrySimilarity * 0.2);
    }

    private bool ShouldUpdateEvent(Event existing, Event scraped)
    {
        // Update if scraped version has longer description
        if (scraped.Description.Length > existing.Description.Length)
            return true;

        // Update if existing has placeholder description
        if (existing.Description.Contains("TBA", StringComparison.OrdinalIgnoreCase) ||
            existing.Description.Contains("To be announced", StringComparison.OrdinalIgnoreCase))
            return true;

        // Update if scraped version has source URL and existing doesn't
        if (!string.IsNullOrEmpty(scraped.SourceUrl) && string.IsNullOrEmpty(existing.SourceUrl))
            return true;

        // Update if dates are more precise (has hours/minutes vs just date)
        if (scraped.StartDate.TimeOfDay != TimeSpan.Zero &&
            existing.StartDate.TimeOfDay == TimeSpan.Zero)
            return true;

        // Otherwise, skip update
        return false;
    }
}
