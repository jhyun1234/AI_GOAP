import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* p0gate — 벽 전체를 치우지 않고, 두 목표에게만 틈을 낸다.

   이 편(5-1)의 **네 번째 샷**이고 M2 의 마지막 잔무다.
   가로로 놓인 띠가 실패 쿨다운이다. 남은 시간이 왼쪽에서 오른쪽으로 계속 흐른다.
   그 위에 목표 카드 넷이 내려와 띠에 막히고, 카드마다 ✕ 가 뜬다(blockCue).
   skipCue 에서 굶주림·피로 두 장에만 P0 태그와 SkipFailureCooldown 플래그가 붙고,
   그 두 장 밑의 띠에만 틈이 벌어져 카드가 아래로 내려가 발동한다. 나머지 둘은 그대로 막혀
   있다 — 쿨다운을 없앤 게 아니라 예외를 하나 뚫었다는 뜻이 그림에 남아야 한다.

   🔴 **이건 예방 조치다.** 원문은 "발동 못하는 사태를 **막기 위해**" 플래그를 넣었다고
   썼지 실제로 주민이 굶었다고 쓰지 않았다. 그래서 화면에 시체·경고·붉은 사고 표시가
   하나도 없고, 상단 riskNote 가 "발동 못 하는 사태를 막으려고"라고 조건법으로만 적는다.
   ✕ 는 "이렇게 되면 못 나간다"는 가정의 그림이지 벌어진 사고의 기록이 아니다.

   🔴 앞선 회차가 쿨다운을 '닫힌 고리에 걸리는 걸쇠'로 그린 적이 있다. 여기서는 고리를
   한 번도 쓰지 않는다. 이 편의 쿨다운은 돌아가는 것이 아니라 **가로로 누워 길을 막는
   것**이고, 사건은 그 위를 도는 것이 아니라 **뚫고 내려가는 것**이다.

   계속 도는 것 = 띠 안을 흐르는 남은 시간, 막힌 카드의 미세한 떨림. */

