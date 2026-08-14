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

/* ── 입체 ──────────────────────────────────────────
   납작한 실루엣을 세우는 붓 둘. 그림이 아니라 도구라 여기 둔다.

   왜: 검정 배경에 흰 실루엣만 놓으면 도형이 **떠 있다**. 깊이를 만드는 것은 원근이 아니라
   ① 가림(무엇이 무엇을 덮는가) ② 명도 위계(가까울수록 밝다) ③ 바닥 그림자다.
   이 셋 중 셋째가 가장 싸고 즉효라 함수로 뽑았다.

   🔴 무채색만 쓴다(채도 0). 팔레트 검사는 색상만 보므로 회색 계열은 3색 규약을 안 깬다 —
      `check.mjs` 가 `sat <= 0.14` 를 회색으로 넘긴다. 다만 `DESIGN_GUIDE.md:95` 가
      **#333 이하의 어두운 fill 을 금지**하므로(검정과 구분이 안 된다) 0x50 아래로 내리지 마라. */

/** 가까울수록 밝은 무채색 계단. 0 = 가장 가까움(흰색) … 3 = 가장 뒤. */
export const DEPTH = ['#FFFFFF', '#D2D2D2', '#9E9E9E', '#6E6E6E'];

/** 바닥 그림자 — 물체가 놓인 자리를 만든다. 없으면 도형이 공중에 뜬다. */
export function castShadow(ctx, x, y, rx, ry, alpha = 0.34) {
  ctx.save();
  ctx.globalAlpha = alpha;
  ctx.fillStyle = '#000000';
  ctx.beginPath();
  ctx.ellipse(x, y, rx, ry, 0, 0, Math.PI * 2);
  ctx.fill();
  ctx.restore();
}

/* ── 3D ────────────────────────────────────────────
   2026-08-12. 사용자: 「이펙트만 준다고 역동적으로 바뀐 게 아니야. 우리는 지금 2D 에서
   움직이고 있잖아. 3D 로 보이게끔 표현해봐」

   맞는 지적이다. 지금까지 한 것은 전부 **화면 평면 안의 운동**이었다 — 확대(camAt)도
   그림 전체를 똑같이 키우는 것이라 「가까워졌다」가 아니라 「커졌다」로 읽힌다.
   깊이는 배율이 아니라 **시차(parallax)** 에서 온다: 카메라가 앞으로 가면 가까운 것이
   먼 것보다 훨씬 빨리 커지고 옆으로 더 많이 흐른다. 그 차이가 3D 다.

   그래서 kind 가 **월드 좌표(x, y, z)** 로 물체를 놓고, 여기서 화면으로 던진다.
     x+ = 오른쪽 · y+ = 아래(화면과 같은 방향, 지면이 y=0) · z+ = 화면 안쪽(멀어짐)
   카메라는 z 음수 쪽에 서서 +z 를 본다. `cam.y` 가 음수면 지면 위(높은 곳)다.

   🔑 원근이 붙으면 공짜로 따라오는 것들:
      · 가까운 것이 크고 먼 것이 작다 — 크기 위계를 손으로 안 정해도 된다
      · 카메라가 움직이면 **물체마다 다른 속도로 흐른다**(시차) = 진짜 3D 신호
      · 깊이 정렬(painter)이 가림을 자동으로 만든다
      · 상자의 옆면 두께가 **위치에 따라 저절로 바뀐다** — 중앙 왼쪽 물체는 오른쪽 면이,
        오른쪽 물체는 왼쪽 면이 보인다. 손으로 그리면 절대 안 맞는 부분이다.
   🔴 전부 인자만 보는 순수 함수다 — seek 의 결정성은 그대로다. */

