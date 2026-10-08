const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { test } = require('node:test');
const { chromium } = require('playwright');

test('RTF fonts, colors and scoped underline render offline on desktop and mobile', async () => {
  const root = path.resolve(__dirname, '../..');
  const fixture = path.join(root, 'tests/Bing.Pdm.Tests/Fixtures/rich-text-styles.pdm');
  const output = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-pdm-rich-text-'));
  const tool = path.join(root, 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll');
  const exported = spawnSync('dotnet', [tool, 'export', fixture, output, 'html,svg', 'en',
    '--diagram-style', 'powerdesigner'], { cwd: root, encoding: 'utf8' });
  assert.equal(exported.status, 0, exported.stderr);
  const screenshots = path.join(root, 'artifacts', 'browser-qa', `rich-text-${Date.now()}`);
  fs.mkdirSync(screenshots, { recursive: true });
  const browser = await chromium.launch({ headless: true,
    ...(process.env.PDM_BROWSER_EXECUTABLE ? { executablePath: process.env.PDM_BROWSER_EXECUTABLE } : {}) });
  try {
    for (const viewport of [{ width: 1280, height: 850 }, { width: 390, height: 844 }]) {
      const page = await browser.newPage({ viewport });
      const errors = [];
      const requests = [];
      page.on('pageerror', error => errors.push(error.message));
      page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
      page.on('request', request => { if (/^https?:/i.test(request.url())) requests.push(request.url()); });
      const url = pathToFileURL(path.join(output, 'rich-text-styles.html')).href;
      await page.goto(url);
      assert.equal(page.url(), url);
      assert.ok((await page.title()).includes('Rich style'));
      const svg = page.locator('.diagram svg');
      const note = svg.locator('[data-symbol-id="note"]');
      assert.ok((await note.textContent()).includes('你好'));
      const red = note.locator('tspan').filter({ hasText: 'Red' }).first();
      assert.equal(await red.getAttribute('font-family'), 'Times New Roman');
      assert.equal(await red.getAttribute('fill'), '#ff0000');
      assert.equal(await red.getAttribute('text-decoration'), 'underline');
      const blue = note.locator('tspan').filter({ hasText: '你好' });
      assert.equal(await blue.getAttribute('fill'), '#0000ff');
      assert.equal(await blue.getAttribute('font-weight'), 'bold');
      assert.equal(await svg.locator('feDropShadow').count(), 2);
      assert.deepEqual(await svg.locator('feDropShadow').evaluateAll(nodes =>
        nodes.map(node => node.getAttribute('flood-color'))), ['#ff0000', '#0000ff']);
      const before = await svg.getAttribute('viewBox');
      await page.getByRole('button', { name: 'Zoom in' }).click();
      assert.notEqual(await svg.getAttribute('viewBox'), before);
      await page.getByRole('button', { name: 'Reset zoom' }).click();
      assert.equal(await svg.getAttribute('viewBox'), before);
      await svg.locator('a[href^="#table-"]').first().click();
      assert.ok(new URL(page.url()).hash.startsWith('#table-'));
      await page.screenshot({ path: path.join(screenshots, `${viewport.width}.png`), fullPage: true });
      assert.deepEqual(errors, []);
      assert.deepEqual(requests, []);
      await page.close();
    }
    console.log(`Rich-text screenshots: ${screenshots}`);
  } finally {
    await browser.close();
    fs.rmSync(output, { recursive: true, force: true });
  }
});
