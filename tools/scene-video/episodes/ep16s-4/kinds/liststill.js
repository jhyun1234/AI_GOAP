import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* liststill — 목록에서 하나가 빠지고 나머지가 제자리를 찾는데, 밖에 남겨 둔 것은 멀쩡하다.

   원문 근거(「10단계」 마지막 문장):
   > "산 사람의 관계 목록에서 죽은 사람이 지워지는 것은 그대로 옳습니다."
   이 편의 착지다. 🔑 **여기서 지워지는 것은 「잘못」이 아니라 「옳음」이다** — 그래서
   앞 두 샷과 달리 지우개도, 훑고 지나가는 것도, 효과음도 없다. 줄 하나가 **조용히 꺼지고**
   아래 줄이 그 자리로 **올라와 정돈된다.** 소리를 얹으면 판정이 사건처럼 들린다.

   🔑 앞 샷(keepshot)에서 떨어져 나온 강조색 한 장이 여기 그대로 다시 나온다 —
   라벨도 같은 「남은 사진」이다. 목록이 정리되는 내내 **그것만 한 번도 안 움직인다.**

   색 규약(회차 공통): **강조색 = 기록(과 그 복사본) / 흰색 = 칸 · 라벨.**

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다:
     · 첫 줄 glow 위끝 122 − 8 = 114 ↔ 칸 위 테두리 안쪽 105.5 → **8.5px**.
     · 마지막 줄 glow 바닥 174 + 12 + 8 = 194 ↔ 칸 아래 테두리 안쪽 204.5 → **10.5px**.
     · 줄 glow 좌우 88 / 264 ↔ 칸 테두리 안쪽 77.5 / 274.5 → **10.5px**.
     · 칸 아래 테두리 바깥 207.5 ↔ 사진 glow 위끝 226 − 1.5 − 8 = 216.5 → **9px**.
     · 라벨1 글립 바닥 95 ↔ 첫 줄 glow 위끝 114 → **19px**(라벨과 칸의 7.5px 는 의도된 쌍).
     · 라벨2 글립 꼭대기 278 ↔ 사진 glow 바닥 258 + 1.5 + 8 = 267.5 → **10.5px**.
   🔴 흰↔흰(서로 다른 것): 라벨1(y 80~95)과 라벨2(y 278~291)는 세로 **183px**.
     화면에 다른 흰 글자는 없다. 라벨1 ↔ 목록 칸은 **의도된 쌍**이다.

   세로 예산(캔버스 307): 최저 = 라벨2 글립 바닥 291. **16px** 남는다.
   가로(캔버스 352): 칸 76~276. 좌 76 · 우 76 남는다.

   ⏱ 자막이 한 줄이라 cue 인덱스가 하나뿐이다. 그래서 **같은 인덱스로 창을 둘 부른다**
      (4단 함정 메모의 팁 — `cue` 는 순수 함수라 이게 된다):
        · `c0 = cue(0, 0.15, 0.35)` — **좁다.** 페이오프(줄이 빠지고 올라옴)를 자막 앞쪽
          1/3 에서 끝낸다. 🔴 마지막 자막이 짧을 때 페이오프가 뒤로 밀리는 사고(ep08s-3)를
          설계로 막는 방식이다.
        · `cT = cue(0, 0.15, 0.95)` — **넓다.** 정돈된 뒤의 밝기 장식이 자막 끝까지 이어진다.
      하나를 앞뒤로 쪼개는 것보다 낫다 — 두 요구(빨리 끝내기 / 끝까지 채우기)가 반대라서다.
   계속 도는 것(30fps 기준): 칸 점선 흐름 38px/s → 1.3px/프레임 ·
      줄 셋의 어긋난 박자 밝기(3.4rad/s · 위상 차 0.8) · 사진의 아주 느린 밝기(1.6rad/s). */

