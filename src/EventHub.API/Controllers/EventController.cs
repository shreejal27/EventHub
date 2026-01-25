using EventHub.Domain.Entities;
using EventHub.Domain.Enums;
using EventHub.Domain.Interfaces;
using EventHub.Domain.ValueObjects;
using EventHub.Infrastructure.Services;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventRepository _repository;
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        IEventRepository repository,
        ILogger<EventsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<Event>>> GetAllEvents()
    {
        _logger.LogInformation("Getting all events");

        var events = await _repository.GetAllAsync();

        return Ok(events);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Event>> GetEventById(int id)
    {
        _logger.LogInformation("Getting event with ID: {EventId}", id);

        var eventEntity = await _repository.GetByIdAsync(id);

        if (eventEntity == null)
        {
            _logger.LogWarning("Event with ID {EventId} not found", id);
            return NotFound(new { message = $"Event with ID {id} not found" });
        }

        return Ok(eventEntity);
    }


    [HttpGet("upcoming")]
    public async Task<ActionResult<List<Event>>> GetUpcomingEvents()
    {
        _logger.LogInformation("Getting upcoming events");

        var events = await _repository.GetUpcomingEventsAsync();

        return Ok(events);
    }

    [HttpGet("category/{category}")]
    public async Task<ActionResult<List<Event>>> GetEventsByCategory(EventCategory category)
    {
        _logger.LogInformation("Getting events for category: {Category}", category);

        var events = await _repository.GetByCategoryAsync(category);

        return Ok(events);
    }

    [HttpGet("city/{city}")]
    public async Task<ActionResult<List<Event>>> GetEventsByCity(string city)
    {
        _logger.LogInformation("Getting events for city: {City}", city);

        var events = await _repository.GetByCityAsync(city);

        return Ok(events);
    }

    [HttpPost]
    public async Task<ActionResult<Event>> CreateEvent([FromBody] CreateEventRequest request)
    {
        _logger.LogInformation("Creating new event: {Title}", request.Title);

        try
        {
            // Create Location value object
            var location = new Location(
                request.Address,
                request.City,
                request.Country,
                request.Latitude,
                request.Longitude
            );

            // Create Event entity
            var @event = new Event(
                request.Title,
                request.Description,
                request.StartDate,
                request.EndDate,
                location,
                request.Category,
                request.Source,
                request.SourceUrl
            );

            // Activate the event (make it visible)
            @event.Activate();

            // Save to database
            var createdEvent = await _repository.AddAsync(@event);

            _logger.LogInformation("Event created successfully with ID: {EventId}", createdEvent.Id);

            // Return 201 Created with location header
            return CreatedAtAction(
                nameof(GetEventById),
                new { id = createdEvent.Id },
                createdEvent
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating event");
            return BadRequest(new { message = ex.Message });
        }
    }


    [HttpPut("{id}/title")]
    public async Task<ActionResult<Event>> UpdateEventTitle(
        int id,
        [FromBody] UpdateTitleRequest request)
    {
        _logger.LogInformation("Updating title for event {EventId}", id);

        try
        {
            var @event = await _repository.GetByIdAsync(id);

            if (@event == null)
            {
                return NotFound(new { message = $"Event with ID {id} not found" });
            }

            @event.UpdateTitle(request.Title);
            await _repository.UpdateAsync(@event);

            _logger.LogInformation("Event {EventId} title updated successfully", id);

            return Ok(@event);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating event title");
            return BadRequest(new { message = ex.Message });
        }
    }

 
    [HttpPut("{id}/cancel")]
    public async Task<ActionResult<Event>> CancelEvent(int id)
    {
        _logger.LogInformation("Cancelling event {EventId}", id);

        try
        {
            var @event = await _repository.GetByIdAsync(id);

            if (@event == null)
            {
                return NotFound(new { message = $"Event with ID {id} not found" });
            }

            @event.Cancel();
            await _repository.UpdateAsync(@event);

            _logger.LogInformation("Event {EventId} cancelled successfully", id);

            return Ok(@event);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling event");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteEvent(int id)
    {
        _logger.LogInformation("Deleting event {EventId}", id);

        var @event = await _repository.GetByIdAsync(id);

        if (@event == null)
        {
            return NotFound(new { message = $"Event with ID {id} not found" });
        }

        await _repository.DeleteAsync(@event);

        _logger.LogInformation("Event {EventId} deleted successfully", id);

        return NoContent(); // 204 - successful deletion
    }

    [HttpGet("health")]
    public ActionResult HealthCheck()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            message = "EventHub API is running!"
        });
    }

    [HttpPost("scrape")]
    public ActionResult TriggerScraping()
    {
        _logger.LogInformation("Manual scraping triggered via API");

        // Queue background job
        var jobId = BackgroundJob.Enqueue<ScraperService>(
            service => service.ScrapeAllSourcesAsync()
        );

        return Accepted(new
        {
            message = "Scraping job queued",
            jobId = jobId,
            dashboardUrl = "/hangfire",
            note = "Check Hangfire dashboard to monitor progress"
        });
    }

    [HttpGet("scraper-stats")]
    public async Task<ActionResult<ScraperStatistics>> GetScraperStatistics(
        [FromServices] ScraperService scraperService)
    {
        var stats = await scraperService.GetStatisticsAsync();
        return Ok(stats);
    }
}

public record CreateEventRequest(
    string Title,
    string Description,
    DateTime StartDate,
    DateTime EndDate,
    string Address,
    string City,
    string Country,
    double? Latitude,
    double? Longitude,
    EventCategory Category,
    string Source,
    string? SourceUrl = null
);


public record UpdateTitleRequest(string Title);
