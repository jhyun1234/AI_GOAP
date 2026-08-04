import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* wicket — 앵커가 무엇인지 먼저 보여주고, 그다음에 그것을 찾는 코드를 한 창구로 모은다.

   이 샷 하나가 「앵커」의 **정의 → 중복 → 통합 → 효과**를 다 진다. 앞 구성에서는 정의가
   한 편에, 사례가 다른 편에 갈려 있어서 "2편만 본 사람에게 앵커는 끝까지 안 갚는 빚"이라는
   판정을 받았다(ep05s2 검수 1차 §2). 편 경계를 사건 단위로 다시 그으면서 넷을 한자리에 모았다.

   ① defCue  — 원문이 괄호로 붙인 정의가 위에 서고, 화면 가운데 오른쪽에 **앵커 하나**가
      점선 원과 함께 놓인다. 주민 표식 둘이 그 원 둘레를 돈다 — 정해진 자리를 쓰는 사람들.
   ② dupCue  — 왼쪽에 Runner 상자 셋이 서고, 상자마다 똑같은 '앵커 찾기' 조각이 하나씩
      돋아 떤다. 조각 셋이 **각자 따로** 같은 앵커로 선을 뻗는다(= 따로 자란 코드).
   ③ mergeCue — 조각 셋이 상자를 떠나 창구 하나로 빨려 들어가고, 창구가 굵어진다.
      앵커로 가는 선은 이제 창구에서 하나만 나간다.
   ④ addCue  — 새 앵커(집)가 왼쪽 위에 생기고 **창구에만** 붙는다. Runner 상자 셋은
      눈금 하나 안 움직인다. 원문이 실제로 자랑하는 문장이 이 마지막 장면이다.

   🔴 '여럿이 하나로'는 앞선 회차에 두 번 있었지만 둘 다 **모여서 나빠진** 그림이었다.
   여기는 방향이 반대라 합쳐진 쪽을 강조색으로 두껍게 그리고 금·✕·기울기를 하나도 안 썼다.
   그리고 결말이 합침이 아니라 **새 것이 들어와도 왼쪽이 안 흔들림**에 있다.
   🔴 앵커를 '핀 + 점선 원'으로 그린 것이 이 편의 몫이다 — 앞 구성의 같은 이름 그림에는
   앵커가 아예 안 그려져 있었고 '앵커 찾기'라는 글자만 있었다. 정의를 글자로만 주면
   시청자는 낱말을 하나 더 외워야 한다.

   계속 도는 것 = ① 점선 원의 흐름과 그 둘레를 도는 표식 둘(자막 0 부터 끝까지)
   ② 중복 조각의 떨림과 중복 선 위를 흐르는 점(자막 1~2) ③ 합쳐진 뒤 연결선 위를 흐르는 표식. */

