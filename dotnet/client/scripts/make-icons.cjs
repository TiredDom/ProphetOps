// Draws the app icons into public/icons: the forecast mark (a recorded line turning into the dashed
// teal forecast) on the navy shell colour. Run with `node scripts/make-icons.cjs` after changing it.
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');

const NAVY = '#0E2247';
const WHITE = '#FFFFFF';
const TEAL_ON_NAVY = '#86D9C5';
const OUT = path.join(__dirname, '..', 'public', 'icons');

/**
 * `rounded` icons keep transparent corners (browsers and launchers show them as drawn); full-bleed
 * ones fill the square for platforms that cut their own shape. `glyph` is the share of the width the
 * 24-unit mark is scaled to; `stroke` thickens it for small sizes.
 */
function svg(size, { rounded, glyph, stroke = 2 }) {
  const scale = (size * glyph) / 24;
  const offset = (size - 24 * scale) / 2;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">
  <rect width="${size}" height="${size}" rx="${rounded ? size * 0.22 : 0}" fill="${NAVY}"/>
  <g transform="translate(${offset.toFixed(3)} ${(offset + 0.25 * scale).toFixed(3)}) scale(${scale.toFixed(4)})" fill="none" stroke-linecap="round" stroke-linejoin="round">
    <polyline points="3.5 17.5 8 13 11 15 13.5 11.5" stroke="${WHITE}" stroke-width="${stroke}"/>
    <polyline points="13.5 11.5 16.8 8.2 20.5 6" stroke="${TEAL_ON_NAVY}" stroke-width="${stroke}" stroke-dasharray="1.4 2.6" stroke-dashoffset="0.7"/>
    <circle cx="13.5" cy="11.5" r="${(stroke * 0.95).toFixed(2)}" fill="${WHITE}"/>
  </g>
</svg>
`;
}

const icons = [
  { file: 'icon-192.png', size: 192, rounded: true, glyph: 0.74 },
  { file: 'icon-512.png', size: 512, rounded: true, glyph: 0.74 },
  // Maskable icons are cut to a circle or squircle, so the mark stays inside the middle 80%.
  { file: 'icon-maskable-512.png', size: 512, rounded: false, glyph: 0.6 },
  // iOS rounds the corners itself and turns transparency black.
  { file: 'apple-touch-icon.png', size: 180, rounded: false, glyph: 0.72 },
  { file: 'favicon-32.png', size: 32, rounded: true, glyph: 0.86, stroke: 2.6 },
];

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  fs.writeFileSync(path.join(OUT, 'icon.svg'), svg(64, { rounded: true, glyph: 0.84, stroke: 2.4 }));

  const browser = await chromium.launch({ headless: true });
  for (const icon of icons) {
    const page = await browser.newPage({ viewport: { width: icon.size, height: icon.size }, deviceScaleFactor: 1 });
    await page.setContent(`<!doctype html><html><body style="margin:0;background:transparent">${svg(icon.size, icon)}</body></html>`);
    await page.locator('svg').screenshot({ path: path.join(OUT, icon.file), omitBackground: true });
    await page.close();
    console.log('wrote', icon.file);
  }
  await browser.close();
})();
