import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* replay — 같은 판을 한 번 더 돌리고, 그 끝에서 똑같이 묻는다.

   원문 근거:
   > "작업이 끝나고, 저는 다시 한 판을 켜서 방치했습니다.
   >  그리고 똑같이 물었습니다. 무슨 일이 있었지?"

   🔴 「회상 테스트」라는 제도 이름은 이 회차 원문에 **아예 안 나온다**(기획 브리프 §7).
   그래서 화면에도 안 쓴다 — 라벨은 원문 낱말을 그대로 딴 「같은 판, 다시」 하나뿐이다.

   색 규약(이 회차 전체): **흰색 = 마을에 이미 있던 것 / 강조색 = 이번 관찰에서 새로 생긴 것.**
   판 틀 · 표식 셋 · 물음표가 흰색이고, **이번에 다시 돌린 진행 막대만 강조색**이다 —
   이 회차에서 강조색이 처음 들어오는 자리다.
   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세로 좌표로 갈랐다.**
     흰 판 틀 아래 획의 바깥 = y 153.5 / 강조색 막대 y 172~179 · 글로우(blur 8)까지 164~187.
     **10.5px** 뜬다. 아래로는 흰 물음표 글립 꼭대기 약 198(font 46 · baseline 232)이라
     막대 글로우 바닥 187 과 **11px** 뜬다.
   흰 글자끼리(축 B): 「같은 판, 다시」(좌상단 baseline 26)와 물음표(baseline 232)가
   **세로 약 172px** 떨어져 있다.

   🔴 **빈 트랙을 안 그렸다.** 강조색 막대 뒤에 흰 12% 배경 막대를 깔면 두 색이 같은
   픽셀을 나눠 갖는다. 막대는 맨바닥에서 자라고, 폭의 기준은 위 판 틀이 진다.

   ⏱ 판 틀과 표식은 `cue(0)`, 물음표는 `cue(1)`, 진행 막대는 2.2초 루프(`t`)다.
   🔴 **막대가 루프라서 이 샷에도 효과음을 안 달았다**(hadgot 과 같은 이유).

   세로 예산(캔버스 307): 최저 = 물음표 글립 바닥 약 236. 71px 남는다.
   가로: 판 틀 오른 획 바깥 297.5. 좌 54.5 · 우 54.5 남는다.

   계속 도는 것(30fps 기준):
     · 판 틀 둘레 2×(240+96) = **672px** 이 숨을 쉰다.
       `bre = 1.8·(1 + sin(7.6 t))` → 진폭 1.8 · ω 7.6 → **0.456px/프레임** → 약 **306px²**
     · 표식 셋이 184px 를 1.8초에 왕복 → **3.4px/프레임** × 높이 34 × 3개
     · 진행 막대가 240px 를 1.98초에 → **4.0px/프레임** × 두께 7 */

const BX = 56, BY = 56, BW = 240, BH = 96;
const WALK_L = 84, WALK_R = 268, MARK_Y = 104;
const BAR_Y = 172, BAR_H = 7;

const tri = x => 1 - Math.abs(2 * frac(x) - 1);

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
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

    /* 좌상단 라벨은 게이트를 안 건다 — 샷 내내 읽을 수 있어야 한다 */
    if (spec.title) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 26);
      ctx.restore();
    }

    const st = ease(cue(spec.boardCue ?? 0, 0.15, 0.30));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 판 틀 (흰색) ─────────────────────────────── */
    const bre = 1.8 * (1 + Math.sin(t * 7.6));
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, BX + bre, BY + bre, BW - bre * 2, BH - bre * 2, 10);
    ctx.stroke();
    ctx.restore();

    /* ── 판 안에서 계속 걸어 다니는 표식 셋 (흰색) ── */
    for (let i = 0; i < 3; i++) {
      const k = tri(t / 3.6 + i * 0.31);
      const x = lerp(WALK_L, WALK_R, k);
      const y = MARK_Y + 3 * Math.sin(t * 6.2 + i * 1.7);
      ctx.save();
      ctx.globalAlpha = st;
      ctx.translate(x, y);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, -7, -17, 14, 34, 7);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 한 바퀴 돌고 되감겨 다시 도는 진행 막대 (강조색) ── */
    const pu = frac(t / (spec.loopSec ?? 2.2));
    const fw = BW * Math.min(1, pu / 0.9);
    if (fw > 1) {
      ctx.save();
      ctx.globalAlpha = st;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(BX, BAR_Y, fw, BAR_H);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 그 끝에서 묻는다 (흰색) ──────────────────── */
    const ask = ease(cue(spec.askCue ?? 1, 0.15, 0.35));
    ctext('?', 176, lerp(238, 232, ask), 900, lerp(38, 46, ask), tone('ink'), 60, st * ask);

    ctx.textAlign = 'left';
  }
};
