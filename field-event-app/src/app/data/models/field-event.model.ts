/**
 * מייצג אירוע שטח במערכת.
 * ישות זו היא ה-Core של המערכת ומועברת בין ה-Agent לשרת וללקוח.
 */
export interface FieldEvent {
  id: string;
  title: string;
  description: string;
  status: EventStatus;
  location: string;
  timestamp: Date;
  /** ID של הטכנאי שאליו האירוע מוקצה כרגע. null = פנוי. */
  assignedTechnicianId?: string | null;
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