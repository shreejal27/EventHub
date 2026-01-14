using EventHub.Domain.Entities;
using EventHub.Domain.Enums;
using EventHub.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Infrastructure.Persistence.Repositories;
public class EventRepository : IEventRepository
{
    private readonly EventHubDbContext _context;

    public EventRepository(EventHubDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Event?> GetByIdAsync(int id)
    {
        return await _context.Events
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<List<Event>> GetAllAsync()
    {
        return await _context.Events.ToListAsync();
    }

    public async Task<List<Event>> GetByCategoryAsync(EventCategory category)
    {
        return await _context.Events
            .Where(e => e.Category == category)
            .ToListAsync();
    }

    public async Task<List<Event>> GetByStatusAsync(EventStatus status)
    {
        return await _context.Events
            .Where(e => e.Status == status)
            .ToListAsync();
    }

    public async Task<List<Event>> GetUpcomingEventsAsync()
    {
        var now = DateTime.UtcNow;

        return await _context.Events
            .Where(e => e.Status == EventStatus.Active && e.StartDate > now)
            .OrderBy(e => e.StartDate) // Soonest events first
            .ToListAsync();
    }

    public async Task<List<Event>> GetByCityAsync(string city)
    {
        return await _context.Events
            .Where(e => EF.Functions.Like(e.Location.City, $"%{city}%"))
            .ToListAsync();
    }

    public async Task<Event> AddAsync(Event @event)
    {
        await _context.Events.AddAsync(@event);
        await _context.SaveChangesAsync();
        return @event;
    }

    public async Task AddRangeAsync(List<Event> events)
    {
        await _context.Events.AddRangeAsync(events);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Event @event)
    {
        _context.Events.Update(@event);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Event @event)
    {
        _context.Events.Remove(@event);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Events.AnyAsync(e => e.Id == id);
    }

    public async Task<int> CountAsync()
    {
        return await _context.Events.CountAsync();
    }
}
