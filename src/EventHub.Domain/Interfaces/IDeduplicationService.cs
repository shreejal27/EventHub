using EventHub.Domain.Entities;

namespace EventHub.Domain.Interfaces;

public interface IDeduplicationService
{
    Task<Event?> FindDuplicateAsync(Event @event, CancellationToken cancellationToken = default);

    double CalculateSimilarity(Event event1, Event event2);

    Task<DeduplicationResult> ProcessEventsAsync(
        List<Event> scrapedEvents,
        CancellationToken cancellationToken = default);
}

public class DeduplicationResult
{
    public List<Event> NewEvents { get; set; } = new();
    public List<EventUpdatePair> ExistingEvents { get; set; } = new();
    public List<Event> SkippedEvents { get; set; } = new();
    public DeduplicationStatistics Statistics { get; set; } = new();
}

public class EventUpdatePair
{
    public Event ScrapedEvent { get; set; } = null!;
    public Event ExistingEvent { get; set; } = null!;
    public double SimilarityScore { get; set; }
}
public class DeduplicationStatistics
{
    public int TotalProcessed { get; set; }
    public int NewEventsCount { get; set; }
    public int DuplicatesFound { get; set; }
    public int UpdatedEvents { get; set; }
    public int SkippedEvents { get; set; }
    public TimeSpan ProcessingTime { get; set; }
}