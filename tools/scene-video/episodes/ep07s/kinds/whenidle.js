import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* whenidle — 일감이 다 떨어져야 둘의 방향이 갈린다.

   원문: "직업의 진짜 개성은 '할 일이 있을 때'가 아니라 '할 일이 없을 때' 드러납니다.
   급한 일이 있으면 누구나 그 일을 하지만, 한가할 때 뭘 하느냐가 그 사람을 보여주니까요."

   🔴 그래서 두 사람은 **일이 있는 동안 완전히 겹쳐 있다.** 표식 하나만 보이는 것이 정상이고,
   그 위에 이름도 안 붙는다 — 구별할 근거가 화면에 아직 없기 때문이다. drainCue 에서 일감
   칸이 왼쪽부터 비고, 마지막 칸이 비는 순간 IDLE 이 뜬다. partCue 에서야 표식이 둘로
   갈라지고 그때 비로소 이름이 붙는다. **이름이 늦게 붙는 것이 이 샷의 설계 전부다.**

   🔴 일과의 우선순위 대역(여가 < 일과 < 공용 노동 < 명령 < 비상)은 그리지 않는다.
   원문이 대역의 값을 하나도 주지 않아서 눈금을 그리면 지어낸 수치가 된다. 대신 그 규칙의
   **결과**만 그린다 — 칸이 다 비어야 갈라진다는 것이 곧 "진짜 노동보다 한참 아래"다.

   🔴 등장은 전부 cue 에 물렸다. t 로 도는 것은 남은 칸의 빗금 흐름, 궤적 dash, 밭 이삭
   흔들림, 지도 경계를 넘나드는 표식뿐이고 문턱을 가지지 않는다. */

