import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

/**
 * Global interceptor for handling HTTP errors.
 * Its role is to catch network/server-level errors and perform uniform actions (e.g. logging or showing notifications).
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      let errorMessage = 'An unknown error occurred!';

      // Determine the type of error
      if (error.error instanceof ErrorEvent) {
        // Client-side error (e.g. basic network issue)
        errorMessage = `Client Error: ${error.error.message}`;
      } else {
        // Server-side error (e.g. 401, 403, 500)
        switch (error.status) {
          case 401:
            errorMessage = 'Session expired. Please login again.';
            // Here we could invoke AuthService to navigate to the login page
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
      
      // Here we could add a Notifications service to display a user-friendly message
      // alert(errorMessage); 

      return throwError(() => new Error(errorMessage));
    })
  );
};