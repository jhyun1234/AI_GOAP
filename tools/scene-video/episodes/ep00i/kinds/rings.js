import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* rings — 나이테. 한 점에서 바깥으로 한 겹씩.

   "지금까지 뭘 넣었나"를 목록으로 늘어놓으면 일곱 줄짜리 자막이 된다. 이 게임에서 축이
   얹히는 방식은 목록이 아니라 **나이테**다 — 앞 겹을 지우지 않고 그 바깥에 새 겹이 생긴다
   (CLAUDE.md 콘텐츠 추가 비용표가 문자 그대로 그 구조다: 새 계절 = 에셋 1개, 코드 0줄).

   회차의 기본 도형 안에서 이 그림의 몫은 '한 점에서 바깥으로 자란다'.
   계속 도는 것 = 가장 바깥의 이름 없는 점선 겹이 계속 바깥으로 번진다 — 아직 안 붙인 다음 겹.

   🔴 부채꼴 각도를 ±40° 로 묶었다. 반원으로 열면 바깥 겹이 좌우로 잘린다.
   🔴 겹 순서에 밀스톤 번호를 안 붙인다 — '얹혀 온 순서'까지만 주장한다. */

const OX = 176, OY = 286;      // 나이테의 중심(캔버스 바닥에서 21px 위)
const R0 = 28, RSTEP = 32;     // 첫 겹 반지름 / 겹 간격
const FAN = Math.PI * 40 / 180; // 수직에서 좌우로 벌린 각
const PULSE = 3.6;

/** 수직 위쪽을 0 으로 보는 부채꼴 호 */
function fan(ctx, r) {
  ctx.beginPath();
  // 캔버스 각도계: -PI/2 가 위쪽
  ctx.arc(OX, OY, r, -Math.PI / 2 - FAN, -Math.PI / 2 + FAN);
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    const gk = ease(cue(spec.growCue ?? 0, 0.15, 0.6));
    const mk = ease(cue(spec.midCue ?? 1, 0.12, 0.7));
    const tk = ease(cue(spec.topCue ?? 2, 0.12, 0.5));

    const layers = spec.layers || [];
    const n = layers.length;

    ctx.textBaseline = 'alphabetic';
    ctx.textAlign = 'center';

    /* 겹이 켜지는 진행도 : growCue 로 안쪽 셋, midCue 로 여섯째까지, topCue 로 마지막 */
    const reach = lerp(0, n, clamp(gk * 0.42 + mk * 0.44 + tk * 0.2));

    /* ── 가장 바깥 : 아직 이름 없는 겹 (계속 번진다) ── */
    {
      const u = frac(t / PULSE);
      // 🔴 상한은 252 다. 그 위로 열면 ±40° 부채꼴이 좌우 가장자리에 닿는다
      //    (252 × sin40° = 162 → x 14~338, 가장자리에서 14px 남는다).
      const r = lerp(R0 + RSTEP * (n - 1) - 6, 252, u);
      ctx.setLineDash([6, 8]);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.globalAlpha = 1 - u;
      fan(ctx, r); ctx.stroke();
      ctx.globalAlpha = 1;
      ctx.setLineDash([]);
    }

    /* ── 겹 ─────────────────────────────────────── */
    for (let i = 0; i < n; i++) {
      const r = R0 + RSTEP * i;
      const k = clamp(reach - i);
      if (k <= 0.01) continue;
      const outermost = i === n - 1;
      const on = outermost && tk > 0.3;

      ctx.strokeStyle = on ? tone('accent') : tone('ink');
      ctx.lineWidth = on ? 5 : 3;
      ctx.globalAlpha = on ? 1 : lerp(0.3, 0.85, k);
      if (on) setShadow(ctx, GLOW, 12, 0);
      fan(ctx, r); ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;

      // 이름표 — 겹의 꼭대기에 검은 판 위로
      const label = layers[i] || '';
      const ly = OY - r;
      ctx.font = disp(on ? 900 : 800, on ? 16 : 14);
      const tw = ctx.measureText(label).width;
      const pw = tw + 18, ph = on ? 24 : 21;
      ctx.globalAlpha = k;
      ctx.fillStyle = '#000';
      roundRect(ctx, OX - pw / 2, ly - ph / 2, pw, ph, 5);
      ctx.fill();
      ctx.fillStyle = on ? tone('accent') : tone('ink');
      ctx.fillText(label, OX, ly + (on ? 6 : 5));
      ctx.globalAlpha = 1;
    }

    /* ── 중심 ───────────────────────────────────── */
    ctx.fillStyle = tone('ink');
    ctx.beginPath(); ctx.arc(OX, OY, 5, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    ctx.textAlign = 'left';
    ctx.fillText(spec.originLabel || '시작', 16, OY + 4);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(50, OY); ctx.lineTo(OX - 12, OY); ctx.stroke();

    ctx.textAlign = 'left';
  }
};
