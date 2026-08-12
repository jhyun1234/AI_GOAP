/* 📐 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이고, 강조색은 글로우(blur 8)까지 센다.
   반지름 9 짜리 강조색 표식의 「바깥」은 9 + 1.5 + 8 = **18.5**. 상시 숨(±2px)은 여유에
   이미 다 먹여 놓았다. */
import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* farbubble — 기각한 안. 멀리서도 되게 하면 허공에 대고 떠들게 된다.

   원문 근거:
   > "부탁 반경을 늘리는 안은 폐기했습니다. 부탁 연출이 근접을 전제로 만들어져 있거든요 —
   >  양쪽이 하던 일을 멈추고 서로를 마주본 채 8초간 말풍선을 주고받습니다.
   >  반경을 늘리면 맵 반대편끼리 허공에 대고 대화하는 장면이 됩니다."

   🔴 **이 샷은 「하지 않은 일」을 그린다.** 자막이 「멀리서도 되게 **하면**」이라는 조건절로
   말하는 동안에만 떠 있고, 그림도 성립하지 않는 모습(말풍선이 서로에게 못 닿고 쪼그라들며
   흩어진다)으로 끝난다 — 되는 장면을 한 프레임도 안 보여 준다.

   🔴 **8초는 안 그린다.** 이 편은 화면에 6타일 · 38타일 · 8타일 셋을 올리므로, 단위가 다른
   8 이 하나 더 뜨면 슬롯이 섞인다. 「양쪽이 마주보고 주고받는다」는 두 말풍선이 서로
   어긋난 위상으로 번갈아 뜨는 것으로 진다.

   1.4초 루프 : 흰 말풍선이 왼쪽 표식 위에서 떠올라 오른쪽으로 가다 쪼그라들며 사라지고,
   0.7초 뒤 강조색 말풍선이 오른쪽 표식 위에서 떠올라 왼쪽으로 가다 사라진다. 둘이 만나는
   프레임이 하나도 없고 가운데는 끝까지 비어 있다.

   라벨을 다 가려도 「양 끝에 하나씩 떨어져 선 둘이 저마다 무언가를 띄우는데, 그게 상대에게
   닿기 한참 전에 쪼그라들어 흩어진다」가 읽힌다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **두 겹으로 막았다.**
     ⓐ 바닥 눈금(track)을 x 268 에서 끊었다. 목수(accent)의 글로우 왼쪽 끝이 285.5 라
        **17.5px** 뜬다. y 로도 46px 떨어져 있지만, 「끝에서 끝까지 긋는 배경선」은 좌표로도
        자르는 것이 규칙이다.
     ⓑ 가운데를 비웠다. 흰 말풍선은 가장 오른쪽으로 갔을 때도 x 124.7 이고, 강조색 말풍선은
        가장 왼쪽으로 갔을 때도 글로우까지 218.7 이라 **94px** 뜬다.

   세로 예산(캔버스 307): 최저 = 바닥 눈금 215.5. 91px 남는다.
   가로: 오른쪽 = 목수 글로우 + 숨 326.5(여유 23.5px) · 왼쪽 = 눈금 시작 26.

   계속 도는 것 = 말풍선 이동(60px / 0.63초 = **프레임당 3.2px**, 둘이 0.7초 어긋난 위상이라
   쉬는 구간이 없다) · 바닥 눈금 점선 흐름(60px/s = **프레임당 2.0px**) ·
   표식 둘의 상시 숨(**프레임당 0.49px**). */

const VX = 46, CX = 306, AY = 150;
const BASE_Y = 214, BASE_X0 = 26, BASE_X1 = 268;
const PITCH = 12;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, since }) {
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

    /* 꼬리가 자기 주인 쪽을 가리키는 말풍선. dir −1 이면 꼬리가 왼쪽 아래에 붙는다. */
    const bubble = (x, y, sc, dir, color, alpha, glow) => {
      if (alpha <= 0.01) return;
      const hw = 15 * sc, hh = 10 * sc, tl = 7 * sc;
      ctx.save();
      ctx.globalAlpha = alpha;
      if (glow) setShadow(ctx, GLOW, 8);
      ctx.strokeStyle = color; ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(x - hw, y - hh);
      ctx.lineTo(x + hw, y - hh);
      ctx.lineTo(x + hw, y + hh);
      ctx.lineTo(x + hw * 0.25 * dir, y + hh);
      ctx.lineTo(x + hw * 0.55 * dir, y + hh + tl);
      ctx.lineTo(x + hw * 0.55 * dir, y + hh);
      ctx.lineTo(x - hw, y + hh);
      ctx.closePath(); ctx.stroke();
      if (glow) clearShadow(ctx);
      ctx.restore();
    };

    const st = ease(clamp(t / 0.28));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    const bx = i => 2 * Math.sin(t * 7.4 + i * 1.7);
    const by = i => 2 * Math.cos(t * 6.1 + i * 2.3);

    /* ── 바닥 눈금 — 목수 앞에서 끊는다 ───────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.fillStyle = tone('track');
    const ph = ((t * 60) % PITCH + PITCH) % PITCH;
    for (let s = -PITCH; s < BASE_X1 - BASE_X0; s += PITCH) {
      const a = Math.max(0, s + ph), b = Math.min(BASE_X1 - BASE_X0, s + ph + 7);
      if (b <= a) continue;
      ctx.fillRect(BASE_X0 + a, BASE_Y - 1.5, b - a, 3);
    }
    ctx.restore();

    /* ── 말풍선 둘 — 서로에게 못 닿고 흩어진다 ────── */
    /* 루프의 원점을 이 샷의 자막에 물린다 — 자막이 하나뿐이라 값으로는 `t` 와 같지만,
       「무엇에 맞춰 도는가」를 코드에 남겨 둔다(계약 2). */
    const u = frac(Math.max(0, since(spec.talkCue ?? 0)) / (spec.loopSec ?? 1.4));
    const leg = (v, x0, x1, dir, color, glow) => {
      const q = ease(clamp(v / 0.45));
      const a = ease(clamp(v / 0.08)) * (1 - ease(clamp((v - 0.34) / 0.11)));
      bubble(lerp(x0, x1, q), lerp(120, 102, q), lerp(1, 0.62, q), dir, color, st * a, glow);
    };
    leg(u, 62, 116, -1, tone('ink'), false);
    leg(frac(u + 0.5), 290, 236, 1, tone('accent'), true);

    /* ── 양 끝의 둘 ─────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(VX + bx(0), AY + by(0), 9, 0, Math.PI * 2); ctx.stroke();
    ctx.restore();

    ctx.save();
    ctx.globalAlpha = st;
    setShadow(ctx, FAIL_GLOW, 8);
    ctx.strokeStyle = tone('fail'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(CX + bx(1), AY + by(1), 9, 0, Math.PI * 2); ctx.stroke();
    clearShadow(ctx);
    ctx.restore();

    ctext(spec.villageLabel ?? '', VX, 192, 800, 13, tone('sub'), 90, st);
    ctext(spec.carpenterLabel ?? '', CX, 192, 900, 13, tone('accent'), 90, st);

    ctx.textAlign = 'left';
  }
};
