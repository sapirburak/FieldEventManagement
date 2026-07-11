using Microsoft.EntityFrameworkCore;
using FieldEventManagement.Core.Entities;

namespace FieldEventManagement.Infrastructure.Persistence;
/// <summary>
/// Represents the session against the database.
/// Responsible for mapping Domain entities to SQL tables and managing transactions.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<FieldEvent> FieldEvents { get; set; }
    public DbSet<User> Users { get; set; }  
    // Note: EF Core can map the internal List in FieldEvent automatically if configured correctly,
    // or a DbSet for EventStateHistory can be added for direct access.
    /// <summary>
    /// Configures the Relationships and table Constraints in SQL Server.
    /// Uses the Fluent API for precise entity mapping.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure the FieldEvent entity
        modelBuilder.Entity<FieldEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);

            // Map the History (internal collection)
            entity.Metadata.FindNavigation(nameof(FieldEvent.History))?
                  .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        // Configure the EventStateHistory entity
        modelBuilder.Entity<EventStateHistory>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.Property(h => h.ChangedBy).IsRequired();
        });
    }
}