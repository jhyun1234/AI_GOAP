/* 🔴 숫자에 mono 를 쓰지 않는다 — 'JetBrains Mono 라틴' 에는 '줄' 이 없어서
   그 한 글자만 시스템 폴백으로 떨어지고 한 줄 안에서 폰트가 갈린다(lib.js 45~48행). */
import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* windback — 지워지는 것은 방금 본 마을이 아니다.

   🔴 1차 컷이 여기서 틀렸다(사용자 지적 2026-08-01). S1~S8 이 보여준 것은 **M0 재설계
      이후의 지금 마을**인데(성향 벡터·직업 일곱·화폐는 전부 재설계 이후 시스템), 마지막에
      "이 마을이 통째로 지워집니다" 라고만 하니 **방금 본 그 마을이 없어진다**는 뜻이 됐다.
      실제로 지워지는 것은 0·1·2편의 舊 아키텍처이고(커밋 b175ddf, 2026-07-13,
      24,800줄 → 5,977줄), 지금 마을은 **그게 지워진 자리에 새로 지어진 것**이다.

   그래서 이 그림은 나이테를 한 번이 아니라 **두 번** 다룬다. 같은 원점, 같은 반지름,
   선 종류와 이름표만 다르다 — 옛 마을은 점선, 지금 마을은 실선.
     ① 지금 마을(실선)이 선다 → ② 물러나고 옛 마을(점선)이 그 자리에 드러난다
     → ③ 옛 마을이 안으로 빨려 사라진다 → ④ 숫자 → ⑤ 지금 마을이 그 빈 자리에서 다시 자란다.

   🔴 겹 수를 두 마을 사이에 다르게 두지 않았다. 다르게 두면 "옛 마을은 축이 넷이었다" 를
      주장하게 되는데 그런 근거가 없다. 크기 이야기는 숫자가 하고, 나이테는 '같은 자리' 만 말한다.

   회차의 기본 도형 안에서 이 그림의 몫은 '바깥으로 자란 것이 안으로 되감기고, 다시 자란다'.
   계속 도는 것 = 중심에서 바깥으로 번지는 맥동 고리(위치 이동). */

const OX = 176, OY = 286;
const R0 = 26, RSTEP = 25, N = 6;
const RMAX = R0 + RSTEP * (N - 1);   // 151 — 부채꼴 ±40° 에서 x 79~273, 가장자리 안전
const FAN = Math.PI * 40 / 180;
const PULSE = 3.2;

function fan(ctx, r) {
  ctx.beginPath();
  ctx.arc(OX, OY, r, -Math.PI / 2 - FAN, -Math.PI / 2 + FAN);
}

