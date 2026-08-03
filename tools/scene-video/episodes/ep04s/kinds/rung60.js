import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* rung60 — 명령은 새 시스템이 아니라 사다리에 꽂힌 한 칸이다.

   왼쪽 열은 우선순위 숫자다. 명령 칸(60)이 오른쪽에서 밀려 들어와 배고픔(100)·피로(90)
   아래, 일반 작업(≤50) 위에 꽂힌다(pinCue·neighborCue). 그다음 오른쪽 레일의 표식이
   지금 최상위가 어디인지 가리킨다 — 작업 도중 배가 고파지면 100 으로 튀어 오르고
   (leaveCue), 배를 채우면 다시 60 으로 내려온다(returnCue). 중단도 복귀도 이 한 줄의
   오르내림이 전부다.

   마지막에 '명령 전용 상태머신' 상자가 떴다가 그어진다(noFsmCue). 만들지 않은 것을
   화면에 그리는 이유는, 이 편에서 가장 중요한 것이 **없는 것**이기 때문이다.

   🔴 S1 의 사다리 검사와 모양이 겹치지 않게 갈랐다. 저기는 이름 없는 칸을 위에서 아래로
   훑고 아무것도 안 켜진다. 여기는 칸마다 숫자가 붙고, 움직이는 것은 검사선이 아니라
   '지금 최상위'를 가리키는 표식 하나이며, 방향도 위아래 왕복이다.

   계속 도는 것 = 명령 칸 안에서 채워졌다 비워지는 작업 진행 막대(액션에 실제 소요 시간이
   붙었다)와, 복귀 경로 위를 흐르는 점. */

