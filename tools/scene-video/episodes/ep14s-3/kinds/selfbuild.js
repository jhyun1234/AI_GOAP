import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* selfbuild — 해결과 남는 한 줄.
   「그래서 목수만 자기 집을 직접 짓게 했어요 / 다 열어주면 아무도 부탁을 안 하니까요.」

   원문 근거(8단계 절):
   > "해법은 새 장치를 만들지 않고, 같은 커밋에서 어차피 만들던 '이 목표는 이 직업만'이라는
   >  통로를 재사용했습니다. 목수 전용 자가 건축 목표를 하나 추가한 거죠.
   >  우선순위는 남의 부탁보다 낮게 뒀습니다. 자기 집도 짓지만 의뢰가 들어오면 그게 먼저입니다."
   > "조건 없이 열면 전원이 자기 집을 직접 지어버리고 부탁 시스템 자체가 붕괴합니다.
   >  목수라는 병목이 사라지면 이 마을의 사회 시스템이 통째로 의미를 잃습니다."

   🔴 **우선순위 조항은 자막에서 빼고 이 그림이 진다** — 「먼저」가 붙은 「남의 부탁」 칸이
   위, 새로 꽂히는 「내 집 짓기」 칸이 그 **아래**다. 기획 브리프 검산이 이 편을 약 190자로
   재고 「덜어내라」 했는데, 우선순위는 **말 없이도 그림이 질 수 있는 유일한 조각**이었다.
   「먼저」는 원문 어절 그대로다.

   🔴 두 국면이 한 kind 안에 있다.
     Phase A (`since(0)`) — 새 칸이 아래 자리를 오른쪽에서부터 채우며 앉고 조건표가 걸리자,
       여태 비어 있던 왼쪽 자리에 **처음으로** 집이 선다.
       onlyone(훅)이 남겨 둔 빈칸을 여기서 채운다.
     Phase B (`since(1)`) — 조건표를 떼자 카드 둘이 통째로 사라지고 네 자리 **전부**에
       똑같은 집이 서 버린다. onepath(S1)의 깔때기를 해체하는 그림이다 —
       위로 올라가던 칸(부탁)이 없어진 것이 곧 「부탁 시스템 붕괴」다.

   🖼️ 라벨을 전부 가려도 남는 사건:
       **칸 하나가 꽂히자 빈 자리가 채워지고, 그 칸을 떼자 전부가 똑같아지며 위 칸이 사라진다.**

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **좌우 + 시간** 두 겹이다.
     ⓐ 좌우·위아래 : 집(accent) y 122~166(글로우 112~176) · 자리(ink) y 187~217 → 11px 뜬다.
        카드는 위(ink) 78~114 / 아래(accent) 134~170(글로우 124~180) → 10px.
        카드 열(x 146~318)과 목수 자리·집(x 14~85 글로우 포함)은 61px 뜬다.
     ⓑ 시간 : 겹치는 자리가 둘 있는데 **둘 다 시간으로 갈랐다.**
        · 조건표(accent, x 267~325 · y 184~208) ↔ Phase B 넷째 자리(ink, x 276~330 · y 187~217)
        · 카드 열(x 146~318, y 78~180) ↔ Phase B 셋째·넷째 집(accent, 글로우 y 112~176)
        조건표는 `s1 = 0.26` 에, 카드는 `s1 = 0.48` 에 알파가 **정확히 0** 이 되고
        (`1 − ease(1) = 0`), 새 자리와 그 집은 `s1 = 0.58` 부터 나타난다.
        아래 `if (a > 0.004)` 세 줄이 부동소수 꼬리까지 끊는 장치다.

   🔴 **캔버스 밖으로 나가는 이동을 하나도 안 쓴다.** 새 칸은 오른쪽에서 슬라이드하는 대신
      **폭이 자라며** 자리를 채우고(오른쪽 끝 고정), 사라지는 카드는 옆으로 밀리는 대신
      **제자리에서 줄어들며** 꺼진다. 밀어내기로 그리면 알파가 남은 채 화면 끝에 걸려
      `좌우 가장자리 잘림` 이 red 가 된다.

   🔴 화면에 수를 한 개도 안 올렸다. 자리 넷은 `spec.marks` 로만 있다.

   세로 예산(캔버스 307): 최저 = 「목수」 라벨 글립 바닥 242. 65px 남는다.
   가로: 카드 오른쪽 끝 318(글로우 328), 넷째 집 글로우 338(캔버스 352).

   ⏱ 두 국면 다 `since()` 위에 있다 — `cue()`(자막 길이에 비례하는 창)가 아니다.
      ①`latch` 의 `delayMs 900` 이 조건표 안착(`s0 = 0.90`)과 원점을 공유하고
      ②③남는 한 줄이 `s1 = 1.15` 에 완성돼 **자막이 짧아져도 뒤로 안 밀린다**
      (ep08s-3 이 cue 창이 좁아져 페이오프가 0.54초 늦어 반려된 형태를 원리적으로 피한다).
   계속 도는 것 = 집 몸통 안 빗금 흐름(Phase B 에서는 네 채라 면적이 는다) +
      카드 안 빗금 + 조건표 글로우 맥동. */

