/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다. */
import {
  disp, ease, clamp, lerp, fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 다음 편(15-5편)에서 다룰 것의 0.5초 미리보기. `ADR-V25-10`.

   🔴 재료는 이 편이 아니라 **다음 편이 맡은 사건의 원문 문장**에서 뽑았다:
   > "성격 여섯 종이 열다섯 쌍 전부 다른 상위 목표 구성으로 갈렸습니다."
   > "「게으름뱅이 둘 다 그냥 죽었어, 별 이야기가 없었다」"

   화면에서 움직이는 것 : **왼쪽 판**에서 한 점에서 나온 줄이 여섯 갈래로 벌어지고
   그 위에 「통과」가 켜진다. **오른쪽 판**은 똑같이 생겼는데 반 초 내내 아무것도 안 뜬다.
   숫자는 통과인데 볼 것이 없다 — 그게 다음 편의 사건이다.

   🖼 라벨 둘(「다음 편」·「통과」)을 가려도 **「한쪽 판에서는 갈래가 벌어지는데
   똑같이 생긴 옆 판은 계속 비어 있다」**가 읽힌다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세로로 갈랐다.**
   「통과」(강조색)는 기준선 판top+30 · 14px 이라 글자 상자가 판top+16~판top+34 이고,
   **글로우(blur 5)까지 세면 판top+11~판top+39** 다. 갈래(흰색)의 가장 윗줄은 판top+52 라
   **13px** 뜨고, 판 테두리(흰색) 윗선 획 안쪽(판top+1.5)과는 **9.5px** 뜬다.
   🔴 초고는 기준선 판top+27 · 15px · blur 8 이었는데 **글로우가 판top+39 까지 내려가
   갈래(판top+38)를 물었다** — 글자 상자만 재고 글로우를 안 센 것이 원인이다.

   🔴 카드가 덮기 전에 보인다 — 0.5초 루프는 `t` 로 돌아 **예고 음성이 나오는 구간부터**
   계속 되풀이된다. 마지막 3.0초를 아웃트로 카드가 덮어도 이미 여러 바퀴 돈 뒤다. */

const PW = 146, PH = 128, PY = 108, PGAP = 12;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const c0 = cue(spec.peekCue ?? 0, 0.15, 0.55);
    const st = ease(clamp(c0 / 0.12));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const x0 = (w - (PW * 2 + PGAP)) / 2;
    const LP = spec.loopSec ?? 0.5;
    const k = (((t % LP) + LP) % LP) / LP;
    const fan = ease(clamp(k / 0.30));
    const pass = ease(clamp((k - 0.30) / 0.08)) * (1 - ease(clamp((k - 0.44) / 0.05)));
    const off = 1 - ease(clamp((k - 0.46) / 0.04));

    /* ── 판 둘 : 생김새가 같다 ─────────────────────── */
    ctx.save();
    ctx.globalAlpha = st * 0.45;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, x0, PY, PW, PH, 8); ctx.stroke();
    roundRect(ctx, x0 + PW + PGAP, PY, PW, PH, 8); ctx.stroke();
    ctx.restore();

    /* ── 왼쪽 판 : 한 점에서 여섯 갈래로 벌어진다 ──── */
    const lanes = spec.lanes ?? 6;
    const nx = x0 + 24, ny = PY + 84;
    const ex = x0 + 126;
    ctx.save();
    ctx.globalAlpha = st * off;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineCap = 'round';
    for (let i = 0; i < lanes; i++) {
      const ey = ny + (i - (lanes - 1) / 2) * (64 / (lanes - 1));
      ctx.beginPath();
      ctx.moveTo(nx, ny);
      ctx.lineTo(lerp(nx, ex, fan), lerp(ny, ey, fan));
      ctx.stroke();
    }
    ctx.beginPath(); ctx.arc(nx, ny, 4.5, 0, Math.PI * 2);
    ctx.fillStyle = tone('ink'); ctx.fill();
    ctx.restore();

    /* ── 「통과」가 켜진다 ─────────────────────────── */
    if (spec.passLabel && pass > 0.02) {
      ctx.save();
      ctx.globalAlpha = st * pass * off;
      setShadow(ctx, GLOW, 5);
      ctx.textAlign = 'center'; ctx.font = disp(900, 14); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.passLabel, x0 + PW / 2, PY + 30);
      clearShadow(ctx);
      ctx.restore();
    }

    if (spec.label) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 20, 30);
      ctx.restore();
    }
    ctx.textAlign = 'left';
  }
};
