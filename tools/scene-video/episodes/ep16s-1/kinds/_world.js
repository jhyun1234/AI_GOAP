/* ep16s-1 의 **3D 월드** — 이 회차의 네 그림이 같은 공간·같은 광원을 쓰게 하는 조각들.

   🔴 이건 `kind` 가 아니다(scene.json 의 `kind` 로 안 불린다). 회차 폴더 안의 형제 모듈이라
      「그림은 회차가 소유한다」 규약을 안 깬다 — 공유하는 범위가 **이 회차 안**이다.
      engine/lib.js 는 붓(투영·조명·이징)이고, 여기는 **이 회차의 물건**(비석·사람·상자)이다.

   ── 2026-08-12 v9 (사용자 지시) ────────────────────────────────
   「모든 영상은 이제 3D 로 만들어」 · 「터지는 임팩트 효과는 눈이 아프니깐 넣지마」 ·
   「주민을 표현한 모습도 3D 로 만들어 지금은 너무 조잡해」

   그래서 셋을 지킨다:
   ① 네 그림 전부 월드 좌표(x, y, z)로 놓고 원근으로 던진다. 화면 좌표를 직접 쓰지 않는다.
   ② 반전 플래시·집중선은 **전부 걷어냈다**(lib.js 에서도 삭제).
      타격감은 번쩍임이 아니라 **카메라와 무게**로 만든다.
   ③ 사람은 실루엣이 아니라 **머리(구) + 몸통·팔·다리(원기둥)** 로 조립한다.
      납작한 사다리꼴 하나로는 아무리 다듬어도 종잇조각이다.

   🔴 광원은 lib.js 가 정한 하나(화면 왼쪽 위)뿐이다. 물체마다 다른 방향으로 칠하면
      각각은 입체인데 모아 놓으면 한 공간이 아니다 — 3D 는 일관성이 만든다.
   🔴 얼굴·손가락은 안 그린다(`MINIMAL_DESIGN_LANGUAGE:838`). 머리는 이목구비 없는 구다. */

import {
  clamp, lerp, project, gray, litFill, ball, capsule, castShadow, PALETTE,
} from '../../../engine/lib.js';

/* 묘지 — [x, z]. z 0 이 가장 가깝다. 좌우로 번갈아 흩어 카메라가 전진할 때
   양쪽으로 갈라져 흐르게 했다(시차가 가장 잘 보이는 배치). */
export const GRAVES = [
  [0, 0], [-132, 96], [142, 108], [-258, 250], [268, 268], [-104, 430], [120, 452],
];
export const SW = 68, SH = 104, SD = 26;              // 비석 폭·높이·두께 (월드)
export const SLOT = { w: 0.62, h: 0.20, dy: 0.42 };   // 비석 대비 비율

/** 거리 → 밝기. 멀수록 어둡다(공기 원근). */
export const lvOf = d => clamp(0.52 + (520 - d) / 900, 0.32, 1);

/** 지면. 지평선(화면 세로 한가운데) 쪽을 0 으로 두고 발밑만 옅게 — 면이 누워야 바닥이다.
 *  🔴 알파를 지평선 쪽에 주면 '바닥'이 아니라 '회색 벽'으로 읽힌다(v8 실물에서 확인). */
export function ground(ctx, w, h) {
  const hz = h / 2;
  const g = ctx.createLinearGradient(0, hz, 0, h);
  g.addColorStop(0, 'rgba(255,255,255,0.00)');
  g.addColorStop(0.55, 'rgba(255,255,255,0.05)');
  g.addColorStop(1, 'rgba(255,255,255,0.10)');
  ctx.fillStyle = g;
  ctx.fillRect(0, hz, w, h - hz);
}

/** 둥근 윗변 슬래브 경로 (화면 좌표) */
function slab(ctx, x, yBase, sw, sh) {
  const r = sw / 2;
  ctx.beginPath();
  ctx.moveTo(x - r, yBase); ctx.lineTo(x - r, yBase - sh + r);
  ctx.arc(x, yBase - sh + r, r, Math.PI, 0);
  ctx.lineTo(x + r, yBase); ctx.closePath();
}

