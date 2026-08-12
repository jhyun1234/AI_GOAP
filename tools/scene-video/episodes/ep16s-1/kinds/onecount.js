import { disp, mono, ease, easeOut, clamp, lerp, fitCanvas, mkCanvas, tone, gray, cam3, project }
  from '../../../engine/lib.js';
import { ground, person, box, carve, lvOf } from './_world.js';

/* onecount — **책상 앞에 앉은 사람이 「누가 죽었지?」하고 묻는데, 화면이 내놓는 답은
   숫자 하나뿐이다.** 숫자가 1 올라가고, 물음표는 끝까지 안 풀린다.

   원문 근거:
   > "누가 죽었냐고? 사망 카운터 +1. 내가 가진 건 그게 전부인데."
   > "Day 45에 주민 일곱 명이 죽었습니다."

   ── 2026-08-12 v9 (사용자: 모든 영상 3D · 주민도 3D · 터지는 효과 금지) ────────
   🔴 책상·모니터를 **진짜 상자**로 세웠다(`_world.js` box — 여덟 꼭짓점을 각자 던져
      윗면·옆면·앞면을 그린다). 보이는 옆면은 **카메라가 정한다** — 손으로 고르면
      카메라가 움직일 때 어긋난다. 전 버전의 사다리꼴은 한 시점에서만 맞는 그림이었다.
   🔴 사람은 3D 조립이고, **의자에 앉은 높이**로 z 앞쪽에 둔다. 책상이 하반신을 덮는다.
      손가락은 안 그린다 — 손목이 책상 상판에 가려지도록 깊이를 잡았다.
   🔴 반전·집중선 없음. 숫자가 바뀌는 무게는 **한 번 커졌다 앉는 것**이 진다.

   🔴 숫자를 실제로 보여 준다(「+1」 기호만 띄우던 v4 의 실패를 고친 것 유지).
      ⚠️ 6 → 7 의 근거: 원문 「주민 일곱 명이 죽었습니다」 + 「사망 카운터 +1」.
   🔴 색 규약: 이 샷에는 **강조색이 한 점도 없다.** 남는 것이 이름이 아니라 수뿐이라서다.
      물음표도 무채색이고 크기로 세운다.

   phase 0 (S2a) = 사람이 화면을 보고 묻는다 · phase 1 (S2b) = 숫자가 1 올라간다.
   ⏱ `sfx.delayMs 285`(sweep · 정점 0.555초)는 phase 0 에 있다. 「?」의 도약 정점을
      0.555초에 맞췄다 — 묻는 동작과 소리의 정점이 같은 프레임이다. */

