/* kind 모듈이 공유하는 순수 헬퍼.
   여기 있는 함수는 전부 인자만 보고 값을 낸다 — 시간·난수·전역 상태를 읽지 않는다.
   (seek(t) 가 어느 시각으로 뛰어도 같은 그림이 나와야 하기 때문) */

export const clamp = (v, a = 0, b = 1) => v < a ? a : v > b ? b : v;
export const lerp = (a, b, k) => a + (b - a) * k;
export const ease = k => (k = clamp(k), k * k * (3 - 2 * k));            // smoothstep
export const easeOut = k => 1 - Math.pow(1 - clamp(k), 3);
export const frac = v => v - Math.floor(v);

/** 구간 [a,b] 를 0~1 로 정규화. 밖이면 0 또는 1. */
export const span = (v, a, b) => clamp((v - a) / (b - a || 1e-6));

/** 결정적 의사난수 — Math.random() 금지 대체물. 같은 seed = 같은 값. */
export function rnd(seed) {
  let h = seed * 2654435761 % 4294967296;
  h ^= h >>> 15; h = h * 2246822507 % 4294967296;
  h ^= h >>> 13; h = h * 3266489909 % 4294967296;
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

/** DPR 대응 캔버스. 크기가 바뀌었을 때만 재설정한다. */
export function fitCanvas(cv) {
  const r = cv.getBoundingClientRect();
  const dpr = window.devicePixelRatio || 1;
  const w = Math.max(1, Math.round(r.width * dpr));
  const h = Math.max(1, Math.round(r.height * dpr));
  if (cv.width !== w || cv.height !== h) { cv.width = w; cv.height = h; }
  const ctx = cv.getContext('2d');
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.clearRect(0, 0, r.width, r.height);
  return { ctx, w: r.width, h: r.height };
}

export function mkCanvas(root) {
  const cv = document.createElement('canvas');
  root.appendChild(cv);
  return cv;
}

export const PALETTE = {
  hot: '#D6395C', cool: '#4FB6A8', warm: '#E0A458',
  ink: '#E8EAF0', dim: '#6A7183', grid: '#1B1E28', bg: '#101218'
};
export const tone = t => PALETTE[t] || t || PALETTE.ink;

/** 둥근 사각형 경로 */
export function roundRect(ctx, x, y, w, h, r) {
  r = Math.min(r, w / 2, h / 2);
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}