/* ── 조명 ──────────────────────────────────────────
   빛은 **언제나 화면 왼쪽 위**에서 온다. 물체마다 다른 방향으로 칠하면 각각은 입체인데
   모아 놓으면 한 공간이 아니다 — 3D 로 보이게 하는 것은 개별 음영이 아니라 **일관성**이다.

   lv = 거리 밝기(0~1, 멀수록 낮다) · k = 면 밝기(0~1, 빛을 마주볼수록 높다).
   🔴 `DESIGN_GUIDE.md:95` 가 #333 이하 어두운 fill 을 금지하므로 하한을 0x50 에 둔다.
      검정 배경과 구분이 안 되면 입체가 아니라 구멍으로 읽힌다. */
const GMIN = 0x50;
export function gray(v) {
  const c = Math.round(clamp(v) * (255 - GMIN) + GMIN);
  const s = c.toString(16).padStart(2, '0');
  return `#${s}${s}${s}`;
}

/** 평면·기둥용 — 왼쪽이 밝고 오른쪽이 어둡다. x0<x1 은 그 물체의 화면 좌우 끝. */
export function litFill(ctx, x0, x1, lv = 1) {
  const g = ctx.createLinearGradient(x0, 0, x1, 0);
  g.addColorStop(0, gray(lv * 1.00));
  g.addColorStop(0.55, gray(lv * 0.78));
  g.addColorStop(1, gray(lv * 0.52));
  return g;
}

/** 구용 — 빛 쪽으로 치우친 방사 그라데이션. 원기둥 음영과 섞이면 머리가 공으로 선다. */
export function ballFill(ctx, cx, cy, r, lv = 1) {
  const g = ctx.createRadialGradient(cx - r * 0.42, cy - r * 0.46, r * 0.08, cx, cy, r * 1.18);
  g.addColorStop(0, gray(lv * 1.00));
  g.addColorStop(0.5, gray(lv * 0.80));
  g.addColorStop(1, gray(lv * 0.44));
  return g;
}

/** 원기둥(팔·다리·몸통). 두 점을 잇는 캡슐을 축과 무관하게 같은 광원으로 칠한다. */
export function capsule(ctx, ax, ay, bx, by, r, lv = 1) {
  ctx.save();
  ctx.lineCap = 'round'; ctx.lineJoin = 'round';
  ctx.strokeStyle = litFill(ctx, Math.min(ax, bx) - r, Math.max(ax, bx) + r, lv);
  ctx.lineWidth = r * 2;
  ctx.beginPath(); ctx.moveTo(ax, ay); ctx.lineTo(bx, by); ctx.stroke();
  ctx.restore();
}

/** 구 */
export function ball(ctx, cx, cy, r, lv = 1) {
  ctx.save();
  ctx.fillStyle = ballFill(ctx, cx, cy, r, lv);
  ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2); ctx.fill();
  ctx.restore();
}

/** 월드 → 화면. 반환 s 는 그 깊이에서의 배율(길이·크기에 곱하면 된다). */
export function project(wx, wy, wz, cam, w, h) {
  const f = cam.f ?? 520;
  let dx = wx - cam.x, dz = wz - cam.z;
  const yaw = cam.yaw || 0;
  if (yaw) {
    const c = Math.cos(yaw), sn = Math.sin(yaw);
    const nx = dx * c - dz * sn, nz = dx * sn + dz * c;
    dx = nx; dz = nz;
  }
  const d = Math.max(12, dz);                       // 카메라 뒤로 넘어간 것은 눌러 둔다
  const s = f / d;
  return { x: w / 2 + dx * s, y: h / 2 + (wy - cam.y) * s, s, d };
}

/** 3D 카메라 키프레임. at = 샷 시작으로부터 초. 사이는 smoothstep. */
export function cam3(track, t) {
  const D = { x: 0, y: -70, z: -320, yaw: 0, f: 520 };
  if (!track || !track.length) return D;
  let i = 0;
  while (i < track.length - 1 && track[i + 1].at <= t) i++;
  const a = track[i], b = track[i + 1];
  if (!b) return { ...D, ...a };
  const k = ease((t - a.at) / (b.at - a.at || 1e-6));
  const g = key => lerp(a[key] ?? D[key], b[key] ?? D[key], k);
  return { x: g('x'), y: g('y'), z: g('z'), yaw: g('yaw'), f: g('f') };
}

