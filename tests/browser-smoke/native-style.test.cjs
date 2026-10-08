const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { test } = require('node:test');
const { chromium } = require('playwright');

test('PowerDesigner style remains visible and offline', async () => {
  const root = path.resolve(__dirname, '../..');
  const tool = path.join(root, 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll');
  const fixture = path.join(root, 'tests/Bing.Pdm.Tests/Fixtures/native-visual.pdm');
  const output = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-pdm-native-style-'));
  const exported = spawnSync('dotnet', [tool, 'export', fixture, output, 'html,svg', 'en',
    '--diagram-style', 'powerdesigner'], { cwd: root, encoding: 'utf8' });
  assert.equal(exported.status, 0, exported.stderr);
  const browser = await chromium.launch({ headless: true,
    ...(process.env.PDM_BROWSER_EXECUTABLE ? { executablePath: process.env.PDM_BROWSER_EXECUTABLE } : {}) });
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 850 } });
    const external = [];
    page.on('request', request => {
      if (/^https?:\/\//i.test(request.url())) external.push(request.url());
    });
    await page.goto(pathToFileURL(path.join(output, 'native-visual.html')).href);
    const svg = page.locator('.diagram svg');
    assert.equal(await svg.count(), 1);
    assert.ok(await svg.locator('linearGradient').count() >= 1);
    assert.ok(await svg.locator('text').allTextContents().then(x => x.some(t => t.includes('Name'))));
    assert.ok(await svg.locator('polyline').count() >= 1);
    const before = await svg.getAttribute('viewBox');
    await page.getByRole('button', { name: 'Zoom in' }).click();
    assert.notEqual(await svg.getAttribute('viewBox'), before);
    await page.getByRole('button', { name: 'Reset zoom' }).click();
    assert.equal(await svg.getAttribute('viewBox'), before);
    assert.deepEqual(external, []);
  } finally {
    await browser.close();
    fs.rmSync(output, { recursive: true, force: true });
  }
});