const BOX_X0 = 76, BOX_X1 = 276, BOX_Y0 = 104, BOX_Y1 = 206;
const ROW_X0 = 96, ROW_X1 = 256, ROW_H = 12;
const ROW_Y = [122, 148, 174];
const PH_X0 = 140, PH_X1 = 212, PH_Y0 = 226, PH_Y1 = 258;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
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

    const dashRow = (x0, x1, y, seg, gap, off, alpha) => {
      const P = seg + gap, len = x1 - x0;
      const ph = ((off % P) + P) % P;
      ctx.save();
      ctx.globalAlpha = alpha; ctx.fillStyle = tone('ink');
      for (let s = -P + ph; s < len; s += P) {
        const a = Math.max(0, s), b = Math.min(len, s + seg);
        if (b > a) ctx.fillRect(x0 + a, y - 1.5, b - a, 3);
      }
      ctx.restore();
    };

    const dashCol = (y0, y1, x, seg, gap, off, alpha) => {
      const P = seg + gap, len = y1 - y0;
      const ph = ((off % P) + P) % P;
      ctx.save();
      ctx.globalAlpha = alpha; ctx.fillStyle = tone('ink');
      for (let s = -P + ph; s < len; s += P) {
        const a = Math.max(0, s), b = Math.min(len, s + seg);
        if (b > a) ctx.fillRect(x - 1.5, y0 + a, 3, b - a);
      }
      ctx.restore();
    };

    const st = ease(cue(spec.dropCue ?? 0, 0.15, 0.30));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const c0 = cue(spec.dropCue ?? 0, 0.15, 0.35);   // 좁은 창 — 페이오프
    const cT = cue(spec.dropCue ?? 0, 0.15, 0.95);   // 넓은 창 — 꼬리 장식

    const gone = ease(clamp(c0 / 0.45));             // 가운데 줄이 꺼진다
    const lift = ease(clamp((c0 - 0.35) / 0.55));    // 아래 줄이 그 자리로 올라온다

    /* ── 산 사람의 관계 목록 (흰 점선 칸) ── */
    const off = -t * 38;
    dashRow(BOX_X0, BOX_X1, BOX_Y0, 12, 9, off, st * 0.9);
    dashRow(BOX_X0, BOX_X1, BOX_Y1, 12, 9, -off, st * 0.9);
    dashCol(BOX_Y0, BOX_Y1, BOX_X0, 12, 9, -off, st * 0.9);
    dashCol(BOX_Y0, BOX_Y1, BOX_X1, 12, 9, off, st * 0.9);
    ctext(spec.listLabel, 176, 92, 800, 12.5, tone('sub'), 200, st);

    /* ── 줄 셋 (강조색) — 가운데가 빠지고 셋째가 올라온다 ── */
    for (let i = 0; i < 3; i++) {
      const a = i === 1 ? st * (1 - gone) : st;
      if (a <= 0.01) continue;
      const y = i === 2 ? lerp(ROW_Y[2], ROW_Y[1], lift) : ROW_Y[i];
      ctx.save();
      ctx.globalAlpha = a * (0.78 + 0.22 * (0.5 + 0.5 * Math.sin(t * 3.4 - i * 0.8))
                             * (0.6 + 0.4 * cT));
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(ROW_X0, y, ROW_X1 - ROW_X0, ROW_H);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 밖에 남겨 둔 한 장 (강조색) — 한 번도 안 움직인다 ── */
    ctx.save();
    ctx.globalAlpha = st * (0.88 + 0.12 * (0.5 + 0.5 * Math.sin(t * 1.6)));
    setShadow(ctx, GLOW, 8);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    ctx.strokeRect(PH_X0, PH_Y0, PH_X1 - PH_X0, PH_Y1 - PH_Y0);
    ctx.fillStyle = tone('accent');
    ctx.fillRect(PH_X0 + 12, PH_Y0 + 8, PH_X1 - PH_X0 - 24, 6);
    ctx.fillRect(PH_X0 + 12, PH_Y0 + 20, PH_X1 - PH_X0 - 24, 6);
    clearShadow(ctx);
    ctx.restore();

    ctext(spec.keepLabel, 176, 288, 800, 12.5, tone('sub'), 140, st);

    ctx.textAlign = 'left';
  }
};
