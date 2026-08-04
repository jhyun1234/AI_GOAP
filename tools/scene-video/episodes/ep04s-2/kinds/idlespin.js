import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* idlespin — 따로 도는 고리 셋이 한 뿌리에서 나온다.

   공회전은 '앞으로 안 가면서 계속 도는 것'이다. 그래서 세 버그를 막대나 목록이 아니라
   닫힌 고리 셋으로 그린다. 고리마다 밝은 호가 표식을 앞세우고 돌고 있고, 한 바퀴마다
   같은 자리의 ✕ 를 지난다 — 실패하고, 다시 달리고, 또 실패한다(spinCue·nameCue).

   rootCue 에서 아래에서 뿌리 선이 올라와 셋을 잇는다. 원문이 "이 세 버그는 독립적인 것처럼
   보이지만 뿌리는 같다"고 말하는 자리다. 동시에 표식들이 고리를 **벗어나** 뿌리 선을 타고
   오른쪽으로 흘러 나간다 — 도는 것이 흐르는 것으로 바뀌는 게 이 문단의 결말이다.

   🔴 2026-08-04 — 여기 원래 "hookCue 에서 고리 셋이 흐려지고 그 자리를 이 편의 한 줄이
   넘겨받는다"고 적혀 있었고 실제로 그렇게 그렸다. **둘 다 걷어냈다.**
   ①훅은 이제 캔버스가 아니라 아웃트로 카드의 .oc-hook 이 그린다 — 카드가 .vis 를 픽셀 동일
   좌표에 불투명하게 덮으므로 캔버스의 훅은 뜨자마자 가려졌다(검수 실측).
   ②그래서 고리를 흐리게 하던 fade(= 1 − 0.86·hk)도 같이 지웠다. 훅이 사라진 뒤에도 fade 만
   남아 있으면 **예고 구간에 위쪽 화면이 그냥 어두워지고 아무것도 대신 오지 않는다** —
   실제로 그 상태로 한 판 돌았고 검수가 "위쪽 캔버스가 52%를 잃고 대체가 없다"로 잡았다.
   지금은 예고를 읽는 동안에도 고리 셋이 밝기 그대로 돌고, 뿌리 선의 표식과 꼬리가 흐른다.

   🔴 정지 화면을 만들지 않으려고 고치는 장면을 '멈춤'으로 그리지 않았다. 고리가 멈추면
   그 순간부터 화면이 죽는다. 대신 같은 표식이 계속 움직이되 궤적이 닫힌 고리에서 열린
   직선으로 바뀐다 — 고쳐졌다는 뜻이 움직임의 모양으로 나온다.

   🔴 재분할(2026-08-04)로 더한 것 둘: ① 고리마다 표식 앞의 밝은 호. 원본은 상시 움직이는
   것이 반지름 4.5px 표식 셋뿐이라 정적 판정 문턱에 가까웠고, 무엇보다 '공회전'은 점보다
   호가 훨씬 잘 읽힌다. ② 뿌리 선을 흐르는 표식의 꼬리(26px). 같은 이유다.
   🔑 rk 식(= ease(cue(rootCue, 0.15, 0.6)))과 뿌리 길이 식(= lerp(rootL, rootR,
   ease(clamp(rk/0.6))))·이탈 식(out = clamp((rk−0.45)/0.35))은 **한 글자도 안 바꿨다** —
   S3 sweep 의 delayMs 880 이 이 셋에서 유도된 값이다.

   계속 도는 것 = 호와 표식 셋(고치기 전에는 고리를 돌고, 고친 뒤에는 뿌리 선을 흐른다). */

