/* 📐 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이고, 강조색은 글로우(blur 8)까지 센다.
   반지름 9 짜리 강조색 표식의 「바깥」은 9 + 1.5 + 8 = **18.5**. 상시 숨(±2px)은 여유에
   이미 다 먹여 놓았다. */
import {
  disp, ease, clamp,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* starved — 피해. 아무도 집을 못 얻고 굶어 죽었다.

   원문 근거:
   > "결과는 집 부탁이 영원히 성립하지 않고, 집 없는 주민이 굶어 죽는 것이었습니다."

   🔴 **굶어 죽은 것은 「집 없는 주민」이지 마을 전원이 아니다.** 그래서 그림이 그 범위를
   정확히 진다 — 빈 집터를 이고 서 있던 흰 표식 셋만 차례로 꺼지고, 자기 집이 있는
   오른쪽 강조색 표식은 끝까지 그대로 서 있다. 자막이 한 줄로 말하는 것을 그림이 나눠 준다.

   빈 집터는 **끝까지 안 채워진다.** 채워지는 프레임이 하나라도 있으면 「집을 얻었다」가
   되어 원문과 어긋난다.

   🔴 **1차 반려(§2)를 고친 자리 — 낙하가 「굶어」보다 0.96초 일렀다.**
   wav 04 실측: 발화 개시 0.46초 · 쉼표 무음 1.31~1.45초 → **「굶어 죽었죠」 = 1.45~2.32초**
   (그 뒤 `dur` 2.875 까지는 후행 무음이다 — `dur` 을 말 길이로 보면 안 된다).
   옛 `FALL = [0.50, 1.00, 1.50]` 은 표정 셋 중 둘이 그 낱말 전에 이미 닫혔다.
   `FALL = [1.48, 1.83, 2.18]` 로 밀어 **세 낙하가 전부 1.45초 뒤에 시작**하고
   마지막 소멸이 2.78초(자막 끝 2.875초 앞)라 샷 안에서 다 닫힌다.
   2차 검수 재실측: 표정 알파 ≥0.6 창이 `[1.32,1.90]·[1.67,2.25]·[2.02,2.61]` 로 **셋 다
   그 낱말 구간과 겹친다.**
   🔑 표정은 낙하보다 0.30초 먼저 뜨므로 첫 표정이 **1.32초**에 열려 「굶어」의 예비 동작이 된다.
   ⚠️ 앞 절(「아무도 집을 못 얻고」)에는 **자기 그림이 따로 있다** — 끝까지 안 채워지는 빈 집터
   셋이고 샷 내내 켜져 있다. 낙하는 명백히 뒷 절의 그림이라 그 자리로 옮긴 것이 맞다.

   🔴 **집터를 오각형으로 고쳤다(§3-E · `ADR-V25-15` ③-a).** 이 회차에서 집은 오각형인데
   (SH·S4·SO) 여기만 직사각형이라, 라벨을 가리면 「빈 상자 셋」으로 읽히고 「못 얻은 집 셋」
   으로는 안 읽혔다. 꼭짓점은 다른 샷의 집 글립과 같은 식으로 잡았다(밑변 · 처마 · 지붕).
   도형만 바꾼 것이라 좌표 예산(x ±21 · y 101~135)과 겹침 여유는 그대로다.

   라벨을 다 가려도 「빈 집 모양 테두리 아래에 서 있던 것들이 하나씩 꺼지고, 저 멀리 하나만
   그대로 서 있다」가 읽힌다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **가로 좌표**로 갈랐다.
     빈 집터(track)의 오른쪽 끝이 196.5, 목수(accent)의 글로우 왼쪽 끝이 279.5 —
     **83px** 뜬다. 「집 없는 주민」(sub, 중심 118)은 폭 제한 안에서도 오른쪽 끝이 178 을
     안 넘어 **101.5px** 뜬다. 「목수」(accent, 기준선 216)는 목수 아래에만 있다.

   세로 예산(캔버스 307): 최저 = 「집 없는 주민」 글립 바닥 240. 67px 남는다.
   가로: 오른쪽 = 목수 글로우 + 숨 320.5(여유 29.5px) · 왼쪽 = 집터 40.5.

   계속 도는 것 = 빈 집터 셋의 점선 흐름(**78px/s = 프레임당 2.6px** · 오각형 둘레 132.8px × 3
   에 7px 대시 33개) · 표식 낙하(26px / 0.55초 = **프레임당 1.6px**, 셋이 0.35초 간격) ·
   표식 넷의 상시 숨(**프레임당 0.49px**).
   🔑 낙하를 뒤로 밀면서 **가장 조용한 구간이 앞쪽(0~1.32초)으로 옮겨 갔다.** 그 구간을 지는
   것은 점선 흐름이라, 오각형이 직사각형보다 둘레가 13% 짧아진 만큼 **흐름 속도를 60 → 78px/s
   로 올려** 프레임당 갱신 잉크를 옛 값 위로 되돌렸다(약 456 → 515px²). */

const SEATS = [62, 118, 174];          // 마을 사람 셋의 x
const AY = 178;                        // 서 있는 축
const PY = 118;                        // 빈 집터의 중심 y
const PHW = 21, PHH = 17;
const CX = 300;                        // 목수
/* 🔴 wav 04 실측 「굶어 죽었죠」 개시 1.46초 뒤로 셋 다 옮겼다(1차 반려 §2). */
const FALL = [1.48, 1.83, 2.18];
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

    /* 닫힌 다각형의 변마다 흐르는 점선. `setLineDash` 를 안 쓴다 — 긴 점선이 30fps
       3패스에서 알파가 갈린 전례가 있다. 위상은 호출부에서 양수로 정규화해 넘긴다.
       🔑 변마다 같은 위상을 쓰므로 지붕의 비스듬한 두 변에서도 대시 길이가 일정하다. */
    const dashPoly = (pts, ph) => {
      ctx.beginPath();
      for (let i = 0; i < pts.length; i++) {
        const a = pts[i], b = pts[(i + 1) % pts.length];
        const dx = b[0] - a[0], dy = b[1] - a[1];
        const len = Math.hypot(dx, dy);
        if (len < 1) continue;
        const ux = dx / len, uy = dy / len;
        for (let s = -PITCH; s < len; s += PITCH) {
          const p0 = Math.max(0, s + ph), p1 = Math.min(len, s + ph + 7);
          if (p1 <= p0) continue;
          ctx.moveTo(a[0] + ux * p0, a[1] + uy * p0);
          ctx.lineTo(a[0] + ux * p1, a[1] + uy * p1);
        }
      }
      ctx.stroke();
    };

    /* 다른 샷의 집 글립과 같은 다섯 점(밑변 · 처마 · 지붕). 채우지 않는다 — 이 집터는
       끝까지 비어 있는 것이 사건이다. */
    const plot = (x, y, hw, hh) => [
      [x - hw, y + hh], [x - hw, y - hh * 0.2], [x, y - hh],
      [x + hw, y - hh * 0.2], [x + hw, y + hh]
    ];

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
    const ph = ((t * 78) % PITCH + PITCH) % PITCH;
    const s0 = Math.max(0, since(spec.fallCue ?? 0));

    /* ── 빈 집터 셋 — 끝까지 안 채워진다 ──────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3; ctx.lineCap = 'butt';
    for (const x of SEATS) dashPoly(plot(x, PY, PHW, PHH), ph);
    ctx.restore();

    /* ── 마을 사람 셋 — 차례로 꺼진다 ─────────────── */
    SEATS.forEach((x, i) => {
      const fs = FALL[i];
      const fk = ease(clamp((s0 - fs) / 0.55));            // 26px / 0.55초 = 프레임당 1.6px
      const gone = ease(clamp((s0 - fs - 0.30) / 0.30));
      const a = st * (1 - gone);
      const y = AY + 26 * fk;
      if (a > 0.01) {
        ctx.save();
        ctx.globalAlpha = a;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.arc(x + bx(i), y + by(i), 9, 0, Math.PI * 2); ctx.stroke();
        ctx.restore();
      }
      if (spec.face) {
        const appear = ease(clamp((s0 - fs + 0.30) / 0.25));
        ctext(spec.face, x, 156, 800, 15, tone('ink'), 60, st * appear * (1 - gone));
      }
    });

    ctext(spec.waitLabel ?? '', SEATS[1], 236, 800, 13, tone('sub'), 150,
      st * ease(clamp((s0 - 0.30) / 0.40)));

    /* ── 저 멀리 목수 — 끝까지 그대로 서 있다 ─────── */
    ctx.save();
    ctx.globalAlpha = st;
    setShadow(ctx, GLOW, 8);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(CX + bx(3), AY + by(3), 9, 0, Math.PI * 2); ctx.stroke();
    clearShadow(ctx);
    ctx.restore();

    ctext(spec.carpenterLabel ?? '', CX, 216, 900, 13, tone('accent'), 90, st);

    ctx.textAlign = 'left';
  }
};
