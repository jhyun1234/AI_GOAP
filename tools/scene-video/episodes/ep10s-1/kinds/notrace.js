import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* notrace — 말이 건너가면 줄이 그어지는데, 그 줄이 매번 지워진다.

   원문: "그런데 그 대화는 철저하게 '표현 전용'이었습니다. 말풍선은 떴다가 사라지고,
   그걸로 끝이었습니다. 어제 나에게 잔소리를 퍼부은 주민이 오늘 다시 마주쳐도, 게임의
   두뇌 입장에서는 완전히 처음 보는 사이였습니다." / "겉보기엔 살아 있는 마을 같았지만,
   속을 들여다보면 아무것도 쌓이지 않는 마을이었습니다."

   🔴 이 회차의 기본 도형 = **두 사람 사이에 놓인 굽은 줄 하나.** 여기서 그 줄이 처음
   그어지고, 그리고 지워진다. 뒤 세 그림은 전부 이 줄의 변주다.

   🔴 앞 회차와 갈라 놓은 자리: ep09s-2 `brushpast` 는 두 표식이 레인을 오가며 스쳤다.
   여기서는 **두 주민이 좌우에 못 박혀 있고 오가는 것은 말풍선 하나뿐**이다.
   '스쳐 지나감'이라는 사건 자체를 이 회차에서 통째로 안 쓴다.

   🔴 아래 RELATION 자리를 **점선 윤곽**으로 둔 이유: 원문은 값이 0 이었다고 하지 않고
   "관계라는 게 없으니 '저 둘은 사이가 나쁘다' 같은 사실 자체가 존재하질 않았다"고 쓴다.
   그래서 실선 칸(있는데 비었다)이 아니라 **아직 없는 자리**로 그렸고, 끝까지 아무것도
   안 앉는다.

   움직임 두 층:
     ① t — 말이 오가는 한 바퀴(2.4초). 말풍선이 건너가고, 닿으면 줄이 굵게 그어지고,
        곧 왼쪽부터 지워진다. 자막과 무관하게 계속 돈다(정적 구간 대책).
     ② cue — talkCue 로 화면이 켜지고, dayCue 에서 **하루 경계선**이 왼쪽에서 오른쪽으로
        화면을 훑으며 그때 걸려 있던 줄까지 긁어낸다. 경계선이 지나간 뒤에도 ①은 그대로
        되풀이된다 — 지웠다기보다 **애초에 안 남는 것**이라는 뜻이다. */