/** 비석 하나. grow 0~1 로 땅에서 솟는다. 이름 홈을 파고 그 자리를 돌려준다. */
export function stone(ctx, cam, w, h, x, z, o = {}) {
  const grow = o.grow ?? 1;
  const F = project(x, 0, z, cam, w, h);
  if (F.d <= 14 || grow <= 0.01) return null;
  const B = project(x, 0, z + SD, cam, w, h);          // 뒷면 — 각자의 깊이로
  const sw = SW * F.s, sh = SH * F.s * grow;
  const bw = SW * B.s, bh = SH * B.s * grow;
  const lv = lvOf(F.d);

  castShadow(ctx, F.x + (B.x - F.x) * 0.5, F.y, sw * 0.62, sw * 0.16, 0.42);

  ctx.fillStyle = gray(lv * 0.40);                     // 뒷면(두께) — 앞면이 덮고 남는 띠
  slab(ctx, B.x, B.y, bw, bh); ctx.fill();

  ctx.fillStyle = litFill(ctx, F.x - sw / 2, F.x + sw / 2, lv);
  slab(ctx, F.x, F.y, sw, sh); ctx.fill();

  const slotW = sw * SLOT.w, slotH = sh * SLOT.h;
  const slotY = F.y - sh + sh * SLOT.dy;
  ctx.save();                                          // 이름 자리 — 파낸 홈
  ctx.globalCompositeOperation = 'destination-out';
  ctx.fillStyle = '#000';
  ctx.fillRect(F.x - slotW / 2, slotY, slotW, slotH);
  ctx.restore();

  return { F, sw, sh, lv, slot: { x: F.x, y: slotY, w: slotW, h: slotH } };
}

/* ── 사람 ──────────────────────────────────────────
   🔴 2026-08-12 v10 — 사용자: 「그냥 동그라미에 몸을 다 축 내리고 있는 형태야」. 맞다.
      v9 는 **머리(구) + 몸통(캡슐 하나) + 곧은 팔다리**였다. 덩어리가 둘뿐이고 관절이
      없으니 어깨도 허리도 무릎도 없었고, 그래서 어떤 자세를 줘도 「축 늘어진 것」이 됐다.

   그래서 **8두신 정준(Andrew Loomis)의 랜드마크를 수치로 박았다.**
   측정 기준은 바닥(발바닥)=0, 정수리=1 로 정규화한 높이다.
     정수리 1.000 · 턱 0.875 · 어깨 0.833 · 가슴 0.750 · 허리 0.625
     사타구니 0.500(키의 정확히 절반) · 무릎 0.250 · 발목 0.055
   너비는 **머리 높이(=키의 1/8)의 배수**로 잡는다.
     어깨 2.33 · 가슴 1.75 · 허리 1.20 · 골반 1.45 · 머리폭 0.72
   출처: Loomis 8-head canon 정리(jeffkamangara.wordpress.com · skyryedesign.com).
   ⚠️ 정준은 「이상적 비례」이지 실측 인체가 아니다. 우리는 실루엣이 사람으로 읽히는 것이
      목적이라 이쪽이 맞다 — 평균 체형은 7~7.5두신이고 화면에서 더 뭉툭하게 읽힌다.

   🔑 관절을 넣은 것이 이 개정의 핵심이다. 팔은 어깨–팔꿈치–손목, 다리는 골반–무릎–발목
      **두 마디**로 그린다. 한 마디짜리 곧은 막대는 각도를 줘도 「기울어진 막대」일 뿐이고,
      두 마디여야 「굽혔다」가 된다.
   🔴 얼굴·손가락은 안 그린다(MINIMAL_DESIGN_LANGUAGE:838). 머리는 이목구비 없는 타원체다.
   🔴 광원은 lib.js 의 하나(화면 왼쪽 위)뿐이다. 몸통에는 오른쪽 코어 섀도를 깎아 원통으로 세운다. */
export const PH = 118;                   // 키 (월드) — 8두신

/** 바닥 0 · 정수리 1 로 정규화한 세로 랜드마크 */
const Y = {
  ankle: 0.055, knee: 0.250, crotch: 0.500, waist: 0.625,
  chest: 0.750, shoulder: 0.833, chin: 0.875, crown: 1.000,
};
/** 머리 높이(키의 1/8) 배수인 가로 치수 */
const WD = {
  shoulder: 2.33, chest: 1.75, waist: 1.20, hip: 1.45,
  head: 0.72, neck: 0.40, upperArm: 0.40, foreArm: 0.31,
  thigh: 0.52, calf: 0.37, foot: 0.62,
};

