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

/* 4색 팔레트 — youtube-editor/COLOR_PALETTE.md
   🔄 2026-08-12 개정 (테스트 · exp/palette4). 옛 3색은 아래 「왜 늘렸나」 참조.

   배경(깊은 밤) · 흰색 텍스트 · 두 개의 뜻 있는 강조색.
     accent(해결·성공·추가·지금 상태) → #00FF88  hue 152
     fail  (결함·손실·제거·과거의 잘못) → #FF3B5C  hue 350

   🔴 왜 늘렸나 — 옛 주석이 스스로 답을 적어 두고 있었다:
        "warm(주민) · hot(문제/실패) → #FFFFFF   대조 대상은 전부 중립 흰색"
      즉 **「문제/실패」라는 역할은 처음부터 있었고 색이 없어서 흰색으로 접혀 있었다.**
      그 결과 화면만 봐서는 무엇이 고장이고 무엇이 해결인지 구분되지 않았다.
      우리 회차 구조는 전부 「이랬는데(결함) → 이렇게 됐다(해결)」인데 그 축을
      색이 못 지고 있었다. 채널 소개문이 "실패를 숨기지 않습니다"라고 말하는데
      팔레트에 실패의 색이 없던 셈이다.

   🔴 여전히 금지 — fail 을 「그냥 강조」로 쓰지 않는다. 뜻 없는 강조는 accent 가
      맡는다. 색이 늘어난 명분이 뜻이므로, 뜻 없이 쓰면 규약이 무너진다.
   🔴 셋째 강조색을 늘리지 않는다. 필요해지면 그때 창을 하나 더 여는 것이
      지금 안 쓸 색을 미리 두는 것보다 싸다. */
export const PALETTE = {
  // 순수 검정을 뗐다 — OLED 에서 화면이 "꺼진 자리"로 읽혀 여백이 더 비어 보였다.
  // 캔버스가 아니라 CSS 무대 색이라 팔레트 게이트는 이 값을 보지 않는다.
  bg: '#0E1117',
  ink: '#FFFFFF',
  accent: '#00FF88',
  fail: '#FF3B5C',
  sub: 'rgba(255,255,255,0.7)',    // 보조 라벨 — 0.7 이 허용 최소치
  track: 'rgba(255,255,255,0.12)', // 빈 트랙·가이드선 (0.1 미만은 검정에서 안 보임)

  // 옛 이름 호환 — 씬 JSON 이 아직 warm/hot/cool 로 쓰고 있어 매핑만 해 둔다.
  // hot 은 이제 접혀 있지 않다. 이것이 이번 개정의 전부다.
  warm: '#FFFFFF', hot: '#FF3B5C', cool: '#00FF88',
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
export const FAIL_GLOW = 'rgba(255,59,92,0.35)';   // fail 색의 짝 — 같은 세기로 맞춘다
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
