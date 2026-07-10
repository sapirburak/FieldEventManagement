/**
 * מייצג אירוע שטח במערכת.
 * ישות זו היא ה-Core של המערכת ומועברת בין ה-Agent לשרת וללקוח.
 */
export interface FieldEvent {
  /** מזהה ייחודי (GUID) למניעת כפילויות (Idempotency) */
  id: string;
  /** כותרת קצרה וממצה לאירוע */
  title: string;
  /** פירוט מלא של האירוע */
  description: string;
  /** המצב הנוכחי במחזור החיים (State Machine) */
  status: EventStatus;
  /** מיקום גיאוגרפי או תיאור טקסטואלי של המיקום */
  location: string;
  /** חותמת זמן של יצירת האירוע */
  timestamp: Date;
  /** המשתמש שהקצה או עדכן את האירוע לאחרונה */
  lastUpdatedBy?: string;
}

/**
 * המצבים האפשריים במחזור החיים של האירוע (State Machine).
 * הערכים חייבים להתאים בדיוק ל-enum EventStatus שבצד ה-Backend (.NET Core),
 * כי SignalR מסריל את שם ה-enum כמחרוזת.
 */
export enum EventStatus {
  Unassigned = 'Unassigned', // מצב ראשוני – תואם Backend: Unassigned
  Assigned = 'Assigned',     // הוקצה לטכנאי
  InProgress = 'InProgress', // בטיפול
  Completed = 'Completed',   // הושלם – תואם Backend: Completed (לא Closed)
  Cancelled = 'Cancelled'    // מבוטל
}