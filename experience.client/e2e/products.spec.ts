import { test, expect, Page } from '@playwright/test';

async function deleteProduct(page: Page, name: string): Promise<void> {
  await page.goto('/products');
  const row = page.getByRole('row', { name });
  const deleteBtn = row.getByRole('button', { name: /delete/i });
  if (await deleteBtn.isVisible()) {
    await deleteBtn.click();
    await row.getByRole('button', { name: /yes/i }).click();
  }
}

test.describe('Products', () => {
  test('product list loads and shows the table', async ({ page }) => {
    await page.goto('/products');
    await expect(page.getByRole('table')).toBeVisible();
  });

  test('navigating to /products/new shows the create form', async ({ page }) => {
    await page.goto('/products/new');
    await expect(page.getByLabel('Name')).toBeVisible();
    await expect(page.getByRole('button', { name: /save|create/i })).toBeVisible();
  });

  test('create form submit button is disabled when name is empty', async ({ page }) => {
    await page.goto('/products/new');
    await expect(page.getByRole('button', { name: /save|create/i })).toBeDisabled();
  });

  test('create form submit button enables when name is filled', async ({ page }) => {
    await page.goto('/products/new');
    await page.getByLabel('Name').fill('Test Product');
    await expect(page.getByRole('button', { name: /save|create/i })).toBeEnabled();
  });

  test.describe('with a created product', () => {
    let productName: string;

    test.beforeEach(async ({ page }) => {
      productName = `E2E Product ${Date.now()}`;
      await page.goto('/products/new');
      await page.getByLabel('Name').fill(productName);
      await page.getByRole('button', { name: /save|create/i }).click();
      await page.waitForURL('/products');
    });

    test.afterEach(async ({ page }) => {
      await deleteProduct(page, productName);
    });

    test('created product appears in the list', async ({ page }) => {
      await expect(page.getByRole('cell', { name: productName })).toBeVisible();
    });

    test('inline delete confirmation appears on Delete click', async ({ page }) => {
      const row = page.getByRole('row', { name: productName });
      await row.getByRole('button', { name: /delete/i }).click();
      await expect(row.getByRole('button', { name: /yes/i })).toBeVisible();
    });

    test('product detail page shows product name', async ({ page }) => {
      await page.getByRole('cell', { name: productName }).click();
      await expect(page.getByRole('heading', { level: 2 })).toContainText(productName);
      await expect(page.getByRole('link', { name: /find manual/i })).toBeVisible();
    });

    test('"Find Manual" navigates to chat with pre-populated query', async ({ page }) => {
      await page.getByRole('cell', { name: productName }).click();
      await page.getByRole('link', { name: /find manual/i }).click();
      await expect(page).toHaveURL(/\/chat\?q=/);
      const input = page.locator('textarea, input[type="text"]').first();
      await expect(input).toHaveValue(new RegExp(productName));
    });
  });
});