const SPIN = [2.2, 2.9, 1.8];
const RUN = 3.6;
const ARC = 1.0;      // 고리를 도는 밝은 호의 길이(라디안)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, scene }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const sk = ease(cue(spec.spinCue ?? 0, 0.15, 0.6));
    const nk = ease(cue(spec.nameCue ?? 1, 0.15, 0.65));
    const rk = ease(cue(spec.rootCue ?? 2, 0.15, 0.6));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const loops = spec.loops || [];
    const n = Math.max(1, loops.length);
    const cxs = [62, 176, 290];
    const cy = 74, r = 32;
    const rootY = 206, rootL = 20, rootR = w - 20;
    const failAng = -Math.PI * 0.62;


    for (let i = 0; i < n; i++) {
      const cx = cxs[i] ?? (62 + i * 114);
      const born = clamp(sk * (n + 1) - i);
      if (born < 0.02) continue;
      const s = Math.min(1, spring(born));
      const ringA = clamp(born * 1.7);
      const out = clamp((rk - 0.45) / 0.35);

      /* 고리 */
      ctx.globalAlpha = ringA;
      ctx.lineWidth = 3;
      ctx.strokeStyle = rk > 0.45 ? tone('track') : tone('ink');
      ctx.beginPath(); ctx.arc(cx, cy, r * s, 0, Math.PI * 2); ctx.stroke();

      /* 실패 지점 ✕ */
      const fk = clamp(nk * (n + 1) - i);
      if (fk > 0.05) {
        const k = clamp(fk * 1.5);
        const fx = cx + Math.cos(failAng) * r, fy = cy + Math.sin(failAng) * r;
        const rr = 5 * k;
        ctx.globalAlpha = ringA * k;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(fx - rr, fy - rr); ctx.lineTo(fx + rr, fy + rr);
        ctx.moveTo(fx + rr, fy - rr); ctx.lineTo(fx - rr, fy + rr);
        ctx.stroke();
      }

      /* 쿨다운 걸쇠 — 실패 지점 뒤에 걸린다 */
      if (rk > 0.06) {
        const k = clamp(rk * 1.6 - i * 0.12);
        if (k > 0.02) {
          const a = failAng + 0.42;
          const kk = clamp(rk * 1.6 - i * 0.12);
          ctx.globalAlpha = k;
          setShadow(ctx, GLOW, 9, 0);
          ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
          ctx.beginPath();
          ctx.moveTo(cx + Math.cos(a) * (r - 9 * kk), cy + Math.sin(a) * (r - 9 * kk));
          ctx.lineTo(cx + Math.cos(a) * (r + 9 * kk), cy + Math.sin(a) * (r + 9 * kk));
          ctx.stroke();
          clearShadow(ctx);
        }
      }
      ctx.globalAlpha = 1;

      /* 이름 */
      if (fk > 0.05) {
        ctx.globalAlpha = clamp(fk * 1.5);
        ctx.textAlign = 'center';
        const label = loops[i] || '';
        const fs = fit(label, 800, 11.5, 108, 8);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(label, cx, 126);
        ctx.globalAlpha = 1;
      }

      /* 뿌리로 내려가는 줄기 */
      if (rk > 0.02) {
        const k = ease(clamp(rk / 0.7));
        ctx.globalAlpha = clamp(rk * 2);
        ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(cx, rootY); ctx.lineTo(cx, lerp(rootY, 136, k));
        ctx.stroke();
        ctx.globalAlpha = 1;
      }

      /* 계속 도는 것 — 고리를 돌던 호와 표식이 뿌리 선으로 나간다 */
      {
        ctx.fillStyle = tone('accent');
        ctx.strokeStyle = tone('accent');
        if (out < 0.5) {
          const u = frac(t / SPIN[i % 3]) * Math.PI * 2 + failAng;
          // 공회전 — 표식 앞을 도는 밝은 호. 매 프레임 큰 면적이 자리를 옮긴다.
          ctx.globalAlpha = ringA * 0.9;
          ctx.lineWidth = 4;
          ctx.beginPath(); ctx.arc(cx, cy, r, u - ARC, u); ctx.stroke();

          ctx.globalAlpha = clamp(born * 1.7);
          setShadow(ctx, GLOW, 8, 0);
          ctx.beginPath();
          ctx.arc(cx + Math.cos(u) * r, cy + Math.sin(u) * r, 4.5, 0, Math.PI * 2);
          ctx.fill();
          clearShadow(ctx);
        } else {
          const u = frac(t / RUN + i / n);
          const mx = lerp(rootL + 6, rootR - 6, u);
          const a = clamp(born * 1.7) * (1 - Math.abs(u - 0.5) * 1.2);
          ctx.globalAlpha = a;
          setShadow(ctx, GLOW, 8, 0);
          // 꼬리 — 흐르는 방향이 보이고, 정적 판정에 걸릴 만큼 작지도 않다
          ctx.lineWidth = 4;
          ctx.beginPath();
          ctx.moveTo(Math.max(rootL, mx - 26), rootY); ctx.lineTo(mx, rootY);
          ctx.stroke();
          ctx.beginPath(); ctx.arc(mx, rootY, 5, 0, Math.PI * 2); ctx.fill();
          clearShadow(ctx);
        }
        ctx.globalAlpha = 1;
      }
    }

    /* ── 뿌리 ─────────────────────────────────────── */
    if (rk > 0.02) {
      const k = ease(clamp(rk / 0.6));
      ctx.globalAlpha = clamp(rk * 2);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(rootL, rootY);
      ctx.lineTo(lerp(rootL, rootR, k), rootY);
      ctx.stroke();
      ctx.globalAlpha = 1;

      if (rk > 0.35 && spec.root) {
        const kk = clamp((rk - 0.35) / 0.45);
        ctx.globalAlpha = kk;
        ctx.textAlign = 'center';
        const fs = fit(spec.root, 900, 16, w - 40, 11);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.root, w / 2, rootY + 30);
        ctx.globalAlpha = 1;
      }
      if (rk > 0.6 && spec.fixNote) {
        const kk = clamp((rk - 0.6) / 0.4);
        ctx.globalAlpha = kk * 0.95;
        ctx.textAlign = 'center';
        const fs = fit(spec.fixNote, 700, 11, w - 24, 8);
        ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.fixNote, w / 2, rootY + 52);
        ctx.globalAlpha = 1;
      }
    }

    /* 🔴 훅 카드는 여기서 그리지 않는다(2026-08-04 사용자 판정).
       .outrocard 가 .vis 와 좌표가 한 픽셀도 다르지 않고 배경이 불투명이라, 마지막
       OUTRO_MS(2.6초) 동안 이 캔버스는 통째로 덮인다 — 검수 실측으로 ep04s-3 은 훅이
       단 한 프레임도 보인 적이 없었고(카드 등장 35,765ms vs 예고 자막 35,776ms),
       가장 오래 보인 ep05s-1 도 100ms 였다. 훅은 engine/index.html 의 .oc-hook 이 맡아
       카드 안에서 2.6초 내내 서 있다. kind 는 그림에만 집중한다.
       🔑 "두 곳에 적으면 갈린다"가 원래 원칙이었는데 카드와 캔버스 사이에서 깨져 있었다. */
ctx.textAlign = 'left';
  }
};
