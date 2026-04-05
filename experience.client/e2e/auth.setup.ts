import { test as setup, expect } from '@playwright/test';
import path from 'path';

const authFile = path.join(import.meta.dirname, '.auth/user.json');

/**
 * Completes the OIDC login flow through the BFF and saves the resulting
 * session cookie to .auth/user.json so authenticated tests can reuse it
 * without logging in again.
 *
 * Requires:
 *   E2E_USERNAME  — Identity server username
 *   E2E_PASSWORD  — Identity server password
 */
setup('authenticate', async ({ page }) => {
  const username = process.env['E2E_USERNAME'];
  const password = process.env['E2E_PASSWORD'];

  if (!username || !password) {
    throw new Error('E2E_USERNAME and E2E_PASSWORD environment variables must be set');
  }

  // Trigger the BFF login flow — this redirects to Identity server
  await page.goto('/bff/login?returnUrl=/');

  // Fill Identity server login form
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: /log in|sign in/i }).click();

  // Wait until redirected back to the app and the session is established
  await page.waitForURL('https://localhost:50212/**');
  await expect(page).toHaveURL(/^https:\/\/localhost:50212/);

  // Verify the BFF session cookie exists by checking /bff/user
  const response = await page.request.get('/bff/user');
  expect(response.ok()).toBeTruthy();

  await page.context().storageState({ path: authFile });
});
