import { readFileSync, writeFileSync, mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { crc32 } from 'node:zlib';
import { fileURLToPath } from 'node:url';
import type { Page } from '@playwright/test';

/*
 * DEC-192 r5 ("Real upload"): the E2E specs upload REAL files, not zero-filled ones with a magic number - a real
 * PDF (valid objects and cross-reference table), a real DOCX (a genuine OOXML package that Word opens), and a real
 * playable MP4 (e2e/fixtures/meeting-recording.mp4, made once with ffmpeg). When a spec needs a LARGER file, so
 * that a throttled upload shows a progress bar, the padding goes where the format allows it and the file stays
 * valid: an unused stream object inside the PDF, a trailing `free` box in the MP4.
 */

/** A genuine one-page PDF 1.4 with the given text; `padBytes` adds an unreferenced stream object (still valid). */
export function realPdf(text: string, padBytes = 0): Buffer {
  const content = `BT /F1 18 Tf 36 120 Td (${text.replace(/[()\\]/g, '')}) Tj ET`;
  const objects = [
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>',
    `<< /Length ${content.length} >>\nstream\n${content}\nendstream`,
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
  ];
  if (padBytes > 0) objects.push(`<< /Length ${padBytes} >>\nstream\n${' '.repeat(padBytes)}\nendstream`);
  let body = '%PDF-1.4\n';
  const offsets: number[] = [];
  objects.forEach((o, i) => {
    offsets.push(Buffer.byteLength(body));
    body += `${i + 1} 0 obj\n${o}\nendobj\n`;
  });
  const xref = Buffer.byteLength(body);
  body += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  for (const off of offsets) body += `${String(off).padStart(10, '0')} 00000 n \n`;
  body += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
  return Buffer.from(body, 'latin1');
}

/** A genuine .docx (OOXML) with one paragraph, as an uncompressed ("stored") ZIP. */
export function realDocx(text: string): Buffer {
  const esc = text.replace(/&/g, '&amp;').replace(/</g, '&lt;');
  const files: Array<[string, string]> = [
    ['[Content_Types].xml', '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>'],
    ['_rels/.rels', '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>'],
    ['word/document.xml', `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>${esc}</w:t></w:r></w:p></w:body></w:document>`],
  ];
  const local: Buffer[] = [];
  const central: Buffer[] = [];
  let offset = 0;
  for (const [name, content] of files) {
    const data = Buffer.from(content, 'utf8');
    const nameBuf = Buffer.from(name, 'utf8');
    const crc = crc32(data);
    const lh = Buffer.alloc(30);
    lh.writeUInt32LE(0x04034b50, 0); lh.writeUInt16LE(20, 4); lh.writeUInt16LE(0, 6); lh.writeUInt16LE(0, 8);
    lh.writeUInt32LE(0, 10); lh.writeUInt32LE(crc, 14); lh.writeUInt32LE(data.length, 18); lh.writeUInt32LE(data.length, 22);
    lh.writeUInt16LE(nameBuf.length, 26); lh.writeUInt16LE(0, 28);
    local.push(lh, nameBuf, data);
    const ch = Buffer.alloc(46);
    ch.writeUInt32LE(0x02014b50, 0); ch.writeUInt16LE(20, 4); ch.writeUInt16LE(20, 6); ch.writeUInt16LE(0, 8); ch.writeUInt16LE(0, 10);
    ch.writeUInt32LE(0, 12); ch.writeUInt32LE(crc, 16); ch.writeUInt32LE(data.length, 20); ch.writeUInt32LE(data.length, 24);
    ch.writeUInt16LE(nameBuf.length, 28); ch.writeUInt32LE(offset, 42);
    central.push(ch, nameBuf);
    offset += lh.length + nameBuf.length + data.length;
  }
  const cd = Buffer.concat(central);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0); end.writeUInt16LE(files.length, 8); end.writeUInt16LE(files.length, 10);
  end.writeUInt32LE(cd.length, 12); end.writeUInt32LE(offset, 16);
  return Buffer.concat([...local, cd, end]);
}

/** The committed real MP4, optionally followed by a `free` box of `padBytes` (players skip it; still valid). */
export function realMp4(padBytes = 0): Buffer {
  const video = readFileSync(fileURLToPath(new URL('./fixtures/meeting-recording.mp4', import.meta.url)));
  if (padBytes <= 0) return video;
  const free = Buffer.alloc(8 + padBytes);
  free.writeUInt32BE(8 + padBytes, 0);
  free.write('free', 4, 'latin1');
  return Buffer.concat([video, free]);
}

/** Write a buffer to a temp file under its REAL name (Arabic included) and return the path. */
export function toTempFile(name: string, bytes: Buffer): string {
  const dir = mkdtempSync(join(tmpdir(), 'acmp-real-'));
  const path = join(dir, name);
  writeFileSync(path, bytes);
  return path;
}

/**
 * A slow UPLINK for this page (Chromium's DevTools network emulation). The local E2E network finishes a large
 * upload in milliseconds, which is exactly why no test ever saw the missing progress bar (DEF-171). Returns a
 * function that lifts the throttle.
 */
export async function throttleUpload(page: Page, bytesPerSecond: number): Promise<() => Promise<void>> {
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('Network.enable');
  await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 20, downloadThroughput: -1, uploadThroughput: bytesPerSecond });
  return async () => {
    await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 0, downloadThroughput: -1, uploadThroughput: -1 });
  };
}
