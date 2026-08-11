import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* bothways — 뒷정리 한 번이 양쪽을 동시에 지우고, 그 뒤로는 아무것도 못 건너간다.

   원문 근거(「문제」 절):
   > "주민이 사라질 때 호출되는 정리 함수가 관계 기록을 양방향으로 삭제하고 있었던 겁니다.
   >  단짝이 죽으면 남은 사람의 기억에서도 그 관계가 통째로 없어졌습니다.
   >  상실이 생존자에게 전달될 통로 자체가 없었던 거죠."
   🔑 마지막 문장이 이 그림의 아래쪽 절반이다 — **점선 길과 그 위를 건너가는 조각**이
   '전달될 통로' 이고, 그것이 가운데서 끊기는 것이 '통로 자체가 없었다' 다.

   🔴 훅(bondcut)의 「선이 양끝에서 안으로 걷힌다」를 여기서 **뒤집어** 다시 그린다 —
   가운데에서 좌우로 퍼져 나가며 지운다. 같은 사건을 밖에서 안으로 / 안에서 밖으로 두 번
   보여주는 것이 이 편의 되받기다.

   색 규약(회차 공통): **강조색 = 둘이 이어져 있었다는 기록 / 흰색 = 사람 · 칸 · 지우는 것.**

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다:
     · 지우개는 지우는 물건이라 강조 줄과 **접촉이 필연**이다. 그래서 코드에 문턱을 박았다 —
       **지우개 선단에서 8px 앞까지만 강조를 그린다**(`EDGE = 6 + 8`). 축 A 최소 = **8px**.
     · 라벨 글립 바닥 91 ↔ 강조 줄 glow 위끝 136 − 8 = 128 → **37px**.
     · 조각·점선 길(y 200~212) ↔ 강조 줄 glow 바닥 148 + 8 = 156 → **44px**.
   🔴 흰↔흰(서로 다른 것을 가리키는 둘):
     · 「죽은 사람」(x 73~135)과 「남은 사람」(x 217~279) — 같은 baseline 이라 **x 로 갈랐고**
       `maxW` 120 을 걸어 문자열이 길어져도 안 붙는다. 최소 **82px**.
     · 지우개(y 112~172) ↔ 칸 테두리 안쪽(101.5 / 182.5) — **10.5px**. 좌우 끝에서도
       지우개 왼끝 50 ↔ 왼 칸 테두리 41.5 = **8.5px**, 오른끝 302 ↔ 312 − 1.5 = **8.5px**.
     · 건너가는 조각(y 200~212) ↔ 칸 바닥 테두리 185.5 — **14.5px**.
     · 라벨과 그 칸(10.5px)은 **의도된 쌍**이다(라벨이 바로 그 칸을 가리킨다).

   세로 예산(캔버스 307): 최저 = 조각 바닥 212. **95px** 남는다.
   가로(캔버스 352): 칸 40~312. 좌우 **40px**씩 남는다.

   ⏱ 사건 셋이 자막 셋에 그대로 물린다 — 지우개 내려옴 `since(0)`, 좌우 지움 `since(1)`,
      길 끊김 `since(2)`. 🔴 `cue` 가 아니라 `since` 위에 세운 것은 **효과음 때문**이다:
      `sfx.delayMs` 와 원점이 같아야 자막 길이가 바뀌어도 소리 자리를 다시 안 푼다.
      지움은 `since(1)` 0.25~1.10초이고, 그 한가운데 0.675초에 sweep 의 에너지 정점을 맞췄다.
   계속 도는 것(30fps 기준): 조각이 1.5초 주기로 148px 을 건넌다 → 최대 **6.9px/프레임** ·
      칸 테두리 점선 흐름 40px/s → 1.3px/프레임 · 강조 줄 밝기 파동. */