test('native annotation fixture renders Chinese note offline', async () => {
  const root = path.resolve(__dirname, '../..');
  const tool = path.join(root, 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll');
  const fixture = path.join(root, 'tests/Bing.Pdm.Tests/Fixtures/native-annotations.pdm');
  const output = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-pdm-native-note-'));
  const exported = spawnSync('dotnet', [tool, 'export', fixture, output, 'html', 'en',
    '--diagram-style', 'powerdesigner'], { cwd: root, encoding: 'utf8' });
  assert.equal(exported.status, 0, exported.stderr);
  const browser = await chromium.launch({ headless: true,
    ...(process.env.PDM_BROWSER_EXECUTABLE ? { executablePath: process.env.PDM_BROWSER_EXECUTABLE } : {}) });
  try {
    const page = await browser.newPage();
    const external = [];
    page.on('request', request => {
      if (/^https?:\/\//i.test(request.url())) external.push(request.url());
    });
    await page.goto(pathToFileURL(path.join(output, 'native-annotations.html')).href);
    const note = page.locator('.diagram svg [data-symbol-id="o1000"]');
    assert.equal(await note.count(), 1);
    assert.ok((await note.textContent()).includes('你好'));
    assert.deepEqual(external, []);
  } finally {
    await browser.close();
    fs.rmSync(output, { recursive: true, force: true });
  }
});

test('native typography fixture keeps font roles and styles offline', async () => {
  const root = path.resolve(__dirname, '../..');
  const tool = path.join(root, 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll');
  const fixture = path.join(root, 'tests/Bing.Pdm.Tests/Fixtures/native-typography.pdm');
  const output = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-pdm-native-type-'));
  const exported = spawnSync('dotnet', [tool, 'export', fixture, output, 'html,svg', 'en',
    '--diagram-style', 'powerdesigner'], { cwd: root, encoding: 'utf8' });
  assert.equal(exported.status, 0, exported.stderr);
  const browser = await chromium.launch({ headless: true,
    ...(process.env.PDM_BROWSER_EXECUTABLE ? { executablePath: process.env.PDM_BROWSER_EXECUTABLE } : {}) });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const external = [];
    page.on('request', request => {
      if (/^https?:\/\//i.test(request.url())) external.push(request.url());
    });
    await page.goto(pathToFileURL(path.join(output, 'native-typography.html')).href);
    const title = page.locator('.diagram svg text', { hasText: 'Orders' }).first();
    assert.equal(await title.getAttribute('font-family'), 'Times New Roman');
    assert.equal(await title.getAttribute('font-style'), 'italic');
    assert.equal(await title.getAttribute('font-size'), '1200');
    assert.ok(await page.locator('.diagram svg text[text-decoration="underline"]').count() > 0);
    assert.deepEqual(external, []);
  } finally {
    await browser.close();
    fs.rmSync(output, { recursive: true, force: true });
  }
});

test('native nested shapes remain visible offline on mobile', async () => {
  const root = path.resolve(__dirname, '../..');
  const tool = path.join(root, 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll');
  const fixture = path.join(root, 'tests/Bing.Pdm.Tests/Fixtures/native-shapes.pdm');
  const output = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-pdm-native-shapes-'));
  const exported = spawnSync('dotnet', [tool, 'export', fixture, output, 'html,svg', 'en',
    '--diagram-style', 'powerdesigner'], { cwd: root, encoding: 'utf8' });
  assert.equal(exported.status, 0, exported.stderr);
  assert.ok(!exported.stderr.includes('UNSUPPORTED_DIAGRAM_STYLE'), exported.stderr);
  assert.equal((exported.stderr.match(/UNRESOLVED_REFERENCE_LABEL \[o5\]/g) || []).length, 1,
    exported.stderr);
  const browser = await chromium.launch({ headless: true,
    ...(process.env.PDM_BROWSER_EXECUTABLE ? { executablePath: process.env.PDM_BROWSER_EXECUTABLE } : {}) });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const external = [];
    page.on('request', request => {
      if (/^https?:\/\//i.test(request.url())) external.push(request.url());
    });
    await page.goto(pathToFileURL(path.join(output, 'native-shapes.html')).href);
    const svg = page.locator('.diagram svg');
    assert.equal(await svg.locator('[data-symbol-id="o11"] rect:not(clipPath rect)').count(), 2);
    assert.equal(await svg.locator('[data-symbol-id="o12"] ellipse').count(), 1);
    assert.equal(await svg.locator('[data-symbol-id="o14"]').count(), 1);
    assert.ok((await svg.textContent()).includes('Workflow'));
    assert.ok((await svg.textContent()).includes('Decision'));
    assert.ok((await svg.textContent()).includes('Review'));
    assert.deepEqual(external, []);
  } finally {
    await browser.close();
    fs.rmSync(output, { recursive: true, force: true });
  }
});

test('native primary key pane remains visible offline', async () => {
  const root = path.resolve(__dirname, '../..');
  const tool = path.join(root, 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll');
  const fixture = path.join(root, 'tests/Bing.Pdm.Tests/Fixtures/native-keys.pdm');
  const output = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-pdm-native-keys-'));
  const exported = spawnSync('dotnet', [tool, 'export', fixture, output, 'html,svg', 'en',
    '--diagram-style', 'powerdesigner'], { cwd: root, encoding: 'utf8' });
  assert.equal(exported.status, 0, exported.stderr);
  assert.ok(!exported.stderr.includes('UNSUPPORTED_DIAGRAM_STYLE'), exported.stderr);
  const browser = await chromium.launch({ headless: true,
    ...(process.env.PDM_BROWSER_EXECUTABLE ? { executablePath: process.env.PDM_BROWSER_EXECUTABLE } : {}) });
  try {
    const page = await browser.newPage();
    const external = [];
    page.on('request', request => {
      if (/^https?:\/\//i.test(request.url())) external.push(request.url());
    });
    await page.goto(pathToFileURL(path.join(output, 'native-keys.html')).href);
    const svg = page.locator('.diagram svg');
    assert.ok((await svg.textContent()).includes('PK Orders'));
    assert.ok((await svg.textContent()).includes('PK Customers'));
    assert.equal(await svg.locator('[data-symbol-id="o7"] polyline').count(), 1);
    assert.deepEqual(external, []);
  } finally {
    await browser.close();
    fs.rmSync(output, { recursive: true, force: true });
  }
});

test('native index pane remains visible offline', async () => {
  const root = path.resolve(__dirname, '../..');
  const tool = path.join(root, 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll');
  const fixture = path.join(root, 'tests/Bing.Pdm.Tests/Fixtures/native-indexes.pdm');
  const output = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-pdm-native-indexes-'));
  const exported = spawnSync('dotnet', [tool, 'export', fixture, output, 'html,svg', 'en',
    '--diagram-style', 'powerdesigner'], { cwd: root, encoding: 'utf8' });
  assert.equal(exported.status, 0, exported.stderr);
  assert.ok(!exported.stderr.includes('UNSUPPORTED_DIAGRAM_STYLE'), exported.stderr);
  const browser = await chromium.launch({ headless: true,
    ...(process.env.PDM_BROWSER_EXECUTABLE ? { executablePath: process.env.PDM_BROWSER_EXECUTABLE } : {}) });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const external = [];
    page.on('request', request => {
      if (/^https?:\/\//i.test(request.url())) external.push(request.url());
    });
    await page.goto(pathToFileURL(path.join(output, 'native-indexes.html')).href);
    const svg = page.locator('.diagram svg');
    assert.ok((await svg.textContent()).includes('IX Orders Name'));
    assert.equal(await svg.locator('[data-symbol-id="o7"] polyline').count(), 3);
    assert.equal(await svg.locator('[data-symbol-id="o6"] polyline').count(), 0);
    const bounds = await svg.locator('[data-symbol-id="o7"]').getAttribute('data-bounds');
    assert.equal(bounds.split(' ')[3], '4749');
    assert.deepEqual(external, []);
  } finally {
    await browser.close();
    fs.rmSync(output, { recursive: true, force: true });
  }
});

test('native key and index panes remain visible together offline', async () => {
  const root = path.resolve(__dirname, '../..');
  const tool = path.join(root, 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll');
  const fixture = path.join(root, 'tests/Bing.Pdm.Tests/Fixtures/native-keys-indexes.pdm');
  const output = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-pdm-native-keys-indexes-'));
  const exported = spawnSync('dotnet', [tool, 'export', fixture, output, 'html,svg', 'en',
    '--diagram-style', 'powerdesigner'], { cwd: root, encoding: 'utf8' });
  assert.equal(exported.status, 0, exported.stderr);
  assert.ok(!exported.stderr.includes('UNSUPPORTED_DIAGRAM_STYLE'), exported.stderr);
  const browser = await chromium.launch({ headless: true,
    ...(process.env.PDM_BROWSER_EXECUTABLE ? { executablePath: process.env.PDM_BROWSER_EXECUTABLE } : {}) });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const external = [];
    page.on('request', request => {
      if (/^https?:\/\//i.test(request.url())) external.push(request.url());
    });
    await page.goto(pathToFileURL(path.join(output, 'native-keys-indexes.html')).href);
    const upper = page.locator('.diagram svg [data-symbol-id="o7"]');
    const text = await upper.textContent();
    assert.ok(text.includes('PK Orders'));
    assert.ok(text.includes('IX Orders Name'));
    assert.equal(await upper.locator('polyline').count(), 4);
    assert.deepEqual(external, []);
  } finally {
    await browser.close();
    fs.rmSync(output, { recursive: true, force: true });
  }
});

test('multiple native indexes remain visible offline', async () => {
  const root = path.resolve(__dirname, '../..');
  const tool = path.join(root, 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll');
  const fixture = path.join(root, 'tests/Bing.Pdm.Tests/Fixtures/native-multi-indexes.pdm');
  const output = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-pdm-native-multi-indexes-'));
  const exported = spawnSync('dotnet', [tool, 'export', fixture, output, 'html,svg', 'en',
    '--diagram-style', 'powerdesigner'], { cwd: root, encoding: 'utf8' });
  assert.equal(exported.status, 0, exported.stderr);
  assert.ok(!exported.stderr.includes('UNSUPPORTED_DIAGRAM_STYLE'), exported.stderr);
  const browser = await chromium.launch({ headless: true,
    ...(process.env.PDM_BROWSER_EXECUTABLE ? { executablePath: process.env.PDM_BROWSER_EXECUTABLE } : {}) });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const external = [];
    page.on('request', request => {
      if (/^https?:\/\//i.test(request.url())) external.push(request.url());
    });
    await page.goto(pathToFileURL(path.join(output, 'native-multi-indexes.html')).href);
    const svg = page.locator('.diagram svg');
    const text = await svg.textContent();
    assert.ok(text.includes('IX Orders Name'));
    assert.ok(text.includes('IX Orders ID'));
    assert.equal(await svg.locator('[data-symbol-id="o7"] polyline').count(), 6);
    assert.deepEqual(external, []);
  } finally {
    await browser.close();
    fs.rmSync(output, { recursive: true, force: true });
  }
});

test('native alternate key remains visible offline', async () => {
  const root = path.resolve(__dirname, '../..');
  const tool = path.join(root, 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll');
  const fixture = path.join(root, 'tests/Bing.Pdm.Tests/Fixtures/native-alternate-key.pdm');
  const output = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-pdm-native-alternate-key-'));
  const exported = spawnSync('dotnet', [tool, 'export', fixture, output, 'html,svg', 'en',
    '--diagram-style', 'powerdesigner'], { cwd: root, encoding: 'utf8' });
  assert.equal(exported.status, 0, exported.stderr);
  assert.ok(!exported.stderr.includes('UNSUPPORTED_DIAGRAM_STYLE'), exported.stderr);
  const browser = await chromium.launch({ headless: true,
    ...(process.env.PDM_BROWSER_EXECUTABLE ? { executablePath: process.env.PDM_BROWSER_EXECUTABLE } : {}) });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const external = [];
    page.on('request', request => {
      if (/^https?:\/\//i.test(request.url())) external.push(request.url());
    });
    await page.goto(pathToFileURL(path.join(output, 'native-alternate-key.html')).href);
    const upper = page.locator('.diagram svg [data-symbol-id="o7"]');
    const text = await upper.textContent();
    assert.ok(text.includes('AK Orders Name'));
    assert.ok(text.includes('<ak>'));
    assert.equal(await upper.locator('polyline').count(), 2);
    assert.deepEqual(external, []);
  } finally {
    await browser.close();
    fs.rmSync(output, { recursive: true, force: true });
  }
});

for (const [style, pattern] of [['dash', '1200 600'], ['dot', '225 225'],
  ['dashdot', '600 300 150 300'], ['dashdotdot', '600 300 150 300 150 300']]) {
test(`native ${style} reference remains visible offline`, async () => {
  const root = path.resolve(__dirname, '../..');
  const tool = path.join(root, 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll');
  const fixture = path.join(root, `tests/Bing.Pdm.Tests/Fixtures/native-${style}.pdm`);
  const output = fs.mkdtempSync(path.join(os.tmpdir(), `bing-pdm-native-${style}-`));
  const exported = spawnSync('dotnet', [tool, 'export', fixture, output, 'html,svg', 'en',
    '--diagram-style', 'powerdesigner'], { cwd: root, encoding: 'utf8' });
  assert.equal(exported.status, 0, exported.stderr);
  const browser = await chromium.launch({ headless: true,
    ...(process.env.PDM_BROWSER_EXECUTABLE ? { executablePath: process.env.PDM_BROWSER_EXECUTABLE } : {}) });
  try {
    const page = await browser.newPage();
    const external = [];
    page.on('request', request => {
      if (/^https?:\/\//i.test(request.url())) external.push(request.url());
    });
    await page.goto(pathToFileURL(path.join(output, `native-${style}.html`)).href);
    const line = page.locator('.diagram svg [data-symbol-id="o5"]');
    assert.equal(await line.getAttribute('stroke-dasharray'), pattern);
    assert.deepEqual(external, []);
  } finally {
    await browser.close();
    fs.rmSync(output, { recursive: true, force: true });
  }
});
}
