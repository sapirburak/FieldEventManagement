using FieldEventManagement.Core.Entities;
namespace FieldEventManagement.Application.Interfaces { 
/// <summary>
/// מגדיר את פעולות ה-Persistence הנדרשות עבור ניהול אירועי שטח.
/// מימוש ממשק זה מתבצע בשכבת ה-Infrastructure באמצעות Entity Framework Core.
/// </summary>
public interface IFieldEventRepository
{
    /// <summary>
    /// בודק האם אירוע בעל מזהה ייחודי קיים במערכת.
    /// קריטי למימוש מנגנון Idempotency כנגד Retry-ים של ה-Agent.
    /// </summary>
    Task<bool> ExistsAsync(Guid id);

    /// <summary>
    /// מחזיר אירוע יחיד כולל היסטוריית המצבים שלו.
    /// נדרש לכל פעולת עדכון (שינוי סטטוס, הקצאה מחדש) על-מנת לאמת
    /// שהמעבר חוקי דרך ה-State Machine לפני הכתיבה ל-DB.
    /// מחזיר null אם האירוע לא נמצא.
    /// </summary>
    Task<FieldEvent?> GetByIdAsync(Guid id);

    /// <summary>
    /// מוסיף אירוע חדש ל-Context של ה-EF.
    /// </summary>
    Task AddAsync(FieldEvent fieldEvent);

    /// <summary>
    /// מבצע Commit של השינויים אל מול מסד הנתונים.
    /// </summary>
    Task SaveChangesAsync();
}
}