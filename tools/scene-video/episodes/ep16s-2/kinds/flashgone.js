import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* flashgone — 훅. 위 칸에 뭔가 반짝 떴다 사라지고, 아래 사람에게 가는 길은 도중에 끊긴다.

   원문 근거(M13 「사건과 흔적」):
   > "이 항목을 시작하면서 기존 알림이 발생하는 지점을 전수 조사했습니다. 열네 곳이
   >  나왔는데, 세어 보니 상태형 알림이 0개였습니다. 전부 순간 사건이었습니다."
   > "\"굶는 주민 — 식량 0일치\"라는 줄은 누구인지 모르는 정보입니다.
   >  누군지 모르면 개입을 못 합니다."

   🔴 이 회차의 색 규약을 여기서 세운다 — **강조색 = 그 사람에게 가 닿는 것 /
   흰색 = 화면에 있(었)던 것.** 훅에서는 강조색 길이 **끊긴다.** 네 그림(SH·S1·S2·S3)이
   이 규약을 지키고, S3 에서 처음으로 강조색이 사람까지 이어진다.

   🔴 §3-C 계약 — 무덤·죽음 장면을 그리지 않는다(그건 16-1 소유다). 여기서 그리는 것은
   **굶고 있는 사람**과 **알림 자리**이지 죽음이 아니다.

   루프(1.6초): 알림 자리에 흰 막대가 떴다가 곧 사라진다. 그 아래로 강조색 점선이
   뻗어 내려가다 사람에 닿기 전에 꺾쇠 하나만 남기고 끊긴다. 식량 막대(3.2초 루프)는
   그동안 계속 줄어들기만 한다.
   라벨을 다 가려도 「위에서 뭔가 깜빡했다 사라지고, 아래 사람에게 가는 길이 끊기고,
   그 사람의 막대는 계속 줄어든다」가 읽힌다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세로 좌표로 갈랐다.**
     · 알림 자리 바닥 76(선 반폭 1.5 포함 77.5) ↔ 강조색 점선 맨 위 96, 글로우(blur 8)까지 88.
       **10.5px** 뜬다.
     · 강조색 꺾쇠 바닥 = 96 + 34(최대 길이) + 4 + 6 = 140, 글로우까지 148.
       사람 원의 맨 위 = 190 − 32(숨을 다 먹은 반지름) − 1.5(선 반폭) = 156.5. **8.5px** 뜬다.
   흰↔흰(축 B): 「알림 자리」 라벨 글립 바닥 ≈ 27 ↔ 칸 윗선 42.5 = 15.5px /
     얼굴 글자 바닥 ≈ 200 ↔ 식량 막대 윗선 244 = 44px /
     식량 막대 바닥 256 ↔ 「식량」 라벨 글립 윗선 ≈ 263 = 7px(이 둘은 **의도한 쌍**이다).

   세로 예산(캔버스 307): 최저 = 「식량」 라벨 글립 바닥 ≈ 275. 32px 남는다.
   가로: 26~326. 캔버스 352, 좌우 26px씩 남는다.

   계속 도는 것(30fps 기준): 식량 막대가 140px 를 2.5초에 줄어든다 → **1.9px/프레임**
   (면적 12px 높이 × 140px 폭). + 원 반지름 숨 2px · 3.4rad/s + 1.6초 루프의 반짝임. */

const SLOT = { x0: 26, y0: 44, w: 300, h: 32 };
const FIG = { cx: 176, cy: 190, r: 30 };
const GAUGE = { x: 106, y: 244, w: 140, h: 12 };
const ARROW_TOP = 96, ARROW_MAX = 34, DOT_GAP = 9;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const st = ease(cue(spec.slotCue ?? 0, 0.12, 0.30));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.slotLabel) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.slotLabel, 20, 24);
      ctx.restore();
    }

    /* ── 알림 자리 (평시에는 완전히 빈다) ───────────── */
    ctx.save();
    ctx.globalAlpha = st * 0.4;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, SLOT.x0, SLOT.y0, SLOT.w, SLOT.h, 8);
    ctx.stroke();
    ctx.restore();

    const u = frac(t / (spec.loopSec ?? 1.6));

    /* 떴다가 곧 사라지는 알림 한 줄 */
    const on = ease(clamp((u - 0.04) / 0.05)) * (1 - ease(clamp((u - 0.20) / 0.08)));
    if (on > 0.01) {
      ctx.save();
      ctx.globalAlpha = st * on;
      ctx.fillStyle = tone('ink');
      roundRect(ctx, SLOT.x0 + 8, SLOT.y0 + 8, SLOT.w - 16, SLOT.h - 16, 5);
      ctx.fill();
      ctx.restore();
    }

    /* ── 그 사람에게 가는 길 — 뻗다가 끊긴다 ────────── */
    const grow = ease(clamp((u - 0.10) / 0.16));
    const keep = 1 - ease(clamp((u - 0.34) / 0.10));
    const len = ARROW_MAX * grow;
    const al = st * grow * keep;
    if (al > 0.01) {
      ctx.save();
      ctx.globalAlpha = al;
      setShadow(ctx, FAIL_GLOW, 8);
      ctx.fillStyle = tone('fail');
      for (let k = 0; k * DOT_GAP + 5 <= len; k++) {
        ctx.fillRect(FIG.cx - 3, ARROW_TOP + k * DOT_GAP, 6, 5);
      }
      const ty = ARROW_TOP + len + 4;
      ctx.strokeStyle = tone('fail'); ctx.lineWidth = 3;
      ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(FIG.cx - 6, ty); ctx.lineTo(FIG.cx, ty + 6); ctx.lineTo(FIG.cx + 6, ty);
      ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 굶고 있는 사람 ─────────────────────────────── */
    const r = FIG.r + 2 * (0.5 + 0.5 * Math.sin(t * 3.4));
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(FIG.cx, FIG.cy, r, 0, Math.PI * 2); ctx.stroke();
    ctx.textAlign = 'center';
    ctx.font = disp(800, 17); ctx.fillStyle = tone('ink');
    ctx.fillText('ㅠ_ㅠ', FIG.cx, FIG.cy + 6);
    ctx.restore();

    /* ── 계속 줄어들기만 하는 식량 ──────────────────── */
    const v = frac(t / (spec.gaugeSec ?? 3.2));
    const level = 1 - ease(clamp(v / 0.78));
    const ga = st * ease(clamp(v / 0.06)) * (1 - ease(clamp((v - 0.86) / 0.14)));
    if (ga > 0.01) {
      ctx.save();
      ctx.globalAlpha = ga * 0.22;
      ctx.fillStyle = tone('ink');
      roundRect(ctx, GAUGE.x, GAUGE.y, GAUGE.w, GAUGE.h, 6); ctx.fill();
      ctx.restore();
      if (level > 0.005) {
        ctx.save();
        ctx.globalAlpha = ga;
        ctx.fillStyle = tone('ink');
        roundRect(ctx, GAUGE.x, GAUGE.y, Math.max(8, GAUGE.w * level), GAUGE.h, 6); ctx.fill();
        ctx.restore();
      }
    }
    if (spec.foodLabel) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.foodLabel, GAUGE.x, GAUGE.y + GAUGE.h + 16);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
