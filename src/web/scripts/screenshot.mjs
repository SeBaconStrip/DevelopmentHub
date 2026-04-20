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

/** Widgets to capture individually. */
const WIDGETS = [
  { id: 'repositories', name: 'repositories' },
  { id: 'pullRequests',  name: 'pull-requests' },
  { id: 'workflows',    name: 'workflows' },
  { id: 'todos',        name: 'todos' },
  { id: 'quickLinks',   name: 'quick-links' },
];

async function waitForAppReady(page) {
  await page.waitForFunction(() => {
    const root = document.getElementById('root');
    return root && root.children.length > 0;
  }, { timeout: 15_000 });
  await page.waitForTimeout(1200);
}

async function main() {
  const browser = await chromium.launch({ headless: true });

  // ── Dashboard hero (1600×900 @ 2x = 3200×1800, native 16:9, all 5 widgets) ─
  // 1600px viewport → 1560px container, well above the lg breakpoint (1200px).
  const dashCtx = await browser.newContext({
    viewport: { width: 1600, height: 900 },
    deviceScaleFactor: 2,
    colorScheme: 'dark',
  });
  const dashPage = await dashCtx.newPage();
  await dashPage.addInitScript(() => {
    localStorage.setItem('dh-theme', 'violet');
    localStorage.setItem('dh-widgets', JSON.stringify({
      repositories: true, pullRequests: true, workflows: true,
      todos: true, quickLinks: true,
    }));
    // lg (12 cols): repos tall-left, four widgets in 2×2 grid on right.
    // 10 rows × 54px + 11 gaps × 16px = 716px — fits inside 900px viewport.
    localStorage.setItem('dh-layouts', JSON.stringify({
      lg: [
        { i: 'repositories', x: 0, y: 0, w: 5, h: 11 },
        { i: 'pullRequests',  x: 5, y: 0, w: 4, h: 6 },
        { i: 'workflows',    x: 9, y: 0, w: 3, h: 6 },
        { i: 'todos',        x: 5, y: 6, w: 4, h: 5 },
        { i: 'quickLinks',   x: 9, y: 6, w: 3, h: 5 },
      ],
    }));
  });
  console.log('Capturing dashboard → dashboard.png');
  await dashPage.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });
  await waitForAppReady(dashPage);
  await dashPage.screenshot({ path: resolve(OUT_DIR, 'dashboard.png'), fullPage: false });
  console.log('  ✓ saved dashboard.png');
  await dashCtx.close();

  // ── Individual widget shots ────────────────────────────────────────────────
  // viewport=1078 → container=982px (md breakpoint, 10 cols)
  // w:10 = 982px wide, h:9 = 614px tall → ratio 614/982 = 0.625 = exact 16:10
  const widgetCtx = await browser.newContext({
    viewport: { width: 1078, height: 800 },
    colorScheme: 'dark',
  });
  const widgetPage = await widgetCtx.newPage();
  await widgetPage.addInitScript(() => {
    localStorage.setItem('dh-theme', 'violet');
    localStorage.setItem('dh-layouts', JSON.stringify({
      md: [
        { i: 'repositories', x: 0, y: 0,  w: 10, h: 9 },
        { i: 'pullRequests',  x: 0, y: 9,  w: 10, h: 9 },
        { i: 'workflows',    x: 0, y: 18, w: 10, h: 9 },
        { i: 'todos',        x: 0, y: 27, w: 10, h: 9 },
        { i: 'quickLinks',   x: 0, y: 36, w: 10, h: 9 },
      ],
    }));
  });
  await widgetPage.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });
  await waitForAppReady(widgetPage);

  for (const { id, name } of WIDGETS) {
    console.log(`Capturing widget ${id} → ${name}.png`);
    const el = widgetPage.locator(`[data-widget-id="${id}"]`);
    const outPath = resolve(OUT_DIR, `${name}.png`);
    await el.screenshot({ path: outPath });
    console.log(`  ✓ saved ${outPath}`);
  }

  await widgetCtx.close();
  await browser.close();
  console.log('Done.');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
