const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { test } = require('node:test');
const { chromium } = require('playwright');

const repositoryRoot = path.resolve(__dirname, '../..');

function exportFixture() {
  const outputDirectory = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-pdm-browser-smoke-'));
  const fixture = path.join(repositoryRoot, 'tests', 'Bing.Pdm.Tests', 'Fixtures', 'complete.pdm');
  const project = path.join(repositoryRoot, 'samples', 'Bing.Pdm.Tool', 'Bing.Pdm.Tool.csproj');
  const result = spawnSync('dotnet', [
    'run', '--project', project, '--no-restore', '--',
    'export', fixture, outputDirectory, 'html', 'en'
  ], { cwd: repositoryRoot, encoding: 'utf8' });

  assert.equal(result.status, 0, `PDM HTML export failed:\n${result.stdout}\n${result.stderr}`);

  const htmlPath = path.join(outputDirectory, 'complete.html');
  assert.ok(fs.existsSync(htmlPath), `Expected exporter output at ${htmlPath}`);
  return { htmlPath, outputDirectory };
}

test('offline HTML dictionary supports search, diagram links, and zoom', async () => {
  const { htmlPath, outputDirectory } = exportFixture();
  const browserOptions = { headless: true };
  if (process.env.PDM_BROWSER_EXECUTABLE)
    browserOptions.executablePath = process.env.PDM_BROWSER_EXECUTABLE;

  const browser = await chromium.launch(browserOptions);
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  const consoleProblems = [];
  const externalRequests = [];
  page.on('console', message => {
    if (message.type() === 'error' || message.type() === 'warning')
      consoleProblems.push(`${message.type()}: ${message.text()}`);
  });
  page.on('pageerror', error => consoleProblems.push(`pageerror: ${error.message}`));
  page.on('request', request => {
    if (/^https?:\/\//i.test(request.url()))
      externalRequests.push(request.url());
  });

  try {
    const pageUrl = pathToFileURL(htmlPath).href;
    await page.goto(pageUrl, { waitUntil: 'load' });
    assert.equal(new URL(page.url()).protocol, 'file:');
    assert.match(await page.title(), /Data dictionary/i);
    assert.ok(await page.locator('h1').innerText());
    assert.ok(await page.locator('#tables article[data-searchable]').count());
    assert.equal(await page.locator('[data-nextjs-dialog], vite-error-overlay, [data-webpack-dev-server-client-overlay]').count(), 0);

    const screenshotDirectory = path.join(outputDirectory, 'screenshots');
    fs.mkdirSync(screenshotDirectory);
    const desktopScreenshot = path.join(screenshotDirectory, 'dictionary-desktop.png');
    await page.screenshot({ path: desktopScreenshot });

    const search = page.locator('#dictionary-search');
    await search.fill('Order Summary');
    assert.equal(await page.locator('[data-searchable]:not([hidden])').count(), 1);
    assert.match(await page.locator('[data-searchable]:not([hidden])').innerText(), /Order Summary/);
    assert.equal(await page.locator('#tables article:not([hidden])').count(), 0);
    await search.fill('');

    const svg = page.locator('.diagram-panel svg').first();
    const originalViewBox = await svg.getAttribute('viewBox');
    const originalWidth = await svg.evaluate(element => element.viewBox.baseVal.width);
    await page.locator('.diagram-panel button[data-zoom="in"]').first().click();
    const zoomedWidth = await svg.evaluate(element => element.viewBox.baseVal.width);
    assert.ok(zoomedWidth < originalWidth, `Expected zoom-in width ${zoomedWidth} to be below ${originalWidth}`);
    await page.locator('.diagram-panel button[data-zoom="reset"]').first().click();
    assert.equal(await svg.getAttribute('viewBox'), originalViewBox);

    const diagramHeading = page.locator('#diagrams h3').first();
    await diagramHeading.scrollIntoViewIfNeeded();
    const diagramScreenshot = path.join(screenshotDirectory, 'dictionary-diagram.png');
    await page.screenshot({ path: diagramScreenshot });

    const tableLink = page.locator('.diagram-panel svg a[href^="#table-"]').first();
    const tableTarget = await tableLink.getAttribute('href');
    assert.ok(tableTarget, 'Expected a diagram symbol linked to a dictionary table');
    await tableLink.click();
    assert.equal(await page.evaluate(() => location.hash), tableTarget);
    assert.ok(await page.locator(tableTarget).isVisible());

    await page.setViewportSize({ width: 390, height: 844 });
    await page.locator('h1').scrollIntoViewIfNeeded();
    const mobileDimensions = await page.evaluate(() => ({
      viewportWidth: window.innerWidth,
      documentWidth: document.documentElement.scrollWidth
    }));
    assert.ok(mobileDimensions.documentWidth <= mobileDimensions.viewportWidth,
      `Mobile page overflows horizontally: ${JSON.stringify(mobileDimensions)}`);
    const customerIds = page.locator('#tables table tbody tr td:first-child code')
      .filter({ hasText: /^CustomerId$/ });
    const customerIdLines = await customerIds.evaluateAll(elements => elements.map(element => {
      const range = document.createRange();
      range.selectNodeContents(element);
      return range.getClientRects().length;
    }));
    assert.ok(customerIdLines.length > 0, 'Expected fixture columns named CustomerId');
    assert.ok(customerIdLines.every(lineCount => lineCount === 1),
      `Column identifiers should not split across lines on mobile: ${customerIdLines}`);
    const mobileScreenshot = path.join(screenshotDirectory, 'dictionary-mobile.png');
    await page.screenshot({ path: mobileScreenshot });

    assert.deepEqual(externalRequests, [], 'Offline HTML requested external resources');
    assert.deepEqual(consoleProblems, [], 'Browser reported console or runtime problems');

    console.log(JSON.stringify({
      title: await page.title(),
      url: pageUrl,
      search: 'Order Summary filters the dictionary to the matching view',
      zoom: { originalWidth, zoomedWidth, reset: true },
      diagramLink: tableTarget,
      externalRequests: externalRequests.length,
      consoleProblems: consoleProblems.length,
      mobileDimensions,
      screenshots: [desktopScreenshot, diagramScreenshot, mobileScreenshot]
    }, null, 2));
  } finally {
    await browser.close();
  }
});
