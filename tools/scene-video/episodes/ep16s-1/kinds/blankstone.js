import { ease, clamp, easeOut, frac, fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW, cam3 }
  from '../../../engine/lib.js';
import { GRAVES, ground, stone } from './_world.js';

/* blankstone — **묘지 안을 카메라가 걸어 들어간다.** 비석 일곱이 앞뒤로 흩어져 서 있고,
   가장 가까운 비석에 파인 이름 자리가 끝까지 비어 있다.

   원문 근거:
   > "죽은 자리에는 회색 원이 하나씩 남습니다. 무덤입니다. 그런데 그 원에는 아무것도 안
   >  적혀 있습니다."

   ── 2026-08-12 v9 ─────────────────────────────────────────────
   🔴 **터지는 임팩트를 걷어냈다**(사용자: 눈이 아프다). 반전 플래시도 집중선도 없다.
      비석이 자리를 잡는 무게는 **착지 반동과 그림자**가 지고, 리듬은 카메라가 진다.
   물건은 전부 `_world.js` 가 소유한다 — 훅·새김과 **같은 묘지, 같은 광원**이다.

   🔴 색 규약: 강조색 = 이름이 들어갈 자리. 이름 자리는 비석을 **파낸 홈**이고 그 안의
      커서만 강조색이다. 한 획도 안 채워진다 — 그것이 이 편의 진단이다.
   🔴 무덤은 **일곱**(원문 수치). 깊이로 흩었을 뿐 수는 그대로다.

   phase 0 (S1a) = 카메라가 묘지로 들어간다 · phase 1 (S1b) = 앞 비석의 빈 자리로 파고든다.
   계속 도는 것: 카메라가 내내 움직여 정적 구간이 원리적으로 안 생긴다. 커서만 1.1초 주기. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const ph = spec.phase ?? 0;
    const c0 = ph === 0 ? cue(0, 0.15, 0.55) : 1;
    if (c0 <= 0.005) return;
    const cam = cam3(spec.cam3, t);

    ground(ctx, w, h);

    /* 먼 것부터 그린다(painter). 깊이 정렬 하나로 가림이 생긴다. */
    const order = GRAVES.map(([x, z], i) => ({ x, z, i }))
      .sort((a, b) => (b.z - a.z));

    let hero = null;
    for (const o of order) {
      /* 등장 — 먼 것부터. 착지에서 한 번 넘겼다 앉는다(반동). */
      const k = ph === 0 ? clamp((c0 - (GRAVES.length - 1 - o.i) * 0.055) / 0.22) : 1;
      if (k <= 0.01) continue;
      const grow = easeOut(k) + 0.14 * Math.sin(Math.PI * k) * (1 - k) * 2;
      const r = stone(ctx, cam, w, h, o.x, o.z, { grow: clamp(grow, 0, 1.16) });
      if (r && o.i === 0) hero = { ...r, k };
    }

    /* 앞 비석의 커서 — 쓸 자리는 있는데 아무도 안 쓴다 */
    if (hero && hero.k > 0.9 && frac(t / 1.1) < 0.55) {
      const s = hero.slot;
      ctx.save();
      setShadow(ctx, GLOW, 10);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(s.x - s.w * 0.04, s.y + s.h * 0.16, s.w * 0.08, s.h * 0.68);
      clearShadow(ctx);
      ctx.restore();
    }
  }
};
