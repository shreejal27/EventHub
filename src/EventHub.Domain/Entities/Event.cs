using EventHub.Domain.Enums;
using EventHub.Domain.Exceptions;
using EventHub.Domain.ValueObjects;

namespace EventHub.Domain.Entities;

public class Event
{
    public int Id { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public Location Location { get; private set; }
    public EventCategory Category { get; private set; }
    public EventStatus Status { get; private set; }
    public string Source { get; private set; }
    public string? SourceUrl { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    private Event()
    {
        Title = string.Empty;
        Description = string.Empty;
        Location = null!; 
        Source = string.Empty;
    }
    public Event(
        string title,
        string description,
        DateTime startDate,
        DateTime endDate,
        Location location,
        EventCategory category,
        string source,
        string? sourceUrl = null)
    {

        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Event title cannot be empty");

        if (title.Length < 5)
            throw new DomainException("Event title must be at least 5 characters long");

       
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Event description cannot be empty");

        if (description.Length < 10)
            throw new DomainException("Event description must be at least 10 characters long");

        if (startDate < DateTime.UtcNow)
            throw new DomainException("Event start date cannot be in the past");

        if (endDate <= startDate)
            throw new DomainException("Event end date must be after start date");

     
        if (location == null)
            throw new DomainException("Event location is required");

        if (string.IsNullOrWhiteSpace(source))
            throw new DomainException("Event source cannot be empty");

        Title = title.Trim();
        Description = description.Trim();
        StartDate = startDate;
        EndDate = endDate;
        Location = location;
        Category = category;
        Source = source.Trim();
        SourceUrl = sourceUrl?.Trim();

        Status = EventStatus.Draft;

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
    public void Activate()
    {
        if (Status != EventStatus.Draft)
            throw new DomainException($"Cannot activate event with status {Status}");

        Status = EventStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }
    public void Cancel()
    {
        if (Status != EventStatus.Active)
            throw new DomainException($"Cannot cancel event with status {Status}");

        Status = EventStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }
    public void MarkAsCompleted()
    {
        if (Status != EventStatus.Active)
            throw new DomainException($"Cannot complete event with status {Status}");

        Status = EventStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateTitle(string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            throw new DomainException("Event title cannot be empty");

        if (newTitle.Length < 5)
            throw new DomainException("Event title must be at least 5 characters long");

        Title = newTitle.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
    public void UpdateDescription(string newDescription)
    {
        if (string.IsNullOrWhiteSpace(newDescription))
            throw new DomainException("Event description cannot be empty");

        if (newDescription.Length < 10)
            throw new DomainException("Event description must be at least 10 characters long");

        Description = newDescription.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reschedule(DateTime newStartDate, DateTime newEndDate)
    {
        if (newStartDate < DateTime.UtcNow)
            throw new DomainException("Event start date cannot be in the past");

        if (newEndDate <= newStartDate)
            throw new DomainException("Event end date must be after start date");

        StartDate = newStartDate;
        EndDate = newEndDate;
        UpdatedAt = DateTime.UtcNow;
    }
    public bool IsHappeningNow()
    {
        var now = DateTime.UtcNow;
        return now >= StartDate && now <= EndDate;
    }
    public bool IsUpcoming()
    {
        return DateTime.UtcNow < StartDate;
    }

    public bool IsPast()
    {
        return DateTime.UtcNow > EndDate;
    }
    public double DurationInHours()
    {
        return (EndDate - StartDate).TotalHours;
    }

    public override string ToString()
    {
        return $"Event #{Id}: {Title} on {StartDate:yyyy-MM-dd} at {Location.City}";
    }
}