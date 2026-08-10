/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다.
   경로 좌표를 적을 땐 `(경로)` 라고 표시한다.
   🔑 `ep14s-4` 마스터 판정(2026-08-10) — 여유가 다섯 곳에서 1.5~3px 넉넉하게 적혀 있었고
   원인은 부주의가 아니라 **어느 기준으로 적는지가 어디에도 없었던 것**이다. */
import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* spread — 뒤집힘과 착지. 화덕을 각자 집 옆에 놓자 마을이 흩어졌다.

   원문 근거:
   > "그래서 모닥불을 개인 시설로 내렸습니다. … 자기 집 곁에 짓게 했습니다."
   > "식사는 앵커를 아예 풀어서 제자리에서 먹게 했습니다."
   > "순둥이는 이웃 곁에 붙어 살고, 떠돌이는 마을 경계 근처에 외딴집을 짓습니다."
   > "맵이 100칸이면 실효 반경이 40칸이 되고, 집들이 4칸에서 38칸 사이에 흩어집니다."

   앞 세 샷이 「가운데 하나가 여섯을 붙든다」였다면 여기서 **가운데가 없어진다.**
   자막 0 : 가운데 불이 줄어들며 꺼지고, 집들이 뭉쳐 있던 안쪽 고리에서 **각자의 거리로
     밀려 나간다.** 뒤이어 집마다 **바로 곁에** 작은 불이 하나씩 켜진다.
   자막 1 : 가장 안쪽 집에 「순둥이」, 가장 바깥 집에 「떠돌이」 이름이 켜진다.

   라벨을 다 가려도 「한 점에 뭉쳐 있던 것이 흩어지고, 그 점이 여섯 개로 나뉘어 각자 곁에
   하나씩 붙는다」가 읽힌다.

   🔴 **1차 반려를 고친 자리 (A)** — 예전에는 「마을 경계」가 **반지름 96 짜리 고리**였고
   곁불이 집보다 46px 바깥이라 **화덕 여섯이 전부 경계 위·밖에 앉았다**(96·104·118·118·
   134·142). 자막이 「각자 **집 옆에** 화덕을 놨더니」인데 화면은 「화덕은 마을 밖」을 말했다.
   원인은 하나다 — **경계와 집의 최대 거리를 같은 상수(`R_OUT` 96)로 썼다.** 원문은 둘을
   가른다(집은 4~38칸, 실효 반경은 40칸 = 경계가 집보다 **바깥**).
   → 그래서 **경계를 반지름에서 떼어내 캔버스를 두르는 네모(`EDGE_*`)로 옮겼다.** 집 여섯의
   자리(50~96)는 형제 샷 셋과 **한 픽셀도 안 바꿨고**(같은 마을을 네 번 다시 보는 것이다),
   곁불 오프셋만 46 → **38** 로 줄였다. 이제 집도 화덕도 전부 경계 **안**이고, 가장 먼
   🔴 **거짓이었다** — 경계에 제일 가까운 것은 **269° 4.4px** 이고 떠돌이는 23.2px 이다. 경계를 원→네모로 바꾼 수정이 만든 새 주장인데 작성팀도 검수도 안 쟀다(마스터가 잡음) — 원문의 「마을 경계 **근처**」와 맞는다.
   🔑 네모로 바꾼 덤 : 고리(원)는 S1·S2 가 「점수의 극단」·「성격이 준 거리」로 이미 쓰는
   기호다. 경계를 네모로 두면 **다른 종류의 선**이라는 것이 형태만으로 읽힌다.

   🔴 **불이 「날아가지」 않는다.** 가운데 불이 여섯 갈래로 날아가면 그 경로가 집(흰색)을
   반드시 관통한다 — 강조색과 흰색이 같은 픽셀을 나눠 갖는 프레임이 생긴다. 그래서
   **시간으로 갈랐다**: 가운데 불은 자막이 시작되고 0.55초에 완전히 꺼지고 곁불은
   0.95초부터 제자리에서 켜진다. 뜻도 이쪽이 맞는다 — 원문은 공용 모닥불을 **휴면으로
   남기고** 개인 화덕을 새로 만든 것이지 같은 불을 옮긴 게 아니다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **반지름 오프셋으로 갈랐다.**
     곁불은 자기 집에서 반지름 방향 **38px 바깥**에 앉는다. 집 글립은 x±14 · y−18~+12,
     곁불은 글로우까지 x±10 · y−13~+11 이므로 축마다 필요한 간격이 **가로 24 · 세로 25(아래)
     / 29(위)** 다. 여섯 각도 전부에서 한 축이 그걸 넘는다:
       125° dy 31.1(아래·필요 25) · 173° dx 37.7 · 221° dx 28.7 · 269° dy 38.0(위·필요 29) ·
       317° dx 27.8 · 5° dx 37.9.  🔴 **최악 자리를 잘못 짚었었다**(마스터 실측) — 317°(3.8px)·221°(4.7px)가 아니라 **125° 1.95px** 이 최악이다(키 큰 불꽃 h=20 + 글로우 7 을 과소 모델했다). 불투명끼리는 8.95px 이고, 마스터가 최악 위상(절대 26.113초)을 렌더해 픽셀을 세니 **혼합 픽셀 0 · 흰↔강조색 최소 9.91 CSS px** 였다.
     경계(track)와 곁불(accent)도 안 만난다. 🔴 **가장 아슬아슬한 곳이 위쪽**이라 네모의
     윗변만 여백을 10 으로 좁혔다 — 269° 곁불의 글로우 꼭대기가 y 17.3 이고 윗선 안쪽이
     11.5 다(4.4px). 나머지 세 변은 전부 20px 이상 뜬다(173° 왼쪽 39.0↔17.5 ·
     5° 오른쪽 297.3↔334.5 · 125° 아래 241.3↔289.5).

   세로 예산(캔버스 307): 최저 = **경계 네모의 아래 선 바깥 292.5**. 14.5px 남는다.
   그다음이 「마을 경계」 글립 바닥 282(기준선 278) · 125° 곁불 글로우 바닥 241.3.
   가로: 경계 네모의 오른쪽 선 바깥 337.5(여유 14.5) · 5° 곁불 오른쪽 끝 297.3.

   🔴 **1차 반려를 고친 자리 (B)** — 30fps 한 프레임 기준으로 움직임이 없어 5.2초가 정적으로
   잡혔다. 원인은 「면적이 크다」고 적은 것들이 **프레임당 0.3~0.6px** 밖에 안 움직였다는 것.
   세 가지를 키웠다: ①경계 점선이 둘레 1,190px 를 **60px/초**로 돈다(프레임당 2px)
   ②곁불 맥동을 3.4rad/s·진폭 3 → **7.0rad/s·진폭 9**(프레임당 최대 1.05px, 여섯 개)
   ③집 여섯에 **상시 숨**(진폭 2px · 7.4rad/s, 프레임당 0.25px)을 넣었다.
   🔑 ③은 **사건이 아니라 상시 운동**이라 `t` 로 돌린다(사건은 자막에 물린다). 뜻으로도
   맞는다 — 앞 샷(tether)에서 불에 끌려 크게 숨 쉬던 여섯이 여기서는 **자기 자리에서
   작게** 숨 쉰다. 같은 동작의 진폭 차이가 「풀려났다」를 말한다. */

