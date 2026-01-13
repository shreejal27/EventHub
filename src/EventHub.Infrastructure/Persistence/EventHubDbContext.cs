using EventHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Infrastructure.Persistence;
public class EventHubDbContext : DbContext
{
    public DbSet<Event> Events { get; set; } = null!;

   
    public EventHubDbContext(DbContextOptions<EventHubDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventHubDbContext).Assembly);

    }
}