import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const base = resolve(here, '../src/Acmp.Web/src/i18n/locales');

function keys(obj, prefix = '') {
  return Object.entries(obj).flatMap(([k, v]) => {
    const path = prefix ? `${prefix}.${k}` : k;
    return v && typeof v === 'object' ? keys(v, path) : [path];
  });
}

const en = JSON.parse(readFileSync(`${base}/en.json`, 'utf8'));
const ar = JSON.parse(readFileSync(`${base}/ar.json`, 'utf8'));
const enKeys = new Set(keys(en));
const arKeys = new Set(keys(ar));

const missingInAr = [...enKeys].filter((k) => !arKeys.has(k));
const missingInEn = [...arKeys].filter((k) => !enKeys.has(k));

if (missingInAr.length || missingInEn.length) {
  if (missingInAr.length) console.error('Missing in ar.json:', missingInAr.join(', '));
  if (missingInEn.length) console.error('Missing in en.json:', missingInEn.join(', '));
  process.exit(1);
}
console.log(`i18n parity OK (${enKeys.size} keys).`);
