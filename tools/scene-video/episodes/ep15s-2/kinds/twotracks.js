/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다.
   경로 좌표를 적을 땐 `(경로)` 라고 표시한다. */
import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW, GLOW_INK
} from '../../../engine/lib.js';

/* twotracks — 실측. 배고픔 100 · 도망 92, 그래서 늑대가 와도 밥이 이긴다.

   원문 근거:
   > "실측해봤더니 미래의 우려가 아니라 이미 살아 있는 결함이었습니다.
   >  배고픔 목표의 우선순위는 100이고 피신 목표는 92였습니다.
   >  배가 어느 정도 고픈 주민에게 늑대가 접근하면 배고픔이 이깁니다."
   🔴 수치 슬롯 : **100 = 배고픔**(처음부터 끝까지 불변) · **92 = 도망(수정 전)**.
     이 샷에는 105·95·110 이 한 개도 안 나온다.

   자막 0(배고픔이 100인데 도망은 92였어요) : 나란한 두 기둥. 왼쪽 흰 칸이 위, 오른쪽
     강조색 칸이 한참 아래에 앉는다.
   자막 1(배고프면 늑대가 와도 밥이 이기죠) : 아래에서 흰 쐐기가 주민에게 바짝 다가온다.
     그동안에도 **고르는 표식은 계속 왼쪽 위 칸에만 내리꽂힌다.** 오른쪽 칸은 안 골라진다.

   🖼 라벨을 다 가려도 읽힌다 — 「두 기둥에 든 것의 높이가 다르고, 고르는 표식은 매번
     높은 쪽에만 내려앉는다. 아래에서 뾰족한 것이 다가와도 표식은 안 옮겨간다.」

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **가로 좌표로 갈랐다.**
     강조색은 오른쪽 기둥에만 있다: 칸 x 246.5~317.5(획 바깥) · 글로우(blur 5) 241.5~322.5,
     「도망」 글립 상자 x 257~307.
     · 오른쪽 기둥의 흰 난간 : x 228.5~231.5 와 332.5~335.5 → 양쪽 다 **10px** 뜬다.
     · 흰 기준선(y 150)은 **x 222 에서 끊는다** — 강조색 글로우 왼쪽 끝 241.5 까지 19.5px.
       🔑 끝에서 끝까지 긋는 배경선이 강조색 글립을 관통하는 사고를 막는 자리다.
     · 흰 「배고픔」 글립 상자 x 139~189 · 흰 고르는 표식 x 156~172 → 전부 왼쪽 기둥 안.
     · 아래 장면(쐐기·주민·바닥선·「늑대」)은 y 229 아래이고 강조색은 y 224.5 위 →
       가로로도 세로로도 안 만난다.

   세로 예산(캔버스 307) : 최저 = 바닥선 획 바깥 281.5. 25.5px 남는다.
     최고 = 「배고픔」·「도망」 글립 top 73. 63px 남는다.
   가로(캔버스 352) : 오른쪽 최대 = 난간 335.5(16.5px 남음) · 왼쪽 최소 = 바닥선 16.

   ⏱ 계속 도는 것 = 고르는 표식이 1.1초마다 내리꽂힌다(22px 를 0.46초에 = 초당 48px,
     30fps 한 프레임에 1.6px) + 늑대의 접근(초당 50~110px) + 주민의 숨.
     둘 다 `since()` 위에 세워 **TTS 길이가 바뀌어도 안 움직인다.**
     `setLineDash` 는 한 군데도 안 썼다. */

const RAIL_Y0 = 100, RAIL_Y1 = 226;
const GUIDE_Y = 150, GUIDE_X0 = 118, GUIDE_X1 = 222;
const AX = [118, 210], BX = [230, 334];
const A_BLK = { x: 130, w: 68 }, B_BLK = { x: 248, w: 68 };
const A_CX = A_BLK.x + A_BLK.w / 2;      // 164
const B_CX = B_BLK.x + B_BLK.w / 2;      // 282
const BH = 24;
const lvl = v => GUIDE_Y - (v - 100) * 7;   // 100→150 · 92→206

