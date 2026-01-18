using EventHub.Domain.Entities;

namespace EventHub.Domain.Interfaces;

public interface IEventScraper
{
    string SourceName { get; }
    
    Task<List<Event>> ScrapeAsync(CancellationToken cancellationToken = default);

    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}