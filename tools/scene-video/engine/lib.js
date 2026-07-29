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

/* ── 폰트 ──────────────────────────────────────────
   캔버스는 CSS 변수를 못 읽으므로 이름을 여기 한 곳에 둔다. style.css 의 @font-face 와
   같은 이름이어야 한다.

   나눔 기준: **mono 는 실제 로그·코드 문자열(ASCII)에만.** 한국어 라벨은 전부 disp.
   전에는 '탐색한 후보' 같은 한글 라벨도 mono 로 찍었는데, Consolas 에 한글이 없어
   그 글자들만 시스템 폴백으로 따로 떨어져 그려지고 있었다 — 한 줄 안에서 폰트가
   갈리고, 그 폴백이 머신마다 달랐다. */
export const DISP = 'Pretendard, sans-serif';
export const MONO = 'SceneMono, monospace';
/** 한자는 Pretendard 에 없다(한글·라틴 전용). 이 스택은 term kind 전용. */
export const CJK = 'Pretendard, "Malgun Gothic", serif';
export const disp = (weight, px) => `${weight} ${px}px ${DISP}`;
export const mono = (weight, px) => `${weight} ${px}px ${MONO}`;

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

/* ── 등장 모션 ─────────────────────────────────────
   스프링 — 0.86 → 1.05 → 1.00. 가이드 MINIMAL_DESIGN_LANGUAGE 패턴 17.
   평평하게 페이드인만 시키면 카드가 "켜졌다"가 아니라 "그냥 있었다"로 읽힌다.
   가이드 한도(scale 0.8~1.15) 안. k 는 0~1 진행률.
   🔴 화면 폭을 꽉 채우는 요소에 그대로 곱하면 1.05 배에서 잘린다 —
      기준 크기를 0.95 로 줄여 놓고 곱해야 최대치가 원래 폭이 된다. */
export function spring(k) {
  k = clamp(k);
  if (k <= 0) return 0.86;
  const up = easeOut(Math.min(1, k / 0.55));
  const settle = ease(clamp((k - 0.55) / 0.45));
  return lerp(0.86, 1.05, up) - 0.05 * settle;
}

/* ── 깊이 ──────────────────────────────────────────
   같은 hue 안에서의 그라데이션 + palette 색 그림자.
   COLOR_PALETTE.md 219절이 "3색 자체는 안 깬다"는 조건으로 허용한 유일한 확장이다.
   납작한 단색 도형은 검정 화면의 카드 안에서 빈약하게 보인다는 게 가이드의 지적. */
export function depthGrad(ctx, x0, y0, x1, y1, kind = 'accent') {
  const g = ctx.createLinearGradient(x0, y0, x1, y1);
  if (kind === 'accent') { g.addColorStop(0, '#00FF88'); g.addColorStop(1, '#00B85F'); }
  else { g.addColorStop(0, '#FFFFFF'); g.addColorStop(1, '#CFCFCF'); }
  return g;
}
export const GLOW = 'rgba(0,255,136,0.35)';
export const GLOW_INK = 'rgba(255,255,255,0.18)';
/** 그림자는 전역 상태다 — 쓰고 나면 반드시 clearShadow 로 되돌린다 */
export function setShadow(ctx, color, blur, dy = 0) {
  ctx.shadowColor = color; ctx.shadowBlur = blur; ctx.shadowOffsetY = dy;
}
export function clearShadow(ctx) {
  ctx.shadowColor = 'transparent'; ctx.shadowBlur = 0; ctx.shadowOffsetY = 0;
}

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
