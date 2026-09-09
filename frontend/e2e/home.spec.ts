import { test, expect } from '@playwright/test';

test('home presents the main contact journey', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: /Seu próximo passo/i })).toBeVisible();
  await expect(page.getByRole('link', { name: /Calcular meu sistema/i })).toBeVisible();
  await expect(page.getByLabel('Seu nome')).toBeVisible();
});

test('protected CMS redirects unauthenticated visitors to login', async ({ page }) => {
  await page.goto('/admin/conteudo');
  await expect(page).toHaveURL(/\/login$/);
});
