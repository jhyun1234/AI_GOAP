import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* leftquestion — 감상은 날아가고 질문이 남더니, 그 질문이 다음 판에 불을 켠다.

   원문 근거:
   > "감상이 '재미없다'에서 '신기하네'로 바뀌었습니다. 그리고 제일 중요한 건 마지막입니다.
   >  '집과 밭이 있었는데 왜 죽었지?'라는 질문이 생겼습니다."
   > "그래도 질문이 생겼다는 건 큽니다. 질문이 없으면 다음 판을 켤 이유도 없거든요."
   라벨 「감상」·「다음 판」은 둘 다 원문 낱말 그대로다.

   🔴 회차 안 되받기 — S1(replay)에서 흰색으로 켜졌던 물음표가 여기서 **강조색으로** 돌아오고,
   S1 의 판 틀이 오른쪽에 다시 선다. 「매번 하던 질문」이 「이번에 남은 질문」이 되고,
   그것이 다시 「다음 판」으로 이어지는 것이 도형의 연결로 그대로 읽힌다.

   색 규약(이 회차 전체): **흰색 = 마을에 이미 있던 것 / 강조색 = 이번 관찰에서 새로 생긴 것.**
   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **가로 좌표로 갈랐다.**
     강조색 화살 촉 끝 x 176 · 글로우(blur 8)까지 **184** / 흰 판 틀 왼 획 바깥 **194.5**.
     **10.5px** 뜬다. 세로로는 흰 줄 두 개의 바닥 **107** 과 물음표 글로우 꼭대기 **122** 가
     15px 뜨고, 그때쯤 흰 줄은 거의 사라져 있다.
   흰 글자끼리(축 B): 「감상」(중심 x 90 · baseline 66) ↔ 「다음 판」(중심 x 259 · baseline 226)
     이 **x 169px · y 160px** 로 둘 다 갈렸다.

   ⏱ 흰 줄이 사라지는 것과 물음표가 앉는 것은 `since(0)`, 화살과 판 틀 밝아짐은 `cue(1)` 이다.
   🔴 **사라지는 흰 줄의 실효 알파를 설계 단계에서 미리 밀었다.** `ep15s-5` 1차본이
   같은 자리(사라지는 요소)의 최대 실효 알파 0.334 로 지적받았기 때문이다.
   실효 알파 = `st × (1 − fade) × ENTER`, `fade = ease(clamp((ts − 0.50) / 0.75))` 로 풀면
   **최대 약 0.83 @ ts 0.62 · 0.6 이상 약 0.40초(ts 0.36~0.78) · 소멸 ts 1.25** 다.
   그 값이 물음표의 등장 시각(`since(0)` 0.95)을 정한다 — 흰 줄이 충분히 보인 뒤에 온다.
   `latch delayMs 1220` 은 물음표 앉기(0.95~1.60)의 41.5% 지점이다.
   🔴 화살·판 틀은 `cue(1, 0.15, 0.40)` 으로 **span 을 기본 0.7 이 아니라 0.40 으로 좁혔다** —
   마지막 자막이 예상보다 짧아져도 페이오프가 앞쪽 절반에서 끝난다.

   세로 예산(캔버스 307): 최저 = 「다음 판」 글립 바닥 약 231. 76px 남는다.
   가로: 판 틀 오른 획 바깥 323.5. 좌 42.5 · 우 28.5 남는다.

   계속 도는 것(30fps 기준):
     · 판 틀 둘레 2×(126+108) = **468px** 이 숨을 쉰다.
       `bre = 1.4·(1 + sin(9.6 t))` → 진폭 1.4 · ω 9.6 → 0.448px/프레임.
       cue(1) 전에는 알파 0.7 이라 약 **147px²**
     · 🔴 판 틀 안 표식 둘이 **샷 시작부터** 걷는다 — 82px 를 1.1초에 → **2.5px/프레임** ×
       높이 30 × 2개, cue(1) 전 알파 0.6 → 약 **180px²**. 합 **약 327px²**
     · 물음표 숨 진폭 0.9px · 5.4rad/s */

const FX = 196, FY = 96, FW = 126, FH = 108;
const L1 = [44, 84, 92, 7];
const L2 = [52, 100, 76, 7];
const ARROW_X0 = 118, ARROW_LEN = 58, ARROW_Y = 152;

const tri = x => 1 - Math.abs(2 * frac(x) - 1);

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (!txt || alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    const st = ease(cue(spec.askCue ?? 0, 0.15, 0.30));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const lk = ease(cue(spec.linkCue ?? 1, 0.15, 0.40));

    /* ── 오른쪽 : 다음 판 (흰색, 샷 내내 돈다) ────── */
    const bre = 1.4 * (1 + Math.sin(t * 9.6));
    const fa = lerp(0.7, 1.0, lk);
    ctx.save();
    ctx.globalAlpha = st * fa;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, FX + bre, FY + bre, FW - bre * 2, FH - bre * 2, 10);
    ctx.stroke();
    ctx.restore();

    const ma = lerp(0.6, 1.0, lk);
    for (let i = 0; i < 2; i++) {
      const x = lerp(218, 300, tri(t / 2.2 + i * 0.5));
      const y = 150 + 3 * Math.sin(t * 5.8 + i * 2.2);
      ctx.save();
      ctx.globalAlpha = st * ma;
      ctx.translate(x, y);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, -6, -15, 12, 30, 6);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 왼쪽 위 : 감상이 흐려져 없어진다 (흰색) ─── */
    const fade = ease(clamp((since(spec.askCue ?? 0) - 0.50) / 0.75));
    const la = st * (1 - fade);
    if (la > 0.01) {
      ctx.save();
      ctx.globalAlpha = la;
      ctx.fillStyle = tone('ink');
      ctx.fillRect(L1[0], L1[1], L1[2], L1[3]);
      ctx.fillRect(L2[0], L2[1], L2[2], L2[3]);
      ctx.restore();
    }

    /* ── 그 아래 : 질문이 앉는다 (강조색) ─────────── */
    const q = ease(clamp((since(spec.askCue ?? 0) - 0.95) / 0.65));
    if (q > 0.01) {
      /* 🔴 ctext 는 자기 안에서 globalAlpha 를 덮어쓴다 — 알파는 반드시 인자로 넘긴다.
         그림자는 ctext 의 save() 이전 상태라 fillText 까지 살아 있다. */
      const qb = 0.9 * (1 + Math.sin(t * 5.4));
      ctx.save();
      setShadow(ctx, GLOW, 10);
      ctext('?', 90, lerp(180, 172, q) + qb, 900, lerp(40, 54, q), tone('accent'), 70, st * q);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 질문이 다음 판에 불을 켠다 (강조색 화살) ── */
    if (lk > 0.02) {
      const tip = ARROW_X0 + ARROW_LEN * lk;
      ctx.save();
      ctx.globalAlpha = st;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      const shaft = Math.max(0, tip - 12 - ARROW_X0);
      if (shaft > 0.5) ctx.fillRect(ARROW_X0, ARROW_Y - 3, shaft, 6);
      ctx.beginPath();
      ctx.moveTo(tip - 12, ARROW_Y - 9);
      ctx.lineTo(tip, ARROW_Y);
      ctx.lineTo(tip - 12, ARROW_Y + 9);
      ctx.closePath(); ctx.fill();
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 라벨 ─────────────────────────────────────── */
    ctext(spec.feelLabel, 90, 66, 800, 13.5, tone('sub'), 90, st);
    ctext(spec.runLabel, FX + FW / 2, 226, 800, 13.5, tone('sub'), 110, st);

    ctx.textAlign = 'left';
  }
};