const BOX = [
  { x0: 40, x1: 168 },
  { x0: 184, x1: 312 }
];
const BOX_Y0 = 100, BOX_Y1 = 184;
const BAR = [{ x0: 60, x1: 148 }, { x0: 204, x1: 292 }];
const BAR_Y = 136, BAR_H = 12;
const ERA_HW = 6, ERA_Y0 = 112, ERA_Y1 = 172, EDGE = ERA_HW + 8;
const PATH_Y = 206, PATH_X0 = 96, PATH_X1 = 252;
const CHIP = 12, CHIP_Y = 200, MID = 176;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (!txt || alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    /* 가로 점선 — off 로 흐른다 */
    const dashRow = (x0, x1, y, seg, gap, off, alpha) => {
      if (x1 - x0 <= 0.5 || alpha <= 0.01) return;
      const P = seg + gap, len = x1 - x0;
      const ph = ((off % P) + P) % P;
      ctx.save();
      ctx.globalAlpha = alpha; ctx.fillStyle = tone('ink');
      for (let s = -P + ph; s < len; s += P) {
        const a = Math.max(0, s), b = Math.min(len, s + seg);
        if (b > a) ctx.fillRect(x0 + a, y - 1.5, b - a, 3);
      }
      ctx.restore();
    };

    const dashBox = (x0, y0, x1, y1, off, alpha) => {
      dashRow(x0, x1, y0, 11, 9, off, alpha);
      dashRow(x0, x1, y1, 11, 9, -off, alpha);
      ctx.save();
      ctx.globalAlpha = alpha; ctx.fillStyle = tone('ink');
      const P = 20, len = y1 - y0, ph = ((-off % P) + P) % P;
      for (let s = -P + ph; s < len; s += P) {
        const a = Math.max(0, s), b = Math.min(len, s + 11);
        if (b > a) { ctx.fillRect(x0 - 1.5, y0 + a, 3, b - a); ctx.fillRect(x1 - 1.5, y0 + a, 3, b - a); }
      }
      ctx.restore();
    };

    const st = ease(cue(spec.eraserCue ?? 0, 0.15, 0.30));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const off = -t * 40;
    const erase = ease(clamp((since(spec.eraseCue ?? 1) - 0.25) / 0.85));
    const cut = ease(clamp((since(spec.cutCue ?? 2) - 0.20) / 0.55));
    const drop = ease(clamp((since(spec.eraserCue ?? 0) - 0.35) / 0.45));

    /* ── 두 칸 (흰 점선) ── */
    for (const b of BOX) dashBox(b.x0, BOX_Y0, b.x1, BOX_Y1, off, st * 0.9);
    ctext(spec.leftLabel, 104, 88, 800, 12.5, tone('sub'), 120, st);
    ctext(spec.rightLabel, 248, 88, 800, 12.5, tone('sub'), 120, st);

    /* ── 기록 줄 (강조색) — 지우개 선단에서 8px 앞까지만 그린다 ── */
    const xl = lerp(MID, 56, erase);   // 왼쪽 지우개 중심
    const xr = lerp(MID, 296, erase);  // 오른쪽 지우개 중심
    const lim = [
      { a: BAR[0].x0, b: Math.min(BAR[0].x1, xl - EDGE) },
      { a: Math.max(BAR[1].x0, xr + EDGE), b: BAR[1].x1 }
    ];
    for (let i = 0; i < 2; i++) {
      const { a, b } = lim[i];
      if (b - a <= 0.5) continue;
      ctx.save();
      ctx.globalAlpha = st * (0.82 + 0.18 * (0.5 + 0.5 * Math.sin(t * 3.6 + i * 1.7)));
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(a, BAR_Y, b - a, BAR_H);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 지우개 둘 (흰색) — 내려와서 겹쳐 섰다가 좌우로 갈라진다 ── */
    if (drop > 0.01) {
      const dy = lerp(-90, 0, drop);
      ctx.save();
      ctx.globalAlpha = st * drop;
      ctx.fillStyle = tone('ink');
      for (const cx of [xl, xr]) ctx.fillRect(cx - ERA_HW, ERA_Y0 + dy, ERA_HW * 2, ERA_Y1 - ERA_Y0);
      ctx.restore();
    }

    /* ── 전달되는 길 (흰 점선) — cut 이 돌면 가운데가 벌어진다 ── */
    const gap = 24 * cut;
    dashRow(PATH_X0, MID - gap, PATH_Y, 9, 7, off, st * 0.75);
    dashRow(MID + gap, PATH_X1, PATH_Y, 9, 7, off, st * 0.75);

    /* ── 건너가는 조각 (흰색) — cut 뒤로는 가운데서 꺼진다 ── */
    const trav = frac(t / 1.5);
    const cx = lerp(100, 248, ease(clamp(trav / 0.72)));
    const blocked = cut * ease(clamp((cx - 158) / 14));
    const chipA = st * ease(clamp(trav / 0.06))
                * (1 - ease(clamp((trav - 0.76) / 0.12)))
                * (1 - blocked);
    if (chipA > 0.01) {
      ctx.save();
      ctx.globalAlpha = chipA;
      ctx.fillStyle = tone('ink');
      ctx.fillRect(cx - CHIP / 2, CHIP_Y, CHIP, CHIP);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
