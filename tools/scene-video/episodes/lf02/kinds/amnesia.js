import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* amnesia — 세션 칸이 열렸다가 통째로 비워지기를 반복한다. 각 칸 아래 깊이 막대는
   매번 다른 길이로 내려간다 — 그게 「판단의 깊이가 세션마다 들쭉날쭉했다」이다.
   깊이는 rnd(정수)로 뽑는다(같은 씨앗 = 같은 값, 결정적).
   연속 모션은 지우기 순환 그 자체다. 이 샷에서 멈추는 그림은 자기모순이 된다 —
   말하는 내용이 「매번 비워진다」이기 때문이다.
   문자열 출처: published/2026-07-24-unity-goap-methodology-spec-constellation.html 6·8행. */

const N = 5;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kNew = ease(cue(0));     // 매번 새로운 세션
    const kLose = ease(cue(1));    // 기억을 잃고 출근한다
    const kVary = ease(cue(2));    // 깊게 파고 얕게 지나가고
    const kJudge = ease(cue(3));   // 판단의 깊이가 들쭉날쭉

    const gap = 12;
    const cw = (w - M * 2 - gap * (N - 1)) / N;
    const cy = 46, ch = 58;
    const dy = cy + ch + 14, dh = h - dy - 34;

    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('SESSION', M, 32);

    // 순환 : 한 칸이 열려 있는 시간 1.5초, 다섯 칸이 순서대로
    const CYC = 1.5;
    const live = Math.floor(t / CYC) % N;
    const phase = (t % CYC) / CYC;

    for (let i = 0; i < N; i++) {
      const cx = M + i * (cw + gap);
      const isLive = i === live;
      // 등장
      const kIn = clamp((kNew - i * 0.08) / 0.4);
      // 칸
      ctx.strokeStyle = isLive ? tone('accent') : tone('track');
      ctx.lineWidth = isLive ? 4 : 3;
      ctx.save(); ctx.globalAlpha = clamp(0.3 + 0.7 * kIn);
      roundRect(ctx, cx, cy, cw, ch, 4); ctx.stroke();
      ctx.restore();

      // 번호
      ctx.font = mono(700, 9); ctx.fillStyle = tone('sub');
      ctx.fillText(`#${i + 1}`, cx + 7, cy + 15);

      // 내용 — 열려 있는 동안만 찬다. 그 뒤 통째로 지워진다.
      if (isLive && kIn > 0.1) {
        const fill = phase < 0.6 ? ease(phase / 0.6) : 1 - ease((phase - 0.6) / 0.4);
        ctx.fillStyle = tone('accent');
        const lines = 3;
        for (let j = 0; j < lines; j++) {
          const lw = (cw - 22) * (j === 2 ? 0.6 : 1);
          const kj = clamp((fill - j * 0.12) / 0.6);
          if (kj <= 0.02) continue;
          ctx.fillRect(cx + 11, cy + 24 + j * 11, lw * kj, 5);
        }
      }

      // 깊이 막대 — 세션마다 다르다
      if (kVary > 0.03) {
        const depth = 0.28 + 0.68 * rndDepth(i);
        const kk = clamp((kVary - i * 0.07) / 0.45);
        ctx.save(); ctx.globalAlpha = clamp(kk);
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(cx + cw / 2, dy); ctx.lineTo(cx + cw / 2, dy + dh);
        ctx.stroke();
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 5;
        ctx.beginPath();
        ctx.moveTo(cx + cw / 2, dy); ctx.lineTo(cx + cw / 2, dy + dh * depth * ease(kk));
        ctx.stroke();
        ctx.restore();
      }
    }

    // 판정
    if (kJudge > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kJudge);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('DEPTH', M, dy + 12);
      ctx.font = disp(900, 16); ctx.fillStyle = tone('ink');
      const s = '들쭉날쭉';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, w - M - sw, h - 14);
      ctx.restore();
    }
  },
};

/* 결정적 깊이 — lib 의 rnd 와 같은 계열을 쓰되 이 그림 전용 씨앗을 둔다. */
function rndDepth(i) {
  const seeds = [0.86, 0.24, 0.61, 0.15, 0.72];
  return seeds[i % seeds.length];
}
