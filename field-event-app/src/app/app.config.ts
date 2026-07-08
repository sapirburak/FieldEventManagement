import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { provideClientHydration } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { errorInterceptor } from './core/interceptors/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [provideHttpClient(
    withInterceptors([errorInterceptor]) // הזרקת האינטרספטור לכל בקשות ה-HTTP
  ),
  provideRouter(routes),
  provideClientHydration()]
};
