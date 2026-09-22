import { test, expect } from '@playwright/test';

test('home presents the main contact journey', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: /Energia solar distribuída/i })).toBeVisible();
  await expect(page.getByRole('link', { name: /Falar pelo WhatsApp/i })).toBeVisible();
  await expect(page.locator('video.hybrid-video')).toBeVisible();
  await expect(page.getByLabel('Seu nome')).toBeVisible();
});

test('home is usable on a mobile viewport', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');

  await expect(page.getByRole('heading', { name: /Energia solar distribuída/i })).toBeVisible();
  await expect(page.getByRole('link', { name: /WhatsApp/i }).first()).toBeVisible();
  await expect(page.locator('video.hybrid-video')).toBeVisible();

  const hasHorizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1);
  expect(hasHorizontalOverflow).toBe(false);
});

test('protected CMS redirects unauthenticated visitors to login', async ({ page }) => {
  await page.goto('/admin/conteudo');
  await expect(page).toHaveURL(/\/login$/);
});
