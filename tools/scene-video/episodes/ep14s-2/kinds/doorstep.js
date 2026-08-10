import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* doorstep — 문 앞. 값을 낼 수 있는 사람이 매번 같은 자리에서 되밀린다.

   원문 근거:
   > "곡식 6개를 지닌 의뢰인이 고집쟁이 목수에게 집을 부탁했는데 '선불 없이는 안 해'로
   >  거절당했습니다. 의뢰인 잔고는 충분했습니다."

   🔴 이 회차의 기본 도형은 **「받는 쪽에 자리가 없어서, 건너가려던 것이 같은 자리에서
   튕겨 돌아온다」** 한 문장이다. 여기는 그 문장의 첫 변주 — 튕기는 것이 사람이다.
   뒤에서 S1(품삯 알)·S2(줄마다 똑같이)로 같은 튕김이 되풀이되고, S3 에서 처음으로 넘어간다.
   회차 안 되받기는 전부 「뒤집기」 조건을 만족한다.

   🔴 벽을 왼쪽 사람 앞이 아니라 **오른쪽 사람 바로 앞**에 세웠다. 거절은 의뢰인의
   사정이 아니라 목수 쪽에서 나온 것이고, 다음 샷에서 그 벽의 정체가 목수 주머니 끝이었음이
   드러난다 — 같은 그림 어휘를 두 번 쓰는 것이 여기서는 반복이 아니라 **폭로**다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — 좌표로 갈랐다.
     강조색 = 말풍선 y 48~88(+꼬리 116, 글로우 포함 x 165~343)
            · 벽 x 231~237(글로우 최대 213~255), y 100~176(글로우 82~194).
     흰색   = 표식 원 y 121~155 (x ≤ 203 / 279~313) · 「목수」 y 172~190 (x 277~315)
            · 주머니 y 208~246 · 「의뢰인 곡식 6개」 y 258~276.
     즉 **벽 글로우 띠(x 213~255)에는 어떤 흰 요소도 들어오지 않는다** — 다가가는 표식의
     오른쪽 끝이 203 에서 멈추도록 이동 폭을 잡았다(부등식: cx ≤ 186, 186 + 17 = 203 < 213).

   🔴 캔버스 밖으로 안 나간다: 왼쪽 최소 = 주머니 30, 오른쪽 최대 = 말풍선 글로우 343
      (캔버스 352, 가장자리 판정 띠는 바깥 2열). 세로 최저 = 272 베이스라인(글립 ~276).

   ⏱ 튕김은 `t` 주기다(자막이 부르는 사건이 아니라 「계속 그러고 있다」는 상태이고,
      이 샷에는 효과음이 없어 TTS 와 물릴 이유가 없다). 곡식은 자막 1 의 `cue` 에 물렸다.
   계속 도는 것 = 표식 왕복(면적 큼) + 벽 두께·글로우 맥동 + 말풍선 알파 맥동. */

const CY = 138, R = 17;
const HOME_X = 62, NEAR_X = 186, CARP_X = 296;
const WALL_X = 234, WALL_T = 100, WALL_B = 176;
const BUB_L = 178, BUB_R = 330, BUB_T = 48, BUB_B = 88;
const POUCH_L = 30, POUCH_R = 170, POUCH_T = 208, POUCH_H = 38;
const GRAIN_Y = 227, GRAIN_R = 7, GRAIN_X0 = 46, GRAIN_DX = 22;

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
      ctx.fillText(txt, clamp(x, 12 + tw / 2, w - 12 - tw / 2), y);
      ctx.restore();
    };

    /* ── 구획 이름 ─────────────────────────────────── */
    if (spec.title) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    const c0 = cue(spec.askCue ?? 0, 0.15, 0.30);
    const st = ease(clamp(c0 / 0.5));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 다가갔다 되밀린다 ─────────────────────────── */
    const P = spec.knockSec ?? 1.9;
    const u = frac(t / P);
    const app = ease(clamp(u / 0.40));
    const rec = ease(clamp((u - 0.44) / 0.18));
    const cx = HOME_X + (NEAR_X - HOME_X) * app * (1 - rec);
    /* 부딪히는 순간 = 다가감이 최대인 자리. 벽이 굵어지고 밝아진다. */
    const hit = 1 - ease(clamp(Math.abs(u - 0.42) / 0.09));

    /* ── 벽 : 거절 ─────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    setShadow(ctx, GLOW, 8 + 10 * hit);
    ctx.strokeStyle = tone('accent');
    ctx.lineWidth = 4 + 2 * hit; ctx.lineCap = 'round';
    ctx.beginPath(); ctx.moveTo(WALL_X, WALL_T); ctx.lineTo(WALL_X, WALL_B); ctx.stroke();
    clearShadow(ctx);
    ctx.restore();

    /* ── 목수가 하는 말 ────────────────────────────── */
    const bub = ease(clamp((c0 - 0.10) / 0.35));
    if (bub > 0.01) {
      const ba = bub * (0.72 + 0.28 * hit);
      ctx.save();
      ctx.globalAlpha = ba;
      setShadow(ctx, GLOW, 10);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, BUB_L, BUB_T, BUB_R - BUB_L, BUB_B - BUB_T, 11); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(CARP_X, BUB_B); ctx.lineTo(CARP_X, 116); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
      ctext(spec.refuseLine, (BUB_L + BUB_R) / 2, BUB_T + 27, 800, 13.5,
        tone('accent'), BUB_R - BUB_L - 24, ba);
    }

    /* ── 두 사람 ───────────────────────────────────── */
    const mark = (x, face) => {
      ctx.save();
      ctx.globalAlpha = st;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(x, CY, R, 0, Math.PI * 2); ctx.stroke();
      ctx.restore();
      ctext(face, x, CY + 5, 800, 12, tone('ink'), R * 2 - 8, st);
    };
    mark(cx, spec.clientFace);
    mark(CARP_X, spec.carpFace);
    ctext(spec.carpLabel, CARP_X, 186, 800, 12.5, tone('ink'), 78, st);

    /* ── 낼 수 있는 곡식 : 자막 1 ──────────────────── */
    const c1 = cue(spec.grainCue ?? 1, 0.15, 0.62);
    const pv = ease(clamp(c1 / 0.14));
    if (pv > 0.01) {
      ctx.save();
      ctx.globalAlpha = pv * 0.85;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, POUCH_L, POUCH_T, POUCH_R - POUCH_L, POUCH_H, 10); ctx.stroke();
      ctx.restore();

      const n = Math.max(1, spec.grains ?? 6);
      for (let i = 0; i < n; i++) {
        const gi = ease(clamp((c1 - 0.14 - 0.10 * i) / 0.20));
        if (gi <= 0.01) continue;
        ctx.save();
        ctx.globalAlpha = gi;
        ctx.fillStyle = tone('ink');
        ctx.beginPath();
        ctx.arc(GRAIN_X0 + i * GRAIN_DX, GRAIN_Y, GRAIN_R * (0.6 + 0.4 * gi), 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      }
      ctext(spec.pouchLabel, (POUCH_L + POUCH_R) / 2, 272, 900, 13, tone('ink'), 150, pv);
    }

    ctx.textAlign = 'left';
  }
};