const FLOW = 2.6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const bk = ease(cue(spec.blockCue ?? 0, 0.15, 0.60));
    const pk = ease(cue(spec.skipCue ?? 1, 0.15, 0.60));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const goals = spec.goals || [];
    const n = goals.length || 4;
    const CW = 74, GAP = 12;
    const total = n * CW + (n - 1) * GAP;
    const SX = (w - total) / 2;
    const REST = 100, CH = 30;
    const BAND_Y = 152, BAND_H = 28;
    const PASS_Y = 196;

    /* 통과 진행률 — skipCue 하나에서만 갈린다 */
    const open = ease(clamp((pk - 0.12) / 0.4));          // 틈이 벌어지는 정도
    const walk = ease(clamp((pk - 0.35) / 0.5));          // 카드가 내려가는 정도
    const isP0 = i => !!(spec.p0Index || [0, 1]).includes(i);

    /* ── 왜 이 예외가 필요했나 — 조건법으로만 적는다 ── */
    if (bk > 0.02 && spec.riskNote) {
      ctx.globalAlpha = clamp(bk * 2.4);
      ctx.textAlign = 'left';
      const fs = fit(spec.riskNote, 700, 10.5, w - 28, 8);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.riskNote, 14, 26);
      ctx.globalAlpha = 1;
    }

    /* ── 쿨다운 띠 — 틈을 뺀 나머지 구간만 그린다 ── */
    {
      const k = clamp(bk * 2);
      const notches = [];
      if (open > 0.01) {
        for (let i = 0; i < n; i++) {
          if (!isP0(i)) continue;
          const cx = SX + i * (CW + GAP) + CW / 2;
          const nw = (CW + 8) * open;
          notches.push([cx - nw / 2, cx + nw / 2]);
        }
      }
      const segs = [];
      let cursor = 14;
      notches.sort((a, b) => a[0] - b[0]).forEach(([a, b]) => {
        if (a > cursor) segs.push([cursor, a]);
        cursor = Math.max(cursor, b);
      });
      if (cursor < w - 14) segs.push([cursor, w - 14]);

      ctx.globalAlpha = k;
      /* 🔴 문턱을 2 → 6 으로 올린다. 틈 둘이 벌어지면 그 사이에 폭 4px 짜리 조각이
         하나 남는데(notch0=[6,88] · notch1=[92,174] → seg [88,92]), 그건 "막혀 있는 띠"가
         아니라 편집 부스러기로 읽힌다. 6 이면 사라지고, 진짜 남는 띠는 [174,338] 하나다. */
      segs.forEach(([a, b]) => {
        if (b - a < 6) return;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        roundRect(ctx, a, BAND_Y, b - a, BAND_H, 3); ctx.stroke();

        /* 계속 도는 것 — 띠 안을 흐르는 남은 시간 */
        ctx.save();
        ctx.beginPath(); ctx.rect(a + 2, BAND_Y + 2, b - a - 4, BAND_H - 4); ctx.clip();
        for (let i = 0; i < 3; i++) {
          const u = frac(t / FLOW + i / 3);
          const x = lerp(10, w - 10, u);
          ctx.globalAlpha = k * 0.26 * Math.sin(Math.PI * u);
          ctx.fillStyle = tone('ink');
          ctx.fillRect(x - 16, BAND_Y + 2, 32, BAND_H - 4);
        }
        ctx.restore();
        ctx.globalAlpha = k;
      });

      /* 🔴 라벨은 "아직 막혀 있는 띠" 위에 선다 — 뚫린 자리 위가 아니다.
         고정 x=22 였을 때 틈이 벌어지면 라벨만 화면 왼쪽에 남고 그 아래는 통째로
         뚫려 있어서, 이 편의 요점(쿨다운을 없앤 게 아니라 예외를 뚫었다)이 거꾸로
         읽혔다(검수 4-1). 남은 세그먼트 중 가장 넓은 것을 따라간다 — 틈이 안 열린
         동안에는 [14,338] 하나뿐이라 예전과 같은 왼쪽 자리에 선다. */
      const wide = segs.filter(([a, b]) => b - a >= 6).sort((p, q) => (q[1] - q[0]) - (p[1] - p[0]))[0];
      const labX = wide ? Math.min(wide[0] + 8, w - 130) : 22;
      ctx.textAlign = 'left';
      const fs = fit(spec.barrier || '실패 쿨다운', 800, 11.5, 120, 8);
      /* 🔴 baseline 을 BAND_Y−9 가 아니라 BAND_Y−4 로 둔다. −9 이면 라벨 글자 상자가
         y 133~145 를 차지하는데 아래 ✕ 가 그 위를 지나 취소선처럼 보였다(검수 반려 이력).
         −4 이면 글자 상자가 y 138~148 이고 ✕ 는 띠 한가운데(y 160~172)라 완전히 갈린다. */
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.barrier || '실패 쿨다운', labX, BAND_Y - 4);
      ctx.globalAlpha = 1;
    }

    /* ── 목표 카드 ────────────────────────────────── */
    goals.forEach((name, i) => {
      const born = clamp(bk * (n + 0.6) - i * 0.5);
      if (born < 0.02) return;
      const x = SX + i * (CW + GAP);
      const p0 = isP0(i);
      const drop = ease(clamp(born / 0.8));
      let y = lerp(REST - 30, REST, drop);
      const alpha = clamp(born * 1.8);

      if (p0 && walk > 0.01) y = lerp(REST, PASS_Y, walk);
      else if (!p0 && born > 0.9) y += 1.5 * Math.sin(frac(t / 1.1) * Math.PI * 2 + i);

      const col = p0 && pk > 0.15 ? tone('accent') : tone('ink');
      ctx.globalAlpha = alpha;
      ctx.lineWidth = 3; ctx.strokeStyle = col;
      if (p0 && walk > 0.6) setShadow(ctx, GLOW, 12, 0);
      roundRect(ctx, x, y, CW, CH, 3); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      const fs = fit(name, 800, 12.5, CW - 12, 8.5);
      ctx.font = disp(800, fs); ctx.fillStyle = col;
      ctx.fillText(name, x + CW / 2, y + CH / 2 + 5);

      // P0 태그
      if (p0 && pk > 0.05) {
        const k = clamp(pk * 2);
        ctx.globalAlpha = alpha * k;
        ctx.textAlign = 'left';
        ctx.font = mono(700, 9.5); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.p0 || 'P0', x + 2, y - 6);
      }

      // 막힘 표시 — 통과하지 못한 카드에만, 막는 주체인 띠 위에 남는다
      if (born > 0.75 && !(p0 && walk > 0.15)) {
        const k = clamp((born - 0.75) / 0.25);
        const mx = x + CW / 2, my = BAND_Y + BAND_H / 2;
        ctx.globalAlpha = alpha * k;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(mx - 6, my - 6); ctx.lineTo(mx + 6, my + 6);
        ctx.moveTo(mx + 6, my - 6); ctx.lineTo(mx - 6, my + 6);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;
    });

    /* ── 플래그와 발동 ────────────────────────────── */
    if (pk > 0.2 && spec.flag) {
      const k = clamp((pk - 0.2) / 0.4);
      const bw = 214, bx = (w - bw) / 2, by = 238;
      const s = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      roundRect(ctx, bx + (bw - bw * s) / 2, by + (26 - 26 * s) / 2, bw * s, 26 * s, 3);
      ctx.stroke();
      ctx.textAlign = 'center';
      let fs = 11; ctx.font = mono(700, fs);
      while (fs > 7 && ctx.measureText(spec.flag).width > bw - 18) { fs -= 0.5; ctx.font = mono(700, fs); }
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.flag, w / 2, by + 18);
      ctx.globalAlpha = 1;
    }

    if (pk > 0.55 && spec.fireLabel) {
      const k = clamp((pk - 0.55) / 0.45);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.fireLabel, 900, 15, w - 30, 10);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.fireLabel, w / 2, 290);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