/* ── 카메라 ────────────────────────────────────────
   샷 안에서 **줌인·줌아웃**을 한다. 그림 파일의 좌표를 손대지 않고 「어디를 얼마나 크게
   보여 줄지」만 시간축에 올린다.

   왜 필요한가: 모든 샷이 같은 상자에 같은 크기로 그려지면 그림이 아무리 달라도 화면은
   매번 같은 구도로 읽힌다(ep16s-5 실측 — 여섯 샷의 피사체 크기가 전부 같았다).
   컷을 늘리는 데는 천장이 있다(엔진은 샷이 자막 줄을 소유하므로 **샷 ≤ 줄**). 그 위의
   리듬은 컷이 아니라 무브가 진다.

   spec.cam = [{ at, zoom, focus }] — **at 은 샷 시작으로부터 초**(0~1 비율이 아니다.
   `since()`·`sfx.delayMs` 가 전부 초라 단위를 하나로 맞춘다). 사이는 smoothstep,
   앞뒤로는 첫/끝 값을 문다. cam 이 없으면 spec.zoom/spec.focus 를 고정값으로 쓴다.
   focus = **화면 한가운데로 데려올 지점**(0~1 비율). 고정점이 아니라 카메라가 겨누는
   곳이다 — 고정점으로 두면 배율을 바꿀 때마다 focus 를 다시 풀어야 한다.

   🔑 **이게 왜 결정성을 안 깨는가.** 엔진이 금지한 것은 `.shot` 엘리먼트의 CSS
      transform 이다(fitCanvas 가 transform 적용 뒤 크기를 읽어 백킹스토어가 매 프레임
      달라진다 — 실측 74/2924 프레임 불일치). 여기서 스케일하는 것은 **캔버스 ctx** 라
      `getBoundingClientRect` 에 영향이 없고, t 의 순수 함수라 어느 시각으로 뛰어도 같다.
   🔑 '탁 치고 들어오는' 진입(punch)은 따로 만들 필요가 없다 — 짧은 키프레임 두 개가
      곧 punch 다. 그래서 enter 종류를 안 늘리고 여기서 낸다.
   🔴 확대하면 선 굵기와 글로우도 같이 커진다(가까이 있는 것은 굵다). 상자 밖으로 나간
      것은 캔버스에서 잘리는데, 가장자리 검사가 '잘린 글자'와 구분을 못 하므로 무브가
      큰 샷은 scene.json 에 `checks.edge` 로 사유를 선언해라(check.mjs:534). */
export function camAt(ctx, w, h, spec = {}, t = 0) {
  const kf = spec.cam;
  let z, fx, fy;
  if (!kf || !kf.length) {
    z = spec.zoom || 1;
    fx = spec.focus?.[0] ?? 0.5;
    fy = spec.focus?.[1] ?? 0.5;
  } else {
    let i = 0;
    while (i < kf.length - 1 && kf[i + 1].at <= t) i++;
    const a = kf[i], b = kf[i + 1];
    const k = b ? ease((t - a.at) / (b.at - a.at || 1e-6)) : 0;
    const pick = (p, d) => b ? lerp(p(a) ?? d, p(b) ?? d, k) : (p(a) ?? d);
    z  = pick(o => o.zoom, 1);
    fx = pick(o => o.focus?.[0], 0.5);
    fy = pick(o => o.focus?.[1], 0.5);
  }
  if (z === 1 && fx === 0.5 && fy === 0.5) return;
  ctx.translate(w / 2 - fx * w * z, h / 2 - fy * h * z);
  ctx.scale(z, z);
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
