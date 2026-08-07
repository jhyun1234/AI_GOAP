import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* squeezed — 여섯 갈래를 한 줄 높이로 눌러, 그 줄에 드는 둘만 남긴다.

   원문: "주민을 클릭하면 뜨는 정보줄에 '단짝'과 '원한'을 한 명씩만 표시했습니다.
   관계 전체 목록을 띄우고 싶은 유혹도 있었지만, 정보줄은 한 줄이라 욕심을 접었습니다.
   가장 친한 한 명, 가장 미운 한 명. 그거면 '쟤네 사이가 저렇구나'를 3초 안에 읽기에
   충분했습니다."

   🔴 남는 방식이 **솎아냄이 아니라 눌림**이다. ep09s-2 `onepair` 는 후보들이 스스로
   점으로 주저앉았지만, 여기서는 위아래에서 들어온 두 경계선이 **한 줄 높이만 남기고
   나머지를 지운다.** 원문이 말하는 것이 "전체 목록을 띄우고 싶었지만 정보줄은 한 줄"
   이라는 물리적 제약이라, 사라지는 이유가 화면의 높이여야 맞다.

   🔴 ep09s-1 `infoline` 과 갈라 놓은 자리: 그쪽은 화면 프레임 안의 가로 정보줄이
   NAME→TRAIT→JOB 순서로 **차오르고** 아래 코드 칸에서 줄이 올라와 이었다. 여기 한 줄은
   차오르지 않고 **눌려서 생기며**, 안에 든 것은 칸이 아니라 이 회차 내내 써 온
   **굽은 줄**이고, 프레임도 연결선도 밑줄도 없다. 대신 `3s` 훑개가 되풀이 지나간다.

   🔴 화면에 적는 수는 `3s` 하나뿐이고 원문 "3초 안에"에서 왔다. 단짝·원한이 한 명씩
   이라는 사실은 **남은 갈래가 둘뿐인 것**으로 말한다 — 원문에 `1명`은 있으나 도형이
   이미 개수를 지고 있어 글자로 또 적지 않았다.

   움직임 두 층:
     ① t — 남은 줄의 대시가 흐르고, `3s` 훑개가 1.7초 주기로 폭 전체를 지나간다.
     ② cue — pickCue 안에서 눌림(0.10~0.35) → 두 라벨(0.35~0.63) → 훑개(0.78) 까지
        **전부 끝난다.** 마지막 자막(예고)에는 "줄이 내려앉고 위에 빈 줄이 열린다"만
        걸었다. 🔴 아웃트로 카드가 마지막 2.6초를 덮으므로 페이오프를 거기 두면 안 된다. */

