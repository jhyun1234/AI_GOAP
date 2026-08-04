import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* verdict — 이 편이 남기는 것은 기능이 아니라 한 마디다.

   위쪽 띠에서는 명령 표식이 주민 앞 막에 부딪혀 되돌아가기를 **끝없이** 반복한다.
   첫 샷(obey)에서 한 번 일어난 일이 마지막에는 이 마을의 상시 상태가 되어 있다 —
   그래서 이건 사건이 아니라 성질이고, 자막에 안 물리고 t 로 돈다.

   그 아래가 이 샷의 본론이다. 원문은 성공 기준을 이렇게 적어 두었다: 한 판 후 '남에게
   말하고 싶은 순간이 있었나'라는 질문에 '쟤가 배고프다고 명령을 거절했어'라는 대답이
   나오는 것. 그래서 질문은 작은 한 줄로 위에 붙고, 대답이 큰 카드로 앉고, 기술 지표
   (테스트 19개 · Day 157)는 카드 아래 가장 작은 글씨로 내려간다.
   🔴 **크기의 순서가 곧 원문이 말한 우선순위다** — "기술적 완성도가 목적이 아니라,
   그 한 마디가 나오는 경험이 목적".

   🔴 훅과 다음 편 칩을 **마지막 자막(예고)에 물렸다.** 카드가 앉고 나면 그 뒤 3.3초 동안
   화면에 새로 일어나는 일이 없어 통째로 멈춘다 — ep05s 가 그렇게 정적 구간 6.6초를
   만들었다. 예고를 읽는 동안 아래에서 둘이 올라오면 화면이 산다.

   계속 도는 것 = 위쪽 띠의 왕복(2.6초)과 그 경로의 점선 흐름. */

const LOOP = 2.6;
const FLOW = 2.0;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const qk = ease(cue(spec.quoteCue ?? 0, 0.15, 0.5));
    const hkk = ease(cue(spec.hookCue ?? 1, 0.15, 0.5));
    const nkk = ease(cue(spec.nextCue ?? spec.hookCue ?? 1, 0.15, 0.45));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* ── 계속 도는 것 — 되돌아오는 명령 ───────────── */
    {
      const VX = 238, VY = 40;

      if (spec.loopLabel) {
        ctx.textAlign = 'left';
        const fs = fit(spec.loopLabel, 700, 10.5, 80, 8);
        ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.loopLabel, 8, VY + 4);
      }

      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([6, 8]);
      ctx.lineDashOffset = -frac(t / FLOW) * 14;
      ctx.beginPath(); ctx.moveTo(96, VY); ctx.lineTo(210, VY); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      ctx.beginPath(); ctx.arc(VX, VY, 15, 0, Math.PI * 2); ctx.stroke();

      setShadow(ctx, GLOW, 10, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.arc(VX, VY, 26, Math.PI - 0.8, Math.PI + 0.8); ctx.stroke();
      clearShadow(ctx);

      const u = frac(t / LOOP);
      const go = ease(clamp(u / 0.5));
      const back = ease(clamp((u - 0.58) / 0.42));
      const mx = lerp(100, 206, go) - 48 * back;
      ctx.globalAlpha = clamp(Math.sin(Math.PI * u) * 1.6);
      ctx.fillStyle = tone('ink');
      ctx.fillRect(mx - 5.5, VY - 3, 11, 6);
      ctx.globalAlpha = 1;
    }

    /* ── 질문은 작게 ──────────────────────────────── */
    if (qk > 0.02 && spec.askLabel) {
      ctx.globalAlpha = clamp(qk * 2.6);
      ctx.textAlign = 'left';
      const fs = fit(spec.askLabel, 700, 10.5, w - 16, 7.5);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.askLabel, 8, 86);
      ctx.globalAlpha = 1;
    }

    /* ── 대답은 크게 ──────────────────────────────── */
    if (qk > 0.02 && spec.quote) {
      const k = clamp(qk * 1.6);
      const s = Math.min(1, spring(k));
      const cx = 8, cy = 100, cw = w - 16, chh = 62;
      ctx.globalAlpha = k;
      setShadow(ctx, GLOW, 18, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      roundRect(ctx, cx + (cw - cw * s) / 2, cy + (chh - chh * s) / 2, cw * s, chh * s, 5);
      ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      const fs = fit(spec.quote, 900, 20, cw - 26, 12);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.quote, cx + cw / 2, cy + 39);
      ctx.globalAlpha = 1;
    }

    /* ── 지표는 가장 작게 ─────────────────────────── */
    const stats = spec.stats || [];
    if (qk > 0.45 && stats.length) {
      const k = clamp((qk - 0.45) / 0.55);
      ctx.globalAlpha = k * 0.95;
      ctx.textAlign = 'left';
      stats.slice(0, 2).forEach((s, i) => {
        const fs = fit('· ' + s, 700, 10.5, w - 24, 7.5);
        ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
        ctx.fillText('· ' + s, 8, 190 + i * 18);
      });
      ctx.globalAlpha = 1;
    }

    /* ── 다음 편 — 예고 자막과 함께 올라온다 ───────── */
    if (spec.next && nkk > 0.02) {
      const k = clamp(nkk * 1.6);
      const s = Math.min(1, spring(k));
      const cx = 8, cy = 224, cw = w - 16, chh = 30;
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      roundRect(ctx, cx + (cw - cw * s) / 2, cy + (chh - chh * s) / 2, cw * s, chh * s, 3);
      ctx.stroke();

      ctx.textAlign = 'left';
      const lab = spec.next.label || '';
      const lfs = fit(lab, 800, 11, 120, 8.5);
      ctx.font = disp(800, lfs); ctx.fillStyle = tone('accent');
      ctx.fillText(lab, cx + 12, cy + 20);
      const lw = ctx.measureText(lab).width;

      const topic = spec.next.topic || '';
      if (topic) {
        ctx.textAlign = 'right';
        const tfs = fit(topic, 700, 11.5, cw - 36 - lw, 8);
        ctx.font = disp(700, tfs); ctx.fillStyle = tone('ink');
        ctx.fillText(topic, cx + cw - 12, cy + 20);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 남는 한 줄 ───────────────────────────────── */
    /* 🔴 훅 카드는 여기서 그리지 않는다(2026-08-04 사용자 판정).
       .outrocard 가 .vis 와 좌표가 한 픽셀도 다르지 않고 배경이 불투명이라, 마지막
       OUTRO_MS(2.6초) 동안 이 캔버스는 통째로 덮인다 — 검수 실측으로 ep04s-3 은 훅이
       단 한 프레임도 보인 적이 없었고(카드 등장 35,765ms vs 예고 자막 35,776ms),
       가장 오래 보인 ep05s-1 도 100ms 였다. 훅은 engine/index.html 의 .oc-hook 이 맡아
       카드 안에서 2.6초 내내 서 있다. kind 는 그림에만 집중한다.
       🔑 "두 곳에 적으면 갈린다"가 원래 원칙이었는데 카드와 캔버스 사이에서 깨져 있었다. */
ctx.textAlign = 'left';
  }
};