const CY = 150;
const R_IN = 50, R_OUT = 96;          // 집이 서는 거리 — 형제 샷 셋과 같은 값이다
const HEARTH_OFF = 38;                // 곁불은 집에서 이만큼 바깥
/* 마을 경계 — **반지름이 아니라 화면을 두르는 네모**다. 집(최대 98)도 곁불(최대 136)도
   전부 이 안에 든다. 좌우는 캔버스 폭에서 16px 을 뺀다(폭을 상수로 박지 않는다). */
const EDGE_PAD = 16, EDGE_T = 10, EDGE_B = 291;

const SEATS = [
  { deg: 125, ratio: 0.25 },
  { deg: 173, ratio: 0.80 },
  { deg: 221, ratio: 0.10 },
  { deg: 269, ratio: 0.50 },
  { deg: 317, ratio: 0.95 },
  { deg: 5, ratio: 0.50 }
];
const rOf = ratio => R_IN + (ratio - 0.10) / 0.85 * (R_OUT - R_IN);

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const CX = w / 2;

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    /* 🔴 setLineDash 를 안 쓴다(결정성 — 긴 점선이 30fps 3패스에서 알파가 갈린 전례).
       축 정렬 조각을 fillRect 로 직접 놓고 위상은 양수로 정규화한다.
       `off` 는 **둘레를 도는 누적 거리**라 네 변의 무늬가 이어져 한 방향으로 흐른다. */
    const SEG = 12, GAP = 10, PER = SEG + GAP;
    const dashLine = (x0, y0, x1, y1, off, alpha) => {
      if (alpha <= 0.01) return;
      const horiz = y0 === y1;
      const len = horiz ? Math.abs(x1 - x0) : Math.abs(y1 - y0);
      const sx = horiz ? Math.sign(x1 - x0) : Math.sign(y1 - y0);
      const ph = ((off % PER) + PER) % PER;
      ctx.save();
      ctx.globalAlpha = alpha; ctx.fillStyle = tone('track');
      for (let d = -ph; d < len; d += PER) {
        const a = Math.max(0, d), b = Math.min(len, d + SEG);
        if (b <= a) continue;
        if (horiz) ctx.fillRect(x0 + sx * a - (sx < 0 ? (b - a) : 0), y0 - 1.5, b - a, 3);
        else ctx.fillRect(x0 - 1.5, y0 + sx * a - (sx < 0 ? (b - a) : 0), 3, b - a);
      }
      ctx.restore();
    };

    const house = (x, y, alpha, sc) => {
      if (alpha <= 0.01) return;
      const bw = 22 * sc, bh = 18 * sc;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, x - bw / 2, y - bh / 2 + 3, bw, bh, 3); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(x - bw / 2 - 3, y - bh / 2 + 3);
      ctx.lineTo(x, y - bh / 2 - 9 * sc);
      ctx.lineTo(x + bw / 2 + 3, y - bh / 2 + 3);
      ctx.stroke();
      ctx.restore();
    };

    const flame = (x, baseY, half, h, alpha, blur) => {
      if (alpha <= 0.01) return;
      ctx.save();
      ctx.globalAlpha = alpha;
      setShadow(ctx, GLOW, blur);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(x, baseY - h);
      ctx.lineTo(x + half, baseY);
      ctx.lineTo(x - half, baseY);
      ctx.closePath(); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    };

    const c0 = cue(spec.splitCue ?? 0, 0.15, 0.55);
    const st = ease(clamp(c0 / 0.12));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      /* 🔴 이 샷만 구획 이름이 (30, 34) 다 — 나머지 넷은 (20, 24). 경계 네모의 왼쪽 선이
         x 14.5~17.5 라 원래 자리(x 20)에 두면 2.5px 밖에 안 뜨고, 윗선을 y 10 으로 올려
         둔 터라 글자를 그 사이에 끼우면 읽기가 나쁘다. 그래서 **네모 안쪽으로 들였다** —
         글자 상자가 x 30~140 · y 25~37 이고 윗선 안쪽(11.5)과 13.5px 뜬다.
         🔴 윗선을 아래로 내리는 쪽은 못 쓴다 — 그러면 269° 곁불(글로우 꼭대기 y 17.3)이
         경계 **밖**으로 나가 1차 반려 사유 A 가 되살아난다. */
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 30, 34);
      ctx.restore();
    }

    /* ⏱ 세 국면은 `cue` 가 아니라 `since()`(그 자막이 시작된 뒤 흐른 초) 위에 세웠다.
       `cue` 창은 `lead + rel × span` 이라 실측 `rel` 이 있어야 시각이 나오고, 그러면
       `sfx.delayMs` 를 실측 뒤 다시 풀어야 한다. `since()` 는 자막 시작을 0 으로 두므로
       **자막 길이가 바뀌어도 흩어지는 시각이 안 움직인다.**
       (등장 알파 `st` 만 `cue` 를 쓴다 — 그건 시각이 아니라 페이드라 상관없다.) */
    const s0 = Math.max(0, since(spec.splitCue ?? 0));
    const gone = ease(clamp(s0 / 0.55));                    // 가운데 불이 꺼진다
    const move = ease(clamp((s0 - 0.30) / 1.00));           // 집들이 각자 거리로 나간다
    const lit = ease(clamp((s0 - 0.95) / 0.60));            // 곁불이 켜진다

    /* ── 마을 경계 : 네 변이 한 방향으로 흐른다 ────────
       둘레 1,190px 를 60px/초로 도니 30fps 한 프레임에 2px 이 바뀐다(반려 사유 B). */
    const flow = t * 60;
    const eA = st * (0.35 + 0.65 * move);
    const eL = EDGE_PAD, eR = w - EDGE_PAD;
    const wTop = eR - eL, hSide = EDGE_B - EDGE_T;
    dashLine(eL, EDGE_T, eR, EDGE_T, flow, eA);
    dashLine(eR, EDGE_T, eR, EDGE_B, flow - wTop, eA);
    dashLine(eR, EDGE_B, eL, EDGE_B, flow - wTop - hSide, eA);
    dashLine(eL, EDGE_B, eL, EDGE_T, flow - wTop * 2 - hSide, eA);
    if (spec.edgeLabel) {
      ctext(spec.edgeLabel, CX, EDGE_B - 13, 900, 14, tone('ink'), 150, st * move);
    }

    /* ── 가운데 불 : 줄어들며 꺼진다 ────────────────── */
    const puff = 0.5 + 0.5 * Math.sin(t * 3.1);
    const cs = 1 - gone;
    if (cs > 0.02) flame(CX, CY + 11, 10 * cs, (24 + 4 * puff) * cs, st * cs, 8 + 3 * puff);

    /* ── 집과 곁불 ────────────────────────────────── */
    /* 상시 숨 — 앞 샷에서 불에 끌려 크게 숨 쉬던 여섯이 여기서는 제자리에서 작게 숨 쉰다.
       사건이 아니라 상시 운동이라 `t` 로 돈다(사건은 자막에 물린다). */
    const idle = 2.0 * (0.5 + 0.5 * Math.sin(t * 7.4));
    const nameOf = { 221: spec.nearName, 317: spec.farName };
    for (let i = 0; i < SEATS.length; i++) {
      const s = SEATS[i];
      const r = lerp(R_IN, rOf(s.ratio), move) + idle;
      const a = s.deg * Math.PI / 180;
      const x = CX + r * Math.cos(a), y = CY + r * Math.sin(a);

      const hp = 0.5 + 0.5 * Math.sin(t * 7.0 + i * 1.9);
      const hr = r + HEARTH_OFF;
      flame(CX + hr * Math.cos(a), CY + hr * Math.sin(a) + 6,
        5 * lit, (11 + 9 * hp) * lit, st * lit, 5 + 2 * hp);

      house(x, y, st, 1);

      const nm = nameOf[s.deg];
      if (nm) ctext(nm, x, y + 30, 900, 13.5, tone('ink'), 96, st * ease(cue(spec.nameCue ?? 1, 0.12, 0.34)));
    }

    ctx.textAlign = 'left';
  }
};
