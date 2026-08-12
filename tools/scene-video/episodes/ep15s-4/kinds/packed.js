/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다.
   경로 좌표를 적을 땐 `(경로)` 라고 표시한다. */
import {
  disp, ease, clamp, fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* packed — 훅. 집 한 채가 놓일 자리를 둘러싼 땅이 모닥불로 하나씩 다 덮인다.

   원문 근거:
   > "화면에서는 집 주변에 모닥불이 격자로 빼곡히 깔리고 자리 없음 경고가 반복되는
   >  장면이었습니다."
   > "반경이 만원이 되어 밭 지을 자리까지 잡아먹습니다."

   🖼 `ADR-V25-15` ① — 라벨(「집 주변」)을 가려도
   **「무언가가 멈추지 않고 하나씩 늘어 빈 자리를 다 덮는다」**가 읽힌다.
   이 편은 그 잣대가 가장 잘 서는 회차라, 훅 샷에는 글자를 하나(구획 이름)만 뒀다.

   📐 이 회차의 기본 도형 = **「땅 한 자리 = 칸 하나. 그 칸에 무엇이 앉는가.」**
   본문 다섯 샷(SH·S1·S2·S3·S4)이 **같은 땅을 다섯 번 다시 보는 것**이라
   칸 격자(7×4 · 40×38px) · 집 자리(3열의 1·2행) · 불꽃 글립이 다섯 파일에서 전부 같다.
   자리가 바뀌면 시청자가 다른 땅으로 읽는다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **칸 안쪽을 비워서 갈랐다.**
     · 칸 표시를 「가로세로 줄」이 아니라 **모서리 점**(4×4)으로 놓았다. 줄로 그으면
       땅 끝에서 끝까지 가는 흰 선이 가운데 불꽃(강조색)을 반드시 관통한다.
     · 불꽃은 밑동이 cy+9, `h = 10 + 8·hp`(최대 18), 반너비 6, 획 반폭 1.5, 글로우 최대 6 →
       상자가 **x ±13.5 · y cy−15 ~ cy+16.5** 다.
     · 모서리 점은 칸 중심에서 x ±20(점 반너비 2 로 18~22) → **실측 최소 6.92px** 뜬다
       (검수팀이 다각형 최소거리 + 시간축 2ms 간격으로 잰 값 · 초고 표기 6px 보다 넉넉했다).
       모서리 점은 x = cx 에 하나도 없으므로 세로로 만날 일이 없다.
     · 위아래 칸끼리 : r행 글로우 바닥 cy+15 ↔ r+1행 글로우 꼭대기 cy+23 → **8px**.
     · 🔴 **땅 테두리는 칸 격자보다 6px 바깥**(`EDGE`)에 있다. 0행 불꽃 글로우 꼭대기
       y 90 ↔ 위 선 획 안쪽 81.5 = **8.5px**, 3행 글로우 바닥 y 234 ↔ 아래 선 획 안쪽
       242.5 = **8.5px**. (초고는 테두리를 격자에 딱 붙여 3.5px 밖에 안 뜨는 자리가
       위아래 둘 있었다 — 글로우를 세고 나서 테두리를 밖으로 밀었다.)
     · 🔴 **집 자리 괄호 ↔ 불꽃 = 실측 최소 0.80px.** 내가 처음 「가로 5px · 세로 14px」이라
       적은 것은 **획 반폭 1.5 와 괄호의 상시 숨 ±1.2 를 빼먹은 낙관치**였고, 검수팀이
       다각형 최소거리 + 시간축(t 0~4초 · 2ms)으로 다시 재서 0.80px 로 정정했다.
       최악 자리는 **칸 (2,2) 불꽃 밑동 ↔ 괄호 아래 가로팔**(둘의 y 가 거의 같다).
       확대 스틸(5배)에서 검은 띠가 남아 있고 팔레트 게이트도 통과하지만
       🔴 **여유가 1px 미만이다 — 괄호나 칸 크기를 옮기지 마라.** 옮겨야 하면
       괄호 세로 반폭(28)을 줄이거나 상시 숨(1.2)을 낮추는 쪽이 먼저다.

   🔴 결정성 — `setLineDash` 를 한 군데도 안 쓴다(긴 점선이 30fps 3패스에서 알파가
   갈린 전례). 테두리는 `fillRect` 조각으로 놓고 위상은 `((x % P) + P) % P` 로
   양수 정규화한다. 채워지는 개수도 **누적 변수가 아니라 `t` 에서 계산**한다 —
   이 편은 「계속 늘어나는」 그림이라 상태를 쌓으면 seek 한 번에 무너진다.

   🔴 정적 대책(30fps 한 프레임 기준) — 불꽃 26개가 `h` 를 8px 폭으로 8.2rad/s 로
   숨 쉬어 **프레임당 최대 1.09px** 씩 움직이고, 땅 테두리 점선이 둘레 912px 를
   90px/초로 흘러 **프레임당 3px** 옮겨 앉는다. 여기에 2.6초 채움 루프가 겹친다. */

const PLOT_T = 86, CW = 40, CH = 38, COLS = 7, ROWS = 4;
const PLOT_W = COLS * CW;                 // 280
const PLOT_B = PLOT_T + ROWS * CH;        // 238
const EDGE = 6;                           // 땅 테두리는 칸 격자보다 6px 바깥에 있다
const HC = 3, HR0 = 1, HR1 = 2;           // 집이 앉을 두 칸
const HOUSE_Y = PLOT_T + CH * 2;          // 162 — 그 두 칸의 가운데

const CELLS = [];
for (let r = 0; r < ROWS; r++)
  for (let c = 0; c < COLS; c++)
    if (!(c === HC && (r === HR0 || r === HR1))) CELLS.push({ c, r });
/* 집에서 가까운 칸부터 덮인다. 같은 거리는 목록 순서로 가른다 —
   비교자에 `a.i - b.i` 가 있어 정렬이 결정적이다(같은 값에서 흔들리지 않는다). */
const ORDER = CELLS.map((s, i) => ({ ...s, i, d: Math.hypot(s.c - HC, s.r - 1.5) }))
  .sort((a, b) => a.d - b.d || a.i - b.i);

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const PL = (w - PLOT_W) / 2;
    const cx = c => PL + CW * c + CW / 2;
    const cy = r => PLOT_T + CH * r + CH / 2;

    const c0 = cue(spec.fillCue ?? 0, 0.15, 0.55);
    const st = ease(clamp(c0 / 0.12));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 땅 테두리 : 네 변이 한 방향으로 흐른다 ──────── */
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

    /* ── 칸 모서리 점 : 느린 물결로 밝기가 오간다 ────── */
    for (let r = 0; r <= ROWS; r++) {
      for (let c = 0; c <= COLS; c++) {
        const x = PL + CW * c, y = PLOT_T + CH * r;
        const a = 0.16 + 0.16 * (0.5 + 0.5 * Math.sin(t * 3.4 - (c + r) * 0.55));
        ctx.save();
        ctx.globalAlpha = st * a; ctx.fillStyle = tone('ink');
        ctx.fillRect(x - 2, y - 2, 4, 4);
        ctx.restore();
      }
    }

    /* ── 집이 앉을 자리 : 모서리 괄호 넷 ─────────────
       상시 숨(±1.2px · 7.4rad/s → 프레임당 0.44px) — 사건이 아니라 상시 운동이라 `t` 로 돈다. */
    const br = 1.2 * (0.5 + 0.5 * Math.sin(t * 7.4));
    const hx = cx(HC);
    const gL = hx - 23 - br, gR = hx + 23 + br, gT = 134 - br, gB = 190 + br;
    ctx.save();
    ctx.globalAlpha = st * 0.62; ctx.strokeStyle = tone('ink');
    ctx.lineWidth = 3; ctx.lineCap = 'butt';
    const arm = 13;
    const corner = (x, y, sx, sy) => {
      ctx.beginPath();
      ctx.moveTo(x + sx * arm, y); ctx.lineTo(x, y); ctx.lineTo(x, y + sy * arm);
      ctx.stroke();
    };
    corner(gL, gT, 1, 1); corner(gR, gT, -1, 1);
    corner(gL, gB, 1, -1); corner(gR, gB, -1, -1);
    ctx.restore();

    /* ── 불꽃이 하나씩 앉는다 ────────────────────────
       개수는 상태가 아니라 `t` 의 함수다. 0~0.80 채우고, 0.90~1.0 에 통째로 꺼진다. */
    const LP = spec.loopSec ?? 2.6;
    const k = (((t % LP) + LP) % LP) / LP;
    const prog = k / 0.80 * ORDER.length;
    const off = 1 - ease(clamp((k - 0.90) / 0.10));

    const flame = (x, baseY, grow, ph, alpha) => {
      if (alpha <= 0.01 || grow <= 0.01) return;
      const hp = 0.5 + 0.5 * Math.sin(t * 8.2 + ph);
      const h = (10 + 8 * hp) * grow, half = 6 * grow, blur = (4 + 2 * hp) * grow;
      ctx.save();
      ctx.globalAlpha = alpha;
      setShadow(ctx, FAIL_GLOW, blur);
      ctx.strokeStyle = tone('fail'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(x, baseY - h); ctx.lineTo(x + half, baseY); ctx.lineTo(x - half, baseY);
      ctx.closePath(); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    };

    for (let n = 0; n < ORDER.length; n++) {
      const s = ORDER[n];
      flame(cx(s.c), cy(s.r) + 9, ease(clamp(prog - n)), n * 1.7, st * off);
    }

    /* ── 구획 이름 ─────────────────────────────────── */
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
