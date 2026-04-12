import { ApplicationConfig, inject, provideAppInitializer, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { appInterceptor} from './app.interceptor';
import { AuthService } from '../auth/auth.service';
import { ApplicationInsights } from '@microsoft/applicationinsights-web';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    provideRouter(routes),
    provideHttpClient(
      withFetch(), withInterceptors([appInterceptor])
    ),
    provideAppInitializer(async () => {
      try {
        const response = await fetch('/config/telemetry');
        if (response.ok) {
          const config = await response.json() as { connectionString: string | null };
          if (config.connectionString) {
            const appInsights = new ApplicationInsights({
              config: {
                connectionString: config.connectionString,
                enableAutoRouteTracking: true,
                autoTrackPageVisitTime: true,
              }
            });
            appInsights.loadAppInsights();
            appInsights.trackPageView();
          }
        }
      } catch {
        // Telemetry initialization failure must not block app startup
      }
    }),
    provideAppInitializer(() => {
      const authService = inject(AuthService);
      return authService.initialize();
    })
  ]
};
