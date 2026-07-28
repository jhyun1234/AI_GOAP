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

/* 3색 팔레트 — youtube-editor/COLOR_PALETTE.md
   검정 배경 · 흰색 텍스트 · 네온 그린 강조. 이 셋 밖의 색은 쓰지 않는다.
   왜: 색이 늘면 시청자가 "이 색은 무슨 뜻이지"를 매번 풀어야 하고,
       검정 바다에 그린 하나만 빛나야 눈이 자동으로 간다.

   의미 매핑(옛 5색 → 3색):
     warm(주민) · hot(문제/실패) → #FFFFFF  대조 대상은 전부 중립 흰색
     cool(해결/정답)            → #00FF88  강조는 이 색 하나뿐
   대비는 색이 아니라 위계(크기·굵기)와 맥락(라벨·아이콘)으로 만든다. */
export const PALETTE = {
  bg: '#000000',
  ink: '#FFFFFF',
  accent: '#00FF88',
  sub: 'rgba(255,255,255,0.7)',    // 보조 라벨 — 0.7 이 허용 최소치
  track: 'rgba(255,255,255,0.12)', // 빈 트랙·가이드선 (0.1 미만은 검정에서 안 보임)

  // 옛 이름 호환 — 씬 JSON 이 아직 warm/hot/cool 로 쓰고 있어 매핑만 해 둔다
  warm: '#FFFFFF', hot: '#FFFFFF', cool: '#00FF88',
  dim: 'rgba(255,255,255,0.7)', grid: 'rgba(255,255,255,0.12)'
};
export const tone = t => PALETTE[t] || t || PALETTE.ink;

/** 검정 배경 대비 최소 두께 — 1~2px 선은 영상에서 사라진다 */
export const MIN_STROKE = 3;

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