const SWAY = 2.7;    // 이삭 흔들림 주기(초)
const CROSS = 3.3;   // 지도 경계를 넘나드는 주기(초)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const SLOTS = spec.slots ?? 4;
    const actors = spec.actors ?? [];
    const dk = ease(cue(spec.drainCue ?? 0, 0.15, 0.7));
    const pk = ease(cue(spec.partCue ?? 1, 0.15, 0.8));

    /* ── 일감 칸 ───────────────────────────────────── */
    if (spec.queueLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.queueLabel, 8, 28);
    }

    const QX = 26, QY = 44, QH = 34;
    const QW = (w - QX * 2 - (SLOTS - 1) * 8) / SLOTS;
    for (let i = 0; i < SLOTS; i++) {
      const x = QX + i * (QW + 8);
      // 왼쪽부터 비워진다
      const left = clamp(1 - (dk * (SLOTS + 0.6) - i));

      ctx.strokeStyle = left > 0.5 ? tone('ink') : tone('track');
      ctx.lineWidth = 3;
      if (left <= 0.5) {
        /* 🔴 빈 칸의 테두리는 계속 흐른다. 일감이 없어도 마을은 계속 훑고 있다 —
           그리고 이 구간(칸이 다 비었는데 아직 안 갈라진 사이)이 이 샷에서 유일하게
           아무 사건도 없는 자리라, 여기가 멈추면 정적 구간이 3초를 넘는다. */
        ctx.setLineDash([10, 8]);
        ctx.lineDashOffset = -(t * 16) % 18;
      }
      roundRect(ctx, x, QY, QW, QH, 6); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      if (left > 0.02) {
        // 빗금이 흐른다 — 아직 남은 일감
        ctx.save();
        roundRect(ctx, x, QY, QW, QH, 6); ctx.clip();
        ctx.globalAlpha = left;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        const off = (t * 15) % 14;
        for (let g = -QH; g < QW + QH; g += 14) {
          ctx.beginPath();
          ctx.moveTo(x + g + off, QY + QH);
          ctx.lineTo(x + g + off + QH, QY);
          ctx.stroke();
        }
        ctx.globalAlpha = 1;
        ctx.restore();
      }
    }

    /* ── IDLE — 칸이 다 비면 뜬다 ──────────────────── */
    const ik = ease(clamp((dk - 0.72) / 0.28));
    if (ik > 0.02 && spec.idleLabel) {
      ctx.globalAlpha = ik;
      ctx.textAlign = 'center';
      ctx.font = disp(900, 17); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.idleLabel, w / 2, 116);
      ctx.globalAlpha = 1;
    }

    /* ── 두 사람 ───────────────────────────────────── */
    const SX = 58, SY = 186;                 // 겹쳐 서 있는 자리
    const rowY = i => i === 0 ? 154 : 222;
    const endX = w - 92;

    /* 겹쳐 있는 동안의 고리 — 둘이 한 자리에 있다는 표시이자,
       칸이 다 빈 뒤 갈라지기 전까지의 유일한 움직임이다. */
    const together = clamp(1 - pk * 2.2);
    if (together > 0.02) {
      const r = 13 + 5 * (0.5 - 0.5 * Math.cos(frac(t / 2.2) * Math.PI * 2));
      ctx.globalAlpha = together * 0.75;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(SX, SY, r, 0, Math.PI * 2); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    for (let i = 0; i < actors.length; i++) {
      /* 🔴 앞으로 당겨 놓은 값이다. 처음엔 travel 이 pk × 1.25 − 0.1 이라 자막이
         「농부는 밭을 둘러보고, 탐험가는 지도 밖으로 나가요」를 말하는 동안 표식이
         21% 만 움직여 있었고 이름도 목적지도 아직 화면에 없었다 — 나레이션이 부르는 것을
         화면이 안 보여주는 상태다(쇼츠 문법 계약 2). 지금은 pk 0.63 에 도착한다. */
      const y = lerp(SY, rowY(i), clamp(pk * 1.5));
      const x = lerp(SX, endX, ease(clamp(pk * 1.6)));

      // 궤적 — 갈라진 뒤에만 보인다
      if (pk > 0.06) {
        ctx.globalAlpha = clamp(pk * 2) * 0.8;
        ctx.setLineDash([9, 8]);
        ctx.lineDashOffset = -(t * 14) % 17;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(SX, SY);
        ctx.quadraticCurveTo((SX + endX) / 2, rowY(i), x, y);
        ctx.stroke();
        ctx.setLineDash([]); ctx.lineDashOffset = 0;
        ctx.globalAlpha = 1;
      }

      // 이름 — 갈라진 뒤에 붙는다
      const nk = clamp((pk - 0.08) / 0.28);
      if (nk > 0.02 && actors[i].name) {
        ctx.globalAlpha = nk;
        ctx.textAlign = 'center';
        const fs = fit(actors[i].name, 800, 13, 74, 9);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(actors[i].name, x, y - 15);
        ctx.globalAlpha = 1;
      }

      setShadow(ctx, GLOW, 7, 0);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(x, y, 6, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);
    }

    /* ── 목적지 둘 — 밭과 지도 밖 ──────────────────── */
    const tk = clamp((pk - 0.14) / 0.34);
    if (tk > 0.02) {
      // 밭 — 이삭 셋이 흔들린다
      const fx = w - 46, fy = rowY(0);
      ctx.globalAlpha = tk;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      for (let s = 0; s < 3; s++) {
        const sw = Math.sin(frac(t / SWAY) * Math.PI * 2 + s * 1.1) * 3.4;
        ctx.beginPath();
        ctx.moveTo(fx - 14 + s * 14, fy + 13);
        ctx.lineTo(fx - 14 + s * 14 + sw, fy - 12);
        ctx.stroke();
      }
      ctx.textAlign = 'center';
      let fs = fit(actors[0]?.to ?? '', 800, 12, 84, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(actors[0]?.to ?? '', fx, fy + 30);

      // 지도 경계 — 표식이 계속 넘나든다
      const bx = w - 62, by = rowY(1);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([6, 6]);
      ctx.beginPath(); ctx.moveTo(bx, by - 26); ctx.lineTo(bx, by + 20); ctx.stroke();
      ctx.setLineDash([]);

      const cx = bx + lerp(-16, 20, 0.5 - 0.5 * Math.cos(frac(t / CROSS) * Math.PI * 2));
      setShadow(ctx, GLOW, 6, 0);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(cx, by - 4, 5, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);

      fs = fit(actors[1]?.to ?? '', 800, 12, 88, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(actors[1]?.to ?? '', bx, by + 36);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
