import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* blindspot — 나가는 문이 없는 구간.

   가로 축은 피로다. 70 을 넘으면 명령을 거부한다(holeCue). 문제는 그 오른쪽에 뚫려 있던
   구간이다 — 쉬어도 피로가 안 줄면 거부가 '일시적인 상태'가 아니라 사실상 영구가 된다
   (stuckCue). 주민 표식이 그 구간 안에서 좌우로만 떨고 왼쪽으로 못 나간다.

   fixCue 에서 회복이 열린다. 산책과 모닥불 곁 휴식이 각각 왼쪽으로 미는 화살표로 붙고,
   모닥불 쪽이 더 길다 — 건물이 존재하는 의미를 회복량으로도 나타낸 설계라, 두 화살표의
   길이 차이 자체가 그 뜻이다. 표식은 그때부터 왼쪽으로 흘러 거부 구간을 빠져나간다.

   🔴 이 편에서 유일한 가로 축이다. 앞의 계기(포만감)는 세로였고 우선순위도 세로였다.
   여기만 눕힌 이유는 '못 나간다 → 나간다'가 방향의 이야기이기 때문이다. 세로였다면
   회복이 '떨어짐'으로 읽혀 좋을 것이 나쁜 것처럼 보인다.

   계속 도는 것 = 표식(갇힌 동안은 제자리 떨림, 풀린 뒤에는 왼쪽으로 흐르는 순환). */

const SHAKE = 1.4;
const FLOW = 4.6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const hk = ease(cue(spec.holeCue ?? 0, 0.15, 0.6));
    const sk = ease(cue(spec.stuckCue ?? 1, 0.15, 0.6));
    const fk = ease(cue(spec.fixCue ?? 2, 0.15, 0.62));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const x0 = 22, x1 = w - 22, ay = 156;
    const xOf = v => x0 + (v / 100) * (x1 - x0);
    const refuse = spec.refuseAt ?? 70;
    const z0 = (spec.zone && spec.zone[0]) ?? 70;
    const z1 = (spec.zone && spec.zone[1]) ?? 90;

    /* ── 축 ───────────────────────────────────────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(x0, ay); ctx.lineTo(x1, ay); ctx.stroke();

    ctx.textAlign = 'left';
    ctx.font = disp(800, 11); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.axis || '피로', x0, ay + 30);
    ctx.textAlign = 'right';
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('100', x1, ay + 30);

    /* ── 거부 문턱 ────────────────────────────────── */
    {
      const k = clamp(hk * 1.8);
      const tx = xOf(refuse);
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(tx, ay - 26); ctx.lineTo(tx, ay + 16); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.font = mono(700, 15); ctx.fillStyle = tone('ink');
      ctx.fillText(String(refuse), tx, ay - 32);

      // 거부 구간
      ctx.globalAlpha = k * 0.14;
      ctx.fillStyle = tone('ink');
      ctx.fillRect(tx, ay - 12, x1 - tx, 24);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const s = spec.refuseLabel || '';
      const fs = fit(s, 800, 11.5, x1 - tx + 40, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(s, (tx + x1) / 2, ay - 46);
      ctx.globalAlpha = 1;
    }

    /* ── 사각지대 ─────────────────────────────────── */
    const zx0 = xOf(z0), zx1 = xOf(z1);
    if (sk > 0.03) {
      const k = clamp(sk * 1.6);
      const closed = clamp(fk * 1.6);
      ctx.globalAlpha = k;
      ctx.lineWidth = 4;
      ctx.strokeStyle = closed > 0.5 ? tone('accent') : tone('accent');
      ctx.setLineDash(closed > 0.5 ? [] : [6, 6]);
      roundRect(ctx, zx0, ay - 16, zx1 - zx0, 32, 3); ctx.stroke();
      ctx.setLineDash([]);

      ctx.textAlign = 'center';
      const s = spec.zoneLabel || '사각지대';
      const fs = fit(s, 900, 13, zx1 - zx0 + 60, 9);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(s, (zx0 + zx1) / 2, ay + 40);
      ctx.globalAlpha = 1;

      if (sk > 0.4 && spec.stuckLabel) {
        const kk = clamp((sk - 0.4) / 0.5) * (1 - clamp(fk * 1.6));
        if (kk > 0.02) {
          ctx.globalAlpha = kk;
          ctx.textAlign = 'center';
          const fs2 = fit(spec.stuckLabel, 800, 12.5, w - 24, 9);
          ctx.font = disp(800, fs2); ctx.fillStyle = tone('ink');
          ctx.fillText(spec.stuckLabel, w / 2, 100);
          ctx.globalAlpha = 1;
        }
      }
    }

    /* ── 주민 표식 ────────────────────────────────── */
    if (hk > 0.2) {
      const stuckX = xOf(82) + Math.sin(frac(t / SHAKE) * Math.PI * 2) * 7;
      const u = frac(t / FLOW);
      const flowX = xOf(lerp(88, 34, u));
      const mx = lerp(stuckX, flowX, ease(fk));
      ctx.globalAlpha = clamp((hk - 0.2) * 3);
      setShadow(ctx, GLOW, 10, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      ctx.beginPath(); ctx.arc(mx, ay, 10, 0, Math.PI * 2); ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 회복이 열린다 ────────────────────────────── */
    const rec = spec.recover || [];
    rec.slice(0, 2).forEach((r, i) => {
      const born = clamp(fk * 2.2 - i * 0.55);
      if (born < 0.02) return;
      const y = 210 + i * 34;
      const len = (i === 0 ? 34 : 62) * ease(clamp(born));
      const ax = xOf(84);

      ctx.globalAlpha = clamp(born * 1.6);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(ax, y); ctx.lineTo(ax - len, y);
      ctx.moveTo(ax - len, y); ctx.lineTo(ax - len + 7, y - 5);
      ctx.moveTo(ax - len, y); ctx.lineTo(ax - len + 7, y + 5);
      ctx.stroke();

      ctx.textAlign = 'right';
      const nm = r.name || '';
      const fs = fit(nm, 800, 12, ax - 70 - 8, 8.5);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(nm, ax - 74, y + 5);

      ctx.textAlign = 'left';
      ctx.font = mono(700, 15); ctx.fillStyle = tone('accent');
      ctx.fillText(String(r.d ?? ''), ax + 10, y + 5);
      ctx.globalAlpha = 1;
    });

    if (fk > 0.55 && spec.adr) {
      const k = clamp((fk - 0.55) / 0.45);
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.adr, 800, 11.5, w - 16, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.adr, 8, 288);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