const WORK = 3.2;
const TRAVEL = 2.6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const pk = ease(cue(spec.pinCue ?? 0, 0.15, 0.6));
    const bk = ease(cue(spec.neighborCue ?? 1, 0.15, 0.65));
    const lk = ease(cue(spec.leaveCue ?? 2, 0.15, 0.6));
    const rk = ease(cue(spec.returnCue ?? 3, 0.15, 0.6));
    const fk = ease(cue(spec.noFsmCue ?? 4, 0.15, 0.55));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const rungs = spec.rungs || [];
    const rowY = [50, 102, 154, 206], rh = 40;
    const bx = 66, bw = 200;
    const cmdIdx = rungs.findIndex(r => r.cmd);
    const ci = cmdIdx < 0 ? 2 : cmdIdx;
    const center = i => rowY[i] + rh / 2;

    // 표식 위치 — 60 에서 100 으로 튀었다 돌아온다
    const my = lerp(lerp(center(ci), center(0), ease(lk)), center(ci), ease(rk));

    /* ── 레일 ─────────────────────────────────────── */
    const railX = 288;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(railX, rowY[0] + 4); ctx.lineTo(railX, rowY[3] + rh - 4);
    ctx.stroke();

    /* ── 칸 ───────────────────────────────────────── */
    rungs.slice(0, 4).forEach((r, i) => {
      const y = rowY[i];
      const isCmd = i === ci;
      const born = isCmd ? clamp(pk * 1.8) : clamp(bk * 2.2 - Math.abs(i - ci) * 0.25);
      if (born < 0.02) return;

      /* 명령 칸은 오른쪽에서 밀려 들어온다.
         🔴 출발점을 화면 밖(w−20)이 아니라 오른쪽 여백에 딱 붙인 w−8−bw 로 잡았다.
         밖에서 들어오면 칸의 오른쪽 절반이 잘린 채 몇 프레임 그려지고 가장자리 점검에
         걸린다. 78px 짜리 짧은 밀림이라도 '자리를 찾아 들어간다'는 뜻은 그대로다. */
      const slide = isCmd ? lerp(w - 8 - bw, bx, ease(clamp(pk / 0.85))) : bx;
      const hot = isCmd ? clamp(1 - Math.abs(my - center(i)) / 30) : clamp(1 - Math.abs(my - center(i)) / 30);

      ctx.globalAlpha = clamp(born * 1.6);
      ctx.lineWidth = isCmd ? 4 : 3;
      if (isCmd) setShadow(ctx, GLOW, 14, 0);
      ctx.strokeStyle = isCmd ? tone('accent') : (hot > 0.5 ? tone('ink') : tone('track'));
      roundRect(ctx, slide, y, bw, rh, 4); ctx.stroke();
      clearShadow(ctx);

      // 우선순위 숫자
      ctx.textAlign = 'right';
      ctx.font = mono(700, 19);
      ctx.fillStyle = isCmd ? tone('accent') : (hot > 0.5 ? tone('ink') : tone('sub'));
      ctx.fillText(String(r.v ?? ''), 58, y + 28);

      // 이름
      ctx.textAlign = 'left';
      const nm = r.name || '';
      const fs = fit(nm, isCmd ? 900 : 800, isCmd ? 16 : 14, bw - 24, 10);
      ctx.font = disp(isCmd ? 900 : 800, fs);
      ctx.fillStyle = isCmd ? tone('accent') : (hot > 0.5 ? tone('ink') : tone('sub'));
      ctx.fillText(nm, slide + 12, y + (isCmd ? 24 : 26));

      /* 계속 도는 것 — 명령 칸 안의 작업 진행 막대 */
      if (isCmd && pk > 0.85) {
        const u = frac(t / WORK);
        ctx.globalAlpha = clamp(born * 1.6) * 0.9;
        ctx.fillStyle = tone('accent');
        ctx.fillRect(slide + 12, y + 30, (bw - 24) * u, 5);
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(slide + 12, y + 32.5); ctx.lineTo(slide + bw - 12, y + 32.5);
        ctx.stroke();
        ctx.fillStyle = tone('accent');
        ctx.fillRect(slide + 12, y + 30, (bw - 24) * u, 5);
      }
      ctx.globalAlpha = 1;
    });

    /* ── 지금 최상위 표식 ─────────────────────────── */
    if (pk > 0.5) {
      const k = clamp((pk - 0.5) * 2);
      ctx.globalAlpha = k;
      setShadow(ctx, GLOW, 10, 0);
      ctx.fillStyle = tone('accent');
      ctx.beginPath();
      ctx.moveTo(railX - 9, my);
      ctx.lineTo(railX + 6, my - 8);
      ctx.lineTo(railX + 6, my + 8);
      ctx.closePath(); ctx.fill();
      clearShadow(ctx);

      if (spec.markLabel) {
        ctx.textAlign = 'left';
        const fs = fit(spec.markLabel, 700, 10, w - railX - 20, 7.5);
        ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.markLabel, railX + 12, my + 4);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 이탈과 복귀의 자취 ───────────────────────── */
    if (lk > 0.05) {
      const upK = ease(clamp(lk));
      const downK = ease(clamp(rk));
      ctx.globalAlpha = 0.85;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([5, 5]);
      // 올라간 자취
      ctx.beginPath();
      ctx.moveTo(railX - 22, center(ci));
      ctx.lineTo(railX - 22, lerp(center(ci), center(0), upK));
      ctx.stroke();
      // 내려온 자취
      if (rk > 0.02) {
        ctx.beginPath();
        ctx.moveTo(railX - 34, center(0));
        ctx.lineTo(railX - 34, lerp(center(0), center(ci), downK));
        ctx.stroke();
      }
      ctx.setLineDash([]);
      ctx.globalAlpha = 1;

      /* 계속 도는 것 — 복귀 경로를 흐르는 점 */
      if (rk > 0.6) {
        const u = frac(t / TRAVEL);
        const py = lerp(center(0), center(ci), u);
        ctx.globalAlpha = 0.9 * Math.sin(Math.PI * u);
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(railX - 34, py, 4, 0, Math.PI * 2); ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 만들지 않은 것 ───────────────────────────── */
    if (fk > 0.03) {
      const k = clamp(fk * 1.5);
      const y = 256, bh = 34;
      ctx.globalAlpha = k * 0.85;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
      roundRect(ctx, 8, y, w - 16, bh, 3); ctx.stroke();
      ctx.textAlign = 'left';
      const s = spec.noFsm || '';
      const fs = fit(s, 800, 13, 190, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(s, 18, y + 22);
      const nw = ctx.measureText(s).width;

      // 그어 지운다
      if (fk > 0.35) {
        const kk = clamp((fk - 0.35) / 0.45);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        setShadow(ctx, GLOW, 8, 0);
        ctx.beginPath();
        ctx.moveTo(14, y + 17); ctx.lineTo(14 + (nw + 10) * kk, y + 17);
        ctx.stroke();
        clearShadow(ctx);
      }
      if (fk > 0.5 && spec.noFsmNote) {
        const kk = clamp((fk - 0.5) / 0.5);
        ctx.globalAlpha = kk;
        ctx.textAlign = 'right';
        const fs2 = fit(spec.noFsmNote, 900, 12, w - 40 - nw, 8.5);
        ctx.font = disp(900, fs2); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.noFsmNote, w - 18, y + 22);
      }
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
