using Microsoft.EntityFrameworkCore;
using FieldEventManagement.Core.Entities;

namespace FieldEventManagement.Infrastructure.Persistence;
/// <summary>
/// מייצג את ה-Session מול מסד הנתונים.
/// אחראי על מיפוי הישויות מה-Domain לטבלאות SQL ועל ניהול הטרנזקציות.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<FieldEvent> FieldEvents { get; set; }
    public DbSet<User> Users { get; set; }  
    // הערה: EF Core יודע למפות את ה-List הפנימי ב-FieldEvent אוטומטית אם נגדיר זאת נכון, 
    // או שניתן להוסיף DbSet ל-EventStateHistory אם נרצה גישה ישירה.
    /// <summary>
    /// מגדיר את הקשרים (Relationships) ואת אילוצי הטבלאות (Constraints) ב-SQL Server.
    /// משתמש ב-Fluent API למיפוי מדויק של הישויות.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // הגדרת Entity ל-FieldEvent
        modelBuilder.Entity<FieldEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);

            // מיפוי ה-History (אוסף פנימי)
            entity.Metadata.FindNavigation(nameof(FieldEvent.History))?
                  .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        // הגדרת Entity ל-EventStateHistory
        modelBuilder.Entity<EventStateHistory>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.Property(h => h.ChangedBy).IsRequired();
        });
    }
}