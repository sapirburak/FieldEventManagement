import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

/**
 * Interceptor גלובלי לטיפול בשגיאות HTTP.
 * תפקידו לתפוס שגיאות ברמת הרשת/שרת ולבצע פעולות אחידות (כמו לוגינג או הצגת התראות).
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      let errorMessage = 'An unknown error occurred!';

      // ניתוח סוג השגיאה
      if (error.error instanceof ErrorEvent) {
        // שגיאת צד לקוח (למשל בעיית רשת בסיסית)
        errorMessage = `Client Error: ${error.error.message}`;
      } else {
        // שגיאת צד שרת (למשל 401, 403, 500)
        switch (error.status) {
          case 401:
            errorMessage = 'Session expired. Please login again.';
            // כאן נוכל להפעיל AuthService כדי לנתב לעמוד התחברות
            break;
          case 403:
            errorMessage = 'Access denied.';
            break;
          case 500:
            errorMessage = 'Server error. Please try again later.';
            break;
          default:
            errorMessage = `Error Code: ${error.status}\nMessage: ${error.message}`;
        }
      }

      console.error('Global Error Caught:', errorMessage);
      
      // כאן נוכל להוסיף שירות Notifications להצגת הודעה יפה למשתמש
      // alert(errorMessage); 

      return throwError(() => new Error(errorMessage));
    })
  );
};