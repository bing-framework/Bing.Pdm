const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { test } = require('node:test');
const { chromium } = require('playwright');

const root = path.resolve(__dirname, '../..');
const tool = path.join(root, 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll');

function exportFixture(name) {
  const output = fs.mkdtempSync(path.join(os.tmpdir(), `bing-pdm-${name}-`));
  const fixture = path.join(root, `tests/Bing.Pdm.Tests/Fixtures/${name}.pdm`);
  const result = spawnSync('dotnet', [tool, 'export', fixture, output, 'html,svg', 'en',
    '--diagram-style', 'powerdesigner'], { cwd: root, encoding: 'utf8' });
  assert.equal(result.status, 0, `${name} export failed:\n${result.stdout}\n${result.stderr}`);
  return { output, html: path.join(output, `${name}.html`) };
}

function browserOptions() {
  return { headless: true, ...(process.env.PDM_BROWSER_EXECUTABLE ?
    { executablePath: process.env.PDM_BROWSER_EXECUTABLE } : {}) };
}

test('native trigger and paragraph fixtures render offline across desktop and mobile', async () => {
  assert.ok(fs.existsSync(tool), `CLI assembly was not built at ${tool}`);
  const trigger = exportFixture('native-triggers');
  const paragraphs = exportFixture('native-paragraphs');
  const screenshots = path.join(root, 'artifacts', 'browser-qa',
    `native-trigger-paragraph-${Date.now()}`);
  fs.mkdirSync(screenshots, { recursive: true });
  const browser = await chromium.launch(browserOptions());
  try {
    for (const viewport of [{ width: 1280, height: 850 }, { width: 390, height: 844 }]) {
      const page = await browser.newPage({ viewport });
      const errors = [];
      const external = [];
      page.on('pageerror', error => errors.push(`pageerror: ${error.message}`));
      page.on('console', message => {
        if (message.type() === 'error' || message.type() === 'warning')
          errors.push(`${message.type()}: ${message.text()}`);
      });
      page.on('request', request => {
        if (/^https?:/i.test(request.url())) external.push(request.url());
      });
      try {
        await page.goto(pathToFileURL(trigger.html).href, { waitUntil: 'load' });
        const triggerSvg = page.locator('.diagram-panel svg').first();
        assert.match(await triggerSvg.textContent(), /After insert audit/);
        const triggerTable = triggerSvg.locator('[data-symbol-id="o7"]');
        assert.ok((await triggerTable.textContent()).includes('After insert audit'));
        assert.ok(await triggerTable.locator('polyline').count() >= 2);
        const triggerLink = triggerSvg.locator('a[href^="#table-"]').first();
        const triggerTarget = await triggerLink.getAttribute('href');
        await triggerLink.click();
        assert.ok((await page.url()).includes(triggerTarget.slice(1)));
        const triggerViewBox = await triggerSvg.getAttribute('viewBox');
        await page.locator('[data-zoom="in"]').first().click();
        assert.notEqual(await triggerSvg.getAttribute('viewBox'), triggerViewBox);
        await page.locator('[data-zoom="reset"]').first().click();
        assert.equal(await triggerSvg.getAttribute('viewBox'), triggerViewBox);
        await page.screenshot({ path: path.join(screenshots, `triggers-${viewport.width}.png`), fullPage: true });

        await page.goto(pathToFileURL(paragraphs.html).href, { waitUntil: 'load' });
        const paragraphSvg = page.locator('.diagram-panel svg').first();
        const note = paragraphSvg.locator('[data-symbol-id="o1000"]');
        assert.match(await note.textContent(), /Center/);
        assert.match(await note.textContent(), /Right/);
        assert.ok(await note.locator('[clip-path]').count() >= 1);
        assert.ok(await note.locator('tspan').count() >= 3);
        const paragraphLink = paragraphSvg.locator('a[href^="#table-"]').first();
        assert.ok(await paragraphLink.count() === 1);
        await page.screenshot({ path: path.join(screenshots, `paragraphs-${viewport.width}.png`), fullPage: true });
        assert.deepEqual(external, []);
        assert.deepEqual(errors, []);
      } finally {
        await page.close();
      }
    }
    console.log(`Native trigger/paragraph screenshots: ${screenshots}`);
  } finally {
    await browser.close();
    fs.rmSync(trigger.output, { recursive: true, force: true });
    fs.rmSync(paragraphs.output, { recursive: true, force: true });
  }
});