/** sink 0~1 로 땅에 가라앉는다. lean = 앞으로 기운 정도(rad). sit = 앉은 자세. */
export function person(ctx, cam, w, h, x, z, o = {}) {
  const sink = o.sink ?? 0;
  const F = project(x, 0, z, cam, w, h);
  if (F.d <= 14 || sink >= 1) return null;
  const s = F.s, lv = o.lv ?? lvOf(F.d);
  const bx = F.x, by = F.y + PH * sink * s;
  const u = PH * s;                        // 화면상 키
  const hu = u / 8;                        // 머리 한 개
  const lean = o.lean ?? 0;
  const sit = !!o.sit;

  /* 앉으면 사타구니가 의자 높이로 올라오고 상반신 비율은 그대로다.
     다리는 책상·비석에 가려지므로 무릎까지만 접어 둔다. */
  const drop = sit ? 0.22 : 0;
  const yv = k => by - u * (k - drop);                       // 정규화 높이 → 화면 y
  const lx = k => bx + Math.sin(lean) * u * (k - drop);      // 앞으로 기운 몫

  castShadow(ctx, bx, F.y, hu * 1.2, hu * 0.34, 0.40 * (1 - sink));

  ctx.save();
  /* 땅 밑은 안 보인다 — 가라앉음이 '작아짐'이 아니라 '들어감'으로 읽히는 이유 */
  ctx.beginPath(); ctx.rect(0, 0, w, F.y + 1); ctx.clip();

  const shY = yv(Y.shoulder), chY = yv(Y.chest), waY = yv(Y.waist), hipY = yv(Y.crotch);
  const kneeY = yv(Y.knee), ankY = yv(Y.ankle);
  const shX = lx(Y.shoulder), hipX = lx(Y.crotch);
  const shW = hu * WD.shoulder, chW = hu * WD.chest, waW = hu * WD.waist, hiW = hu * WD.hip;

  /* ── 다리 — 골반–무릎–발목 두 마디 ─────────────── */
  if (!sit) {
    const sw = o.walk ?? 0;                                   // 걸음 위상 (-1~1)
    for (const side of [-1, 1]) {
      const swing = sw * side;
      const kx = hipX + side * hu * 0.30 + swing * hu * 0.55;
      const ax = bx + side * hu * 0.30 + swing * hu * 0.95;
      const shade = side < 0 ? 0.80 : 0.92;                   // 먼 다리를 어둡게
      capsule(ctx, hipX + side * hu * 0.34, hipY, kx, kneeY, hu * WD.thigh / 2, lv * shade);
      capsule(ctx, kx, kneeY, ax, ankY, hu * WD.calf / 2, lv * shade);
      capsule(ctx, ax, ankY, ax + hu * 0.40, by, hu * 0.16, lv * shade);   // 발
    }
  }

  /* ── 몸통 — 어깨·가슴·허리·골반을 잇는 하나의 부피 ── */
  ctx.beginPath();
  ctx.moveTo(shX - shW / 2, shY);
  ctx.quadraticCurveTo(lx(Y.chest) - chW / 2 - hu * 0.06, chY, lx(Y.waist) - waW / 2, waY);
  ctx.quadraticCurveTo(hipX - hiW / 2 - hu * 0.04, (waY + hipY) / 2, hipX - hiW / 2, hipY);
  ctx.lineTo(hipX + hiW / 2, hipY);
  ctx.quadraticCurveTo(hipX + hiW / 2 + hu * 0.04, (waY + hipY) / 2, lx(Y.waist) + waW / 2, waY);
  ctx.quadraticCurveTo(lx(Y.chest) + chW / 2 + hu * 0.06, chY, shX + shW / 2, shY);
  ctx.closePath();
  ctx.fillStyle = litFill(ctx, shX - shW / 2, shX + shW / 2, lv);
  ctx.fill();
  ctx.save();                                                  // 코어 섀도 — 등 쪽을 깎는다
  ctx.clip();
  const cg = ctx.createLinearGradient(shX + shW * 0.04, 0, shX + shW * 0.62, 0);
  cg.addColorStop(0, 'rgba(0,0,0,0)');
  cg.addColorStop(1, 'rgba(0,0,0,0.42)');
  ctx.fillStyle = cg;
  ctx.fillRect(shX - shW, shY - hu, shW * 2, u);
  ctx.restore();

  /* ── 팔 — 어깨–팔꿈치–손목 두 마디 ─────────────── */
  if (o.arm !== false) {
    const reach = o.reach ?? 0;                                // 앞으로 뻗은 정도 0~1
    for (const side of [-1, 1]) {
      const sx = shX + side * (shW / 2 - hu * 0.12);
      const ex = sx + side * hu * 0.30 + reach * hu * 0.55 * side;
      const ey = shY + hu * 1.45;
      const wx = ex + side * hu * 0.10 + reach * hu * 1.05 * side;
      const wy = ey + hu * (1.25 - reach * 0.85);
      const shade = side < 0 ? 0.82 : 0.98;
      capsule(ctx, sx, shY + hu * 0.10, ex, ey, hu * WD.upperArm / 2, lv * shade);
      capsule(ctx, ex, ey, wx, wy, hu * WD.foreArm / 2, lv * shade);
    }
  }

  /* ── 목 + 머리 ─────────────────────────────────── */
  capsule(ctx, shX, shY, lx(Y.chin), yv(Y.chin), hu * WD.neck / 2, lv * 0.86);
  const hx = lx((Y.chin + Y.crown) / 2), hy = yv((Y.chin + Y.crown) / 2);
  ctx.save();
  ctx.translate(hx, hy);
  ctx.scale(WD.head, 1);                                       // 머리는 세로로 길다
  ball(ctx, 0, 0, hu * 0.5, lv);
  ctx.restore();

  ctx.restore();
  return { F, u, hu, bx, by, headY: hy, shY, lv };
}

