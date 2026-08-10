/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다.
   경로 좌표를 적을 땐 `(경로)` 라고 표시한다.
   🔑 `ep14s-4` 마스터 판정(2026-08-10) — 이 편에서도 좌표 셋이 **경로 기준**이라 실여유가 7.5→6.0px · 최소 x 23→21.5 다(전부 안전해 되돌리지 않는다). 여유가 1.5~3px 넉넉하게 적혀 있었고
   원인은 부주의가 아니라 **어느 기준으로 적는지가 어디에도 없었던 것**이다. */
import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* everyrow — 한 사람의 사고가 아니었다. 누구를 데려와도 같은 자리에서 막힌다.

   원문 근거:
   > "게다가 이 게임의 채집 목표가 '곡식 4개 확보'라서 모든 주민이 평상시에 4개를
   >  채우고 다닙니다. 즉 선불 성격 목수는 구조적으로 항상 거절하고 있었습니다."

   🔴 기본 도형의 세 번째 변주. 앞 샷의 격자를 **여러 줄로 복제**해서 「이 판은 우연이
   아니라 규칙이다」를 도형만으로 보여준다. 줄마다 위상을 어긋나게 흔들어 놓았는데,
   그래도 **되밀리는 x 좌표는 셋이 똑같다** — 그게 「구조적」의 그림 번역이다.

   🔴 줄 수는 **세 줄**이다. 화면 어디에도 줄 수를 글자로 안 적었다 — 원문에 「주민이 N명」
   이라는 수가 없고, 하필 이 편에는 4(평상시 곡식)라는 수가 이미 있어서 줄을 넷으로 그리면
   그 수와 헷갈린다. 셋은 「여럿」을 뜻하는 그림이지 셈이 아니다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — 부등식으로 막았다.
     · 흰 것: 표식 x 23~45 · 이미 지닌 곡식 알 최대 x = 146.5 + 5.5 = 152
              · 칸 테두리(흰색 31%) 최대 x = 250.5.
     · 강조색: 품삯 무리 최소 x = 258 (가장 왼쪽까지 밀고 들어왔을 때) · 최대 x = 334.
       → 테두리와 7.5px 뜬다. 그래서 **이 그림의 강조색에는 글로우가 하나도 없다.**
     · 아래 강조색 글자는 y 258~276, 흰 것의 최저는 y 234 → 24px 뜬다.

   🔴 캔버스 밖으로 안 나간다: 최대 x 334 · 최소 x 23 · 세로 최저 272 베이스라인(글립 ~276).

   ⏱ 흔들림은 `since(0)` 주기 1.6초에 줄마다 0.16초씩 어긋나게 물렸다. 이 샷에는 효과음이
      없지만, 앞뒤 샷과 같은 원점(자막 시작)을 쓰면 세 그림의 박자가 서로 맞는다.
   계속 도는 것 = 세 줄의 품삯 무리 왕복(면적 큼) + 아래 글자 등장. */

const ROWS = [100, 160, 220];
const N = 8, CW = 20, GAP = 3.5, X0 = 66;
const CELL_H = 28;
const MARK_X = 34, MARK_R = 11;
const HELD_R = 5.5, WAGE_R = 5, WAGE_DX = 12;
const REST_L = 268;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, cue, since }) {
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
      ctx.fillText(txt, clamp(x, 12 + tw / 2, w - 12 - tw / 2), y);
      ctx.restore();
    };

    const cellL = i => X0 + i * (CW + GAP);
    const cellCx = i => cellL(i) + CW / 2;
    const held = Math.max(0, Math.min(N, spec.held ?? 4));
    const wage = Math.max(1, spec.wage ?? 5);

    /* ── 구획 이름 ─────────────────────────────────── */
    if (spec.title) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    const c0 = cue(spec.rowCue ?? 0, 0.15, 0.55);
    if (c0 <= 0.01) { ctx.textAlign = 'left'; return; }
    const s0 = Math.max(0, since(spec.rowCue ?? 0));
    const P = spec.loopSec ?? 1.6;

    for (let r = 0; r < ROWS.length; r++) {
      const a = ease(clamp((c0 - 0.10 * r) / 0.30));
      if (a <= 0.01) continue;
      const cy = ROWS[r];

      /* 사람 */
      ctx.save();
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(MARK_X, cy, MARK_R, 0, Math.PI * 2); ctx.stroke();
      ctx.restore();

      /* 여덟 칸 */
      ctx.save();
      ctx.globalAlpha = a * 0.45;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      for (let i = 0; i < N; i++) { roundRect(ctx, cellL(i), cy - CELL_H / 2, CW, CELL_H, 5); ctx.stroke(); }
      ctx.restore();

      /* 평상시에 이미 지니고 다니는 곡식 */
      ctx.save();
      ctx.globalAlpha = a;
      ctx.fillStyle = tone('ink');
      for (let i = 0; i < held; i++) {
        ctx.beginPath(); ctx.arc(cellCx(i), cy, HELD_R, 0, Math.PI * 2); ctx.fill();
      }
      ctx.restore();

      /* 품삯 — 다가왔다가 같은 자리에서 되밀린다 */
      const u = frac(Math.max(0, s0 - 0.16 * r) / P);
      const L = REST_L
        - 10 * ease(clamp(u / 0.34))
        + 18 * ease(clamp((u - 0.36) / 0.22))
        - 8 * ease(clamp((u - 0.62) / 0.30));
      ctx.save();
      ctx.globalAlpha = a;
      ctx.fillStyle = tone('accent');
      for (let k = 0; k < wage; k++) {
        ctx.beginPath(); ctx.arc(L + WAGE_R + k * WAGE_DX, cy, WAGE_R, 0, Math.PI * 2); ctx.fill();
      }
      ctx.restore();
    }

    const rl = ease(clamp((c0 - 0.45) / 0.35));
    ctext(spec.verdict, w / 2, 272, 900, 16, tone('accent'), w - 120, rl);

    ctx.textAlign = 'left';
  }
};
