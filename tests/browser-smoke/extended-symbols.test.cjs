const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { test } = require('node:test');
const { chromium } = require('playwright');

test('extended PDM symbols remain visible in offline HTML', async () => {
  const root = path.resolve(__dirname, '../..');
  const output = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-pdm-symbol-smoke-'));
  const fixture = path.join(root, 'tests/Bing.Pdm.Tests/Fixtures/symbols-compat.pdm');
  const tool = path.join(root, 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll');
  const result = spawnSync('dotnet', [tool, 'export', fixture, output, 'html'], { encoding: 'utf8' });
  assert.equal(result.status, 0, result.stderr);
  const html = path.join(output, 'symbols-compat.html');
  const options = { headless: true };
  if (process.env.PDM_BROWSER_EXECUTABLE) options.executablePath = process.env.PDM_BROWSER_EXECUTABLE;
  const browser = await chromium.launch(options);
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  const external = [];
  const errors = [];
  page.on('request', request => { if (/^https?:\/\//.test(request.url())) external.push(request.url()); });
  page.on('pageerror', error => errors.push(error.message));
  try {
    await page.goto(pathToFileURL(html).href);
    for (const id of ['area', 'ellipse', 'note', 'text', 'predefined', 'polyline', 'note-link', 'dependency'])
      assert.equal(await page.locator(`[data-symbol-id="${id}"]`).count(), 1, id);
    assert.match(await page.locator('[data-symbol-id="note"]').textContent(), /你好/);
    assert.equal(await page.locator('[data-symbol-id="ellipse"] ellipse').count(), 1);
    assert.equal(await page.locator('[data-symbol-id="note-link"] polyline').count(), 1);
    const svg = page.locator('.diagram-panel svg');
    const before = await svg.getAttribute('viewBox');
    await page.locator('[data-zoom="in"]').click();
    assert.notEqual(await svg.getAttribute('viewBox'), before);
    await page.locator('[data-zoom="reset"]').click();
    assert.equal(await svg.getAttribute('viewBox'), before);
    await page.screenshot({ path: path.join(output, 'symbols-desktop.png') });
    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({ path: path.join(output, 'symbols-mobile.png') });
    assert.deepEqual(external, []);
    assert.deepEqual(errors, []);
  } finally {
    await browser.close();
  }
});
