/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다.
   경로 좌표를 적을 땐 `(경로)` 라고 표시한다. */
import {
  disp, ease, clamp, lerp, fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* twoasks — 주인을 정할 때 보던 것이 하나뿐이었고, 나머지 하나를 켜자 다 풀린다.

   원문 근거:
   > "원인은 자가 소유 배정 판정이 '무엇을 짓는지'를 안 보고 '누가 바쁜지'만 봤던 것입니다."
   > "판정을 '이 건물이 그 부탁의 소유 배정 대상인가'로 바꿔서 해결했습니다."

   자막 0 : 아래에 물음 상자 둘이 **제자리에서 자라며** 나타난다 —
     「누가 바쁜지」에는 강조색 점이 켜져 있고 「무엇을 짓는지」는 꺼져 있다.
   자막 1 : 꺼져 있던 쪽에 점이 켜지고(0.15~0.50초) → 위 표시등이 **처음으로 찬다**
     (0.50~0.95초) → 넘쳐 있던 불꽃 스물다섯이 먼 것부터 사라져 **마지막 하나가 1.475초에
     닫히고**(0.80~1.475초) → 집이 자리에서 **자라 선다**(1.20~1.80초).

   🖼 `ADR-V25-15` ① — 라벨 다섯을 다 가려도
   **「두 표시 중 꺼져 있던 쪽이 켜지자, 위 칸이 차고, 빼곡하던 것이 하나만 남고,
   비어 있던 자리에 무언가가 선다」**가 읽힌다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세 겹으로 갈랐다.**
     ⓐ 물음 상자(흰 테두리 · 경로 y 254~286)의 강조색 점은 상자 안쪽 x+19 에 앉고
        반지름 5.5 라 테두리 획 바깥(x+1.5)과 **12px** 뜬다. 글자는 x+34 부터라 **9.5px**.
        상자 위로는 땅 아래 테두리 획 바깥(245.5)과 **7px** 뜬다(상자 획 바깥 252.5).
     ⓑ 표시등 안쪽 강조색 사각(x0+6~x0+20 · y 53~67)과 흰 테두리 획 안쪽(x0+1.5)이
        **4.5px** 뜬다.
     ⓒ 🔴 **아래 두 값은 검수팀이 다각형 최소거리 + 시간축(t 0~4초 · 2ms)으로 다시 잰
        실측이다.** 내가 처음 적은 값은 획 반폭(1.5)과 상시 숨(±1.2)을 빼먹어 낙관적이었다.
        · **끝난 뒤**(since(1) 1.475초 이후) — 남는 것은 KEEP 불꽃 하나뿐이고, 그 글로우
          꼭대기(y 204)와 집 지붕 획 바깥 사이가 **약 18px**. 여기는 넉넉하다.
        · **집이 자라는 동안**(since(1) 1.20~1.475초 · 약 0.28초) — 아직 안 사라진 칸
          (2,1) 불꽃과 지붕 왼쪽 처마가 **2.45px** 까지 붙는다. 확대 스틸(5배)에서 검은 띠가
          남아 있고 팔레트 게이트도 통과하지만 **여유가 얇다 — 이 둘을 옮기지 마라.**
          🔑 더 벌리려면 `builtK` 시작을 1.20 → 1.50 으로 미는 것이 유일하게 안전한 손잡이다
          (그러면 겹치는 창이 0 이 된다. 대신 집 완성이 2.10초로 밀린다).

   🔴 **불이 집으로 옮겨 가지 않는다.** 옮기는 연출을 쓰면 그 경로가 흰 집을 관통한다.
   대신 **시간으로 갈랐다** — 불꽃은 1.475초에 마지막 하나까지 닫히고 집은 1.20초부터
   자란다(위 ⓒ 의 0.28초 창이 그 겹침이다).

   🔴 정적 대책 — 불꽃 26→1 개가 8.2rad/s 로 숨 쉬고, 사라짐이 26단계로 0.70초에 걸쳐
   흐르며, 땅 테두리가 90px/초로 돈다. 자막 1 의 뒤쪽은 집이 자라는 구간이다. */

const PLOT_T = 86, CW = 40, CH = 38, COLS = 7, ROWS = 4;
const PLOT_W = COLS * CW, PLOT_B = PLOT_T + ROWS * CH;   // 280 / 238
const EDGE = 6;                                          // 테두리는 칸 격자보다 6px 바깥
const HC = 3, HR0 = 1, HR1 = 2;
const KEEP = { c: 3, r: 3 };          // 주인이 붙는 그 모닥불 — 목수가 맨 처음 지은 것

const CELLS = [];
for (let r = 0; r < ROWS; r++)
  for (let c = 0; c < COLS; c++)
    if (!(c === HC && (r === HR0 || r === HR1))) CELLS.push({ c, r });
const ORDER = CELLS.map((s, i) => ({ ...s, i, d: Math.hypot(s.c - HC, s.r - 1.5) }))
  .sort((a, b) => a.d - b.d || a.i - b.i);

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const PL = (w - PLOT_W) / 2;
    const cx = c => PL + CW * c + CW / 2;
    const cy = r => PLOT_T + CH * r + CH / 2;

    const c0 = cue(spec.askCue ?? 0, 0.15, 0.55);
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

    const s0 = Math.max(0, since(spec.askCue ?? 0));
    const s1 = Math.max(0, since(spec.fixCue ?? 1));
    const askK = ease(clamp((s0 - 0.15) / 0.35));      // 두 물음이 자리를 잡는다
    const leftOn = ease(clamp((s1 - 0.15) / 0.35));    // 꺼져 있던 쪽이 켜진다
    const lampK = ease(clamp((s1 - 0.50) / 0.45));     // 표시등이 찬다
    const clearK = clamp((s1 - 0.80) / 0.70);          // 넘친 불이 사라진다
    const builtK = ease(clamp((s1 - 1.20) / 0.60));    // 집이 선다

    /* ── 불꽃 : 먼 것부터 사라지고 하나만 남는다 ───── */
    for (let n = 0; n < ORDER.length; n++) {
      const s = ORDER[n];
      const keep = s.c === KEEP.c && s.r === KEEP.r;
      const rank = ORDER.length - 1 - n;               // 집에서 먼 것이 0
      /* 🔴 배율이 `ORDER.length`(26) 였을 때 **마지막 하나가 안 닫혔다**(1차 반려).
         `clearK` 는 최댓값이 1 이라 집에서 가장 가까운 칸(rank 25)의 인자가
         (26 − 25) / 1.6 = 0.625 에서 멈춰 `grow` 0.316 으로 얼어붙었고, 그 불꽃이
         샷 끝까지 약 1.8초 남아 **이 편 착지의 전부인 「하나만 남는다」를 화면이 부정**했다.
         배율에 램프 폭(1.6)을 더해 마지막 하나까지 0 에 닿게 한다 —
         rank 25 는 clearK 0.906(since(1) 1.434)에 지기 시작해 0.964(**1.475**)에 사라진다. */
      const gone = keep ? 0 : ease(clamp((clearK * (ORDER.length + 1.6) - rank) / 1.6));
      const grow = 1 - gone;
      if (grow <= 0.01) continue;
      const hp = 0.5 + 0.5 * Math.sin(t * 8.2 + n * 1.7);
      const h = (10 + 8 * hp) * grow, half = 6 * grow, blur = (4 + 2 * hp) * grow;
      const x = cx(s.c), y = cy(s.r) + 9;
      ctx.save();
      ctx.globalAlpha = st;
      setShadow(ctx, FAIL_GLOW, blur);
      ctx.strokeStyle = tone('fail'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(x, y - h); ctx.lineTo(x + half, y); ctx.lineTo(x - half, y);
      ctx.closePath(); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 집 자리 괄호는 집이 서면서 사라진다 ───────── */
    const br = 1.2 * (0.5 + 0.5 * Math.sin(t * 7.4));
    const hx = cx(HC), hy = 162;
    if (builtK < 0.99) {
      ctx.save();
      ctx.globalAlpha = st * 0.62 * (1 - builtK); ctx.strokeStyle = tone('ink');
      ctx.lineWidth = 3; ctx.lineCap = 'butt';
      const corner = (x, y, sx, sy) => {
        ctx.beginPath();
        ctx.moveTo(x + sx * 13, y); ctx.lineTo(x, y); ctx.lineTo(x, y + sy * 13);
        ctx.stroke();
      };
      corner(hx - 23 - br, hy - 28 - br, 1, 1); corner(hx + 23 + br, hy - 28 - br, -1, 1);
      corner(hx - 23 - br, hy + 28 + br, 1, -1); corner(hx + 23 + br, hy + 28 + br, -1, -1);
      ctx.restore();
    }

    /* ── 집이 제자리에서 자라 선다 ─────────────────── */
    if (builtK > 0.02) {
      const sc = lerp(0.8, 1, builtK);
      const bw = 40 * sc, bh = 36 * sc, base = hy + 22;
      ctx.save();
      ctx.globalAlpha = st * builtK;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3.5; ctx.lineJoin = 'round';
      roundRect(ctx, hx - bw / 2, base - bh, bw, bh, 3); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(hx - bw / 2 - 3, base - bh);
      ctx.lineTo(hx, base - bh - 12 * sc);
      ctx.lineTo(hx + bw / 2 + 3, base - bh);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 위 띠 : 표시등이 처음으로 찬다 ────────────── */
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
      if (lampK > 0.02) {
        ctx.save();
        ctx.globalAlpha = st * lampK;
        setShadow(ctx, GLOW, 7);
        ctx.fillStyle = tone('accent');
        ctx.fillRect(x0 + 6, 53, 14, 14);
        clearShadow(ctx);
        ctx.restore();
      }
    }

    /* ── 아래 띠 : 주인을 정할 때 보는 두 가지 ─────── */
    const CHW = 138, CHH = 32, CHY = 254, GAPX = 36;
    const bx = (w - (CHW * 2 + GAPX)) / 2;
    const chip = (x, label, on) => {
      const sc = lerp(0.82, 1, askK);
      const cw2 = CHW * sc, ch2 = CHH * sc;
      const px = x + (CHW - cw2) / 2, py = CHY + (CHH - ch2) / 2;
      ctx.save();
      ctx.globalAlpha = st * askK * lerp(0.5, 1, on);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, px, py, cw2, ch2, 6); ctx.stroke();
      ctx.restore();
      /* 🔴 점과 글자도 상자와 **같은 배율로** 안쪽에 앉는다 — 상자만 줄이면
         자라는 동안 강조색 점이 흰 테두리를 물고 나간다(등장 첫 프레임에서 1.1px 까지). */
      if (on > 0.02) {
        ctx.save();
        ctx.globalAlpha = st * askK * on;
        setShadow(ctx, GLOW, 6);
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(px + 19 * sc, py + 16 * sc, 5.5 * sc, 0, Math.PI * 2); ctx.fill();
        clearShadow(ctx);
        ctx.restore();
      }
      let fs = 12.5 * sc; ctx.font = disp(800, fs);
      while (fs > 8 && ctx.measureText(label).width > 92 * sc) { fs -= 0.5; ctx.font = disp(800, fs); }
      ctx.save();
      ctx.globalAlpha = st * askK * lerp(0.55, 1, on);
      ctx.textAlign = 'left'; ctx.fillStyle = tone('ink');
      ctx.fillText(label, px + 34 * sc, py + 21 * sc);
      ctx.restore();
    };
    if (askK > 0.01) {
      chip(bx, spec.askA ?? '', leftOn);
      chip(bx + CHW + GAPX, spec.askB ?? '', 1);
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
