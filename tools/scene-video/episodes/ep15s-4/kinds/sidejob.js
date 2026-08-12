/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다.
   경로 좌표를 적을 땐 `(경로)` 라고 표시한다. */
import {
  disp, ease, clamp, lerp, fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* sidejob — 목수가 집 부탁을 받고, 그 자리에 집 대신 자기 모닥불을 놓는다.

   원문 근거:
   > "목수가 집 부탁을 수락합니다 → 그 상태에서 자기 모닥불을 짓습니다"
   > "'내 모닥불 있음' 플래그가 0으로 남습니다"

   자막 0 : 집 자리(모서리 괄호 넷)가 **제자리에서 자라며** 잡히고(부탁 접수),
     그다음 그 아래 칸에 **강조색 불꽃 하나**가 자란다(자기 모닥불).

   🖼 `ADR-V25-15` ① — 라벨 셋(「집 부탁」·「내 모닥불」)을 다 가려도
   **「자리만 잡아 두고, 그 옆 칸에 다른 것 하나를 새로 켠다」**가 읽힌다.

   🔴 **밖에서 밀려 들어오는 등장이 없다.** 괄호는 0.72배에서 1.0배로 **제자리에서 자라고**
   불꽃도 같은 자리에서 커진다 — 이 캔버스(352px)에서 translate 등장은 거의 항상
   가장자리를 문다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다(좌표) — 집 자리 괄호는 x 176±23(=153/199),
   불꽃은 3열 3행 칸의 x 176±13.5 이고 **세로로 갈린다**: 괄호 아래변 y 190(상시 숨 +1.2 ·
   획 반폭 1.5 까지 192.7), 불꽃 글로우 꼭대기 y 204 → **11.3px** 뜬다.
   🔑 이 샷은 불꽃이 **집 바로 아래 한 칸뿐**이라 여유가 크다. 같은 쌍의 회차 최소값은
   여러 칸이 켜지는 SH·S3 에서 나오고 **실측 0.80px**(칸 (2,2) ↔ 괄호 아래 가로팔) 이다 —
   `packed.js` 머리 주석에 사유와 함께 적었다. 내 초고 표기(5px)가 획 반폭과 상시 숨을
   빼먹은 낙관치였고 검수팀이 다각형 최소거리로 정정했다.
   「내 모닥불」 글자(13.5px · 기준선 y 264 → 글자 상자 약 250~268)는 땅 아래 테두리
   획 바깥(245.5)과 **약 5px** 뜨고, 캔버스 아래(307)와 **39px** 남는다.
   ⚠️ 둘 다 흰색이라 색이 섞일 위험은 없다 — 이 여백은 **읽기** 때문에 잡은 것이다.

   🔴 정적 대책 — 자막 하나짜리 샷이라 움직이는 것이 적다. 그래서 셋을 겹쳤다:
     ①땅 테두리 점선이 둘레 912px 를 90px/초로 흐른다(프레임당 3px · 마디 45개)
     ②집 자리 괄호가 상시 ±1.2px 로 숨 쉰다(7.4rad/s → 프레임당 0.44px · 팔 여덟)
     ③칸 모서리 점 40개의 밝기가 물결로 오간다.
   불꽃이 하나뿐이라 ①이 이 샷의 주력이다 — 그래서 테두리를 `track`(0.12)이 아니라
   **흰색 0.26** 으로 올렸다(0.12 로는 프레임 차가 문턱 언저리에 붙는다). */

const PLOT_T = 86, CW = 40, CH = 38, COLS = 7, ROWS = 4;
const PLOT_W = COLS * CW, PLOT_B = PLOT_T + ROWS * CH;   // 280 / 238
const EDGE = 6;                                          // 테두리는 칸 격자보다 6px 바깥
const HC = 3;
const MINE = { c: 3, r: 3 };              // 목수가 놓는 첫 모닥불 — 집 자리 바로 아래 칸

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const PL = (w - PLOT_W) / 2;
    const cx = c => PL + CW * c + CW / 2;
    const cy = r => PLOT_T + CH * r + CH / 2;

    const c0 = cue(spec.takeCue ?? 0, 0.15, 0.55);
    const st = ease(clamp(c0 / 0.12));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha; ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    /* ── 땅 테두리 ─────────────────────────────────── */
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

    const s0 = Math.max(0, since(spec.takeCue ?? 0));
    const place = ease(clamp((s0 - 0.15) / 0.45));    // 부탁이 자리를 잡는다
    const own = ease(clamp((s0 - 0.95) / 0.45));      // 자기 모닥불이 자란다

    /* ── 집이 앉을 자리 : 제자리에서 자라는 모서리 괄호 ── */
    const br = 1.2 * (0.5 + 0.5 * Math.sin(t * 7.4));
    const sc = lerp(0.72, 1, place);
    const hx = cx(HC), hy = 162;
    const halfW = 23 * sc + br, halfH = 28 * sc + br;
    ctx.save();
    ctx.globalAlpha = st * 0.62 * place; ctx.strokeStyle = tone('ink');
    ctx.lineWidth = 3; ctx.lineCap = 'butt';
    const arm = 13 * sc;
    const corner = (x, y, sx, sy) => {
      ctx.beginPath();
      ctx.moveTo(x + sx * arm, y); ctx.lineTo(x, y); ctx.lineTo(x, y + sy * arm);
      ctx.stroke();
    };
    corner(hx - halfW, hy - halfH, 1, 1); corner(hx + halfW, hy - halfH, -1, 1);
    corner(hx - halfW, hy + halfH, 1, -1); corner(hx + halfW, hy + halfH, -1, -1);
    ctx.restore();

    /* ── 자기 모닥불 하나 ──────────────────────────── */
    if (own > 0.01) {
      const hp = 0.5 + 0.5 * Math.sin(t * 8.2);
      const h = (10 + 8 * hp) * own, half = 6 * own, blur = (4 + 2 * hp) * own;
      const fx = cx(MINE.c), fy = cy(MINE.r) + 9;
      ctx.save();
      ctx.globalAlpha = st;
      setShadow(ctx, FAIL_GLOW, blur);
      ctx.strokeStyle = tone('fail'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(fx, fy - h); ctx.lineTo(fx + half, fy); ctx.lineTo(fx - half, fy);
      ctx.closePath(); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();

      if (spec.mineLabel) {
        ctext(spec.mineLabel, fx, cy(MINE.r) + 45, 900, 13.5, tone('ink'), 120,
          st * ease(clamp((s0 - 1.15) / 0.35)));
      }
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
