import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* unbidden — 시키지 않았는데 간다.

   이 편의 첫 프레임이 답해야 하는 것은 "무슨 게임인가"가 아니라 "왜 이게 게임인가"다.
   그래서 화면을 위아래로 가른다. 위는 **플레이어의 자리**이고 지금 비어 있다 —
   점선 슬롯 하나와, 거기서 아래로 뻗다가 허공에서 끊긴 토막. 아래는 **주민의 자리**이고
   거기서만 실제로 획이 나간다.

   회차의 기본 도형("위에서 내려오지 않고 아래에서 스스로 뻗는다")의 원형이다.
   계속 도는 것 = 획 위를 흐르는 진행 점(위치 이동) + 끊긴 토막 끝에서 사그라지는 신호.

   🔴 위 토막과 아래 획은 절대 닿지 않는다. 닿는 순간 "명령이 내려갔다"로 읽힌다. */

const FLOW = 3.4;   // 진행 점 한 바퀴(초)
const BLINK = 2.2;  // 끊긴 토막 끝 신호

/** 2차 베지어 위의 점 */
function qpt(p0, p1, p2, k) {
  const m = 1 - k;
  return {
    x: m * m * p0.x + 2 * m * k * p1.x + k * k * p2.x,
    y: m * m * p0.y + 2 * m * k * p1.y + k * k * p2.y
  };
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const gk = ease(cue(spec.gapCue ?? 0, 0.15, 0.6));
    const sk = ease(cue(spec.selfCue ?? 1, 0.15, 0.6));

    ctx.textBaseline = 'alphabetic';
    ctx.textAlign = 'center';

    /* ── 위 : 비어 있는 명령 슬롯 ───────────────── */
    const slotW = 178, slotH = 44;
    const sx = w / 2 - slotW / 2, sy = 26;

    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
    ctx.fillText(spec.slotLabel || '명령', w / 2, sy - 9);

    ctx.setLineDash([7, 6]);
    ctx.lineWidth = 3;
    ctx.strokeStyle = gk > 0.02 ? tone('ink') : tone('track');
    ctx.globalAlpha = lerp(0.45, 1, gk);
    roundRect(ctx, sx, sy, slotW, slotH, 8);
    ctx.stroke();
    ctx.globalAlpha = 1;
    ctx.setLineDash([]);

    ctx.fillStyle = tone('sub'); ctx.font = disp(800, 15);
    ctx.fillText(spec.slotValue || '— 없음 —', w / 2, sy + slotH / 2 + 6);

    /* 슬롯에서 아래로 뻗다 만 토막 — 허공에서 끊긴다.
       끝점은 주민(y 206)에서 한참 위(y 112)에 멎는다. */
    const stubTop = sy + slotH + 6;
    const stubBot = 112;
    ctx.setLineDash([5, 7]);
    ctx.lineWidth = 3;
    ctx.strokeStyle = tone('track');
    ctx.beginPath();
    ctx.moveTo(w / 2, stubTop); ctx.lineTo(w / 2, stubBot);
    ctx.stroke();
    ctx.setLineDash([]);

    // 끊긴 끝에서 사그라지는 신호 — 아래로 내려가려다 사라진다(계속 돈다)
    {
      const u = frac(t / BLINK);
      const y = lerp(stubTop + 6, stubBot, u);
      ctx.globalAlpha = (1 - u) * 0.55;
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(w / 2, y, 3.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 가름선 : 위는 플레이어, 아래는 주민 ────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([2, 9]);
    ctx.beginPath(); ctx.moveTo(16, 132); ctx.lineTo(w - 16, 132); ctx.stroke();
    ctx.setLineDash([]);

    /* ── 아래 : 주민에게서 스스로 뻗는 획 ───────── */
    const p0 = { x: 52, y: 206 };
    const p1 = { x: 168, y: 188 };
    const p2 = { x: 286, y: 246 };

    /* 🔴 획은 첫 프레임부터 끝까지 그어져 있다. sk 로 길이를 키웠더니 콜드 오픈에서
       획이 목적지에 못 닿은 채 떠 있어 "고장 난 화면" 으로 읽혔다(스틸 0.4s).
       이 샷의 뜻은 '지금 가는 중' 이지 '이제 출발한다' 가 아니다 — 이미 벌어지던 일이므로
       길이가 아니라 **색과 강조**만 selfCue 를 탄다. */
    const grown = 1;
    const stroke = sk > 0.02 ? tone('accent') : tone('ink');
    if (sk > 0.02) setShadow(ctx, GLOW, 12, 0);
    ctx.strokeStyle = stroke; ctx.lineWidth = 4;
    ctx.beginPath();
    ctx.moveTo(p0.x, p0.y);
    for (let i = 1; i <= 40; i++) {
      const q = qpt(p0, p1, p2, (i / 40) * grown);
      ctx.lineTo(q.x, q.y);
    }
    ctx.stroke();
    clearShadow(ctx);

    // 획 위를 흐르는 진행 점 — 계속 돈다
    {
      const u = frac(t / FLOW);
      const q = qpt(p0, p1, p2, u * grown);
      ctx.fillStyle = stroke;
      ctx.beginPath(); ctx.arc(q.x, q.y, 4.5, 0, Math.PI * 2); ctx.fill();
    }

    // 주민
    ctx.lineWidth = 4;
    ctx.strokeStyle = tone('ink'); ctx.fillStyle = '#000';
    ctx.beginPath(); ctx.arc(p0.x, p0.y, 14, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
    ctx.fillText(spec.agentLabel || '주민', p0.x, p0.y + 32);

    // 목적지 — 처음부터 서 있고 selfCue 에서 강조된다(획과 같은 이유로 등장 연출을 뺐다)
    {
      const k = clamp(sk * 1.4);
      const s = lerp(1, spring(k), k);
      const bw = 44 * s, bh = 34 * s;
      ctx.save();
      ctx.translate(p2.x, p2.y);
      if (sk > 0.02) setShadow(ctx, GLOW, 12, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = stroke; ctx.fillStyle = '#000';
      roundRect(ctx, -bw / 2, -bh / 2, bw, bh, 6);
      ctx.fill(); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
      ctx.fillStyle = stroke; ctx.font = disp(800, 13);
      ctx.fillText(spec.destLabel || '식사', p2.x, p2.y + 5);
    }

    ctx.textAlign = 'left';
  }
};
