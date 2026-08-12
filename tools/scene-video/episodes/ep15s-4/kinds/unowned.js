/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다.
   경로 좌표를 적을 땐 `(경로)` 라고 표시한다. */
import {
  disp, ease, clamp, lerp, fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* unowned — 지어 놓은 모닥불에 주인이 안 붙고, 그래서 또 짓고 또 짓는다.

   원문 근거:
   > "소유 배정을 건너뜁니다 → '내 모닥불 있음' 플래그가 0으로 남습니다
   >  → 모닥불 목표가 다시 발동합니다"

   자막 0 : 위쪽에 표시등 하나(「내 모닥불 있음」)가 **제자리에서 자라며** 나타나는데
     **안이 빈 채**다. 아래에는 앞 샷에서 지은 불꽃 하나가 그대로 타고 있다.
   자막 1 : 집 자리 둘레 칸에 불꽃이 **0.42초마다 하나씩 다섯 개** 더 앉는다.
     그동안 위 표시등은 끝까지 비어 있다.

   🔴 **표시등을 깜빡이게 하지 않았다.** 원문은 「소유 배정을 **건너뜁니다**」이지
   「붙었다 떨어집니다」가 아니다 — 한 번 켰다 끄면 화면이 원문에 없는 일을 주장한다.
   그래서 **끝까지 빈 채로 두고**, 켜지는 것은 고친 뒤(S4 twoasks)뿐이다.

   🖼 `ADR-V25-15` ① — 라벨 둘(「주인 정하기」·「내 모닥불 있음」)을 다 가려도
   **「위의 칸은 계속 비어 있는데 아래에서는 같은 것이 자꾸 하나씩 늘어난다」**가 읽힌다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **띠와 땅으로 갈랐다.**
     표시등(흰 테두리 26×26 · 경로 y 47~73 · 획 바깥 74.5)은 땅 테두리(위 선 y 80) 위
     띠에 있고, 가장 위 칸 불꽃의 글로우 꼭대기가 y 90 이라 **15.5px** 뜬다.
     표시등 안쪽 강조색 사각(14×14)은 이 샷에서 아예 안 그려진다(알파 0).
     🔑 이 샷의 최소 여유는 집 자리 괄호 ↔ 불꽃 쪽이고, 회차 최소값(**실측 0.80px**)과
     그 사유는 `packed.js`·`noroom.js` 머리 주석에 적었다 — 내 초고 표기가 획 반폭과
     상시 숨을 빼먹은 낙관치였고 검수팀이 다각형 최소거리로 정정했다.

   🔴 정적 대책 — 불꽃 1~6개가 8.2rad/s 로 숨 쉬고(프레임당 최대 1.09px),
   땅 테두리가 90px/초로 흐르고(프레임당 3px), 자막 1 구간에서는 0.42초마다
   새 불꽃이 0.28초에 걸쳐 자란다. */

const PLOT_T = 86, CW = 40, CH = 38, COLS = 7, ROWS = 4;
const PLOT_W = COLS * CW, PLOT_B = PLOT_T + ROWS * CH;   // 280 / 238
const EDGE = 6;                                          // 테두리는 칸 격자보다 6px 바깥
const HC = 3;
const FIRST = { c: 3, r: 3 };                    // 앞 샷에서 지은 첫 모닥불
/* 뒤이어 앉는 다섯 — 집 자리를 둘러싼 칸들이다(가까운 순). */
const NEXT = [{ c: 2, r: 2 }, { c: 4, r: 2 }, { c: 2, r: 1 }, { c: 4, r: 1 }, { c: 3, r: 0 }];
const T0 = 0.20, STEP = 0.42, GROW = 0.28;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const PL = (w - PLOT_W) / 2;
    const cx = c => PL + CW * c + CW / 2;
    const cy = r => PLOT_T + CH * r + CH / 2;

    const c0 = cue(spec.ringCue ?? 0, 0.15, 0.55);
    const st = ease(clamp(c0 / 0.12));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 땅 테두리 · 칸 모서리 점 ─────────────────── */
    const SEG = 11, GAP = 9, PER = SEG + GAP;
    const dashLine = (x0, y0, x1, y1, off, alpha) => {
      if (alpha <= 0.01) return;
      const horiz = y0 === y1;
      const len = horiz ? Math.abs(x1 - x0) : Math.abs(y1 - y0);
      const sx = horiz ? Math.sign(x1 - x0) : Math.sign(y1 - y0);
      const ph = ((off % PER) + PER) % PER;
      ctx.save();
      ctx.globalAlpha = alpha * 0.26; ctx.fillStyle = tone('ink');
      for (let d = -ph; d < len; d += PER) {
        const a = Math.max(0, d), b = Math.min(len, d + SEG);
        if (b <= a) continue;
        if (horiz) ctx.fillRect(x0 + sx * a - (sx < 0 ? (b - a) : 0), y0 - 1.5, b - a, 3);
        else ctx.fillRect(x0 - 1.5, y0 + sx * a - (sx < 0 ? (b - a) : 0), 3, b - a);
      }
      ctx.restore();
    };
    const flow = t * 90;
    const eL = PL - EDGE, eR = PL + PLOT_W + EDGE, eT = PLOT_T - EDGE, eB = PLOT_B + EDGE;
    const eW = eR - eL, eH = eB - eT;
    dashLine(eL, eT, eR, eT, flow, st);
    dashLine(eR, eT, eR, eB, flow - eW, st);
    dashLine(eR, eB, eL, eB, flow - eW - eH, st);
    dashLine(eL, eB, eL, eT, flow - eW * 2 - eH, st);

    for (let r = 0; r <= ROWS; r++) for (let c = 0; c <= COLS; c++) {
      const x = PL + CW * c, y = PLOT_T + CH * r;
      const a = 0.16 + 0.16 * (0.5 + 0.5 * Math.sin(t * 3.4 - (c + r) * 0.55));
      ctx.save();
      ctx.globalAlpha = st * a; ctx.fillStyle = tone('ink');
      ctx.fillRect(x - 2, y - 2, 4, 4);
      ctx.restore();
    }

    /* ── 집이 앉을 자리 ───────────────────────────── */
    const br = 1.2 * (0.5 + 0.5 * Math.sin(t * 7.4));
    const hx = cx(HC), hy = 162;
    ctx.save();
    ctx.globalAlpha = st * 0.62; ctx.strokeStyle = tone('ink');
    ctx.lineWidth = 3; ctx.lineCap = 'butt';
    const corner = (x, y, sx, sy) => {
      ctx.beginPath();
      ctx.moveTo(x + sx * 13, y); ctx.lineTo(x, y); ctx.lineTo(x, y + sy * 13);
      ctx.stroke();
    };
    corner(hx - 23 - br, hy - 28 - br, 1, 1); corner(hx + 23 + br, hy - 28 - br, -1, 1);
    corner(hx - 23 - br, hy + 28 + br, 1, -1); corner(hx + 23 + br, hy + 28 + br, -1, -1);
    ctx.restore();

    /* ── 불꽃들 ───────────────────────────────────── */
    const flame = (c, r, grow, ph) => {
      if (grow <= 0.01) return;
      const hp = 0.5 + 0.5 * Math.sin(t * 8.2 + ph);
      const h = (10 + 8 * hp) * grow, half = 6 * grow, blur = (4 + 2 * hp) * grow;
      const x = cx(c), y = cy(r) + 9;
      ctx.save();
      ctx.globalAlpha = st;
      setShadow(ctx, FAIL_GLOW, blur);
      ctx.strokeStyle = tone('fail'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(x, y - h); ctx.lineTo(x + half, y); ctx.lineTo(x - half, y);
      ctx.closePath(); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    };

    flame(FIRST.c, FIRST.r, 1, 0);
    const s1 = Math.max(0, since(spec.againCue ?? 1));
    for (let i = 0; i < NEXT.length; i++) {
      flame(NEXT[i].c, NEXT[i].r, ease(clamp((s1 - (T0 + i * STEP)) / GROW)), (i + 1) * 1.7);
    }

    /* ── 위 띠 : 「내 모닥불 있음」 표시등 — 끝까지 비어 있다 ── */
    const s0 = Math.max(0, since(spec.ringCue ?? 0));
    const lampK = ease(clamp((s0 - 0.15) / 0.40));
    if (spec.flagLabel && lampK > 0.01) {
      ctx.font = disp(800, 13);
      const tw = ctx.measureText(spec.flagLabel).width;
      const total = 26 + 10 + tw;
      const x0 = (w - total) / 2, lampX = x0 + 13, lampY = 60;
      const sc = lerp(0.78, 1, lampK);
      ctx.save();
      ctx.globalAlpha = st * lampK;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.strokeRect(lampX - 13 * sc, lampY - 13 * sc, 26 * sc, 26 * sc);
      ctx.textAlign = 'left'; ctx.fillStyle = tone('ink'); ctx.font = disp(800, 13);
      ctx.fillText(spec.flagLabel, x0 + 36, lampY + 5);
      ctx.restore();
    }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 30);
      ctx.restore();
    }
    ctx.textAlign = 'left';
  }
};
