/**
 * Captures screenshots of the React frontend (in mock mode) for the marketing website.
 *
 * Usage:
 *   npm run build:screenshot
 *   npm run preview:screenshot &
 *   npm run screenshot
 *
 * Or set BASE_URL / OUT_DIR env vars for CI use.
 */

import { chromium } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));

const BASE_URL = process.env.BASE_URL ?? 'http://localhost:5174';
const OUT_DIR = resolve(
  process.env.OUT_DIR ?? resolve(__dirname, '../../..', 'website/images'),
);

mkdirSync(OUT_DIR, { recursive: true });

/** Routes to capture. Each writes <name>.png to OUT_DIR. */
const PAGES = [
  { route: '/',                name: 'dashboard' },
  { route: '/repositories',   name: 'repositories' },
  { route: '/pull-requests',  name: 'pull-requests' },
  { route: '/workflows',      name: 'workflows' },
];

async function waitForAppReady(page) {
  // MSW service worker registers, then React renders.
  // Wait until the root element has rendered (no loading spinner visible).
  await page.waitForFunction(() => {
    const root = document.getElementById('root');
    return root && root.children.length > 0;
  }, { timeout: 15_000 });
  // Extra settle time for animations / query fetches
  await page.waitForTimeout(1200);
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1280, height: 800 },
    colorScheme: 'dark',
  });

  const page = await context.newPage();

  // Set dark theme in localStorage before first navigation
  await page.addInitScript(() => {
    localStorage.setItem('dh-theme', 'dark');
  });

  for (const { route, name } of PAGES) {
    console.log(`Capturing ${route} → ${name}.png`);
    await page.goto(`${BASE_URL}${route}`, { waitUntil: 'networkidle' });
    await waitForAppReady(page);
    const outPath = resolve(OUT_DIR, `${name}.png`);
    await page.screenshot({ path: outPath, fullPage: false });
    console.log(`  ✓ saved ${outPath}`);
  }

  await browser.close();
  console.log('Done.');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
