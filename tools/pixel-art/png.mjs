// PNG 입출력 (M22-3차 W5a) — 의존성 0 (node 표준 zlib만).
//
// 지원 범위를 **의도적으로 좁혔다**: colorType 6(RGBA) · bitDepth 8 · interlace 0.
// 이 리포의 소스 시트(Kenmi 팩)가 전부 그 한 조합이기 때문이다 (명세 전제 ③).
// 범위 밖 파일은 조용히 깨지지 않고 명시적으로 던진다 — 픽셀이 틀린 채 굽히는 것보다
// 멈추는 쪽이 싸다.
//
// 왜 라이브러리를 안 쓰나: 도구 트랙 규약 = 의존성 0 (scene-video 선례). 설치 환경이
// 파이프라인의 전제가 되면 "내 PC에서는 되는데"가 생긴다.

import { readFileSync, writeFileSync } from 'node:fs';
import { deflateSync, inflateSync } from 'node:zlib';

const SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

/** 이미지 한 장 = 폭·높이 + RGBA 바이트 (길이 = w*h*4). 픽셀 조작은 전부 이 형태로 한다. */
export function createImage(width, height) {
  return { width, height, data: Buffer.alloc(width * height * 4) }; // 전부 투명(0,0,0,0)
}

// ── CRC32 (PNG 청크 검사합) ────────────────────────────────────────────────
const CRC_TABLE = (() => {
  const t = new Int32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c;
  }
  return t;
})();

function crc32(buf) {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = CRC_TABLE[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

// ── 디코드 ────────────────────────────────────────────────────────────────

/** PNG 파일 → {width, height, data(RGBA)}. 범위 밖 포맷이면 throw. */
export function readPng(path) {
  const buf = readFileSync(path);
  if (!buf.subarray(0, 8).equals(SIGNATURE)) throw new Error(`${path}: PNG 서명 아님`);

  let width = 0, height = 0;
  const idat = [];
  let off = 8;
  while (off < buf.length) {
    const len = buf.readUInt32BE(off);
    const type = buf.toString('ascii', off + 4, off + 8);
    const body = buf.subarray(off + 8, off + 8 + len);
    if (type === 'IHDR') {
      width = body.readUInt32BE(0);
      height = body.readUInt32BE(4);
      const [bitDepth, colorType, , , interlace] = [body[8], body[9], body[10], body[11], body[12]];
      if (bitDepth !== 8 || colorType !== 6 || interlace !== 0)
        throw new Error(`${path}: 지원 밖 (bitDepth=${bitDepth} colorType=${colorType} ` +
                        `interlace=${interlace}) — RGBA8 비인터레이스만 다룬다`);
    } else if (type === 'IDAT') {
      idat.push(body);
    } else if (type === 'IEND') {
      break;
    }
    off += 12 + len; // len(4) + type(4) + body + crc(4)
  }
  if (!width || !height) throw new Error(`${path}: IHDR 없음`);

  const raw = inflateSync(Buffer.concat(idat));
  const stride = width * 4;
  const data = Buffer.alloc(stride * height);
  // 스캔라인 디필터 — 각 줄 앞 1바이트가 필터 종류 (PNG 사양 9.2)
  for (let y = 0; y < height; y++) {
    const filter = raw[y * (stride + 1)];
    const src = raw.subarray(y * (stride + 1) + 1, y * (stride + 1) + 1 + stride);
    const cur = data.subarray(y * stride, (y + 1) * stride);
    const prev = y > 0 ? data.subarray((y - 1) * stride, y * stride) : null;
    for (let x = 0; x < stride; x++) {
      const a = x >= 4 ? cur[x - 4] : 0;          // 왼쪽 픽셀
      const b = prev ? prev[x] : 0;                // 위 픽셀
      const c = prev && x >= 4 ? prev[x - 4] : 0;  // 왼쪽 위
      let v = src[x];
      switch (filter) {
        case 0: break;                              // None
        case 1: v = v + a; break;                   // Sub
        case 2: v = v + b; break;                   // Up
        case 3: v = v + ((a + b) >> 1); break;       // Average
        case 4: {                                   // Paeth
          const p = a + b - c;
          const pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
          v = v + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
          break;
        }
        default: throw new Error(`${path}: 알 수 없는 필터 ${filter} (줄 ${y})`);
      }
      cur[x] = v & 0xff;
    }
  }
  return { width, height, data };
}

// ── 인코드 ────────────────────────────────────────────────────────────────

function chunk(type, body) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(body.length, 0);
  const typed = Buffer.concat([Buffer.from(type, 'ascii'), body]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(typed), 0);
  return Buffer.concat([len, typed, crc]);
}

/**
 * {width,height,data} → PNG 파일. 필터는 전부 0(None)이다 — 16×16급 그림이라 압축률보다
 * **결정성**이 중요하다 (같은 레시피 → 같은 바이트, W5a DoD).
 */
export function writePng(path, img) {
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(img.width, 0);
  ihdr.writeUInt32BE(img.height, 4);
  ihdr[8] = 8;  // bitDepth
  ihdr[9] = 6;  // colorType RGBA
  ihdr[10] = 0; // compression
  ihdr[11] = 0; // filter
  ihdr[12] = 0; // interlace

  const stride = img.width * 4;
  const raw = Buffer.alloc((stride + 1) * img.height);
  for (let y = 0; y < img.height; y++) {
    raw[y * (stride + 1)] = 0; // filter None
    img.data.copy(raw, y * (stride + 1) + 1, y * stride, (y + 1) * stride);
  }
  const out = Buffer.concat([
    SIGNATURE,
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
  writeFileSync(path, out);
  return out.length;
}

// ── 픽셀 유틸 ─────────────────────────────────────────────────────────────

export function getPixel(img, x, y) {
  if (x < 0 || y < 0 || x >= img.width || y >= img.height) return [0, 0, 0, 0];
  const i = (y * img.width + x) * 4;
  return [img.data[i], img.data[i + 1], img.data[i + 2], img.data[i + 3]];
}

export function setPixel(img, x, y, [r, g, b, a]) {
  if (x < 0 || y < 0 || x >= img.width || y >= img.height) return;
  const i = (y * img.width + x) * 4;
  img.data[i] = r; img.data[i + 1] = g; img.data[i + 2] = b; img.data[i + 3] = a;
}

/** 알파 0이 아닌 픽셀만 덮어쓴다 (레이어 합성의 기본 — 투명은 아래를 살린다). */
export function blitImage(dst, src, dx, dy, srcRect = null) {
  const [sx, sy, sw, sh] = srcRect ?? [0, 0, src.width, src.height];
  for (let y = 0; y < sh; y++)
    for (let x = 0; x < sw; x++) {
      const p = getPixel(src, sx + x, sy + y);
      if (p[3] === 0) continue;
      setPixel(dst, dx + x, dy + y, p);
    }
}

export const toHex = ([r, g, b, a]) =>
  a === 0 ? 'transparent'
          : '#' + [r, g, b].map(v => v.toString(16).padStart(2, '0')).join('');

export function fromHex(hex) {
  const h = hex.replace('#', '');
  return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16), 255];
}