const CY = 148;
const STRIP_Y = 110, STRIP_H = 76;
const HOME_R = 13;
const SLIDE = 44;                                  // 예고에서 한 줄이 내려앉는 거리
const TY = [64, 100, 132, 164, 196, 232];          // 뻗어 있던 갈래들의 끝 높이
const KEEP = [2, 3];                               // 한 줄 안에 드는 둘

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const midText = (txt, weight, px, cx, y, pad = 12) => {
      if (!txt) return;
      ctx.font = disp(weight, px);
      const tw = ctx.measureText(txt).width;
      ctx.textAlign = 'center';
      ctx.fillText(txt, clamp(cx, pad + tw / 2, w - pad - tw / 2), y);
      ctx.textAlign = 'left';
    };

    const X0 = 18, X1 = w - 18;
    const HX = X0 + 30;
    const AX0 = X0 + 46, AX2 = X1 - 44;

    const c0 = cue(spec.pickCue ?? 0, 0.25, 0.80);
    const nk = ease(cue(spec.nextCue ?? 1, 0.15, 0.30));
    const app = clamp(c0 * 6);
    if (app <= 0.02) return;

    const dy = SLIDE * nk;                                   // 예고에서 통째로 내려앉는다
    const sq = ease(clamp((c0 - 0.10) / 0.25));              // 눌림
    const bandTop = lerp(16, STRIP_Y, sq);
    const bandBot = lerp(290, STRIP_Y + STRIP_H, sq);
    const bk = ease(clamp((c0 - 0.35) / 0.13));              // BEST
    const gk = ease(clamp((c0 - 0.50) / 0.13));              // GRUDGE
    const scanK = ease(clamp((c0 - 0.78) / 0.10));           // 3s 훑개

    /* ── 갈래들 ─────────────────────────────────────── */
    const pathTo = (ty, off) => {
      const bow = ty < CY ? -22 : 22;
      const cyq = (CY + ty) / 2 + bow;
      ctx.beginPath();
      const N = 40;
      for (let i = 0; i <= N; i++) {
        const k = i / N, m = 1 - k;
        const x = m * m * AX0 + 2 * m * k * ((AX0 + AX2) / 2) + k * k * AX2;
        const y = m * m * CY + 2 * m * k * cyq + k * k * ty;
        if (i === 0) ctx.moveTo(x, y + off); else ctx.lineTo(x, y + off);
      }
    };

    ctx.lineCap = 'round';
    for (let i = 0; i < TY.length; i++) {
      const ty = TY[i];
      const keep = KEEP.includes(i);
      const vis = keep
        ? 1
        : clamp((ty - bandTop - 8) / 12) * clamp((bandBot - ty - 8) / 12);
      const a = app * vis * (keep ? lerp(0.55, 1, sq) : 0.55);
      if (a <= 0.02) continue;

      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('accent');
      ctx.lineWidth = keep ? lerp(4, 9, sq) : 4;
      ctx.setLineDash(keep ? [24, 10] : [18, 10]);
      ctx.lineDashOffset = -(t * 26 + i * 9) % 34;
      pathTo(ty, dy); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(AX2, ty + dy, keep ? 6 : 4.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }
    ctx.lineCap = 'butt';

    /* ── 좁혀 들어오는 두 경계선 ────────────────────── */
    const bandA = app * clamp(sq * 5) * clamp(1 - (sq - 0.86) / 0.14);
    if (bandA > 0.02) {
      ctx.globalAlpha = bandA * 0.8;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      for (const by of [bandTop, bandBot]) {
        ctx.beginPath(); ctx.moveTo(X0, by + dy); ctx.lineTo(X1, by + dy); ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }

    /* ── 남은 한 줄 ─────────────────────────────────── */
    const stripA = app * ease(clamp((sq - 0.72) / 0.28));
    if (stripA > 0.02) {
      ctx.globalAlpha = stripA * 0.9;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, X0, STRIP_Y + dy, X1 - X0, STRIP_H, 10); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 라벨 — 남은 두 갈래 끝에 하나씩 앉는다 ─────── */
    if (bk > 0.02) {
      ctx.globalAlpha = app * bk * 0.9;
      ctx.fillStyle = tone('sub');
      midText(spec.bestLabel ?? '', 800, 10.5, AX2, TY[KEEP[0]] - 10 + dy);
      ctx.globalAlpha = 1;
    }
    if (gk > 0.02) {
      ctx.globalAlpha = app * gk * 0.9;
      ctx.fillStyle = tone('sub');
      midText(spec.grudgeLabel ?? '', 800, 10.5, AX2, TY[KEEP[1]] + 20 + dy);
      ctx.globalAlpha = 1;
    }

    /* ── 그 주민 ────────────────────────────────────── */
    ctx.globalAlpha = app;
    setShadow(ctx, GLOW, 9, 0);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
    ctx.beginPath(); ctx.arc(HX, CY + dy, HOME_R, 0, Math.PI * 2); ctx.stroke();
    clearShadow(ctx);
    ctx.globalAlpha = 1;

    /* ── 3s 훑개 — 한 줄이 한눈에 읽힌다 ────────────── */
    if (scanK > 0.02) {
      const sx = lerp(X0 + 4, X1 - 4, frac(t / 1.7));
      ctx.globalAlpha = app * scanK * 0.9;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      ctx.beginPath();
      ctx.moveTo(sx, STRIP_Y + 2 + dy); ctx.lineTo(sx, STRIP_Y + STRIP_H - 2 + dy);
      ctx.stroke();
      ctx.fillStyle = tone('accent');
      midText(spec.readLabel ?? '', 900, 13, sx, STRIP_Y - 6 + dy);
      ctx.globalAlpha = 1;
    }

    /* ── 예고 — 그 줄이 내려앉고 위에 빈 줄이 하나 열린다 ──
       그리고 주민에게서 획이 하나 그 빈 줄로 올라간다(다음 편이 얹힐 자리). */
    if (nk > 0.02) {
      const NY = 100, NH = 42;
      ctx.globalAlpha = app * clamp(nk * 1.6) * 0.6;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.setLineDash([11, 9]);
      ctx.lineDashOffset = -(t * 15) % 20;
      roundRect(ctx, X0, NY, X1 - X0, NH, 10); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      ctx.globalAlpha = app * clamp(nk * 1.6);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      const y0 = CY + dy - HOME_R - 2;
      ctx.beginPath();
      ctx.moveTo(HX, y0); ctx.lineTo(HX, lerp(y0, NY + NH - 6, ease(nk)));
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
