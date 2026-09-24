import { test, expect } from '@playwright/test';

test('home presents the main contact journey', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: /Energia solar distribuída/i })).toBeVisible();
  await expect(page.getByRole('link', { name: /Falar pelo WhatsApp/i })).toBeVisible();
  await expect(page.locator('video.hybrid-video')).toBeVisible();
  await expect(page.getByLabel('Seu nome')).toBeVisible();
  await expect(page.locator('.project-feature')).toHaveCount(0);
  await expect(page.locator('.video-frame p')).toHaveCount(0);
  await expect(page.locator('[data-reveal]')).toHaveCount(7);
  await expect(page.locator('.offer-cta')).toHaveCount(3);
  await expect(page.locator('.offer-carousel')).toHaveAttribute('tabindex', '0');
  await expect(page.locator('.hero-bg')).toHaveAttribute('src', 'assets/hero-solar-garage-battery.png');

  const videoResponse = await page.request.get('/assets/video.mp4');
  expect(videoResponse.ok()).toBe(true);
  const heroResponse = await page.request.get('/assets/hero-solar-garage-battery.png');
  expect(heroResponse.ok()).toBe(true);

  for (const selector of ['video.hybrid-video']) {
    const hasMetadata = await page.locator(selector).evaluate(async video => {
      if (video.readyState >= 1) return true;
      return new Promise<boolean>(resolve => {
        video.addEventListener('loadedmetadata', () => resolve(true), { once: true });
        window.setTimeout(() => resolve(false), 10000);
      });
    });
    expect(hasMetadata, `metadata not loaded for ${selector}`).toBe(true);
  }

  await expect.poll(async () => page.locator('video.hybrid-video').evaluate(video => !video.paused), { timeout: 10000 }).toBe(true);

  const geometry = await page.evaluate(() => {
    const wave = document.querySelector('.solar-command > .section-wave') as HTMLElement;
    const svg = wave.querySelector('svg')!.getBoundingClientRect();
    const frame = document.querySelector('.video-frame')!.getBoundingClientRect();
    const video = document.querySelector('video.hybrid-video')!.getBoundingClientRect();
    return {
      waveWidth: wave.getBoundingClientRect().width,
      svgWidth: svg.width,
      videoGap: frame.bottom - video.bottom,
      videoTopGap: video.top - frame.top
    };
  });
  expect(geometry.svgWidth).toBeCloseTo(geometry.waveWidth, 0);
  expect(geometry.videoGap).toBeCloseTo(0, 0);
  expect(geometry.videoTopGap).toBeGreaterThanOrEqual(0);

  const revealTarget = page.locator('[data-reveal]').nth(1);
  await revealTarget.scrollIntoViewIfNeeded();
  await expect.poll(() => revealTarget.evaluate(section => section.classList.contains('is-revealed')), { timeout: 2500 }).toBe(true);
});

test('home is usable on a mobile viewport', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');

  await expect(page.getByRole('heading', { name: /Energia solar distribuída/i })).toBeVisible();
  await expect(page.getByRole('link', { name: /WhatsApp/i }).first()).toBeVisible();
  await expect(page.locator('video.hybrid-video')).toBeVisible();
  await expect(page.locator('.project-chevron')).toHaveCount(2);
  await expect(page.locator('.project-chevron').first()).toBeHidden();
  await expect(page.locator('.project-thumbnails')).toHaveCSS('touch-action', 'pan-x');

  const hasHorizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1);
  expect(hasHorizontalOverflow).toBe(false);
});

test('opens a project image when a thumbnail is clicked', async ({ page }) => {
  await page.goto('/');
  await page.locator('.project-thumbnail').first().click();
  await expect(page.locator('.project-lightbox')).toBeVisible();
  await expect(page.locator('.project-lightbox img')).toBeVisible();
});

test('protected CMS redirects unauthenticated visitors to login', async ({ page }) => {
  await page.goto('/admin/conteudo');
  await expect(page).toHaveURL(/\/login$/);
});
