using System.Text.Json;
using EventHub.Domain.Entities;
using EventHub.Domain.Enums;
using EventHub.Domain.ValueObjects;
using EventHub.Infrastructure.Scrapers.Base;
using Microsoft.Extensions.Logging;

namespace EventHub.Infrastructure.Scrapers.Eventbrite;

public class EventbriteScraper : BaseEventScraper
{
    private readonly EventbriteScraperOptions _options;

    public EventbriteScraper(
        ILogger<EventbriteScraper> logger,
        HttpClient httpClient,
        EventbriteScraperOptions options)
        : base(logger, httpClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public override string SourceName => "Eventbrite";


    protected override async Task<List<Event>> ScrapeEventsInternalAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Fetching events from Eventbrite API");

        var events = new List<Event>();

        try
        {
            // REAL IMPLEMENTATION WOULD BE:
            // var response = await HttpClient.GetAsync(
            //     $"https://www.eventbriteapi.com/v3/events/search/?location.address={_options.Location}&token={_options.ApiKey}",
            //     cancellationToken
            // );
            // var json = await response.Content.ReadAsStringAsync(cancellationToken);
            // var apiResponse = JsonSerializer.Deserialize<EventbriteApiResponse>(json);

            // FOR LEARNING - Create sample events
            events = CreateSampleEvents();

            Logger.LogInformation("Successfully fetched {Count} events from Eventbrite", events.Count);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP error while fetching from Eventbrite: {Message}", ex.Message);
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "JSON parsing error from Eventbrite: {Message}", ex.Message);
        }

        return events;
    }

    private List<Event> CreateSampleEvents()
    {
        var events = new List<Event>();

        try
        {
            var location1 = new Location(
                "Convention Center",
                _options.DefaultCity,
                _options.DefaultCountry,
                _options.DefaultLatitude,
                _options.DefaultLongitude
            );

            var event1 = new Event(
                title: "Tech Conference 2026",
                description: "Annual technology conference featuring talks on AI, cloud computing, and software architecture. Network with industry leaders and learn about cutting-edge technologies.",
                startDate: DateTime.UtcNow.AddDays(30),
                endDate: DateTime.UtcNow.AddDays(30).AddHours(8),
                location: location1,
                category: EventCategory.Technology,
                source: SourceName,
                sourceUrl: "https://eventbrite.com/tech-conference-2026"
            );

            events.Add(event1);

            // Sample Event 2: Music Festival
            var location2 = new Location(
                "City Park Amphitheater",
                _options.DefaultCity,
                _options.DefaultCountry,
                _options.DefaultLatitude,
                _options.DefaultLongitude
            );

            var event2 = new Event(
                title: "Summer Music Festival",
                description: "Three-day music festival featuring local and international artists. Multiple stages, food vendors, and family-friendly activities. Genres include rock, pop, and electronic music.",
                startDate: DateTime.UtcNow.AddDays(60),
                endDate: DateTime.UtcNow.AddDays(62),
                location: location2,
                category: EventCategory.Music,
                source: SourceName,
                sourceUrl: "https://eventbrite.com/summer-music-fest"
            );

            events.Add(event2);

            // Sample Event 3: Business Networking
            var location3 = new Location(
                "Downtown Hotel Conference Room",
                _options.DefaultCity,
                _options.DefaultCountry,
                _options.DefaultLatitude,
                _options.DefaultLongitude
            );

            var event3 = new Event(
                title: "Startup Networking Breakfast",
                description: "Monthly networking breakfast for entrepreneurs and startup founders. Share experiences, make connections, and learn from guest speakers. Continental breakfast provided.",
                startDate: DateTime.UtcNow.AddDays(15),
                endDate: DateTime.UtcNow.AddDays(15).AddHours(2),
                location: location3,
                category: EventCategory.Business,
                source: SourceName,
                sourceUrl: "https://eventbrite.com/startup-networking"
            );

            events.Add(event3);

            Logger.LogDebug("Created {Count} sample events", events.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating sample events: {Message}", ex.Message);
        }

        return events;
    }

    private List<Event> ParseEventbriteResponse(EventbriteApiResponse apiResponse)
    {
        var events = new List<Event>();

        if (apiResponse?.Events == null)
        {
            Logger.LogWarning("Eventbrite API response is null or has no events");
            return events;
        }

        foreach (var apiEvent in apiResponse.Events)
        {
            try
            {
                // Parse location
                var location = new Location(
                    address: apiEvent.Venue?.Address ?? "Unknown Address",
                    city: apiEvent.Venue?.City ?? _options.DefaultCity,
                    country: apiEvent.Venue?.Country ?? _options.DefaultCountry,
                    latitude: apiEvent.Venue?.Latitude,
                    longitude: apiEvent.Venue?.Longitude
                );

                // Parse dates
                var startDate = TryParseDate(apiEvent.Start?.Utc) ?? DateTime.UtcNow.AddDays(7);
                var endDate = TryParseDate(apiEvent.End?.Utc) ?? startDate.AddHours(2);

                // Map category
                var category = MapEventbriteCategory(apiEvent.CategoryId);

                // Create Event entity
                var @event = new Event(
                    title: apiEvent.Name ?? "Untitled Event",
                    description: apiEvent.Description ?? "No description available",
                    startDate: startDate,
                    endDate: endDate,
                    location: location,
                    category: category,
                    source: SourceName,
                    sourceUrl: apiEvent.Url
                );

                events.Add(@event);
            }
            catch (Exception ex)
            {
                // Log and skip this event, continue with others
                Logger.LogWarning(
                    ex,
                    "Error parsing Eventbrite event {EventId}: {Message}",
                    apiEvent.Id,
                    ex.Message
                );
            }
        }

        return events;
    }

    private EventCategory MapEventbriteCategory(string? categoryId)
    {
        // Eventbrite uses string category IDs
        // Map them to our enum
        return categoryId switch
        {
            "103" => EventCategory.Music,
            "102" => EventCategory.Business,
            "101" => EventCategory.Technology,
            "105" => EventCategory.Sports,
            "104" => EventCategory.Arts,
            "110" => EventCategory.FoodAndDrink,
            _ => EventCategory.Unknown
        };
    }
}

public class EventbriteScraperOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Location { get; set; } = "Kathmandu, Nepal";
    public string DefaultCity { get; set; } = "Kathmandu";
    public string DefaultCountry { get; set; } = "Nepal";
    public double DefaultLatitude { get; set; } = 27.7172;
    public double DefaultLongitude { get; set; } = 85.3240;
}

public class EventbriteApiResponse
{
    public List<EventbriteEvent>? Events { get; set; }
    public Pagination? Pagination { get; set; }
}

public class EventbriteEvent
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public EventbriteDateTime? Start { get; set; }
    public EventbriteDateTime? End { get; set; }
    public EventbriteVenue? Venue { get; set; }
    public string? CategoryId { get; set; }
}

public class EventbriteDateTime
{
    public string? Utc { get; set; }
    public string? Local { get; set; }
}

public class EventbriteVenue
{
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class Pagination
{
    public int PageCount { get; set; }
    public int PageNumber { get; set; }
}