/* ── 상자 ──────────────────────────────────────────
   책상·모니터용. 여덟 꼭짓점을 각자 던져 **윗면·옆면·앞면**을 그린다.
   보이는 옆면은 카메라 위치가 정한다 — 손으로 고르면 카메라가 움직일 때 어긋난다. */
export function box(ctx, cam, w, h, x, y, z, bw, bh, bd, o = {}) {
  const P = (dx, dy, dz) => project(x + dx, y + dy, z + dz, cam, w, h);
  const hw = bw / 2;
  const fL = P(-hw, 0, 0), fR = P(hw, 0, 0), fLT = P(-hw, -bh, 0), fRT = P(hw, -bh, 0);
  const bL = P(-hw, 0, bd), bR = P(hw, 0, bd), bLT = P(-hw, -bh, bd), bRT = P(hw, -bh, bd);
  const lv = o.lv ?? lvOf(fL.d);
  const quad = (a, b, c, d, fill) => {
    ctx.beginPath();
    ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.lineTo(c.x, c.y); ctx.lineTo(d.x, d.y);
    ctx.closePath(); ctx.fillStyle = fill; ctx.fill();
  };
  /* 윗면 — 카메라가 위에 있으면 보인다(우리 카메라는 늘 지면 위) */
  quad(fLT, fRT, bRT, bLT, gray(lv * 0.66));
  /* 옆면 — 뒤 모서리가 앞 모서리보다 바깥에 있는 쪽이 보이는 쪽이다 */
  if (bR.x > fR.x) quad(fR, bR, bRT, fRT, gray(lv * 0.46));
  else if (bL.x < fL.x) quad(fL, bL, bLT, fLT, gray(lv * 0.46));
  /* 앞면 */
  quad(fL, fR, fRT, fLT, litFill(ctx, fL.x, fR.x, lv));
  return { fL, fR, fLT, fRT, lv };
}

/** 상자 앞면을 파낸다(모니터 유리·이름 홈). 배경색으로 칠하면 경계에서 팔레트 위반 색이 난다. */
export function carve(ctx, path) {
  ctx.save();
  ctx.globalCompositeOperation = 'destination-out';
  ctx.fillStyle = '#000';
  path();
  ctx.fill();
  ctx.restore();
}
