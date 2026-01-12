using EventHub.Domain.Entities;
using EventHub.Domain.Enums;
using EventHub.Domain.Exceptions;
using EventHub.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace EventHub.Tests.Domain.Entities;

public class EventTests
{
    private Location CreateValidLocation()
    {
        return new Location(
            address: "123 Main Street",
            city: "New York",
            country: "USA",
            latitude: 40.7128,
            longitude: -74.0060
        );
    }

    [Fact]
    public void Constructor_WithValidData_CreatesEvent()
    {
        // Arrange - Set up test data
        var title = "Tech Meetup 2025";
        var description = "Monthly gathering for developers to network and learn";
        var startDate = DateTime.UtcNow.AddDays(7);
        var endDate = startDate.AddHours(2);
        var location = CreateValidLocation();
        var category = EventCategory.Technology;
        var source = "Eventbrite";

        // Act - Execute the code being tested
        var @event = new Event(
            title,
            description,
            startDate,
            endDate,
            location,
            category,
            source
        );

        @event.Should().NotBeNull();
        @event.Title.Should().Be(title);
        @event.Description.Should().Be(description);
        @event.StartDate.Should().Be(startDate);
        @event.EndDate.Should().Be(endDate);
        @event.Location.Should().Be(location);
        @event.Category.Should().Be(category);
        @event.Source.Should().Be(source);
        @event.Status.Should().Be(EventStatus.Draft); // New events start as Draft
        @event.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        @event.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithSourceUrl_StoresSourceUrl()
    {
        // Arrange
        var location = CreateValidLocation();
        var sourceUrl = "https://eventbrite.com/event/12345";

        // Act
        var @event = new Event(
            "Tech Meetup",
            "Monthly tech meetup for developers",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite",
            sourceUrl
        );

        // Assert
        @event.SourceUrl.Should().Be(sourceUrl);
    }

    [Fact]
    public void Constructor_TrimsWhitespaceFromStrings()
    {
        // Arrange - Input with extra spaces
        var titleWithSpaces = "  Tech Meetup  ";
        var descriptionWithSpaces = "  Monthly meetup  ";
        var sourceWithSpaces = "  Eventbrite  ";
        var location = CreateValidLocation();

        // Act
        var @event = new Event(
            titleWithSpaces,
            descriptionWithSpaces,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            sourceWithSpaces
        );

        // Assert - Whitespace should be trimmed
        @event.Title.Should().Be("Tech Meetup");
        @event.Description.Should().Be("Monthly meetup");
        @event.Source.Should().Be("Eventbrite");
    }

    [Fact]
    public void Constructor_WithEmptyTitle_ThrowsDomainException()
    {
        // Arrange
        var location = CreateValidLocation();

        // Act - Create action that should throw
        Action act = () => new Event(
            "", // Empty title - INVALID!
            "Valid description with enough characters",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );

        // Assert - Verify exception is thrown
        act.Should().Throw<DomainException>()
           .WithMessage("Event title cannot be empty");
    }

    [Fact]
    public void Constructor_WithWhitespaceTitle_ThrowsDomainException()
    {
        // Arrange
        var location = CreateValidLocation();

        // Act
        Action act = () => new Event(
            "   ", // Only whitespace - INVALID!
            "Valid description with enough characters",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage("Event title cannot be empty");
    }

    [Fact]
    public void Constructor_WithTitleLessThan5Characters_ThrowsDomainException()
    {
        // Arrange
        var location = CreateValidLocation();

        // Act
        Action act = () => new Event(
            "Tech", // Only 4 characters - INVALID!
            "Valid description with enough characters",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage("Event title must be at least 5 characters long");
    }

    [Fact]
    public void Constructor_WithEmptyDescription_ThrowsDomainException()
    {
        // Arrange
        var location = CreateValidLocation();

        // Act
        Action act = () => new Event(
            "Tech Meetup",
            "", // Empty description - INVALID!
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage("Event description cannot be empty");
    }

    [Fact]
    public void Constructor_WithDescriptionLessThan10Characters_ThrowsDomainException()
    {
        // Arrange
        var location = CreateValidLocation();

        // Act
        Action act = () => new Event(
            "Tech Meetup",
            "Too short", // Only 9 characters - INVALID!
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage("Event description must be at least 10 characters long");
    }

    [Fact]
    public void Constructor_WithPastStartDate_ThrowsDomainException()
    {
        // Arrange
        var location = CreateValidLocation();
        var pastDate = DateTime.UtcNow.AddDays(-1); // Yesterday - INVALID!

        // Act
        Action act = () => new Event(
            "Tech Meetup",
            "Valid description with enough characters",
            pastDate,
            pastDate.AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage("Event start date cannot be in the past");
    }

    [Fact]
    public void Constructor_WithEndDateBeforeStartDate_ThrowsDomainException()
    {
        // Arrange
        var location = CreateValidLocation();
        var startDate = DateTime.UtcNow.AddDays(7);
        var endDate = startDate.AddHours(-1); // End before start - INVALID!

        // Act
        Action act = () => new Event(
            "Tech Meetup",
            "Valid description with enough characters",
            startDate,
            endDate,
            location,
            EventCategory.Technology,
            "Eventbrite"
        );

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage("Event end date must be after start date");
    }

    [Fact]
    public void Constructor_WithEndDateEqualToStartDate_ThrowsDomainException()
    {
        // Arrange
        var location = CreateValidLocation();
        var startDate = DateTime.UtcNow.AddDays(7);
        var endDate = startDate; // Same as start - INVALID! (zero duration)

        // Act
        Action act = () => new Event(
            "Tech Meetup",
            "Valid description with enough characters",
            startDate,
            endDate,
            location,
            EventCategory.Technology,
            "Eventbrite"
        );

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage("Event end date must be after start date");
    }

    [Fact]
    public void Constructor_WithNullLocation_ThrowsDomainException()
    {
        // Act
        Action act = () => new Event(
            "Tech Meetup",
            "Valid description with enough characters",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            null!, // Null location - INVALID!
            EventCategory.Technology,
            "Eventbrite"
        );

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage("Event location is required");
    }

    [Fact]
    public void Constructor_WithEmptySource_ThrowsDomainException()
    {
        // Arrange
        var location = CreateValidLocation();

        // Act
        Action act = () => new Event(
            "Tech Meetup",
            "Valid description with enough characters",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            "" // Empty source - INVALID!
        );

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage("Event source cannot be empty");
    }

    [Fact]
    public void Activate_WhenStatusIsDraft_ChangesStatusToActive()
    {
        // Arrange
        var location = CreateValidLocation();
        var @event = new Event(
            "Tech Meetup",
            "Monthly tech meetup for developers",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );
        // Event starts as Draft

        // Act
        @event.Activate();

        // Assert
        @event.Status.Should().Be(EventStatus.Active);
    }

    [Fact]
    public void Activate_UpdatesTimestamp()
    {
        // Arrange
        var location = CreateValidLocation();
        var @event = new Event(
            "Tech Meetup",
            "Monthly tech meetup for developers",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );
        var originalUpdatedAt = @event.UpdatedAt;

        // Wait a tiny bit to ensure timestamp changes
        Thread.Sleep(10);

        // Act
        @event.Activate();

        // Assert
        @event.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ThrowsDomainException()
    {
        // Arrange
        var location = CreateValidLocation();
        var @event = new Event(
            "Tech Meetup",
            "Monthly tech meetup for developers",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );
        @event.Activate(); // Already activated

        // Act - Try to activate again
        Action act = () => @event.Activate();

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage("Cannot activate event with status Active");
    }

    [Fact]
    public void Cancel_WhenStatusIsActive_ChangesStatusToCancelled()
    {
        // Arrange
        var location = CreateValidLocation();
        var @event = new Event(
            "Tech Meetup",
            "Monthly tech meetup for developers",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );
        @event.Activate(); // Must be Active to cancel

        // Act
        @event.Cancel();

        // Assert
        @event.Status.Should().Be(EventStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenStatusIsDraft_ThrowsDomainException()
    {
        // Arrange
        var location = CreateValidLocation();
        var @event = new Event(
            "Tech Meetup",
            "Monthly tech meetup for developers",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );
        // Event is Draft, not Active

        // Act
        Action act = () => @event.Cancel();

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage("Cannot cancel event with status Draft");
    }

    [Fact]
    public void IsUpcoming_WhenStartDateInFuture_ReturnsTrue()
    {
        // Arrange
        var location = CreateValidLocation();
        var @event = new Event(
            "Tech Meetup",
            "Monthly tech meetup for developers",
            DateTime.UtcNow.AddDays(7), // Future date
            DateTime.UtcNow.AddDays(7).AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );

        // Act
        var result = @event.IsUpcoming();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsUpcoming_WhenStartDateInPast_ReturnsFalse()
    {
        // Arrange
        var location = CreateValidLocation();
        var futureDate = DateTime.UtcNow.AddDays(7);
        var @event = new Event(
            "Tech Meetup",
            "Monthly tech meetup for developers",
            futureDate,
            futureDate.AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );

        // Manually set to past (in real app, time passes naturally)
        // For testing, we use reflection to modify private property
        typeof(Event).GetProperty(nameof(Event.StartDate))!
            .SetValue(@event, DateTime.UtcNow.AddDays(-1));

        // Act
        var result = @event.IsUpcoming();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void DurationInHours_ReturnsCorrectDuration()
    {
        // Arrange
        var location = CreateValidLocation();
        var startDate = DateTime.UtcNow.AddDays(1);
        var endDate = startDate.AddHours(3); // 3-hour event
        var @event = new Event(
            "Tech Meetup",
            "Monthly tech meetup for developers",
            startDate,
            endDate,
            location,
            EventCategory.Technology,
            "Eventbrite"
        );

        // Act
        var duration = @event.DurationInHours();

        // Assert
        duration.Should().Be(3.0);
    }

    [Fact]
    public void UpdateTitle_WithValidTitle_UpdatesTitle()
    {
        // Arrange
        var location = CreateValidLocation();
        var @event = new Event(
            "Tech Meetup",
            "Monthly tech meetup for developers",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );
        var newTitle = "Tech Meetup 2025 Special Edition";

        // Act
        @event.UpdateTitle(newTitle);

        // Assert
        @event.Title.Should().Be(newTitle);
    }

    [Fact]
    public void UpdateTitle_WithInvalidTitle_ThrowsDomainException()
    {
        // Arrange
        var location = CreateValidLocation();
        var @event = new Event(
            "Tech Meetup",
            "Monthly tech meetup for developers",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            location,
            EventCategory.Technology,
            "Eventbrite"
        );

        // Act
        Action act = () => @event.UpdateTitle("Hi"); // Too short

        // Assert
        act.Should().Throw<DomainException>();
    }
}

