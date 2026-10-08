const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const { createHash } = require('node:crypto');
const { execFileSync } = require('node:child_process');

async function main() {
  const root = path.resolve(__dirname, '../..');
  const fixture = process.argv[2] || 'native-visual';
  if (!/^native-[a-z0-9-]+$/.test(fixture)) throw new Error('Invalid native fixture name.');
  const baseline = path.join(root, 'docs/visual-baseline',
    fixture === 'native-visual' ? '' : fixture);
  const output = process.argv[3] ? path.resolve(process.argv[3]) : baseline;
  if (process.argv[3] && ['original.png', 'rendered.png', 'overlay.png', 'difference.png', 'metrics.json', 'evidence.json']
      .some(name => fs.existsSync(path.join(output, name))))
    throw new Error('Comparison artifacts already exist; choose a new candidate output directory.');
  const source = path.join(root, 'tests/Bing.Pdm.Tests/Fixtures', fixture + '.png');
  const svgFile = path.join(output, fixture + '-PhysicalDiagram_1.svg');
  const geometry = JSON.parse(fs.readFileSync(path.join(baseline, 'native-geometry.json'), 'utf8'));
  const original = fs.readFileSync(source);
  const svg = fs.readFileSync(svgFile);
  const browser = await chromium.launch({ headless: true,
    ...(process.env.PDM_BROWSER_EXECUTABLE ? { executablePath: process.env.PDM_BROWSER_EXECUTABLE } : {}) });
  try {
    const page = await browser.newPage();
    const comparison = await page.evaluate(async ({ originalData, svgData, geometry }) => {
      function load(src) {
        return new Promise((resolve, reject) => {
          const image = new Image();
          image.onload = () => resolve(image);
          image.onerror = reject;
          image.src = src;
        });
      }
      const native = await load(`data:image/png;base64,${originalData}`);
      const rendered = await load(`data:image/svg+xml;base64,${svgData}`);
      const width = native.naturalWidth;
      const height = native.naturalHeight;
      const svgDocument = new DOMParser().parseFromString(atob(svgData), 'image/svg+xml');
      const root = svgDocument.documentElement;
      const viewBox = root.getAttribute('viewBox').split(/\s+/).map(Number);
      const geometryErrors = geometry.symbols.map(symbol => {
        const node = [...root.querySelectorAll('[data-symbol-id]')]
          .find(item => item.getAttribute('data-symbol-id') === symbol.id);
        const rawBounds = node?.getAttribute('data-bounds');
        if (!rawBounds) throw new Error(`Missing symbol bounds: ${symbol.id}`);
        const renderedBounds = rawBounds.split(/\s+/).map((value, index) =>
          Number(value) * (index % 2 === 0
            ? width / viewBox[2] : height / viewBox[3]));
        return { id: symbol.id, nativeBounds: symbol.bounds,
          renderedBounds: renderedBounds.map(value => Number(value.toFixed(2))),
          meanAbsoluteError: symbol.bounds.reduce((sum, value, index) =>
            sum + Math.abs(value - renderedBounds[index]), 0) / 4 };
      });
      const lineErrors = (geometry.lines || []).map(line => {
        const node = [...root.querySelectorAll('polyline[data-symbol-id]')]
          .find(item => item.getAttribute('data-symbol-id') === line.id);
        if (!node || node.points.numberOfItems !== line.points.length)
          throw new Error(`Missing line points: ${line.id}`);
        const renderedPoints = line.points.map((_, index) => {
          const point = node.points.getItem(index);
          return [point.x * width / viewBox[2], point.y * height / viewBox[3]];
        });
        const error = line.points.reduce((sum, point, index) =>
          sum + Math.abs(point[0] - renderedPoints[index][0]) +
            Math.abs(point[1] - renderedPoints[index][1]), 0) / (line.points.length * 2);
        return { id: line.id, nativePoints: line.points,
          renderedPoints: renderedPoints.map(point => point.map(value => Number(value.toFixed(2)))),
          meanAbsoluteError: error };
      });
      const canvas = document.createElement('canvas');
      canvas.width = width; canvas.height = height;
      const context = canvas.getContext('2d', { willReadFrequently: true });
      context.fillStyle = '#fff'; context.fillRect(0, 0, width, height);
      context.drawImage(native, 0, 0);
      const a = context.getImageData(0, 0, width, height);
      context.fillStyle = '#fff'; context.fillRect(0, 0, width, height);
      context.drawImage(rendered, 0, 0, width, height);
      const renderImage = canvas.toDataURL('image/png').split(',')[1];
      const b = context.getImageData(0, 0, width, height);
      const overlay = context.createImageData(width, height);
      const difference = context.createImageData(width, height);
      let sum = 0, changed = 0;
      for (let index = 0; index < a.data.length; index += 4) {
        let total = 0;
        for (let channel = 0; channel < 3; channel++) {
          const delta = Math.abs(a.data[index + channel] - b.data[index + channel]);
          total += delta;
          overlay.data[index + channel] = Math.round((a.data[index + channel] + b.data[index + channel]) / 2);
        }
        const error = Math.round(total / 3);
        sum += error;
        if (error > 10) changed++;
        difference.data[index] = error;
        difference.data[index + 1] = 0;
        difference.data[index + 2] = 0;
        overlay.data[index + 3] = difference.data[index + 3] = 255;
      }
      context.putImageData(overlay, 0, 0);
      const overlayImage = canvas.toDataURL('image/png').split(',')[1];
      context.putImageData(difference, 0, 0);
      return { width, height, geometryErrors, lineErrors, meanAbsoluteError: sum / (width * height),
        changedFraction: changed / (width * height), renderImage, overlayImage,
        differenceImage: canvas.toDataURL('image/png').split(',')[1] };
    }, { originalData: original.toString('base64'), svgData: svg.toString('base64'), geometry });
    fs.mkdirSync(output, { recursive: true });
    fs.copyFileSync(source, path.join(output, 'original.png'));
    for (const [name, data] of [['rendered.png', comparison.renderImage],
      ['overlay.png', comparison.overlayImage], ['difference.png', comparison.differenceImage]])
      fs.writeFileSync(path.join(output, name), Buffer.from(data, 'base64'));
    const { renderImage, overlayImage, differenceImage, ...metrics } = comparison;
    fs.writeFileSync(path.join(output, 'metrics.json'), JSON.stringify(metrics, null, 2) + '\n');
    const hashFile = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
    let baseCommit = null;
    try { baseCommit = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(); }
    catch { /* Source archives may not include Git metadata. */ }
    const candidate = path.join(root, 'src/Bing.Pdm/bin/Debug/netstandard2.0/Bing.Pdm.dll');
    const evidence = {
      generatedAt: new Date().toISOString(), fixture, baseCommit,
      nativeSoftwareVersion: 'PowerDesigner 16.7.4.6866', nativeDpi: 96,
      browser: browser.version(), platform: process.platform, node: process.version,
      modelSha256: hashFile(path.join(root, 'tests/Bing.Pdm.Tests/Fixtures', fixture + '.pdm')),
      originalPngSha256: hashFile(source),
      geometrySha256: hashFile(path.join(baseline, 'native-geometry.json')),
      svgSha256: hashFile(svgFile), coreAssemblySha256: fs.existsSync(candidate) ? hashFile(candidate) : null,
      cliAssemblySha256: hashFile(path.join(root, 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll')),
      harnessSha256: hashFile(__filename),
      visualAcceptance: 'PENDING_HUMAN_REVIEW'
    };
    fs.writeFileSync(path.join(output, 'evidence.json'), JSON.stringify(evidence, null, 2) + '\n');
    console.log(JSON.stringify(metrics));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exitCode = 1; });
