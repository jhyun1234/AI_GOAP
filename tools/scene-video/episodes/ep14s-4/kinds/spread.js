import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* spread — 뒤집힘과 착지. 화덕을 각자 집 옆에 놓자 마을이 흩어졌다.

   원문 근거:
   > "그래서 모닥불을 개인 시설로 내렸습니다. … 자기 집 곁에 짓게 했습니다."
   > "식사는 앵커를 아예 풀어서 제자리에서 먹게 했습니다."
   > "순둥이는 이웃 곁에 붙어 살고, 떠돌이는 마을 경계 근처에 외딴집을 짓습니다."

   앞 세 샷이 「가운데 하나가 여섯을 붙든다」였다면 여기서 **가운데가 없어진다.**
   자막 0 : 가운데 불이 줄어들며 꺼지고, 집들이 뭉쳐 있던 안쪽 고리에서 **각자의 거리로
     밀려 나간다.** 뒤이어 집마다 곁에 **작은 불**이 하나씩 켜진다.
   자막 1 : 가장 안쪽 집에 「순둥이」, 가장 바깥 집에 「떠돌이」 이름이 켜진다.

   라벨을 다 가려도 「한 점에 뭉쳐 있던 것이 흩어지고, 그 점이 여섯 개로 나뉘어 각자에게
   하나씩 붙는다」가 읽힌다 — 이 편에서 그림이 가장 유리한 자리다.

   🔴 **불이 「날아가지」 않는다.** 가운데 불이 여섯 갈래로 날아가면 그 경로가 집(흰색)을
   반드시 관통한다 — 강조색과 흰색이 같은 픽셀을 나눠 갖는 프레임이 생긴다. 그래서
   **시간으로 갈랐다**: 가운데 불은 자막이 시작되고 0.55초에 완전히 꺼지고 곁불은
   0.95초부터 제자리에서 켜진다. 뜻도 이쪽이 맞는다 — 원문은 공용 모닥불을 **휴면으로
   남기고** 개인 화덕을 새로 만든 것이지 같은 불을 옮긴 게 아니다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **반지름 오프셋으로 갈랐다.**
     곁불은 자기 집에서 반지름 방향으로 **46px 바깥**에 앉는다. 집 글립은 x±14 · y−18~+12,
     곁불은 글로우까지 x±14 · y±15 이므로 **가로로 28px 또는 세로로 27~33px 을 넘으면**
     상자가 안 겹치는데, 여섯 각도(125·173·221·269·317·5) 전부에서 그렇다.
     가장 아슬아슬한 두 자리를 적어 둔다 — **317° 는 가로가 5.6px 남고 221° 는 6.7px 남는다**
     (둘 다 세로로는 겹치므로 가로가 혼자 막는 자리다). 이웃 집과의 간섭은 없다
     (자리 사이 최소 48°).

   세로 예산(캔버스 307): 최저 = 「마을 경계」 글립 바닥 270(기준선 266). 37px 남는다.
   그다음이 125° 곁불의 글로우 바닥 250 — 라벨 상자 위쪽(253)과 3px 뜨고 가로로는
   안 만난다(곁불 x 102~130 · 라벨 x 143~209).
   가로: 5° 곁불의 오른쪽 끝(글로우 포함) 307. 캔버스 352, 여유 45px.

   계속 도는 것 = 마을 경계 고리의 점선 회전(둘레 603px · 면적이 크다) + 곁불 여섯의
   불꽃 맥동(위상을 자리마다 어긋나게 줬다). */

const CY = 150;
const R_IN = 50, R_OUT = 96;
const HEARTH_OFF = 46;

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

    const dashRing = (r, segDeg, gapDeg, phaseDeg, alpha) => {
      if (alpha <= 0.01) return;
      const P = segDeg + gapDeg;
      const ph = ((phaseDeg % P) + P) % P;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      for (let a = ph - P; a < 360; a += P) {
        ctx.beginPath();
        ctx.arc(CX, CY, r, a * Math.PI / 180, (a + segDeg) * Math.PI / 180);
        ctx.stroke();
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
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
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

    /* ── 마을 경계 ────────────────────────────────── */
    dashRing(R_OUT, 9, 8, -t * 10, st * (0.35 + 0.65 * move));
    if (spec.edgeLabel) {
      ctext(spec.edgeLabel, CX, CY + R_OUT + 20, 900, 14, tone('ink'), 150, st * move);
    }

    /* ── 가운데 불 : 줄어들며 꺼진다 ────────────────── */
    const puff = 0.5 + 0.5 * Math.sin(t * 3.1);
    const cs = 1 - gone;
    if (cs > 0.02) flame(CX, CY + 11, 10 * cs, (24 + 4 * puff) * cs, st * cs, 8 + 3 * puff);

    /* ── 집과 곁불 ────────────────────────────────── */
    const nameOf = { 221: spec.nearName, 317: spec.farName };
    for (let i = 0; i < SEATS.length; i++) {
      const s = SEATS[i];
      const r = lerp(R_IN, rOf(s.ratio), move);
      const a = s.deg * Math.PI / 180;
      const x = CX + r * Math.cos(a), y = CY + r * Math.sin(a);

      const hp = 0.5 + 0.5 * Math.sin(t * 3.4 + i * 1.9);
      const hr = r + HEARTH_OFF;
      flame(CX + hr * Math.cos(a), CY + hr * Math.sin(a) + 7,
        6 * lit, (13 + 3 * hp) * lit, st * lit, 6 + 2 * hp);

      house(x, y, st, 1);

      const nm = nameOf[s.deg];
      if (nm) ctext(nm, x, y + 30, 900, 13.5, tone('ink'), 96, st * ease(cue(spec.nameCue ?? 1, 0.12, 0.34)));
    }

    ctx.textAlign = 'left';
  }
};