const GND_Y = 280, WOLF_Y = 262, VX = 150;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

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

    const rail = (x, alpha) => {
      ctx.save();
      ctx.globalAlpha = alpha * 0.5;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineCap = 'butt';
      ctx.beginPath(); ctx.moveTo(x, RAIL_Y0); ctx.lineTo(x, RAIL_Y1); ctx.stroke();
      ctx.restore();
    };

    const s0 = since(spec.scaleCue ?? 0);
    const s1 = since(spec.wolfCue ?? 1);
    const st = ease(clamp(cue(spec.scaleCue ?? 0, 0.15, 0.7) / 0.14));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    for (const x of AX) rail(x, st);
    for (const x of BX) rail(x, st);

    /* 기준선(흰) — x 222 에서 끊는다. 점선은 fillRect 조각.
       🔴 `Math.min` 으로 **클램프한다.** 1차는 `for (x=118; x<222; x+=14) fillRect(x,…,8,3)`
       이라 마지막 조각이 216~**224** 까지 나가 주석(「222 에서 끊는다」)보다 2px 길었다.
       강조색까지 17.5px 남아 안전했지만, **문서와 코드가 갈리면 다음 사람이 문서를 믿는다.**
       `overtake` 와 같은 형태로 맞췄다. */
    ctx.save();
    ctx.globalAlpha = st * 0.45;
    ctx.fillStyle = tone('ink');
    for (let x = GUIDE_X0; x < GUIDE_X1; x += 14) {
      const x1 = Math.min(GUIDE_X1, x + 8);
      if (x1 > x) ctx.fillRect(x, GUIDE_Y - 1.5, x1 - x, 3);
    }
    ctx.restore();

    /* ── 고르는 표식(흰 세모) : 1.1초마다 왼쪽 칸에 내리꽂힌다 ── */
    const q = frac(Math.max(0, s0) / (spec.pickSec ?? 1.1));
    const down = ease(clamp(q / 0.42));
    const back = ease(clamp((q - 0.55) / 0.45));
    const apex = lerp(112, 132, down * (1 - back));
    const hit = ease(clamp((down - 0.80) / 0.20)) * (1 - back);

    /* ── 배고픔 칸(흰) ─────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    if (hit > 0.02) setShadow(ctx, GLOW_INK, 4 + 8 * hit);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, A_BLK.x, lvl(100) - BH / 2, A_BLK.w, BH, 4); ctx.stroke();
    clearShadow(ctx);
    ctx.restore();
    ctext(spec.hungerValue ?? '100', A_CX, lvl(100) + 6, 900, 15, tone('ink'), 56, st);
    if (spec.hungerLabel) ctext(spec.hungerLabel, A_CX, 84, 800, 13, tone('ink'), 84, st);

    ctx.save();
    ctx.globalAlpha = st * (0.35 + 0.65 * (down * (1 - back)));
    ctx.fillStyle = tone('ink');
    ctx.beginPath();
    ctx.moveTo(A_CX, apex);
    ctx.lineTo(A_CX - 8, apex - 12);
    ctx.lineTo(A_CX + 8, apex - 12);
    ctx.closePath(); ctx.fill();
    ctx.restore();

    /* ── 도망 칸(강조색) : 아래에 앉아 있고 아무도 안 고른다 ── */
    ctx.save();
    ctx.globalAlpha = st;
    setShadow(ctx, GLOW, 5);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, B_BLK.x, lvl(92) - BH / 2, B_BLK.w, BH, 4); ctx.stroke();
    clearShadow(ctx);
    ctx.restore();
    ctext(spec.fleeValue ?? '92', B_CX, lvl(92) + 6, 900, 15, tone('accent'), 56, st);
    if (spec.fleeLabel) ctext(spec.fleeLabel, B_CX, 84, 800, 13, tone('accent'), 84, st);

    /* ── 아래 장면 : 늑대가 다가와도 아무 일도 안 일어난다 ── */
    ctx.save();
    ctx.globalAlpha = st * 0.5;
    ctx.fillStyle = tone('ink');
    for (let x = 16; x < 206; x += 14) ctx.fillRect(x, GND_Y - 1.5, 8, 3);
    ctx.restore();

    const lq = frac(Math.max(0, s0) / (spec.prowlSec ?? 1.9));
    const loopK = lq < 0.7 ? ease(lq / 0.7) : 1 - ease((lq - 0.7) / 0.3);
    const near = s1 >= 0 ? ease(clamp((s1 - 0.20) / 0.60)) : 0;
    const wk = Math.max(loopK * (1 - near), near);
    const tip = lerp(52, 118, wk);

    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    ctx.beginPath();
    ctx.moveTo(tip, WOLF_Y);
    ctx.lineTo(tip - 30, WOLF_Y - 16);
    ctx.lineTo(tip - 30, WOLF_Y + 16);
    ctx.closePath(); ctx.stroke();

    ctx.beginPath();
    ctx.arc(VX, WOLF_Y, 10 + 1.2 * Math.sin(t * 6.2), 0, Math.PI * 2); ctx.stroke();
    ctx.restore();
    if (spec.wolfLabel) ctext(spec.wolfLabel, 60, 240, 800, 11, tone('ink'), 60, st * 0.85);

    ctx.textAlign = 'left';
  }
};