const DESK = { x: 0, y: 0, z: -300, w: 560, h: 58, d: 240 };   // 카메라와 사람 사이에만
const MON = { x: -20, y: -58, z: -90, w: 112, h: 78, d: 18 };   // 책상 위, 사람 왼쪽
const MAN = { x: 64, z: -40 };                                  // 책상 먼 모서리 바로 뒤

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const ph = spec.phase ?? 0;
    const st = ph === 0 ? ease(cue(0, 0.15, 0.20)) : 1;
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }
    const s0 = since(0);
    const cam = cam3(spec.cam3, t);

    ground(ctx, w, h);
    ctx.textBaseline = 'alphabetic';

    /* ── 모니터 (뒤) ─────────────────────────────── */
    const M = box(ctx, cam, w, h, MON.x, MON.y, MON.z, MON.w, MON.h, MON.d);
    const gl = 0.09;                                    // 베젤 두께 비율
    const P = (dx, dy) => project(MON.x + dx, MON.y + dy, MON.z, cam, w, h);
    const g0 = P(-MON.w * (0.5 - gl), -MON.h * (1 - gl));
    const g1 = P(MON.w * (0.5 - gl), -MON.h * (1 - gl));
    const g2 = P(MON.w * (0.5 - gl), -MON.h * gl);
    const g3 = P(-MON.w * (0.5 - gl), -MON.h * gl);
    carve(ctx, () => {
      ctx.beginPath();
      ctx.moveTo(g0.x, g0.y); ctx.lineTo(g1.x, g1.y);
      ctx.lineTo(g2.x, g2.y); ctx.lineTo(g3.x, g3.y); ctx.closePath();
    });

    /* 화면 안 — 무덤 일곱(작게)과 죽은 사람 수 */
    ctx.save();
    ctx.beginPath();
    ctx.moveTo(g0.x, g0.y); ctx.lineTo(g1.x, g1.y);
    ctx.lineTo(g2.x, g2.y); ctx.lineTo(g3.x, g3.y); ctx.closePath();
    ctx.clip();
    const gw = g1.x - g0.x, gh = g3.y - g0.y;
    ctx.fillStyle = gray(0.92);
    for (let i = 0; i < 7; i++) {
      const cx = g0.x + gw * (0.09 + i * 0.135), by = g0.y + gh * 0.52;
      const sw = gw * 0.062, sh = gh * 0.30, r = sw / 2;
      ctx.beginPath();
      ctx.moveTo(cx - r, by); ctx.lineTo(cx - r, by - sh + r);
      ctx.arc(cx, by - sh + r, r, Math.PI, 0);
      ctx.lineTo(cx + r, by); ctx.closePath(); ctx.fill();
    }
    const tick = ph === 1 ? clamp((s0 - 1.28) / 0.06) : 0;
    const pop = 1 + 0.26 * tick * (1 - tick) * 4;
    ctx.save();
    ctx.translate(g0.x + gw * 0.5, g0.y + gh * 0.88);
    ctx.scale(pop, pop);
    ctx.font = mono(700, gh * 0.26);
    ctx.textAlign = 'center'; ctx.fillStyle = gray(1);
    ctx.fillText(tick > 0.5 ? '7' : '6', 0, 0);
    ctx.restore();
    if (ph === 1) {                                     // 「+1」이 무덤에서 떠올라 꽂힌다
      const fly = clamp((s0 - 0.55) / 0.75);
      if (fly > 0.005 && fly < 1) {
        const k = fly < 0.16 ? -0.30 * Math.sin(fly / 0.16 * Math.PI) : easeOut((fly - 0.16) / 0.84);
        const fx = lerp(g0.x + gw * 0.16, g0.x + gw * 0.40, k);
        const fy = lerp(g0.y + gh * 0.20, g0.y + gh * 0.80, clamp(k)) - Math.sin(clamp(k) * Math.PI) * gh * 0.10;
        ctx.globalAlpha = 1 - clamp((fly - 0.86) / 0.14);
        ctx.font = mono(700, gh * 0.15);
        ctx.textAlign = 'center'; ctx.fillStyle = gray(1);
        ctx.fillText('+1', fx, fy);
      }
    }
    ctx.restore();

    /* ── 사람 (모니터와 책상 사이 높이, 책상 앞) ──── */
    person(ctx, cam, w, h, MAN.x, MAN.z, { sit: true, lean: -0.16 });

    /* ── 책상 (앞) — 하반신과 손목을 덮는다 ───────── */
    box(ctx, cam, w, h, DESK.x, DESK.y, DESK.z, DESK.w, DESK.h, DESK.d);

    /* ── 물음표 — 이 샷의 주인공. 끝까지 안 풀린다 ── */
    const qk = ph === 0 ? clamp((s0 - 0.30) / 0.42) : 1;
    if (qk > 0.01) {
      const Q = project(MAN.x + 16, -158, MAN.z, cam, w, h);
      const k = ph === 0 ? easeOut(qk) + 0.30 * Math.sin(Math.PI * qk) * (1 - qk) * 2 : 1;
      const shake = ph === 1
        ? Math.sin((s0 - 1.28) * 24) * clamp((s0 - 1.28) / 0.06) * clamp((2.1 - s0) / 0.7) : 0;
      ctx.save();
      ctx.globalAlpha = st * clamp(k);
      ctx.translate(Q.x, Q.y);
      ctx.rotate(Math.sin(t * 2.4) * 0.05 + shake * 0.09);
      ctx.scale(k, k);
      ctx.font = disp(900, 76 * Q.s);
      ctx.textAlign = 'center'; ctx.fillStyle = gray(1);
      ctx.fillText('?', 0, 0);
      ctx.restore();

      if (spec.askLabel) {
        ctx.save();
        ctx.globalAlpha = st * clamp((k - 0.5) / 0.5) * 0.85;
        ctx.font = disp(800, 19 * Q.s);
        ctx.textAlign = 'center'; ctx.fillStyle = tone('sub');
        ctx.fillText(spec.askLabel, Q.x, Q.y - 74 * Q.s);
        ctx.restore();
      }
    }

    ctx.textAlign = 'left';
  }
};
