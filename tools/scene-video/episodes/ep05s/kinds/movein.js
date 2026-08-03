import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* movein — 파일 한 장 안의 값 한 칸이 굴러 바뀌자, 마을의 저녁이 옮겨 간다.

   위는 수정한 파일 카드 한 장(RestByCampfire.asset)이고, 그 안에 필드가 딱 한 줄 있다.
   moveCue 에서 값 칸의 '모닥불'이 위로 밀려 나가고 '집'이 아래에서 올라와 자리를 잡는다.
   같은 진행률로 아래 마을에서는 주민들의 목적지 선이 모닥불에서 집으로 옮겨 붙는다.
   값 한 칸과 마을의 저녁이 같은 손잡이에 걸려 있다는 것이 이 그림의 전부다.

   🔴 지난 편이 화면으로 '모닥불의 다음 자리'를 예고했는데, 이 편 원문에서 실제로
   벌어진 이동은 **모닥불 → 집** 하나뿐이다. 그래서 화면에 다른 건물을 하나도 세우지
   않았다. 집만 있다.

   계속 도는 것 = 모닥불의 세 줄기, 주민 셋의 호흡. */

const FLAME = 1.7;
const BREATH = 2.9;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const fk = ease(cue(spec.fileCue ?? 0, 0.15, 0.6));
    const mk = ease(cue(spec.moveCue ?? 1, 0.15, 0.62));
    const ck = ease(cue(spec.csCue ?? 2, 0.15, 0.6));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* ── 파일 카드 ────────────────────────────────── */
    const CX = 24, CW = w - 48, CY = 26, CH = 92;
    const VBX = 132, VBY = 74, VBW = 180, VBH = 32;
    const roll = ease(clamp(mk / 0.72));

    if (fk > 0.02) {
      const k = clamp(fk * 2);
      const s = Math.min(1, spring(clamp(fk * 1.3)));
      ctx.globalAlpha = k;
      ctx.lineWidth = 4; ctx.strokeStyle = tone('ink');
      roundRect(ctx, CX + (CW - CW * s) / 2, CY + (CH - CH * s) / 2, CW * s, CH * s, 4);
      ctx.stroke();

      ctx.textAlign = 'left';
      let fs = 12.5; ctx.font = mono(700, fs);
      const nm = spec.file || '';
      while (fs > 7 && ctx.measureText(nm).width > CW - 34) { fs -= 0.5; ctx.font = mono(700, fs); }
      ctx.fillStyle = tone('ink');
      ctx.fillText(nm, CX + 16, CY + 24);

      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(CX + 16, CY + 34); ctx.lineTo(CX + CW - 16, CY + 34);
      ctx.stroke();

      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.field || '앵커', CX + 16, VBY + 21);

      // 값 칸 — 한 칸이 굴러 바뀐다
      ctx.strokeStyle = mk > 0.5 ? tone('accent') : tone('track');
      ctx.lineWidth = 3;
      roundRect(ctx, VBX, VBY, VBW, VBH, 3); ctx.stroke();

      ctx.save();
      ctx.beginPath(); ctx.rect(VBX + 2, VBY + 2, VBW - 4, VBH - 4); ctx.clip();
      ctx.textAlign = 'center';
      const from = spec.from || '', to = spec.to || '';
      ctx.font = disp(900, 17);
      ctx.globalAlpha = k * (1 - roll);
      ctx.fillStyle = tone('ink');
      ctx.fillText(from, VBX + VBW / 2, VBY + 22 - VBH * roll);
      ctx.globalAlpha = k * roll;
      ctx.fillStyle = tone('accent');
      ctx.fillText(to, VBX + VBW / 2, VBY + 22 + VBH * (1 - roll));
      ctx.restore();
      ctx.globalAlpha = 1;
    }

    if (fk > 0.5 && spec.fileNote) {
      const k = clamp((fk - 0.5) / 0.5);
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.fileNote, 800, 11, w - 48, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.fileNote, CX, 138);
      ctx.globalAlpha = 1;
    }

    /* ── 마을 ─────────────────────────────────────── */
    const GY = 244;
    const FIRE = { x: 74, y: GY };
    const HOME = { x: 278, y: GY };

    if (fk > 0.35) {
      const k = clamp((fk - 0.35) / 0.5);
      ctx.globalAlpha = k * 0.9;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(20, GY); ctx.lineTo(w - 20, GY); ctx.stroke();
      ctx.globalAlpha = 1;

      // 모닥불 — 장작과 세 줄기
      ctx.globalAlpha = k * (1 - 0.55 * roll);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(FIRE.x - 16, GY - 2); ctx.lineTo(FIRE.x + 16, GY - 12);
      ctx.moveTo(FIRE.x - 16, GY - 12); ctx.lineTo(FIRE.x + 16, GY - 2);
      ctx.stroke();
      for (let i = 0; i < 3; i++) {
        const u = frac(t / FLAME + i / 3);
        const bx = FIRE.x + (i - 1) * 9;
        const top = GY - 18 - 20 * Math.sin(Math.PI * u) - i * 2;
        ctx.globalAlpha = k * (1 - 0.55 * roll) * (0.45 + 0.5 * Math.sin(Math.PI * u));
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(bx, GY - 16);
        ctx.quadraticCurveTo(bx + 6, (GY - 16 + top) / 2, bx, top);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;

      // 집
      ctx.globalAlpha = k * (0.45 + 0.55 * roll);
      ctx.strokeStyle = roll > 0.5 ? tone('accent') : tone('sub');
      ctx.lineWidth = 4;
      if (roll > 0.5) setShadow(ctx, GLOW, 12, 0);
      roundRect(ctx, HOME.x - 27, GY - 40, 54, 40, 3); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(HOME.x - 34, GY - 40); ctx.lineTo(HOME.x, GY - 62); ctx.lineTo(HOME.x + 34, GY - 40);
      ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;

      ctx.textAlign = 'center';
      ctx.font = disp(800, 11);
      ctx.globalAlpha = k * 0.85;
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.from || '모닥불', FIRE.x, GY + 18);
      ctx.fillStyle = roll > 0.5 ? tone('accent') : tone('sub');
      ctx.fillText(spec.to || '집', HOME.x, GY + 18);
      ctx.globalAlpha = 1;

      // 주민 셋과 목적지 선
      const tx = lerp(FIRE.x, HOME.x, roll);
      const ty = lerp(GY - 20, GY - 22, roll);
      [148, 176, 204].forEach((vx, i) => {
        const br = 1.6 * Math.sin(frac(t / BREATH + i / 3) * Math.PI * 2);
        const vy = GY - 4 + br;
        ctx.globalAlpha = k * 0.8;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.setLineDash([7, 6]);
        ctx.lineDashOffset = -(t * 10) % 13;
        ctx.beginPath(); ctx.moveTo(vx, vy - 14); ctx.lineTo(tx, ty); ctx.stroke();
        ctx.setLineDash([]); ctx.lineDashOffset = 0;

        ctx.globalAlpha = k;
        ctx.fillStyle = tone('ink');
        roundRect(ctx, vx - 6, vy - 20, 12, 20, 3); ctx.fill();
        ctx.beginPath(); ctx.arc(vx, vy - 26, 5, 0, Math.PI * 2); ctx.fill();
        ctx.globalAlpha = 1;
      });
    }

    /* ── .cs 는 안 건드렸다 ───────────────────────── */
    if (ck > 0.05 && spec.csNote) {
      const k = clamp(ck * 1.6);
      const s = Math.min(1, spring(clamp(ck * 1.3)));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.csNote, 900, 14.5, w - 28, 10);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.csNote, w / 2, 292);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
