import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* onecall — 제각각 다른 곳을 가리키던 바늘 열 개가 **제자리에서 동시에** 같은 방향으로 돈다.

   원문: "같은 세계를 보고 모두가 같은 틱에 같은 판단을 내리니, 개별 지능이 아니라
   하나의 스크립트로 움직이는 무리처럼 보였던 겁니다."

   🔴 **1차 검수 R4 로 통째로 갈아엎은 그림이다.** 1차본(sametick)은 「가로 시간축 + 한 눈금
   위의 세로 기둥 + TICK 라벨 + 훑는 표식」이었는데, 형제 편 ep08s-2 의 같은 이름 그림과
   구도가 통째로 같았다. 두 편 모두 그 컷이 가장 오래 머무는 페이오프였다.
   🔑 8-2 는 네 그림이 전부 **1차원 축 위의 자리**(점이 축 어디에 앉는가)다. 그래서 이 편은
   **눈금 있는 측정 축을 하나도 쓰지 않는다** — 여기서 값을 지는 것은 자리가 아니라 각도다.

   🔴 그리고 이 편의 사건은 8-2 처럼 「값이 겹친 것」이 아니라 **「행동이 겹친 것」**이다.
   그래서 아무도 움직여 오지 않는다 — **이동이 0 이다.** 열은 처음부터 제 칸에 있고,
   바뀌는 것은 위치가 아니라 **가리키는 방향**뿐이다. 모이는 그림(수렴)을 쓰면 8-2 의
   sametick 과도, ep07s-1 의 oneheap 과도 같은 계열이 된다.

   🔴 **정렬은 스태거 없이 열이 정확히 같은 진행도(ak)로 돈다.** 그게 "같은 틱"이다.
   앞 샷(oneshove)은 출발 높이가 달라 넘는 시점이 조금씩 어긋나는데, 여기서는 한 프레임도
   안 어긋난다 — 두 샷이 같은 사건을 다르게 재는 자리다.

   🔴 흔들림의 **위상**이 개별성의 지표다. 정렬 전에는 열이 저마다 다른 주기·위상으로
   흔들리고(살아 있는 열), 정렬 뒤에는 주기도 위상도 하나로 수렴해 **흔들림까지 한 몸**이
   된다. 밝기를 흔든 것이 아니라 각도를 흔든 것이고, 이 흔들림이 t 의 함수라 문턱이 없다.

   🔴 숫자를 한 자도 안 적었다. 인원 열은 **칸의 개수**로만 진다(앞 샷의 막대 열 개와 같은
   방식이다). 각 칸이 누구인지도 안 적었다 — 원문이 이 대목에서 이름을 부르지 않는다. */

const COLS = 5, ROWS = 2;
const AIM = -Math.PI / 2;                 // 정렬된 뒤 열이 함께 가리키는 방향
const WOBBLE = 0.10;                      // 흔들림 폭(rad)
const COMMON_W = 2.1, COMMON_P = 0.6;     // 정렬 뒤 열이 공유하는 주기·위상

/* 제각각인 출발 각도와 저마다의 흔들림 — 값이 아니라 도형 파라미터다 */
const START = [-2.30, -0.55, 1.95, 0.40, 2.75, -1.20, 0.95, -2.85, 1.45, -0.15];
const OWN_W = [1.70, 2.30, 1.35, 2.90, 2.05, 1.55, 2.55, 1.90, 2.75, 1.45];
const OWN_P = [0.30, 1.90, 3.40, 5.10, 0.90, 2.60, 4.30, 6.00, 1.40, 3.90];

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

    const N = Math.min(spec.cells ?? 10, START.length);
    const X0 = 16, CW = 56, CH = 56, GX = 10, GY = 16, TOP = 84;
    const cellX = c => X0 + c * (CW + GX);
    const cellY = r => TOP + r * (CH + GY);

    const c0 = cue(spec.alignCue ?? 0, 0.15, 0.8);
    /* 🔴 ak 에 i 가 안 들어간다 — 열이 정확히 같은 순간에 같은 만큼 돈다 */
    const ak = ease(clamp((c0 - 0.42) / 0.34));
    const locked = ak > 0.85;

    /* ── 위 라벨 ────────────────────────────────────── */
    if (spec.gridLabel) {
      const a = clamp(c0 * 5);
      if (a > 0.02) {
        ctx.globalAlpha = a;
        ctx.textAlign = 'left';
        ctx.font = disp(800, fit(spec.gridLabel, 800, 12, 140, 9));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.gridLabel, X0, 62);
        ctx.globalAlpha = 1;
      }
    }
    if (spec.matchLabel) {
      const a = clamp((c0 - 0.70) / 0.16);
      if (a > 0.02) {
        ctx.globalAlpha = a;
        ctx.textAlign = 'right';
        ctx.font = disp(900, fit(spec.matchLabel, 900, 12.5, 140, 9));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.matchLabel, X0 + COLS * (CW + GX) - GX, 62);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 칸 열 개 ───────────────────────────────────── */
    for (let i = 0; i < N; i++) {
      const a = clamp(c0 * 6 - i * 0.09);
      if (a <= 0.02) continue;
      const c = i % COLS, r = (i / COLS) | 0;
      const x = cellX(c), y = cellY(r);
      const cx = x + CW / 2, cy = y + CH / 2;

      /* 칸 — 테두리 점선이 계속 흐른다(이 샷에서 면적이 가장 큰 움직임) */
      ctx.globalAlpha = a * 0.9;
      ctx.setLineDash([9, 7]);
      ctx.lineDashOffset = -(t * 16) % 16;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      roundRect(ctx, x, y, CW, CH, 7); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;

      /* 정렬된 뒤에는 칸 위쪽 모서리에 같은 표식이 하나씩 선다 */
      if (locked) {
        ctx.globalAlpha = a * clamp((ak - 0.85) / 0.15);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(cx, y); ctx.lineTo(cx, y + 7); ctx.stroke();
        ctx.globalAlpha = 1;
      }

      /* 바늘 — 위치는 안 바뀌고 방향만 바뀐다.
         흔들림의 주기·위상이 저마다에서 하나로 수렴한다. */
      const base = lerp(START[i], AIM, ak);
      const spd = lerp(OWN_W[i], COMMON_W, ak);
      const pha = lerp(OWN_P[i], COMMON_P, ak);
      const ang = base + WOBBLE * Math.sin(t * spd + pha);

      ctx.save();
      ctx.translate(cx, cy);
      ctx.rotate(ang);
      ctx.globalAlpha = a;
      ctx.strokeStyle = locked ? tone('accent') : tone('ink');
      ctx.lineWidth = 3;
      if (locked) setShadow(ctx, GLOW, 8, 0);
      ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(20, 0); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();

      ctx.globalAlpha = a;
      ctx.fillStyle = locked ? tone('accent') : tone('ink');
      ctx.beginPath(); ctx.arc(cx, cy, 3, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 판정 ───────────────────────────────────────── */
    if (spec.note) {
      const k = ease(clamp((c0 - 0.72) / 0.28));
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.note, 900, 18, w - 30, 11);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.note, w / 2, 262 - 10 * (1 - k));
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
