import {
  disp, ease, clamp, lerp, frac, easeOut,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* faceoff — 흘러가던 둘이 멈춰 서서 서로를 보고, 성립하는 원이 반으로 줄어든다.

   원문: "그래서 대화가 성립하면 잠깐 멈춰 서로를 마주보게 만들고, 대화가 성립하는
   반경도 4타일에서 2타일로 줄였습니다."

   🔴 앞의 두 샷과 **같은 도형이 뒤집히는 자리**다. brushpast 의 레인 둘이 그대로 있고
   (y 118 · 182), wideband 의 원이 그대로 있는데, 이번에는 표식이 **멈추고** 원이 **줄어든다.**

   🔴 감속을 밝기가 아니라 **자국의 간격**으로 그린다. 지나온 자리에 남는 눈금이
   easeOut 간격이라 끝으로 갈수록 촘촘해진다 — 멈춘다는 뜻을 길이가 진다.

   🔴 값 두 개(4 · 2)는 **같은 자리에서 한 글자가 다른 글자로 바뀌는 방식**으로 보인다.
   두 문자열을 나란히 띄우면 화면이 자막을 옮겨 적는 꼴이 되고, 줄어드는 것은 값이
   아니라 **원**이어야 하기 때문이다. 4 는 흰색(이전), 2 는 강조색(지금)이다.

   🔴 반지름 88 → 44 는 정확히 절반이다. 원문이 4 → 2 라고 못박았으므로 비를 그대로 그렸고,
   앞 샷(wideband)의 88 과 같은 값에서 출발한다.

   🔴 멈춘 뒤에 계속 도는 것: 두 원의 테두리 점선과, 둘 사이를 오가는 말풍선이다.
   말풍선이 오간다는 것이 「대화가 성립했다」이고, 앞 샷에서 찰나에만 뜨던 것과 대비된다. */

const LA = 118, LB = 182;
const CY = 150, R4 = 88, R2 = 44;
const TICKS = 7;
const BUB_PER = 2.4;

const pp = (t, p) => { const u = frac(t / p); return u < 0.5 ? u * 2 : 2 - u * 2; };

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

    const X0 = 20, cx = w * 0.5;
    const ax0 = X0 + 24, ax1 = cx - 26;
    const bx0 = w - X0 - 24, bx1 = cx + 26;

    const c0 = cue(spec.stopCue ?? 0, 0.15, 0.8);
    const mk = ease(clamp(c0 / 0.62));                    // 다가와서 멈춘다
    const fk = ease(clamp((c0 - 0.45) / 0.4));            // 서로를 향해 돌아본다
    const sk = ease(cue(spec.radiusCue ?? 1, 0.15, 0.75));
    const ak = clamp(sk * 3.2);                           // 원이 나타난다
    const shk = ease(clamp((sk - 0.32) / 0.55));          // 원이 줄어든다

    const ax = lerp(ax0, ax1, mk), bx = lerp(bx0, bx1, mk);

    /* ── 감속 자국 — 끝으로 갈수록 촘촘해진다 ────────
       🔴 원보다 **먼저** 그린다. 자국이 원의 테두리와 한 점에서 만나는데,
       나중에 그리면 흐린 흰 선이 강조색 원 위에 얹혀 그 점에서 두 색이 섞인다. */
    const trail = (x0, x1, y) => {
      for (let i = 0; i < TICKS; i++) {
        const e = easeOut(i / (TICKS - 1));
        const a = clamp((mk - e * 0.92) * 5) * 0.65;
        if (a <= 0.02) continue;
        const px = lerp(x0, x1, e);
        ctx.globalAlpha = a;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(px, y - 8); ctx.lineTo(px, y + 8); ctx.stroke();
        ctx.globalAlpha = 1;
      }
    };
    trail(ax0, ax1, LA);
    trail(bx0, bx1, LB);

    /* ── 반경 — 이전(흰 점선)과 지금(강조 점선) ─────── */
    if (ak > 0.02) {
      ctx.globalAlpha = ak * 0.55 * (1 - 0.45 * shk);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.setLineDash([12, 9]);
      ctx.lineDashOffset = -(t * 15) % 21;
      ctx.beginPath(); ctx.arc(cx, CY, R4, 0, Math.PI * 2); ctx.stroke();

      const r = lerp(R4, R2, shk);
      ctx.globalAlpha = ak;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.lineDashOffset = (t * 15) % 21;
      ctx.beginPath(); ctx.arc(cx, CY, r, 0, Math.PI * 2); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 라벨과, 한 자리에서 바뀌는 값 ──────────────── */
    if (spec.label) {
      /* 🔴 원과 같은 cue 에 물린다 — 원이 없는데 라벨만 떠 있으면 무엇의 반경인지 알 수 없다 */
      const a = ak;
      if (a > 0.02) {
        ctx.globalAlpha = a;
        ctx.textAlign = 'left';
        const fs = fit(spec.label, 800, 12.5, 120, 9);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.label, X0, 34);
        const lw = ctx.measureText(spec.label).width;
        ctx.globalAlpha = 1;

        const vx = X0 + lw + 10;
        ctx.font = disp(900, 19);
        if (spec.fromValue) {
          ctx.globalAlpha = ak * (1 - shk);
          ctx.fillStyle = tone('ink');
          ctx.fillText(String(spec.fromValue), vx, 36);
          ctx.globalAlpha = 1;
        }
        if (spec.toValue) {
          ctx.globalAlpha = ak * shk;
          ctx.fillStyle = tone('accent');
          ctx.fillText(String(spec.toValue), vx, 36);
          ctx.globalAlpha = 1;
        }
      }
    }

    /* ── 표식 둘 — 멈춘 뒤 서로를 향해 돌아본다 ──────── */
    const one = (x, y, a0, a1, a) => {
      if (a <= 0.02) return;
      const ang = lerp(a0, a1, fk);
      ctx.save();
      ctx.translate(x, y); ctx.rotate(ang);
      ctx.globalAlpha = a;
      ctx.strokeStyle = fk > 0.6 ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(8, 0); ctx.lineTo(23, 0); ctx.stroke();
      ctx.restore();

      ctx.globalAlpha = a;
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(x, y, 6.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    };
    const toB = Math.atan2(LB - LA, bx1 - ax1);
    one(ax, LA, 0, toB, clamp(mk * 4));
    one(bx, LB, Math.PI, Math.PI + toB, clamp(mk * 4 - 0.3));

    /* ── 오가는 말 ──────────────────────────────────
       🔴 **표식 위로 못 가게 두 겹으로 막았다**(검수 C6 · D1). 1차본은 u ∈ [0, 1] 이라 양 끝에서
       말풍선이 흰 표식을 통째로 삼켰고, 가운데 강조색 점(r 2.4)이 흰 원(r 6.5) 바로 위에
       얹혔다. 🔑 **팔레트 게이트가 구조적으로 못 보는 자리다** — 반투명 강조색이 흰 것 위에
       얹혀도 hue 가 152 로 약분되어 통과한다. 그래서 좌표로 푼다:
       ⓐ**u 를 0.22~0.78 로 줄이고** ⓑ**경로 법선 (0.776, −0.630) 으로 30px 밀었다.**

       🔴 **법선 22 로는 모자랐다(검수 D1).** 표식의 **원**은 떨어졌는데 같은 `one()` 이 그리는
       **방향선**(중심에서 8~18~23px · lineWidth 3)이 남아 120fps 표본 43개에서 −3.00px 로
       스쳤다. 내가 적었던 「5px 뜬다」는 **상자 테두리 반폭 1.5px 를 안 뺀 값**이었다.
       🔴 **아래 증명은 거짓이었다(2026-08-07 마스터 M2). 고쳐 적는다.**
       전제가 「두 방향선은 **연결선 위에** 놓인다」인데, `one()` 의 각이 `lerp(0, toB, fk)` 라
       **회전 중에는 방향선이 거의 수평 = 말풍선을 밀어낸 법선 쪽**을 향한다. 전제는 `fk=1`
       에서만 참이다. 그래서 아래 「최소 6.25px」는 **정상상태만 잰 값**이다.
         정상상태(fk=1)만 → 최소 **+9.30px**(표식 B 방향선)  ← 내가 잰 자리
         전 구간        → 최소 **−1.12px**(표식 A 방향선)  ← 안 잰 자리
         30fps 실프레임 → frame 420~424(abs 14,000~14,133) **5프레임 음수**
       🔴 **이 kind 를 다른 회차로 복사하기 전에 이걸 닫아라.** 권장은 말풍선 등장 문턱을
       `fk > 0.25` → `fk > 0.72` 로(회전이 끝난 뒤 띄우기) — 법선을 더 미는 게 아니라
       **원인을 없앤다.** 이 회차는 가려지는 글자 0 · 5프레임(0.17초) · 3.1 디바이스 px 라
       마스터가 비차단으로 승인했다(이전 −3.00px/0.371초에서 개선된 것이기도 하다).
       🔑 교훈: **움직이는 도형은 「정상상태」가 아니라 「전 구간」에서 잰다.** 각이 시간에
       따라 변하면 「어느 직선 위에 놓인다」는 전제 자체가 구간마다 다르다.
       (아래는 정상상태 기준의 옛 계산 — 기록으로 남긴다)
         상자의 법선 방향 지지폭 = 18.5·|0.776| + 12.5·|0.630| = **22.24px** (테두리 반폭 포함)
         + 방향선 반폭 1.5 → **23.74px 이상**이어야 한다. 22 는 이 값 아래였다.
       **30 을 쓰면 여유가 6.26px** 이고, 실제 최소 간극(바깥선 대 바깥선)은 아래와 같다:
         표식 B 방향선 **6.25px**(u = 0.78, 최소) · 표식 A 방향선 8.81 · 표식 A 원 9.72 ·
         표식 B 원 13.98. 상자 모서리의 최대 중심거리는 57.5px 라 **흰 점선 원(R4 88)의
         테두리도 안 넘는다.**
       왕복 거리는 82.5 → 46px 로 줄지만 원 둘의 점선이 이 샷의 면적을 이미 지고 있다. */
    if (fk > 0.25) {
      const a = clamp((fk - 0.25) / 0.35);
      const u = lerp(0.22, 0.78, pp(t, BUB_PER));
      const nx = 0.776 * 30, ny = -0.630 * 30;        // 경로 법선으로 밀어낸 양
      const mx = lerp(ax, bx, u) + nx, my = lerp(LA, LB, u) + ny;
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, mx - 17, my - 11, 34, 22, 6); ctx.stroke();
      ctx.fillStyle = tone('accent');
      for (let i = -1; i <= 1; i++) {
        ctx.beginPath(); ctx.arc(mx + i * 8, my, 2.4, 0, Math.PI * 2); ctx.fill();
      }
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
