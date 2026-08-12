import { disp, ease, easeOut, clamp, lerp, fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW, cam3, project }
  from '../../../engine/lib.js';
import { GRAVES, ground, stone, person, PH } from './_world.js';

/* namefade — 훅. **내가 이름을 아는 주민 하나**가 묘지 안쪽으로 걸어 들어가 가라앉고,
   그 자리에 비석이 솟으면 이름표가 꺼진다. 남는 것은 구별이 안 되는 일곱이다.

   원문 근거:
   > "Day 45에 주민 일곱 명이 죽었습니다."
   > "화면을 가득 채운 회색 원 일곱 개를 보면서, 저는 그중 누가 제가 아침에 지켜보던
   >  그 주민인지 알 수 없었습니다."

   ── 2026-08-12 v9 (사용자: 모든 영상 3D · 주민도 3D · 터지는 효과 금지) ────────
   🔴 **걷는 방향을 화면 가로가 아니라 깊이로 돌렸다.** 전에는 주민이 좌→우로 미끄러졌다.
      지금은 z 를 따라 **카메라에서 멀어지며** 무리 속으로 들어간다 — 걸을수록 작아지고,
      그것이 「무리에 섞여 사라진다」를 크기로 말한다. 가로 이동으로는 안 나오는 뜻이다.
   🔴 주민은 실루엣이 아니라 **머리(구) + 몸통·팔·다리(원기둥)** 다(`_world.js` person).
   🔴 반전 플래시·집중선 없음. 솟는 무게는 반동과 그림자가 진다.

   🔴 이 편의 색 규약을 여기서 세운다:
      **강조색 = 이름, 또는 이름이 들어갈 자리 / 흰 회색 = 그 밖의 전부.**
      이 샷은 강조색이 **켜져 있다가 꺼지는** 유일한 샷이고, 본문에서는 한 번도 안 켜지며,
      carvename 에서 처음으로 다시 채워진다.
   🔴 화면 글자는 「Day 45」 하나뿐이다(게임이 무덤에 찍는 표기의 인용 · ADR-V-10 예외 ①).

   ⏱ 한 번의 사건으로 간다(루프 없음): 걷는다 → 가라앉는다 → 비석이 솟는다 →
      이름표가 꺼진다 → 일곱이 똑같아진다. 훅 자막 4.7초 위에 그대로 펼친다. */

const WALK_Z0 = -60, WALK_Z1 = 0;      // 카메라 앞에서 첫 비석 자리까지

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const st = ease(cue(0, 0.15, 0.20));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }
    const cam = cam3(spec.cam3, t);

    const walk = easeOut(clamp((t - 0.40) / 1.20));
    const sink = ease(clamp((t - 1.65) / 0.55));
    const rise = clamp((t - 1.90) / 0.50);
    const tagOff = ease(clamp((t - 2.55) / 0.45));
    const cursor = clamp((t - 3.10) / 0.30);

    ground(ctx, w, h);

    if (spec.dayLabel) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
      ctx.font = disp(800, 13); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.dayLabel, 18, 26);
      ctx.restore();
    }

    /* 이미 이름을 잃은 여섯 — 먼 것부터 */
    const rest = GRAVES.slice(1).map(([x, z]) => ({ x, z })).sort((a, b) => b.z - a.z);
    const wz = lerp(WALK_Z0, WALK_Z1, walk);

    for (const o of rest) if (o.z > wz) stone(ctx, cam, w, h, o.x, o.z);

    /* 일곱 번째 — 걸어 들어가 가라앉는다. 그 자리에 비석이 솟는다. */
    const P = person(ctx, cam, w, h, 0, wz, { sink, lean: 0.06 });
    const hero = rise > 0.01
      ? stone(ctx, cam, w, h, 0, WALK_Z1, { grow: clamp(easeOut(rise) + 0.14 * Math.sin(Math.PI * clamp(rise)) * (1 - clamp(rise)) * 2, 0, 1.16) })
      : null;

    for (const o of rest) if (o.z <= wz) stone(ctx, cam, w, h, o.x, o.z);

    /* 이름표 — 사람 머리 위에 떠 있다가, 비석이 서면 이름 자리로 내려앉고 꺼진다 */
    const ta = st * (1 - tagOff);
    if (ta > 0.01 && P) {
      const tw = P.u * 0.42, th = P.u * 0.13;
      const upY = P.headY - P.u * 0.22;
      const toY = hero ? hero.slot.y + hero.slot.h / 2 : upY;
      const y = lerp(upY, toY, ease(clamp((t - 2.25) / 0.40)));
      ctx.save();
      ctx.globalAlpha = ta;
      setShadow(ctx, GLOW, 10);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(P.bx - tw / 2, y - th / 2, tw, th);
      clearShadow(ctx);
      ctx.restore();
    }

    /* 커서 — 쓸 자리는 남았는데 아무도 안 쓴다 */
    if (cursor > 0.01 && hero && ((t - 3.10) % 1.1) < 0.6) {
      const s = hero.slot;
      ctx.save();
      ctx.globalAlpha = st * cursor;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(s.x - s.w * 0.04, s.y + s.h * 0.18, s.w * 0.08, s.h * 0.64);
      clearShadow(ctx);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