/** 부채꼴 꼭대기에 검은 판을 깔고 얹는 이름표 */
function apexLabel(ctx, text, r, color, alpha, weight = 900, size = 14) {
  ctx.font = disp(weight, size);
  const tw = ctx.measureText(text).width;
  const y = OY - r;
  ctx.globalAlpha = alpha;
  ctx.fillStyle = '#000';
  roundRect(ctx, OX - (tw + 18) / 2, y - 11, tw + 18, 22, 5); ctx.fill();
  ctx.fillStyle = color;
  ctx.textAlign = 'center';
  ctx.fillText(text, OX, y + 5);
  ctx.globalAlpha = 1;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, scene }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    const rk = ease(cue(spec.recordCue ?? 0, 0.15, 0.5));
    const ok = ease(cue(spec.oldCue ?? 1, 0.12, 0.55));
    const ek = ease(cue(spec.eraseCue ?? 2, 0.12, 0.6));
    const nk = ease(cue(spec.numCue ?? 3, 0.12, 0.5));
    const gk = ease(cue(spec.regrowCue ?? 4, 0.12, 0.6));

    ctx.textBaseline = 'alphabetic';
    ctx.textAlign = 'center';

    /* ── 중심에서 번지는 맥동 (계속 돈다) ────────
       🔴 고리 하나에 알파 (1-u) 를 걸었더니 주기 끝자락에서 거의 안 보여, 그 동안 화면이
          멈춘 것으로 잡혔다(정적 구간 3.0s = 한도에 정확히 걸침). 반 주기 어긋난 고리 둘로
          바꾸고 포락선을 sin(πu) 로 두면 언제나 하나는 한창 진해서 빈 구간이 없다. */
    {
      ctx.setLineDash([5, 7]);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      for (let i = 0; i < 2; i++) {
        const u = frac(t / PULSE + i * 0.5);
        ctx.globalAlpha = Math.sin(u * Math.PI) * 0.9;
        fan(ctx, lerp(10, RMAX + 14, u)); ctx.stroke();
      }
      ctx.globalAlpha = 1;
      ctx.setLineDash([]);
    }

    /* ── 지금 마을 (실선) ───────────────────────
       처음에 서 있다가 옛 마을에 자리를 내주고, 마지막에 그 자리에서 다시 자란다. */
    const nowFade = rk * (1 - ok * 0.95);
    const nowA = Math.max(nowFade, gk);
    const regrown = gk > 0.02;
    if (nowA > 0.02) {
      const reach = regrown ? lerp(0, N, clamp(gk * 1.15)) : N;
      for (let i = 0; i < N; i++) {
        const k = clamp(reach - i);
        if (k <= 0.01) continue;
        ctx.strokeStyle = regrown ? tone('accent') : tone('ink');
        ctx.lineWidth = regrown ? 5 : 3;
        ctx.globalAlpha = nowA * lerp(0.35, 1, k);
        if (regrown) setShadow(ctx, GLOW, 12, 0);
        fan(ctx, R0 + RSTEP * i); ctx.stroke();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 0·1·2편의 마을 (점선) ─────────────────
       oldCue 에서 같은 자리에 드러나고, eraseCue 에서 안쪽으로 빨려 사라진다. */
    let oldA = 0;
    if (ok > 0.02) {
      ctx.setLineDash([7, 6]);
      for (let i = 0; i < N; i++) {
        const base = R0 + RSTEP * i;
        // 바깥 겹부터 늦게 출발해 안으로 말려 들어간다
        const pull = clamp((ek - (N - 1 - i) * 0.07) / 0.55);
        const a = ok * (1 - ease(pull));
        if (a <= 0.02) continue;
        oldA = Math.max(oldA, a);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.globalAlpha = a;
        fan(ctx, lerp(base, R0 - 8, ease(pull))); ctx.stroke();
        ctx.globalAlpha = 1;
      }
      ctx.setLineDash([]);
    }

    // 중심
    ctx.fillStyle = regrown ? tone('accent') : tone('ink');
    ctx.globalAlpha = Math.max(nowA, oldA, 0.3);
    ctx.beginPath(); ctx.arc(OX, OY, 5, 0, Math.PI * 2); ctx.fill();
    ctx.globalAlpha = 1;

    /* ── 이름표 — 둘 중 진한 쪽만 그린다 ────────
       크로스페이드 중에 둘 다 반투명으로 겹치면 글자가 뭉개진다. */
    if (oldA >= nowA && oldA > 0.15) {
      apexLabel(ctx, spec.oldLabel || '', RMAX, tone('ink'), oldA, 800, 13);
    } else if (nowA > 0.15) {
      const r = regrown ? R0 + RSTEP * Math.max(0, Math.min(N - 1, Math.floor(N * clamp(gk * 1.15)) - 1)) : RMAX;
      apexLabel(ctx, spec.nowLabel || '', r, regrown ? tone('accent') : tone('ink'), nowA, 900, 14);
    }

    /* ── 다음 편 이름 ──────────────────────────── */
    if (ek > 0.05) {
      ctx.globalAlpha = ek * 0.9;
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 13);
      ctx.textAlign = 'center';
      ctx.fillText(spec.shrinkLabel || '', w / 2, 30);
      ctx.globalAlpha = 1;
    }

    /* ── 숫자 한 줄 : 24,800줄 → 5,977줄 ─────── */
    if (nk > 0.02) {
      const before = spec.before || '', after = spec.after || '';
      const fB = disp(800, 17), fA = disp(900, 25), fArr = disp(900, 18);
      ctx.font = fB; const wB = ctx.measureText(before).width;
      ctx.font = fArr; const wArr = ctx.measureText('→').width;
      ctx.font = fA; const wA = ctx.measureText(after).width;
      const gap = 11;
      const total = wB + gap + wArr + gap + wA;
      let x = w / 2 - total / 2;
      const baseY = 62;

      ctx.textAlign = 'left';
      ctx.globalAlpha = nk;

      ctx.font = fB; ctx.fillStyle = tone('sub');
      ctx.fillText(before, x, baseY);
      // 그어지는 선
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(x - 2, baseY - 6);
      ctx.lineTo(x - 2 + (wB + 4) * ease(clamp(nk * 1.6)), baseY - 6);
      ctx.stroke();
      x += wB + gap;

      ctx.font = fArr; ctx.fillStyle = tone('sub');
      ctx.fillText('→', x, baseY);
      x += wArr + gap;
      ctx.globalAlpha = 1;

      const s = spring(clamp(nk * 1.4));
      ctx.save();
      ctx.translate(x + wA / 2, baseY - 8); ctx.scale(s, s);
      setShadow(ctx, GLOW, 14, 0);
      ctx.font = fA; ctx.fillStyle = tone('accent'); ctx.textAlign = 'center';
      ctx.fillText(after, 0, 8);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── hook — 마지막까지 남는 한 줄 ──────────── */
    if (gk > 0.25 && scene?.hook) {
      ctx.textAlign = 'center';
      ctx.globalAlpha = clamp((gk - 0.25) / 0.5) * 0.72;
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 12);
      String(scene.hook).split('\n').forEach((l, i) => {
        ctx.fillText(l, w / 2, 92 + i * 18);
      });
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
