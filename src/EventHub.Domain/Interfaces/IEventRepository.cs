using EventHub.Domain.Entities;
using EventHub.Domain.Enums;

namespace EventHub.Domain.Interfaces;

public interface IEventRepository
{
    Task<Event?> GetByIdAsync(int id);
    Task<List<Event>> GetAllAsync();
    Task<List<Event>> GetByCategoryAsync(EventCategory category);
    Task<List<Event>> GetByStatusAsync(EventStatus status);
    Task<List<Event>> GetUpcomingEventsAsync();
    Task<List<Event>> GetByCityAsync(string city);
    Task<Event> AddAsync(Event @event);
    Task AddRangeAsync(List<Event> events);
    Task UpdateAsync(Event @event);
    Task DeleteAsync(Event @event);
    Task<bool> ExistsAsync(int id);
    Task<int> CountAsync();
}