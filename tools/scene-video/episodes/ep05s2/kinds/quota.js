import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* quota — 건물은 길을 끊고, 공사에는 정원이 있다.

   가운데 집. blockCue 에서 마을을 가로지르던 통행선이 집 테두리에 닿아 끊기고 양쪽에
   ✕ 가 남는다(BlocksMovement). quotaCue 에서 아래쪽 일꾼들이 공사 문으로 올라오는데,
   앞선 둘이 들어가고 나면 빗장이 내려와 문을 닫고 남은 일꾼들은 방향을 틀어 물러난다.

   🔴 직전 편이 '한 타일에 겹쳐 ✕ → 반경으로 흩어짐'을 이미 그렸다. 여기서는 겹치는
   장면도 흩어지는 장면도 없다. 일꾼들은 처음부터 각자 다른 줄로 올라오고, 문제는 자리가
   겹치는 것이 아니라 **들어갈 수 있는 인원에 상한이 있다**는 것이다. 그래서 사건은
   충돌 표시가 아니라 **닫히는 빗장** 하나다.

   🔴 원문은 MaxWorkers 의 값을 말하지 않는다. 그래서 일꾼 수도 통과 인원도 화면에
   숫자로 적지 않는다 — 다섯과 둘은 도형 파라미터일 뿐이다.

   계속 도는 것 = 통행선 위를 흐르는 파선, 물러난 일꾼들의 걸음, 끊긴 자리 앞에서
   되돌아가는 점. 통행선은 처음엔 이어져 있고 집이 앉으면서 끊긴다. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const bk = ease(cue(spec.buildCue ?? 0, 0.15, 0.6));
    const mk = ease(cue(spec.blockCue ?? 1, 0.15, 0.62));
    const qk = ease(cue(spec.quotaCue ?? 2, 0.15, 0.75));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const HX = 132, HW = 88, HTOP = 58, HBOT = 134;
    const APEX = 26, EAVE = 122;
    const PATH_Y = 104;
    const GATE_Y = 200, GX0 = 106, GX1 = 246;

    /* ── 집 ───────────────────────────────────────── */
    if (bk > 0.02) {
      const k = clamp(bk * 2);
      const s = Math.min(1, spring(clamp(bk * 1.3)));
      ctx.globalAlpha = k;
      ctx.save();
      ctx.translate(176, (HTOP + HBOT) / 2);
      ctx.scale(s, s);
      ctx.translate(-176, -(HTOP + HBOT) / 2);

      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      setShadow(ctx, GLOW, 12, 0);
      roundRect(ctx, HX, HTOP, HW, HBOT - HTOP, 3); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(EAVE, HTOP); ctx.lineTo(176, APEX); ctx.lineTo(352 - EAVE, HTOP);
      ctx.stroke();
      clearShadow(ctx);

      // 안쪽 빗금 — 통과 못 하는 면
      ctx.save();
      ctx.beginPath(); ctx.rect(HX + 3, HTOP + 3, HW - 6, HBOT - HTOP - 6); ctx.clip();
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      for (let i = -3; i < 9; i++) {
        const x = HX + i * 14;
        ctx.beginPath();
        ctx.moveTo(x, HBOT); ctx.lineTo(x + (HBOT - HTOP), HTOP);
        ctx.stroke();
      }
      ctx.restore();
      ctx.restore();
      ctx.globalAlpha = 1;
    }

    /* ── 통행선 — 이어져 있다가 집이 앉으면서 끊긴다 ────
       🔴 2026-08-04 검수 2차 반려. 1차 때 "선과 점을 bk 로 올렸다"고 적었는데 **선은 안 고쳤다.**
       moveTo(16)→lineTo(HX) / moveTo(HX+HW)→lineTo(w−16) 두 토막이라 태어날 때부터 끊겨 있었고,
       게이트만 mk→bk 로 옮긴 것이었다. 그래 놓고 주석에 "끊기지 않은 길 위로 집이 떨어진다"고
       써서 거짓 자기보고를 하나 더 얹었다(같은 종류 3연속 — ep04s2 §4-E · 1차 §1 · 2차).
       실측도 그대로였다: 자막 0 구간 m = 0.000747 로 임계의 0.93배, check 최대 정적 3.0s 로
       게이트(>3.0)를 한 프레임 차로 통과했을 뿐이다. 점(201px²)만 움직였기 때문이다.

       이제 진짜로 잇는다. cut 이 0 이면 두 토막이 집 한가운데서 만나 한 줄이 되고,
       mk 가 오르면 집 폭만큼 벌어진다. 흐르는 파선을 얹어 폭 전체가 매 프레임 바뀌게 했다 —
       "마을을 가로지르던 통행선"이 화면에서 참이 되고, 검수 실험본이 m 0.002764(임계 3.46배) ·
       최대 정적 1.0s 를 낸 것이 이 처방이다. */
    if (bk > 0.02) {
      const k = clamp(bk * 1.8) * (mk > 0.02 ? 1 : 0.75);
      const cut = mk > 0.02 ? clamp(mk * 1.8) : 0;
      const hcx = HX + HW / 2;
      const gL = lerp(hcx, HX, cut), gR = lerp(hcx, HX + HW, cut);

      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.setLineDash([14, 10]);
      ctx.lineDashOffset = -frac(t / 1.8) * 24;   // 계속 도는 것 — 길 위를 흐르는 통행
      ctx.beginPath();
      ctx.moveTo(16, PATH_Y); ctx.lineTo(gL, PATH_Y);
      ctx.moveTo(gR, PATH_Y); ctx.lineTo(w - 16, PATH_Y);
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.lineDashOffset = 0;

      const xk = mk > 0.02 ? clamp((mk - 0.3) / 0.4) : 0;
      if (xk > 0.02) {
        ctx.globalAlpha = k * xk;
        [HX, HX + HW].forEach(mx => {
          ctx.beginPath();
          ctx.moveTo(mx - 7, PATH_Y - 7); ctx.lineTo(mx + 7, PATH_Y + 7);
          ctx.moveTo(mx + 7, PATH_Y - 7); ctx.lineTo(mx - 7, PATH_Y + 7);
          ctx.stroke();
        });
      }

      /* 계속 도는 것 — 끊긴 자리 앞에서 되돌아가는 점 */
      const u = 0.5 - 0.5 * Math.cos(frac(t / 2.4) * Math.PI * 2);
      ctx.globalAlpha = k * 0.85;
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(lerp(24, HX - 12, u), PATH_Y, 4, 0, Math.PI * 2); ctx.fill();
      ctx.beginPath(); ctx.arc(lerp(w - 24, HX + HW + 12, u), PATH_Y, 4, 0, Math.PI * 2); ctx.fill();

      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      ctx.font = mono(700, 10); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.blocks || 'BlocksMovement', 16, PATH_Y - 13);
      ctx.globalAlpha = 1;
    }

    if (mk > 0.5 && spec.standNote) {
      const k = clamp((mk - 0.5) / 0.5);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.standNote, 700, 11, w - 40, 8);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.standNote, w / 2, 152);
      ctx.globalAlpha = 1;
    }

    /* ── 공사 문과 정원 ───────────────────────────── */
    if (qk > 0.02) {
      const k = clamp(qk * 2);

      // 문턱
      ctx.globalAlpha = k * 0.8;
      ctx.setLineDash([8, 6]);
      ctx.lineDashOffset = -(t * 11) % 14;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(GX0, GATE_Y); ctx.lineTo(GX1, GATE_Y); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;

      // 일꾼 — 앞의 둘만 들어간다
      const lanes = [56, 116, 176, 236, 296];
      const away = [-30, 0, 0, 56, 40];
      const passIdx = [2, 1];
      const walk = ease(clamp(qk / 0.45));
      const turn = ease(clamp((qk - 0.45) / 0.4));

      lanes.forEach((lane, i) => {
        const pass = passIdx.includes(i);
        let x = lane, y = 268;
        if (pass) {
          const order = passIdx.indexOf(i);
          const p = ease(clamp((walk - order * 0.16) / 0.7));
          y = lerp(268, 188, p);
        } else {
          x = lane + away[i] * turn;
          y = 268 + 3 * Math.sin(frac(t / 1.3) * Math.PI * 2 + i);
        }
        ctx.globalAlpha = k * (pass ? 1 : 1 - 0.25 * turn);
        ctx.fillStyle = pass ? tone('accent') : tone('ink');
        roundRect(ctx, x - 7, y - 20, 14, 20, 3); ctx.fill();
        ctx.beginPath(); ctx.arc(x, y - 26, 5.5, 0, Math.PI * 2); ctx.fill();
        ctx.globalAlpha = 1;
      });

      // 빗장이 내려와 문을 닫는다
      const bar = ease(clamp((qk - 0.45) / 0.35));
      if (bar > 0.01) {
        const by = lerp(GATE_Y - 26, GATE_Y - 5, bar);
        ctx.globalAlpha = clamp(bar * 1.8);
        setShadow(ctx, GLOW, 12, 0);
        ctx.fillStyle = tone('accent');
        roundRect(ctx, GX0, by, GX1 - GX0, 10, 3); ctx.fill();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }

      // 이름표 — 빗장 양옆
      if (qk > 0.4) {
        const kk = clamp((qk - 0.4) / 0.4);
        ctx.globalAlpha = kk;
        ctx.textAlign = 'left';
        let fs = 9.5; ctx.font = mono(700, fs);
        const nm = spec.quota || '';
        while (fs > 6.5 && ctx.measureText(nm).width > GX0 - 24) { fs -= 0.5; ctx.font = mono(700, fs); }
        ctx.fillStyle = tone('sub');
        ctx.fillText(nm, 12, GATE_Y + 4);

        ctx.textAlign = 'right';
        const nfs = fit(spec.quotaNote || '정원제', 900, 15, w - GX1 - 18, 10);
        ctx.font = disp(900, nfs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.quotaNote || '정원제', w - 12, GATE_Y + 6);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
