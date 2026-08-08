import {
  disp, ease, clamp, spring,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* mismatch — 훅(4단 고정 구조 ②). 이 편 전체를 한 장으로 줄인 그림이다.

   훅 자막: "오늘 개발 일지 내용은, 주민 4명인데 겨울 목표는 20개예요."
   원문 근거: "겨울 대비 목표가 \"조리식 20개\"로 못 박혀 있었는데, 예전에 주민이 10명이던
   시절에 잡은 숫자였습니다." / "그 뒤 주민을 4명으로 줄였는데도 20이라는 숫자는 그대로
   남았습니다."

   🔴 이 회차의 축은 **「한 덩어리를 몇이 나누는가」** 다. 훅은 그 축의 **끊긴 자리**를 보인다 —
   위 칸에 잠긴 20, 아래 칸에 넷뿐인 사람, 그리고 둘을 잇던 흰 막대가 **가운데에서 뚝 끊어진다.**
   숫자와 사람 수가 서로를 모르는 사이가 됐다는 것이 이 편이 약속하는 사건 전부다.

   ⏱ 자막 한 줄(약 4.9초)에 네 박자를 순서대로 물린다. 창은 `cue(beatCue, 0.15, 0.95)` 로
      넓게 잡았다 — 마지막 낱말(「스무 개예요」)까지 그림이 따라가야 하기 때문이다.
      ① appear  (0 → 0.18)  빈 틀 둘과 라벨이 들어온다          … "오늘 개발 일지"
      ② headOn  (0.30 → 0.58) 아래 표식 넷이 흰색으로 켜진다      … "주민 네 명인데"
      ③ goalOn  (0.60 → 0.84) 위에 강조색 20 이 잠긴 채 앉는다    … "겨울 목표는"
      ④ breakK  (0.86 → 1.00) 둘을 잇던 흰 막대가 끊어진다        … "스무 개예요"

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **행을 갈랐다.**
      강조색: 잠금 테두리 y 56~126 · 숫자 글리프 y 70~108(둘 다 x 는 가운데).
      흰색:  라벨 y 34(글리프 아래) · 끊어지는 막대 y 148~190 · 표식 y 233~259 · 바닥 점선 y 288.
      가장 가까운 쌍이 **22px** 떨어져 있어 후광(GLOW, 14)을 빼도 8px 남는다.
      ⚠️ 좌표가 보장하는 것은 「강조색이 흰 픽셀을 **불투명하게 덮지 않는다**」까지다.

   🔴 클립을 한 군데도 안 쓴다. 텍스트는 전부 fitFont 로 폭을 재서 넣는다.

   계속 도는 것 = 잠금 테두리의 점선 흐름 · 켜진 표식의 바운스(켜지기 전에는 빈 윤곽의
   점선 흐름) · 바닥 점선의 흐름. */

const LOCK_T = 56, LOCK_H = 70, LOCK_W = 150;
const NUM_Y = 108;
const LINK_T = 148, LINK_B = 190, LINK_C = 169;
const MARK_Y = 246, MARK_R = 13;
const BASE_Y = 288;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const cx = w / 2;

    const fitFont = (txt, weight, start, max, min = 8, fam = disp) => {
      let fs = start; ctx.font = fam(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = fam(weight, fs); }
      return fs;
    };

    const c = cue(spec.beatCue ?? 0, 0.15, 0.95);
    const appear = ease(clamp(c / 0.18));
    const headOn = ease(clamp((c - 0.30) / 0.28));
    const goalOn = ease(clamp((c - 0.60) / 0.24));
    const breakK = ease(clamp((c - 0.86) / 0.14));

    /* ── 바닥 점선 — 자막과 무관하게 계속 흐른다 ───── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([7, 9]);
    ctx.lineDashOffset = -(t * 15) % 16;
    ctx.beginPath(); ctx.moveTo(16, BASE_Y); ctx.lineTo(w - 16, BASE_Y); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;

    if (appear <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 위 라벨 ───────────────────────────────────── */
    if (spec.goalLabel) {
      ctx.globalAlpha = appear;
      ctx.textAlign = 'center';
      ctx.font = disp(800, fitFont(spec.goalLabel, 800, 12, w - 80));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.goalLabel, cx, 34);
      ctx.globalAlpha = 1;
    }

    /* ── 잠금 테두리 : 숫자가 들어오기 전부터 자리를 지킨다 ──
       점선만 흐르고 테두리 자체는 한 픽셀도 안 움직인다 = 「못 박혀 있었다」. */
    const lw = Math.min(LOCK_W, w - 60);
    ctx.save();
    ctx.globalAlpha = appear * 0.6;
    ctx.setLineDash([11, 8]);
    ctx.lineDashOffset = -(t * 22) % 19;
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    roundRect(ctx, cx - lw / 2, LOCK_T, lw, LOCK_H, 10); ctx.stroke();
    ctx.restore();

    /* ── 잠긴 숫자 ─────────────────────────────────── */
    const num = String(spec.goalNumber ?? '');
    if (num && goalOn > 0.01) {
      const fs = fitFont(num, 900, 52, lw - 34, 18);
      const sc = spring(goalOn);
      ctx.save();
      ctx.translate(cx, NUM_Y); ctx.scale(sc, sc); ctx.translate(-cx, -NUM_Y);
      ctx.globalAlpha = clamp(goalOn * 1.6);
      setShadow(ctx, GLOW, 14);
      ctx.textAlign = 'center';
      ctx.font = disp(900, fs);
      ctx.fillStyle = tone('accent');
      ctx.fillText(num, cx, NUM_Y);
      clearShadow(ctx);
      ctx.restore();
      ctx.globalAlpha = 1;
    }

    /* ── 둘을 잇던 막대 : 가운데에서 끊어진다 ───────── */
    const gap = 11 * breakK;
    ctx.save();
    ctx.globalAlpha = appear;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 5;
    ctx.beginPath();
    ctx.moveTo(cx, LINK_T); ctx.lineTo(cx, LINK_C - gap);
    ctx.moveTo(cx, LINK_C + gap); ctx.lineTo(cx, LINK_B);
    ctx.stroke();
    /* 끊긴 자국 — 벌어진 뒤에만 양쪽 끝에 짧은 가로 획이 남는다 */
    if (breakK > 0.04) {
      ctx.globalAlpha = appear * clamp(breakK * 1.4);
      ctx.lineWidth = 4;
      const nub = 9 * breakK;
      ctx.beginPath();
      ctx.moveTo(cx - nub, LINK_C - gap); ctx.lineTo(cx + nub, LINK_C - gap);
      ctx.moveTo(cx - nub, LINK_C + gap); ctx.lineTo(cx + nub, LINK_C + gap);
      ctx.stroke();
    }
    ctx.restore();

    /* ── 아래 라벨 ─────────────────────────────────── */
    if (spec.headLabel) {
      ctx.globalAlpha = appear;
      ctx.textAlign = 'center';
      ctx.font = disp(800, fitFont(spec.headLabel, 800, 12, w - 80));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.headLabel, cx, 214);
      ctx.globalAlpha = 1;
    }

    /* ── 사람 : 넷뿐이다 ───────────────────────────── */
    const n = Math.max(1, spec.heads ?? 4);
    const pitch = Math.min(64, (w - 80) / n);
    const x0 = cx - pitch * (n - 1) / 2;
    for (let i = 0; i < n; i++) {
      const x = x0 + i * pitch;
      const k = ease(clamp((headOn - i * 0.10) / 0.60));

      /* 켜지기 전 — 빈 윤곽의 점선이 흐른다(정적 방지) */
      if (k < 0.98) {
        ctx.save();
        ctx.globalAlpha = appear * (1 - k) * 0.8;
        ctx.setLineDash([5, 6]);
        ctx.lineDashOffset = (t * 11) % 11;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.arc(x, MARK_Y, MARK_R, 0, Math.PI * 2); ctx.stroke();
        ctx.restore();
      }
      /* 켜진 뒤 — 통통 뛴다 */
      if (k > 0.02) {
        const my = MARK_Y + Math.sin(t * 4.8 + i * 1.1) * 3 * k;
        ctx.save();
        ctx.globalAlpha = clamp(k * 1.5);
        ctx.fillStyle = tone('ink');
        ctx.beginPath(); ctx.arc(x, my, MARK_R * k, 0, Math.PI * 2); ctx.fill();
        ctx.restore();
      }
    }

    ctx.textAlign = 'left';
  }
};
