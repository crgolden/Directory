import { test, expect } from '@playwright/test';

test.describe('Chat', () => {
  test('chat page loads and creates a conversation', async ({ page }) => {
    await page.goto('/chat');
    // A new conversation is created on init — sidebar should list it
    await expect(page.locator('button.conversation-item').first()).toBeVisible({ timeout: 10000 });
  });

  test('send button is disabled when input is empty', async ({ page }) => {
    await page.goto('/chat');
    await expect(page.getByRole('button', { name: /send/i })).toBeDisabled();
  });

  test('send button enables when input has text', async ({ page }) => {
    await page.goto('/chat');
    await page.locator('textarea').fill('Hello');
    await expect(page.getByRole('button', { name: /send/i })).toBeEnabled();
  });

  test('pre-populates input from ?q= query param', async ({ page }) => {
    const query = 'Help me find the manual for my TV';
    await page.goto(`/chat?q=${encodeURIComponent(query)}`);
    await expect(page.locator('textarea')).toHaveValue(query);
  });

  test('"New Conversation" button clears messages and starts a fresh conversation', async ({ page }) => {
    await page.goto('/chat');
    await page.getByRole('button', { name: /new conversation/i }).click();
    // After clearing, the input should be empty and a new conversation created
    await expect(page.locator('textarea')).toHaveValue('');
    await expect(page.locator('button.conversation-item')).toHaveCount(2, { timeout: 10000 });
  });
});
