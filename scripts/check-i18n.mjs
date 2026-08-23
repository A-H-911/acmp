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

/*
 * ⚠ VALUE-LEVEL MOJIBAKE GUARD (DEF-070, and DEF-064 before it). Key parity is necessary and NOT
 * SUFFICIENT: it passes just as cheerfully over a value whose UTF-8 was read as Latin-1, because the
 * key is still there and the corruption is still valid UTF-8 text. Nothing else in the build can see
 * it either — the ENGLISH half of an affected string is untouched, so no screen looks wrong to a
 * reviewer who does not read Arabic, and no linter or type-checker has anything to object to.
 *
 * It has now happened twice, from both directions: written that way once (DEF-064, 7 values shipped
 * to main), and re-introduced by MERGING a branch cut before the repair (DEF-070) — where taking the
 * branch side of an ar.json conflict is the ordinary, careful-looking thing to do.
 *
 * THE SIGNATURE: Arabic encoded UTF-8 then decoded as Latin-1 always lands in the Ø/Ù/Ã block, and
 * correct Arabic never contains those characters at all. So a single one in an Arabic value is
 * enough — no threshold to tune, and no way for a partially-corrupted string to slip under it.
 */
const MOJIBAKE = /[ØÙÃ]/;

function values(obj, prefix = '') {
  return Object.entries(obj).flatMap(([k, v]) => {
    const path = prefix ? `${prefix}.${k}` : k;
    return v && typeof v === 'object' ? values(v, path) : [[path, String(v)]];
  });
}

const corrupted = values(ar).filter(([, v]) => MOJIBAKE.test(v));
if (corrupted.length) {
  console.error(
    `ar.json holds ${corrupted.length} value(s) whose UTF-8 was decoded as Latin-1 — write literal UTF-8, ` +
      'never backslash-u escapes materialised through unicode_escape, and never copy from the stale side of a merge:',
  );
  for (const [k, v] of corrupted.slice(0, 10)) console.error(`  ${k} = ${v}`);
  process.exit(1);
}

const missingInAr = [...enKeys].filter((k) => !arKeys.has(k));
const missingInEn = [...arKeys].filter((k) => !enKeys.has(k));

if (missingInAr.length || missingInEn.length) {
  if (missingInAr.length) console.error('Missing in ar.json:', missingInAr.join(', '));
  if (missingInEn.length) console.error('Missing in en.json:', missingInEn.join(', '));
  process.exit(1);
}
console.log(`i18n parity OK (${enKeys.size} keys).`);
