import { disp, ease, easeOut, clamp, lerp, fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW, cam3 }
  from '../../../engine/lib.js';
import { GRAVES, ground, stone, person } from './_world.js';

/* carvename — 죽고 나면 물어볼 데가 없다. 그래서 **아직 서 있는 동안** 이름을 건네받아
   비석에 새긴다. 그리고 **그 다음에** 가라앉는다.

   원문 근거:
   > "그래서 죽는 순간, 즉 주민이 아직 살아 있는 함수 안에서 이름을 인자로 넘기게 했습니다."
   > "무덤 표기를 \"{shortName} · Day {day}\" 형식으로 바꿨습니다."

   ── 2026-08-12 v9 ─────────────────────────────────────────────
   🔴 같은 묘지 월드, 같은 광원. 사람은 3D 조립(머리 구 + 몸통·팔·다리 원기둥)이다.
   🔴 **터지는 임팩트를 걷어냈다**(사용자: 눈이 아프다). 꽂히는 무게는 **비석이 한 번
      흔들리는 것**과 이름이 왼쪽부터 파이는 것으로 진다. 번쩍임 없이도 타점은 선다.
   🔴 사건 순서는 원문의 인과 그대로다 — 살아 있을 때 넘긴다 → 그 다음에 가라앉는다.
        0.55~1.10  아직 서 있는 사람에게서 이름 조각이 떠올라 날아간다
        1.10       비석의 이름 자리에 꽂힌다 (latch 효과음 자리 · delayMs 1100)
        1.10~1.50  파인 자리에 이름이 채워진다
        1.30~1.80  그 다음에 사람이 가라앉는다

   🔴 ⚠️ 새기는 이름 값(`spec.carvedName`)은 **원문 기사에 없다.** 원문은 형식만 주고
      실제 이름은 주지 않으며, 게임 코드(SimulationLoop.RecordDeath)도 런타임 값이라
      리포에 없다. 「원문에 없는 값은 화면에 안 적는다」는 규칙의 **사용자 지정 예외**다. */

const HERO_Z = 0, MAN_X = -150, MAN_Z = 26;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const ph = spec.phase ?? 0;
    const g0 = ph === 0 ? ease(cue(0, 0.15, 0.30)) : 1;
    if (g0 <= 0.01) { ctx.textAlign = 'left'; return; }
    const cam = cam3(spec.cam3, t);

    const s1 = ph === 0 ? -1 : since(0);
    const fly = clamp((s1 - 0.55) / 0.55);
    const hit = clamp((s1 - 1.06) / 0.10) * clamp((1.45 - s1) / 0.30);
    const fill = ease(clamp((s1 - 1.10) / 0.40));
    const sink = ease(clamp((s1 - 1.30) / 0.50));

    ground(ctx, w, h);

    /* 뒤 비석들 — 같은 묘지의 나머지. 먼 것부터 */
    for (const [x, z] of [...GRAVES.slice(1)].sort((a, b) => b[1] - a[1])) {
      stone(ctx, cam, w, h, x, z);
    }

    /* 앞 비석 — 꽂히는 순간 한 번 흔들린다. 맞은 것은 흔들려야 맞은 것으로 읽힌다. */
    ctx.save();
    if (hit > 0.01) ctx.translate(Math.sin(s1 * 78) * 5 * hit, 0);
    const hero = stone(ctx, cam, w, h, 0, HERO_Z);
    ctx.restore();

    /* 사람 — 아직 서 있다. 이름을 넘긴 뒤에야 가라앉는다. */
    const P = person(ctx, cam, w, h, MAN_X, MAN_Z, { sink, lean: 0.05 });

    /* 새겨진 이름 — 왼쪽부터 파인다. 통째로 뜨면 '나타났다'로 읽힌다. */
    if (hero && fill > 0.01) {
      const s = hero.slot, name = spec.carvedName ?? '';
      ctx.save();
      ctx.beginPath();
      ctx.rect(s.x - s.w / 2, s.y, s.w * clamp(fill / 0.85), s.h);
      ctx.clip();
      ctx.globalAlpha = g0;
      setShadow(ctx, GLOW, 5);
      ctx.fillStyle = tone('accent');
      ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      let fs = s.h * 0.72;
      ctx.font = disp(900, fs);
      while (fs > 7 && ctx.measureText(name).width > s.w * 0.86) {
        fs -= 0.5; ctx.font = disp(900, fs);
      }
      ctx.fillText(name, s.x, s.y + s.h / 2 + s.h * 0.03);
      clearShadow(ctx);
      ctx.restore();
      ctx.textBaseline = 'alphabetic';
    }

    /* 이름 조각이 건너간다 — 살아 있을 때 넘긴다. 살짝 뒤로 뺐다가 포물선으로. */
    if (P && hero && fly > 0.005 && fly < 1) {
      const k = fly < 0.18 ? -0.32 * Math.sin(fly / 0.18 * Math.PI) : easeOut((fly - 0.18) / 0.82);
      const cx = lerp(P.bx, hero.slot.x, k);
      const cy = lerp(P.headY - P.u * 0.14, hero.slot.y + hero.slot.h / 2, clamp(k))
        - Math.sin(clamp(k) * Math.PI) * P.u * 0.30;
      const cw = P.u * 0.34, chh = P.u * 0.11;
      ctx.save();
      ctx.globalAlpha = g0;
      setShadow(ctx, GLOW, 10);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(cx - cw / 2, cy - chh / 2, cw, chh);
      clearShadow(ctx);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
