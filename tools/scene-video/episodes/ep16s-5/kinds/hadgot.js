import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* hadgot — 갖출 건 다 갖췄는데 사람만 비어서 무너진다. (훅)

   원문 근거:
   > "순둥이와 고집쟁이가 굶어 죽었네 집과 밭이 있었는데 이번 관찰은 신기하네."
   > "'집과 밭이 있었는데 왜 죽었지?'라는 질문이 생겼습니다."

   🔴 훅 계약(기획 브리프 §2) — 이 회차는 `ep15s-5` 가 이미 내보낸 문장·수치를 안 쓰고
   **이 편 고유물인 새 질문**에서 시작한다. 그래서 이 그림에는 로그도, 가득 찬 계단도,
   빈 점선 그릇도 없다. 있는 것은 **집 · 밭 · 사람 · 비어 가는 칸** 넷뿐이다.

   색 규약(이 회차 전체): **흰색 = 마을에 이미 있던 것 / 강조색 = 이번 관찰에서 새로 생긴 것.**
   🟢 **이 샷에는 강조색이 한 점도 없다.** 훅은 아직 아무것도 못 알아본 상태라서다 —
   강조색은 S1 의 진행 막대에서 처음 들어온다. 그래서 「흰색 ↔ 강조색 겹침」이
   이 샷에서는 **성립 자체를 안 한다.**
   흰 글자끼리(축 B): 「집」(중심 x 77) · 「밭」(중심 x 275) 이 같은 baseline 216 에 있지만
   **x 로 약 184px** 떨어져 있다. 각 라벨과 그 모양은 세로 27.5px 로 붙는데 의도된 쌍이다.

   ⏱ 등장은 `cue(0)`, 사건은 2.6초 루프(`t`)다.
   🔴 **루프이기 때문에 이 샷에는 효과음을 안 달았다** — 한 번만 내면 두 번째 바퀴부터
   그림과 어긋나고, 매 바퀴 내면 시끄럽다. 이 회차의 소리는 「한 번만 일어나는 일」인
   S2·S3 에만 붙는다.

   세로 예산(캔버스 307): 최저 = 라벨 글립 바닥 약 220. 87px 남는다.
   🔴 주저앉는 표식은 **다 앉은 자리가 최저**다 — 반높이가 22→10 으로 줄지만 중심 y 가
   150→166 으로 내려가는 쪽이 더 크다. 바운딩 반높이 `hh·cosθ + 8·sinθ` 를 sit 0~1 에서
   훑으면 최댓값이 sit=1 의 166 + 10×0.976 + 8×0.218 = 177.5(선 반폭 포함 **179**)이고,
   도중 값(sit 0.3 → 175.2)이 그보다 작다.
   가로: 집 처마 왼쪽 37.5 · 밭 오른쪽 313.5. 좌 37.5 · 우 38.5 남는다.

   계속 도는 것(30fps 기준):
     · 집(둘레 약 394px: 몸통 224 + 지붕 두 변 94 + 처마 76) + 밭(둘레 248 + 이랑 3×58 = 422)
       = **816px** 이 숨을 쉰다. `bre = 1.5·(1 + sin(9.0 t))` → 진폭 1.5 · ω 9.0 →
       **0.45px/프레임** → 약 **367px² 상당**
     · 게이지 채움(폭 10px)이 78px 를 1.25초에 비운다 → **1.9px/프레임**
     · 표식 숨 진폭 0.6px · 7.4rad/s → 0.15px/프레임 */

/* 집 */
const HB_X = 46, HB_Y = 126, HB_W = 62, HB_H = 50;
const H_EAVE_L = 39, H_EAVE_R = 115, H_APEX_X = 77, H_APEX_Y = 98;
/* 밭 */
const FD_X = 238, FD_Y = 126, FD_W = 74, FD_H = 50;
/* 게이지 */
const G_X = 197, G_Y = 106, G_W = 20, G_H = 88;
const GF_X = 202, GF_W = 10, GF_TOP = 111, GF_BOT = 189;
/* 사람 */
const FIG_X = 176;

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

    const st = ease(cue(spec.showCue ?? 0, 0.15, 0.30));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* 안쪽으로만 줄어드는 숨 — 가장자리 예산을 안 건드린다 */
    const bre = 1.5 * (1 + Math.sin(t * 9.0));

    /* ── 집 (흰색, 끝까지 그대로) ─────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.lineJoin = 'round'; ctx.lineCap = 'round';
    ctx.strokeRect(HB_X + bre, HB_Y + bre, HB_W - bre * 2, HB_H - bre * 2);
    ctx.beginPath();
    ctx.moveTo(H_EAVE_L + bre, HB_Y + bre);
    ctx.lineTo(H_APEX_X, H_APEX_Y + bre * 1.4);
    ctx.lineTo(H_EAVE_R - bre, HB_Y + bre);
    ctx.stroke();
    ctx.restore();

    /* ── 밭 (흰색, 끝까지 그대로) ─────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.lineJoin = 'round'; ctx.lineCap = 'round';
    ctx.strokeRect(FD_X + bre, FD_Y + bre, FD_W - bre * 2, FD_H - bre * 2);
    for (let i = 0; i < 3; i++) {
      const y = FD_Y + 13 + i * 12 + (i - 1) * -bre * 0.5;
      ctx.beginPath();
      ctx.moveTo(FD_X + 8 + bre, y);
      ctx.lineTo(FD_X + FD_W - 8 - bre, y);
      ctx.stroke();
    }
    ctx.restore();

    /* ── 2.6초 루프 : 칸이 비고 → 사람이 주저앉고 → 되풀이 ── */
    const u = frac(t / (spec.loopSec ?? 2.6));
    const vis = ease(clamp(u / 0.06)) * (1 - ease(clamp((u - 0.90) / 0.10)));
    const drain = ease(clamp((u - 0.10) / 0.48));
    const sit = ease(clamp((u - 0.62) / 0.24));

    /* 게이지 틀 — 사람이 깜빡여도 자리는 그대로 있다 */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    ctx.strokeRect(G_X, G_Y, G_W, G_H);
    ctx.restore();

    /* 채움 — 위에서부터 비어 내려간다 */
    const fh = (GF_BOT - GF_TOP) * (1 - drain);
    if (fh > 0.5) {
      ctx.save();
      ctx.globalAlpha = st * vis;
      ctx.fillStyle = tone('ink');
      ctx.fillRect(GF_X, GF_BOT - fh, GF_W, fh);
      ctx.restore();
    }

    /* 사람 — 접히듯 주저앉는다 (90도로 눕지 않는다) */
    const br = 0.6 * (1 + Math.sin(t * 7.4));
    const hh = lerp(22, 10, sit);
    ctx.save();
    ctx.globalAlpha = st * vis;
    ctx.translate(FIG_X, lerp(150, 166, sit) + br);
    ctx.rotate(sit * 0.22);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, -8, -hh, 16, hh * 2, 8);
    ctx.stroke();
    ctx.restore();

    /* ── 라벨 ─────────────────────────────────────── */
    ctext(spec.houseLabel, H_APEX_X, 216, 800, 14, tone('sub'), 60, st);
    ctext(spec.fieldLabel, FD_X + FD_W / 2, 216, 800, 14, tone('sub'), 60, st);

    ctx.textAlign = 'left';
  }
};
