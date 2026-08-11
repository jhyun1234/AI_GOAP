/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다.
   경로 좌표를 적을 땐 `(경로)` 라고 표시한다. */
import {
  disp, ease, clamp, lerp, fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* noroom — 부탁받은 집이 서려다 도로 눌리고, 남아 있던 빈 칸까지 다 덮인다.

   원문 근거:
   > "모닥불 우선순위가 부탁받은 집보다 높아서 부탁은 시작도 못 합니다
   >  → 반경이 만원이 되어 밭 지을 자리까지 잡아먹습니다."

   자막 0 : 집 자리에서 집 윤곽이 4px 솟으며 흐리게 떠오르다(0.15~0.42초)
     **도로 사라진다**(0.42~0.62초 · 효과음 thud 가 0.42초에 온다).
     그 뒤 남아 있던 빈 칸 열 개가 0.11초 간격으로 차례로 덮여 **빈 자리가 0** 이 된다.

   🖼 `ADR-V25-15` ① — 라벨 둘(「부탁받은 집」·「내 모닥불 있음」)을 다 가려도
   **「가운데에서 뭔가 서려다 도로 없어지고, 남아 있던 빈 칸이 차례로 다 덮인다」**가 읽힌다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — 솟는 집 윤곽은 지붕 처마까지 x 176±23
   (=153/199), 옆 칸(2열·4열) 불꽃의 가장 안쪽이 x 149.5 · 202.5(획 반폭 1.5 포함)다.
   세로로는 집 윗점 136(솟을 때 132) · 아랫변 184 이고, 위아래 칸(3열 0행·3행) 불꽃의
   글로우가 y 120 과 y 204 에서 멈춘다.
   🔴 **이 샷은 스물여섯 칸이 다 켜지므로 회차 최소 여유가 여기서 나온다 — 실측 0.80px**
   (칸 (2,2) 불꽃 밑동 ↔ 집 자리 괄호의 아래 가로팔). 내 초고 표기(5px)는 **획 반폭 1.5 와
   괄호 상시 숨 ±1.2 를 빼먹은 낙관치**였고, 검수팀이 다각형 최소거리 + 시간축(2ms)으로
   정정했다. 확대 스틸(5배)에서 검은 띠가 남아 있고 팔레트 게이트도 통과하지만
   **여유가 1px 미만이니 괄호·칸·불꽃 크기를 옮기지 마라.**

   🔴 정적 대책 — 불꽃 16~26개가 8.2rad/s 로 숨 쉰다(프레임당 최대 1.09px).
   집 윤곽이 0.15~0.62초에 뜨고 지고, 0.45~1.70초에 새 불꽃 열 개가 자란다.
   땅 테두리는 90px/초로 계속 흐른다. */

const PLOT_T = 86, CW = 40, CH = 38, COLS = 7, ROWS = 4;
const PLOT_W = COLS * CW, PLOT_B = PLOT_T + ROWS * CH;   // 280 / 238
const EDGE = 6;                                          // 테두리는 칸 격자보다 6px 바깥
const HC = 3, HR0 = 1, HR1 = 2;

const CELLS = [];
for (let r = 0; r < ROWS; r++)
  for (let c = 0; c < COLS; c++)
    if (!(c === HC && (r === HR0 || r === HR1))) CELLS.push({ c, r });
const ORDER = CELLS.map((s, i) => ({ ...s, i, d: Math.hypot(s.c - HC, s.r - 1.5) }))
  .sort((a, b) => a.d - b.d || a.i - b.i);
const LIT = 16;                    // 샷이 시작될 때 이미 타고 있는 개수
const T0 = 0.45, STEP = 0.11, GROW = 0.26;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const PL = (w - PLOT_W) / 2;
    const cx = c => PL + CW * c + CW / 2;
    const cy = r => PLOT_T + CH * r + CH / 2;

    const c0 = cue(spec.blockCue ?? 0, 0.15, 0.55);
    const st = ease(clamp(c0 / 0.12));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

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

    const s0 = Math.max(0, since(spec.blockCue ?? 0));

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

    /* ── 서려다 마는 집 ───────────────────────────── */
    const rise = ease(clamp((s0 - 0.15) / 0.27)) * (1 - ease(clamp((s0 - 0.42) / 0.20)));
    if (rise > 0.02) {
      const lift = 4 * rise, sc = lerp(0.9, 1, rise);
      const bw = 40 * sc, bh = 36 * sc, base = hy + 22 - lift;
      ctx.save();
      ctx.globalAlpha = st * 0.9 * rise;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, hx - bw / 2, base - bh, bw, bh, 3); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(hx - bw / 2 - 3, base - bh);
      ctx.lineTo(hx, base - bh - 12 * sc);
      ctx.lineTo(hx + bw / 2 + 3, base - bh);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 불꽃 : 열여섯은 이미 타고 있고, 열 개가 차례로 앉는다 ── */
    for (let n = 0; n < ORDER.length; n++) {
      const s = ORDER[n];
      const grow = n < LIT ? 1 : ease(clamp((s0 - (T0 + (n - LIT) * STEP)) / GROW));
      if (grow <= 0.01) continue;
      const hp = 0.5 + 0.5 * Math.sin(t * 8.2 + n * 1.7);
      const h = (10 + 8 * hp) * grow, half = 6 * grow, blur = (4 + 2 * hp) * grow;
      const x = cx(s.c), y = cy(s.r) + 9;
      ctx.save();
      ctx.globalAlpha = st;
      setShadow(ctx, GLOW, blur);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(x, y - h); ctx.lineTo(x + half, y); ctx.lineTo(x - half, y);
      ctx.closePath(); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 위 띠 : 표시등은 여전히 비어 있다 ─────────── */
    if (spec.flagLabel) {
      ctx.font = disp(800, 13);
      const tw = ctx.measureText(spec.flagLabel).width;
      const x0 = (w - (36 + tw)) / 2;
      ctx.save();
      ctx.globalAlpha = st;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.strokeRect(x0, 47, 26, 26);
      ctx.textAlign = 'left'; ctx.fillStyle = tone('ink'); ctx.font = disp(800, 13);
      ctx.fillText(spec.flagLabel, x0 + 36, 65);
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