const AY = 148;          // 두 주민이 선 높이
const R = 15;            // 주민 표식 반지름
const BUB_DY = -58;      // 말풍선이 지나는 높이(주민 위)
const ARC_DIP = 34;      // 줄이 아래로 처지는 깊이
const P = 2.4;           // 말이 한 번 오가는 주기(초)
const TRACK_Y = 238, TRACK_H = 34;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    /* 글자는 폭을 재서 줄인다 — 문자열을 잘라 도망가지 않는다 */
    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const X0 = 18, X1 = w - 18;
    const LX = X0 + 46, RX = X1 - 46;

    const c0 = cue(spec.talkCue ?? 0, 0.15, 0.55);
    const c1 = cue(spec.dayCue ?? 1, 0.20, 0.45);
    const app = clamp(c0 * 6);
    if (app <= 0.02) return;

    /* ── 한 바퀴 ────────────────────────────────────── */
    const u = frac(t / P);
    const fly = clamp((u - 0.05) / 0.44);          // 말풍선이 건너간다
    const born = clamp((u - 0.49) / 0.10);         // 닿자마자 줄이 그어진다
    const gone = clamp((u - 0.61) / 0.30);         // 왼쪽부터 지워진다

    /* ── 하루 경계선 ────────────────────────────────── */
    const dk = ease(clamp(c1 / 0.62));
    const dayX = lerp(X0 - 8, X1 + 8, dk);
    const dayA = clamp(c1 * 6) * clamp(1 - (c1 - 0.62) / 0.22);
    /* 경계선이 지나가는 동안에는 그 왼쪽의 줄이 남아 있지 못한다.
       다 지나가고 선이 스러진 뒤(c1 >= 0.84)에는 다시 ①만 돈다. */
    const ax0 = LX + 14, ax2 = RX - 14;
    const scrape = (c1 > 0.0005 && c1 < 0.84) ? clamp((dayX - ax0) / (ax2 - ax0)) : 0;

    /* ── 줄(관계) — 그어졌다가 지워진다 ─────────────── */
    const kStart = Math.max(gone, scrape);
    if (born > 0.02 && kStart < 0.995) {
      const cyq = AY + 10 + 2 * ARC_DIP;
      const pt = k => {
        const m = 1 - k;
        return [m * m * ax0 + 2 * m * k * ((ax0 + ax2) / 2) + k * k * ax2,
                m * m * (AY + 10) + 2 * m * k * cyq + k * k * (AY + 10)];
      };
      ctx.globalAlpha = app * clamp(born * 1.6);
      ctx.strokeStyle = tone('ink');
      ctx.lineWidth = lerp(3, 9, ease(born));
      ctx.lineCap = 'round';
      ctx.beginPath();
      const N = 44;
      for (let i = 0; i <= N; i++) {
        const [x, y] = pt(lerp(kStart, 1, i / N));
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      }
      ctx.stroke();
      ctx.lineCap = 'butt';
      ctx.globalAlpha = 1;
    }

    /* ── 주민 둘 — 좌우에 못 박혀 있다 ──────────────── */
    const hit = clamp((u - 0.49) / 0.05) * clamp((0.72 - u) / 0.10);   // 말이 닿으면 살짝 물러선다
    for (const s of [-1, 1]) {
      const px = (s < 0 ? LX : RX) + s * 5 * hit;
      ctx.globalAlpha = app;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.arc(px, AY, R, 0, Math.PI * 2); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 말풍선 — 왼쪽에서 오른쪽으로 건너간다 ──────── */
    if (fly > 0.001 && fly < 0.999) {
      const bx = lerp(LX + 8, RX - 8, ease(fly));
      const by = AY + BUB_DY - 10 * Math.sin(Math.PI * fly);
      const a = app * clamp(fly * 9) * clamp((1 - fly) * 9);
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, bx - 15, by - 11, 30, 22, 6); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(bx - 5, by + 11); ctx.lineTo(bx + 1, by + 19); ctx.lineTo(bx + 7, by + 11);
      ctx.stroke();
      ctx.fillStyle = tone('ink');
      for (let i = -1; i <= 1; i++) {
        ctx.beginPath(); ctx.arc(bx + i * 8, by, 2.4, 0, Math.PI * 2); ctx.fill();
      }
      ctx.globalAlpha = 1;
    }

    /* ── 하루 경계선 ────────────────────────────────── */
    if (dayA > 0.02) {
      ctx.globalAlpha = dayA;
      setShadow(ctx, GLOW, 10, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(dayX, 30); ctx.lineTo(dayX, 214); ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── RELATION 자리 — 열리기는 하는데 끝까지 빈다 ── */
    const tk = ease(clamp((c1 - 0.45) / 0.50));
    if (tk > 0.02) {
      ctx.globalAlpha = tk * 0.9;
      ctx.textAlign = 'left';
      ctx.font = disp(800, fit(spec.trackLabel ?? '', 800, 10.5, (X1 - X0) - 8, 8));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.trackLabel ?? '', X0, TRACK_Y - 8);

      ctx.globalAlpha = tk * 0.45;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.setLineDash([10, 9]);
      ctx.lineDashOffset = -(t * 14) % 19;
      roundRect(ctx, X0, TRACK_Y, X1 - X0, TRACK_H, 8); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