const MARK_CY = 202, MARK_W = 54, MARK_H = 30;
const HOUSE_BASE = 166, HOUSE_H = 44, HOUSE_W = 50;
const SELF_LABEL_Y = 238;
const CARD_W = 172, CARD_H = 36;
const ASK_CY = 96, OWN_CY = 152, FIRST_Y = 64;
const TAG_W = 58, TAG_H = 24, TAG_CY = 196;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01 || !txt) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 12 + tw / 2, w - 12 - tw / 2), y);
      ctx.restore();
    };

    /** 카드 안 빗금 — 계속 흐른다(면적 있는 움직임). 호출 전에 clip 을 잡아 둔다. */
    const hatch = (x0, y0, bw, bh, speed, alpha) => {
      if (bh <= 5 || alpha <= 0.01) return;
      ctx.lineWidth = 4; ctx.globalAlpha = alpha;
      const S = 16, off = (t * speed) % S;
      for (let x = x0 - bh - S + off; x < x0 + bw + S; x += S) {
        ctx.beginPath();
        ctx.moveTo(x, y0 + bh); ctx.lineTo(x + bh, y0);
        ctx.stroke();
      }
    };

    /** 강조색 집 하나. */
    const drawHouse = (cx, k, alpha) => {
      if (k <= 0.02 || alpha <= 0.01) return;
      const h = HOUSE_H * k;
      const bodyW = HOUSE_W * 0.78, bodyH = h * 0.60, eave = HOUSE_BASE - bodyH;
      ctx.save();
      ctx.globalAlpha = alpha;
      setShadow(ctx, GLOW, 10);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(cx - bodyW / 2, HOUSE_BASE);
      ctx.lineTo(cx - bodyW / 2, eave);
      ctx.lineTo(cx - HOUSE_W / 2, eave);
      ctx.lineTo(cx, HOUSE_BASE - h);
      ctx.lineTo(cx + HOUSE_W / 2, eave);
      ctx.lineTo(cx + bodyW / 2, eave);
      ctx.lineTo(cx + bodyW / 2, HOUSE_BASE);
      ctx.closePath();
      ctx.stroke();
      clearShadow(ctx);
      ctx.beginPath();
      ctx.rect(cx - bodyW / 2, eave, bodyW, bodyH);
      ctx.clip();
      hatch(cx - bodyW / 2, eave, bodyW, bodyH, 24, alpha * 0.30);
      ctx.restore();
    };

    const n = Math.max(2, spec.marks ?? 4);
    const mxs = [];
    for (let i = 0; i < n; i++) mxs.push(w * (0.14 + 0.24 * i));
    const CX = w * 0.66;
    const CARD_R = CX + CARD_W / 2;                       // 카드 오른쪽 끝 — 고정이다

    const s0 = since(spec.fixLine ?? 0);
    const s1 = since(spec.openLine ?? 1);

    const st = ease(clamp(s0 / 0.10));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const cardIn = ease(clamp(s0 / 0.28));
    const grow = ease(clamp((s0 - 0.30) / 0.42));
    const lock = ease(clamp((s0 - 0.62) / 0.28));         // 안착 = s0 0.90 (latch delayMs 900)
    const rise = ease(clamp((s0 - 1.05) / 1.00));

    const tagA = lock * (1 - ease(clamp(s1 / 0.26)));     // s1 0.26 에 정확히 0
    const cardA = 1 - ease(clamp((s1 - 0.20) / 0.28));    // s1 0.48 에 정확히 0
    const spread = ease(clamp((s1 - 0.58) / 0.57));       // 페이오프 완성 = s1 1.15
    const selfLabA = 1 - ease(clamp((s1 - 0.30) / 0.30));

    /* ── 할 일 두 칸 (Phase A) ─────────────────────── */
    if (cardA > 0.004) {
      const sc = 0.86 + 0.14 * cardA;                     // 꺼질 때 제자리에서 줄어든다

      /* 위 칸 — 남의 부탁. 「먼저」가 그 위에 붙는다(우선순위는 자리로 말한다). */
      const aA = st * cardIn * cardA;
      ctx.save();
      ctx.globalAlpha = aA;
      ctx.translate(CX, ASK_CY); ctx.scale(sc, sc); ctx.translate(-CX, -ASK_CY);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, CX - CARD_W / 2, ASK_CY - CARD_H / 2, CARD_W, CARD_H, 8);
      ctx.stroke();
      ctx.beginPath();
      ctx.rect(CX - CARD_W / 2 + 3, ASK_CY - CARD_H / 2 + 3, CARD_W - 6, CARD_H - 6);
      ctx.clip();
      hatch(CX - CARD_W / 2 + 3, ASK_CY - CARD_H / 2 + 3, CARD_W - 6, CARD_H - 6, 15, aA * 0.16);
      ctx.restore();
      ctext(spec.askCard, CX, ASK_CY + 5, 800, 14 * sc, tone('ink'), CARD_W - 20, aA);
      ctext(spec.firstLabel, CX, FIRST_Y, 900, 13, tone('accent'), 84, aA);

      /* 아래 칸 — 새로 꽂히는 「내 집 짓기」.
         오른쪽 끝을 붙박아 두고 **폭이 자라며** 자리를 채운다(밖으로 안 나간다). */
      if (grow > 0.02) {
        const bw = lerp(28, CARD_W, grow) * sc;
        const ox = CARD_R - bw / 2;
        const oA = grow * cardA;
        ctx.save();
        ctx.globalAlpha = oA;
        setShadow(ctx, GLOW, 10);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        roundRect(ctx, ox - bw / 2, OWN_CY - CARD_H * sc / 2, bw, CARD_H * sc, 8);
        ctx.stroke();
        clearShadow(ctx);
        ctx.restore();
        ctext(spec.ownCard, ox, OWN_CY + 5, 900, 14, tone('accent'), bw - 20, oA * grow);
      }
    }

    /* ── 조건표 : 「목수만」이 모서리에 철컥 걸린다 ────
       x 는 고정이다 — 카드와 함께 밀리게 하면 알파가 남은 채 오른쪽 끝에 걸린다. */
    if (tagA > 0.004) {
      const pulse = 8 + 4 * Math.sin(t * 2.8);
      const tx = w * 0.84;
      const ty = lerp(TAG_CY - 12, TAG_CY, lock);
      ctx.save();
      ctx.globalAlpha = tagA;
      setShadow(ctx, GLOW, pulse);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, tx - TAG_W / 2, ty - TAG_H / 2, TAG_W, TAG_H, 6);
      ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
      ctext(spec.lockLabel, tx, ty + 4, 800, 12, tone('accent'), TAG_W - 10, tagA);
    }

    /* ── 자리와 집 ──────────────────────────────────
       0번은 목수 자신(Phase A 에서 처음으로 집을 얻는다).
       1번부터는 Phase B 에서만 나타난다 — 조건을 떼면 전원이 직접 짓는다. */
    for (let i = 0; i < n; i++) {
      const a = i === 0 ? st : spread;
      if (a <= 0.004) continue;
      const cx = mxs[i];

      drawHouse(cx, i === 0 ? rise : spread, a);

      ctx.save();
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, cx - MARK_W / 2, MARK_CY - MARK_H / 2, MARK_W, MARK_H, 7);
      ctx.stroke();
      ctx.restore();

      const face = i === 0
        ? (rise > 0.55 ? spec.happyFace : spec.selfFace)
        : spec.happyFace;
      ctext(face, cx, MARK_CY + 5, 800, 14, tone('ink'), MARK_W - 12, a);
    }

    ctext(spec.selfLabel, mxs[0], SELF_LABEL_Y, 900, 14, tone('accent'), 92, st * selfLabA);

    ctx.textAlign = 'left';
  }
};
