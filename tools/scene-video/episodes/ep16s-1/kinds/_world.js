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
   머리(구) + 몸통·팔·다리(원기둥). 비율은 6.5등신 언저리로 잡아 실루엣이 사람으로 읽히게.
   한 z 평면 위의 부위라 밑동의 배율 하나로 전부 잰다 — 인물 안에서까지 원근을 나누면
   가까운 팔만 커져 만화적으로 일그러진다(실측 후 되돌림). */
export const PH = 118;                                  // 키 (월드)

/** sink 0~1 로 땅에 가라앉는다. lean 은 앞으로 기운 정도(라디안). */
export function person(ctx, cam, w, h, x, z, o = {}) {
  const sink = o.sink ?? 0;
  const F = project(x, 0, z, cam, w, h);
  if (F.d <= 14 || sink >= 1) return null;
  const s = F.s, lv = o.lv ?? lvOf(F.d);
  const bx = F.x, by = F.y + PH * sink * s;             // 가라앉으면 밑동이 내려간다
  const u = PH * s;                                     // 화면상 키
  const lean = o.lean ?? 0;
  const lx = k => bx + Math.sin(lean) * u * k;          // 높이 k 에서의 앞쪽 치우침

  castShadow(ctx, bx, F.y, u * 0.26, u * 0.07, 0.40 * (1 - sink));

  ctx.save();
  /* 땅 밑은 안 보인다 — 가라앉음이 '작아짐'이 아니라 '들어감'으로 읽히는 이유 */
  ctx.beginPath(); ctx.rect(0, 0, w, F.y + 1); ctx.clip();

  /* 앉은 자세 — 엉덩이가 의자 높이에 오고 상반신만 보인다. 다리는 안 그린다:
     책상이 어차피 덮고, 안 보이는 것을 그리면 덮인 자리에서 실루엣만 지저분해진다. */
  const sit = !!o.sit;
  const hipK = sit ? 0.30 : 0.46;
  const hipY = by - u * hipK, shY = by - u * (hipK + 0.34), headY = by - u * (hipK + 0.47);
  const legDx = u * 0.058, armDx = u * 0.155;

  if (!sit) {
    capsule(ctx, lx(hipK) - legDx, hipY, bx - legDx, by, u * 0.055, lv * 0.86);   // 다리
    capsule(ctx, lx(hipK) + legDx, hipY, bx + legDx, by, u * 0.055, lv * 0.86);
  }
  capsule(ctx, lx(hipK), hipY, lx(hipK + 0.34), shY, u * 0.112, lv);              // 몸통
  if (o.arm !== false) {                                                          // 팔
    const handY = hipY - u * (sit ? 0.02 : -0.06);
    capsule(ctx, lx(hipK + 0.34) - armDx * 0.6, shY, lx(hipK + 0.16) - armDx, handY, u * 0.045, lv * 0.92);
    capsule(ctx, lx(hipK + 0.34) + armDx * 0.6, shY, lx(hipK + 0.16) + armDx, handY, u * 0.045, lv * 0.92);
  }
  ball(ctx, lx(hipK + 0.47), headY, u * 0.118, lv);                               // 머리

  ctx.restore();
  return { F, u, bx, by, headY, shY, lv };
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
