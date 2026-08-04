import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* refusal — 명령이 주민 앞에서 자기 상태에 걸린다.

   앞 샷(obey)은 되튕김의 **결과**만 보여 줬다. 이 샷은 그 사이에 무엇이 있는지를 연다.
   명령 칩이 오른쪽에서 주민에게 다가오고, 주민이 자기 상태 둘을 꺼내 보고(같은 자막의
   뒷부분), 두 문턱이 숫자로 채워지고(thresholdCue), 조건에 걸리면 말풍선이 뜨면서 칩이
   되밀린다(refuseCue).

   🔴 말풍선 문구는 원문 그대로다. 이 편에서 화면에 올릴 것이 딱 하나라면 이 두 마디다 —
   나레이션은 둘 중 하나만 읽고, 나머지 하나는 화면에서만 읽힌다. 시청자가 스스로 읽은
   문장이 남는다.

   🔴 **한 자막 안에서 창을 앞뒤로 갈랐다.** 재분할로 이 샷의 자막이 4줄에서 3줄이 되면서
   첫 줄이 칩 도착과 상태 칸 등장을 함께 부르게 됐다("명령을 받으면 / 주민이 자기 상태부터
   봐요"). 그래서 ok 는 그 자막의 앞 35% 만 쓰고(칩이 먼저 앉는다), sk 는 같은 자막의
   뒤쪽 구간(raw 0.45~1.0)만 쓴다. 둘은 곱해지거나 빼지는 관계가 아니라 독립 경로라
   같은 cue 에 물려도 상쇄되지 않는다.

   🔴 세 번째 갈래(수락)도 같은 방식으로 뒤로 밀었다. 1차본에서는 이 갈래가 거부 말풍선보다
   **먼저** 뜨는 결함이 있었고(ep04s 검수 C-4), 지금은 nk 가 refuseCue 자막의 뒤쪽 구간
   (raw 0.55~1.0)이라 말풍선 둘 다음에 열린다. 거부만 있는 판정이 아니라는 것을 화면에서도
   세 갈래로 보여야 '랜덤이 아니라 상태 판정'이 성립한다.

   계속 도는 것 = 주민 발밑 선택 링의 점선 회전과 호흡. */

const RING = 3.0;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const ok = ease(cue(spec.orderCue ?? 0, 0.15, 0.35));
    const sk = ease(clamp((cue(spec.statCue ?? 0, 0.15, 1.0) - 0.45) / 0.55));
    const tk = ease(cue(spec.thresholdCue ?? 1, 0.15, 0.6));
    const rk = ease(cue(spec.refuseCue ?? 2, 0.15, 0.5));
    const nk = ease(clamp((cue(spec.notRandomCue ?? 2, 0.15, 0.95) - 0.55) / 0.45));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* ── 주민 + 선택 링 ───────────────────────────── */
    const vx = 56, vy = 74 + Math.sin(frac(t / 2.6) * Math.PI * 2) * 2;
    {
      ctx.globalAlpha = clamp(ok * 2);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      ctx.beginPath(); ctx.arc(vx, vy, 17, 0, Math.PI * 2); ctx.stroke();

      /* 계속 도는 것 — 발밑 선택 링 */
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([7, 6]);
      ctx.lineDashOffset = -frac(t / RING) * 13;
      ctx.beginPath();
      ctx.ellipse(vx, vy + 24, 26, 8, 0, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      ctx.textAlign = 'center';
      ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.villager || '주민', vx, vy + 48);
      ctx.globalAlpha = 1;
    }

    /* ── 명령 칩 — 다가왔다 밀려난다 ──────────────── */
    {
      const app = ease(clamp(ok / 0.85));
      const back = ease(clamp(rk / 0.7));
      const cw = 92, chh = 26;
      const cx = lerp(w - 8 - cw, 96, app) + back * 74;
      const cy = vy - chh / 2;

      ctx.globalAlpha = clamp(ok * 2.4) * (1 - back * 0.45);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, cx, cy, cw, chh, 3); ctx.stroke();
      ctx.textAlign = 'center';
      const s = spec.orderLabel || '촌장의 명령';
      const fs = fit(s, 800, 11.5, cw - 12);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(s, cx + cw / 2, cy + 18);
      ctx.globalAlpha = 1;
    }

    /* 거부 — 주민 앞에 막이 선다 */
    if (rk > 0.08) {
      const k = clamp((rk - 0.08) / 0.4);
      ctx.globalAlpha = k;
      setShadow(ctx, GLOW, 12, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.arc(vx, vy, 29, -0.9 * k, 0.9 * k);
      ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 판정 두 갈래 ─────────────────────────────── */
    const tests = spec.tests || [];
    const cw = 132, rowY = [132, 180], rh = 40;
    tests.slice(0, 2).forEach((tst, i) => {
      const y = rowY[i];
      const born = clamp(sk * 2.2 - i * 0.5);
      if (born < 0.02) return;

      ctx.globalAlpha = clamp(born * 1.6);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, 8, y, cw, rh, 3); ctx.stroke();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12); ctx.fillStyle = tone('ink');
      ctx.fillText(tst.stat || '', 18, y + 18);

      // 문턱은 뒤에 채워진다
      const th = clamp(tk * 2.2 - i * 0.5);
      if (th > 0.05) {
        ctx.globalAlpha = clamp(born * 1.6) * clamp(th * 1.5);
        const s = tst.cmp || '';
        const fs = fit(s, 900, 14, cw - 22, 10);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(s, 18, y + 34);
      }
      ctx.globalAlpha = 1;

      // 화살표
      if (th > 0.4) {
        const k = clamp((th - 0.4) / 0.4);
        ctx.globalAlpha = k;
        ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
        const ax = 8 + cw + 4, ay = y + rh / 2;
        ctx.beginPath();
        ctx.moveTo(ax, ay); ctx.lineTo(ax + 12 * k, ay);
        ctx.moveTo(ax + 12 * k, ay); ctx.lineTo(ax + 12 * k - 5, ay - 4);
        ctx.moveTo(ax + 12 * k, ay); ctx.lineTo(ax + 12 * k - 5, ay + 4);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }

      // 말풍선
      const bk = clamp(rk * 2.1 - i * 0.55);
      if (bk > 0.03) {
        const k = clamp(bk * 1.4);
        const s = Math.min(1, spring(k));
        const bx = 8 + cw + 22, bw = w - 8 - bx;
        ctx.globalAlpha = k;
        setShadow(ctx, GLOW, i === 0 ? 14 : 8, 0);
        ctx.lineWidth = i === 0 ? 4 : 3;
        ctx.strokeStyle = tone('accent');
        roundRect(ctx, bx + (bw - bw * s) / 2, y + (rh - rh * s) / 2, bw * s, rh * s, 4);
        ctx.stroke();
        clearShadow(ctx);
        ctx.textAlign = 'center';
        const txt = tst.say || '';
        const fs = fit(txt, 800, 12.5, bw - 16, 8.5);
        ctx.font = disp(800, fs * s); ctx.fillStyle = tone('accent');
        ctx.fillText(txt, bx + bw / 2, y + rh / 2 + 5);
        ctx.globalAlpha = 1;
      }
    });

    /* ── 세 번째 갈래 ─────────────────────────────── */
    if (nk > 0.03 && spec.accept) {
      const k = clamp(nk * 1.6);
      const y = 230, bh = 32;
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
      ctx.setLineDash([7, 6]);
      roundRect(ctx, 8, y, w - 16, bh, 3); ctx.stroke();
      ctx.setLineDash([]);
      ctx.textAlign = 'center';
      const fs = fit(spec.accept, 800, 12.5, w - 34, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.accept, w / 2, y + 21);
      ctx.globalAlpha = 1;
    }

    if (nk > 0.4 && spec.assetNote) {
      const k = clamp((nk - 0.4) / 0.5);
      ctx.globalAlpha = k * 0.95;
      ctx.textAlign = 'left';
      const fs = fit(spec.assetNote, 700, 10.5, w - 16, 7.5);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.assetNote, 8, 282);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
