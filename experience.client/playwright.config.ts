import { defineConfig, devices } from '@playwright/test';

/**
 * E2E tests require credentials via environment variables:
 *   E2E_USERNAME  — Identity server login username
 *   E2E_PASSWORD  — Identity server login password
 *
 * The servers are started automatically via webServer when not already running.
 * For local dev with servers already running, set SKIP_WEBSERVER=1 to skip startup.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  workers: 1,
  reporter: [['list'], ['html', { open: 'never' }]],

  use: {
    baseURL: 'https://localhost:50212',
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
  },

  webServer: process.env['SKIP_WEBSERVER'] ? [] : [
    {
      command: 'dotnet run --project ../Experience.Server',
      url: 'https://localhost:7150/healthz',
      ignoreHTTPSErrors: true,
      reuseExistingServer: true,
      timeout: 60000,
    },
    {
      command: 'npm start',
      url: 'https://localhost:50212',
      ignoreHTTPSErrors: true,
      reuseExistingServer: true,
      timeout: 60000,
    },
  ],

  projects: [
    {
      name: 'setup',
      testMatch: /auth\.setup\.ts/,
    },
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        storageState: 'e2e/.auth/user.json',
      },
      dependencies: ['setup'],
    },
  ],
});
