import { test, expect } from '@playwright/test';

test.describe('admin dashboard routes', () => {
  for (const route of [
    '/admin/dimensionamento',
    '/admin/cotacao',
    '/admin/fornecedores',
    '/admin/propostas',
    '/admin/conteudo',
  ]) {
    test(`${route} protects the workspace`, async ({ page }) => {
      await page.goto(route);
      await expect(page).toHaveURL(/\/login$/);
      await expect(page.getByRole('heading', { name: /Acesso administrativo/i })).toBeVisible();
    });
  }
});

test('dimensioning and proposal routes do not overflow on mobile', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  for (const route of ['/admin/dimensionamento', '/admin/cotacao', '/admin/propostas']) {
    await page.goto(route);
    await expect(page).toHaveURL(/\/login$/);
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth > window.innerWidth + 1,
    );
    expect(overflow, `${route} has horizontal overflow`).toBe(false);
  }
});
