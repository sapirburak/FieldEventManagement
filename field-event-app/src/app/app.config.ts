import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { errorInterceptor } from './core/interceptors/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [provideHttpClient(
    withInterceptors([errorInterceptor]) // Inject the interceptor for all HTTP requests
  ),
  provideRouter(routes),
 ]
};
