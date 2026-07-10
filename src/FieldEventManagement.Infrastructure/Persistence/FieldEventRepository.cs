using FieldEventManagement.Application.Interfaces;
using FieldEventManagement.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FieldEventManagement.Infrastructure.Persistence;
/// <summary>
/// מימוש קונקרטי של ה-IFieldEventRepository.
/// אחראי על כל פעולות ה-CRUD מול ה-DbContext. 
/// מחלקה זו מבודדת את שכבת ה-Application מהפרטים הטכניים של Entity Framework Core.
/// </summary>
public class FieldEventRepository : IFieldEventRepository
{
    private readonly ApplicationDbContext _context;

    public FieldEventRepository(ApplicationDbContext context) => _context = context;
  
    /// <summary>
    /// בודק קיום אירוע בבסיס הנתונים באמצעות מזהה ייחודי. 
    /// חיוני להבטחת עקביות (Idempotency) בעת קליטת הודעות מה-Agent.
    /// </summary>
    public async Task<bool> ExistsAsync(Guid id) =>
        await _context.FieldEvents.AnyAsync(e => e.Id == id);

    /// <summary>
    /// שולף אירוע כולל היסטוריית המצבים (Include) שנדרשת ל-State Machine.
    /// Include הכרחי כאן כי ה-History הוא private collection שלא נטען אוטומטית.
    /// </summary>
    public async Task<FieldEvent?> GetByIdAsync(Guid id) =>
        await _context.FieldEvents
            .Include("_history") // שם השדה הפנימי – נדרש כי ה-collection הוא private
            .FirstOrDefaultAsync(e => e.Id == id);

    public async Task AddAsync(FieldEvent fieldEvent) =>
        await _context.FieldEvents.AddAsync(fieldEvent);

    public async Task SaveChangesAsync() =>
        await _context.SaveChangesAsync();
}