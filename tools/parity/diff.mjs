#!/usr/bin/env node
/*
 * Pixel-by-pixel diff of two PNGs (reference vs implementation).
 *   node diff.mjs <ref.png> <app.png> <out-diff.png> [threshold]
 * Exits 0 if mismatch <= allowance, 1 if over, 2 on size mismatch.
 * Prints the mismatched-pixel count and percentage. threshold = per-pixel
 * colour tolerance (0..1, default 0.1); the two crops MUST be the same size.
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { PNG } from 'pngjs';
import pixelmatch from 'pixelmatch';

const [, , refPath, appPath, outPath, thRaw] = process.argv;
if (!refPath || !appPath || !outPath) {
  console.error('usage: node diff.mjs <ref.png> <app.png> <out-diff.png> [threshold]');
  process.exit(2);
}
const threshold = thRaw ? Number(thRaw) : 0.1;

const ref = PNG.sync.read(readFileSync(refPath));
const app = PNG.sync.read(readFileSync(appPath));
if (ref.width !== app.width || ref.height !== app.height) {
  console.error(`SIZE MISMATCH ref ${ref.width}x${ref.height} vs app ${app.width}x${app.height} — crop to equal size before diffing`);
  process.exit(2);
}
const { width, height } = ref;
const diff = new PNG({ width, height });
const mismatched = pixelmatch(ref.data, app.data, diff.data, width, height, { threshold });
writeFileSync(outPath, PNG.sync.write(diff));
const pct = (100 * mismatched / (width * height)).toFixed(2);
console.log(`mismatched ${mismatched}px / ${width * height} (${pct}%) -> ${outPath}`);
process.exit(mismatched === 0 ? 0 : 1);