const RX = 10, RW = 112, RH = 42, RY = [110, 164, 218];
const WX = 150, WW = 186, WY = 168, WH = 60;      // x 150~336 · y 168~228
const WCX = WX + WW / 2, WCY = WY + WH / 2;       // 243 · 198
const PIN = { x: 268, y: 76, r: 26 };
const NEW = { x: 176, y: 76 };

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const dk = ease(cue(spec.defCue ?? 0, 0.15, 0.62));
    const pk = ease(cue(spec.dupCue ?? 1, 0.15, 0.62));
    const mk = ease(cue(spec.mergeCue ?? 2, 0.15, 0.62));
    const ak = ease(cue(spec.addCue ?? 3, 0.15, 0.6));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* ── 정의 — 원문 4절이 괄호로 붙인 그대로 ─────── */
    if (dk > 0.02 && spec.anchorDef) {
      ctx.globalAlpha = clamp(dk * 2.2) * 0.95;
      ctx.textAlign = 'left';
      const fs = fit(spec.anchorDef, 700, 10.5, w - 20, 8);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.anchorDef, 10, 20);
      ctx.globalAlpha = 1;
    }

    /* ── 앵커 하나 — 고정된 자리 ─────────────────── */
    if (dk > 0.02) {
      const k = clamp(dk * 2.2);
      ctx.globalAlpha = k * 0.85;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([7, 6]);
      ctx.lineDashOffset = -(t * 20) % 13;      // 계속 도는 것 — 자리를 두르는 점선
      ctx.beginPath(); ctx.arc(PIN.x, PIN.y, PIN.r, 0, Math.PI * 2); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      ctx.globalAlpha = k;
      setShadow(ctx, GLOW, 12, 0);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(PIN.x, PIN.y, 8, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);

      // 계속 도는 것 — 그 자리를 쓰는 주민 둘
      for (let i = 0; i < 2; i++) {
        const u = frac(t / 3.6 + i * 0.5) * Math.PI * 2;
        ctx.globalAlpha = k * 0.9;
        ctx.fillStyle = tone('ink');
        ctx.beginPath();
        ctx.arc(PIN.x + Math.cos(u) * PIN.r, PIN.y + Math.sin(u) * PIN.r, 5.5, 0, Math.PI * 2);
        ctx.fill();
      }

      if (spec.anchorLabel) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'left';
        const fs = fit(spec.anchorLabel, 800, 11, w - 302, 8);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.anchorLabel, 300, 80);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 중복 선 — 셋이 따로 같은 자리를 찾아간다 ── */
    const dupLine = clamp(pk * 1.8) * (1 - clamp(mk * 2));
    if (dupLine > 0.02) {
      RY.forEach((ry, i) => {
        const x0 = RX + 104, y0 = ry + 29;
        ctx.globalAlpha = dupLine * 0.7;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(x0, y0); ctx.lineTo(PIN.x, PIN.y + PIN.r + 2);
        ctx.stroke();

        const u = frac(t / 2.3 + i / 3);
        ctx.globalAlpha = dupLine * 0.9;
        ctx.fillStyle = tone('ink');
        ctx.beginPath();
        ctx.arc(lerp(x0, PIN.x, u), lerp(y0, PIN.y + PIN.r + 2, u), 3.5, 0, Math.PI * 2);
        ctx.fill();
        ctx.globalAlpha = 1;
      });
    }

    /* ── 합친 뒤의 연결선 ─────────────────────────── */
    if (mk > 0.5) {
      const k = clamp((mk - 0.5) / 0.45);
      RY.forEach((ry, i) => {
        const x0 = RX + RW, y0 = ry + 21;
        ctx.globalAlpha = k * 0.75;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(x0, y0); ctx.lineTo(lerp(x0, WX, 0.6), y0); ctx.lineTo(WX, WCY);
        ctx.stroke();

        const u = frac(t / 2.7 + i / 3);
        const bx = lerp(x0, WX, 0.6);
        const mx = u < 0.6 ? lerp(x0, bx, u / 0.6) : lerp(bx, WX, (u - 0.6) / 0.4);
        const my = u < 0.6 ? y0 : lerp(y0, WCY, (u - 0.6) / 0.4);
        ctx.globalAlpha = k * 0.9;
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(mx, my, 3.5, 0, Math.PI * 2); ctx.fill();
        ctx.globalAlpha = 1;
      });

      // 창구에서 앵커로 — 이제 하나뿐이다
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(PIN.x, WY); ctx.lineTo(PIN.x, PIN.y + PIN.r + 2);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── Runner 상자 셋과 그 안의 중복 조각 ───────── */
    RY.forEach((ry, i) => {
      const born = clamp(pk * 3.4 - i * 0.5);
      if (born < 0.02) return;
      const s = Math.min(1, spring(clamp(born)));
      ctx.globalAlpha = clamp(born * 1.8);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, RX + (RW - RW * s) / 2, ry + (RH - RH * s) / 2, RW * s, RH * s, 3);
      ctx.stroke();
      ctx.textAlign = 'left';
      ctx.font = mono(700, 10.5); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.runner || 'Runner', RX + 9, ry + 15);
      ctx.globalAlpha = 1;

      const gone = ease(clamp((mk - i * 0.08) / 0.62));
      const px = lerp(RX + 8, WCX - 26, gone);
      const py = lerp(ry + 20, WCY - 22, gone);
      const pw = lerp(96, 52, gone), ph = 18;
      const jit = gone < 0.02 ? 1.7 * Math.sin(frac(t / 0.95) * Math.PI * 2 + i * 2.1) : 0;
      const pa = clamp(born * 1.8) * (1 - clamp((gone - 0.7) / 0.3));
      if (pa > 0.02) {
        ctx.globalAlpha = pa;
        ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
        roundRect(ctx, px + jit, py, pw, ph, 3); ctx.stroke();
        if (gone < 0.5) {
          ctx.textAlign = 'left';
          const fs = fit(spec.piece || '앵커 찾기', 800, 10.5, pw - 10, 7.5);
          ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
          ctx.fillText(spec.piece || '앵커 찾기', px + jit + 6, py + 13);
        }
        ctx.globalAlpha = 1;
      }
    });

    if (pk > 0.4 && spec.dupNote) {
      ctx.globalAlpha = clamp((pk - 0.4) / 0.4) * (1 - clamp((mk - 0.4) / 0.4));
      ctx.textAlign = 'left';
      const fs = fit(spec.dupNote, 800, 10.5, 176, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.dupNote, RX + 2, 288);
      ctx.globalAlpha = 1;
    }

    /* ── 창구 하나 ────────────────────────────────── */
    if (mk > 0.05) {
      const k = clamp(mk * 1.6);
      const done = clamp((mk - 0.6) / 0.4);
      const s = Math.min(1, spring(clamp(mk * 1.3)));
      ctx.globalAlpha = k;
      ctx.lineWidth = 3 + 2 * done;
      ctx.strokeStyle = tone('accent');
      if (done > 0.4) setShadow(ctx, GLOW, 14, 0);
      roundRect(ctx, WX + (WW - WW * s) / 2, WY + (WH - WH * s) / 2, WW * s, WH * s, 4);
      ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'center';
      let fs = 12; ctx.font = mono(700, fs);
      const nm = spec.wicket || '';
      while (fs > 6.5 && ctx.measureText(nm).width > WW - 18) { fs -= 0.5; ctx.font = mono(700, fs); }
      ctx.fillStyle = tone('accent');
      ctx.fillText(nm, WCX, WY + 30);

      if (spec.wicketNote) {
        const nfs = fit(spec.wicketNote, 800, 11, WW - 18, 8);
        ctx.font = disp(800, nfs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.wicketNote, WCX, WY + 50);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 새 앵커가 들어와도 Runner 는 그대로 ──────── */
    if (ak > 0.02) {
      const k = clamp(ak * 1.9);
      const drop = ease(clamp(ak / 0.7));
      const cy = lerp(NEW.y - 26, NEW.y, drop);

      ctx.globalAlpha = k;
      setShadow(ctx, GLOW, 12, 0);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(NEW.x, cy, 8, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);

      if (ak > 0.45) {
        const kk = clamp((ak - 0.45) / 0.4);
        ctx.globalAlpha = kk;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(NEW.x, cy + 10); ctx.lineTo(NEW.x, lerp(cy + 10, WY, kk));
        ctx.stroke();
      }

      if (spec.newAnchor) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'right';
        const fs = fit(spec.newAnchor, 800, 11, 120, 8);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.newAnchor, NEW.x - 16, 80);
      }
      ctx.globalAlpha = 1;
    }

    if (ak > 0.5 && spec.stillLabel) {
      const k = clamp((ak - 0.5) / 0.45);
      const s = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.stillLabel, 900, 15, RW + 52, 10);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.stillLabel, RX + 2, 288);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
