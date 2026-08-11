import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 회차(`ep16s-3`)에서 화면에 실제로
   벌어지는 일을 0.5초 루프로 돌린다.

   🔴 재료는 **다음 편이 맡은 대목**에서 직접 뽑았다(같은 글 8단계):
   > "만들어 놓고 실제 화면을 보니 문제가 있었습니다. 회고 명부의 각 줄 아래에 연대기를
   >  병기했더니, 게으름뱅이 한 명의 \"명령 거부(피로)\" 아홉 연발이 명부를 통째로
   >  덮었습니다. 기록을 다 보여주는 게 답이 아니었던 겁니다."

   0.5초 루프 : 흰 줄 다섯이 나란히 서 있다가 → 그중 **한 줄만 강조색으로 바뀌고**,
   그 줄 바로 아래에서 강조색 덩어리가 불어나 → 아래 줄들을 화면 밖으로 밀어내고
   자리를 다 차지한다 → 흐려지고 되감긴다.
   라벨을 다 가려도 「여러 줄이 있었는데 한 줄에서 나온 것이 불어나 나머지를 밀어냈다」가 읽힌다.

   🔴 **수를 글자로도 형태로도 주장하지 않는다.** 덮는 것을 낱개 막대로 그리면 그 개수가
   곧 「몇 연발」이라는 수치 주장이 되므로(브리프 §6 이 그 수를 `ep16s-3` 소유로 못박았다),
   **개수를 셀 수 없는 한 덩어리**로 자라게 했다. 줄 다섯은 명부에 사람이 여럿이라는
   뜻이지 마을 인구 수가 아니다 — 원문에 인원 수가 없으므로 라벨도 안 붙였다.
   🔴 본문의 도형(칸 열넷 · 안 사라지는 줄 · 이름 줄과 네모)을 하나도 안 끌고 왔다.
   다음 편의 사건은 「안 보인다」가 아니라 「너무 많이 보인다」라 축이 반대다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세로 좌표로 갈랐다.**
     · 흰 줄(0번) 바닥 103 ↔ 강조색 줄(1번) 맨 위 118, 글로우(blur 8)까지 110. **7px**.
     · 강조색 덩어리 바닥(최대) 204, 글로우까지 212 ↔ 밀려난 흰 줄의 맨 위 =
       덩어리 바닥 + 18 = 222. **10px** 뜬다(밀려나는 동안 간격이 상수로 유지된다).
   흰↔흰(축 B): 「다음 편」 라벨(좌 x20, baseline 26) ↔ 첫 줄 맨 위 96 = 70px.
     줄끼리 세로 간격 22, 줄 높이 7 → **15px**(같은 명부의 줄들이라 의도한 무리다).

   세로 예산(캔버스 307): 최저 = 밀려나는 셋째 줄이 알파 0 이 되기 직전의 바닥 ≈ 258.
   49px 남는다. 가로: 84~268. 캔버스 352, 좌 84 · 우 84 남는다.

   ⏱ 등장은 예고 자막 `cue(0)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.

   계속 도는 것(30fps 기준): 0.5초 루프. 덩어리가 74px 를 0.2초에 자란다 → **12px/프레임**
   (폭 184px). 밀려나는 줄 셋이 같은 속도로 내려간다. */

const ROW_X = 92, ROW_W = 168, ROW_H = 7;
const ROW_Y = [96, 118, 140, 162, 184];
const HOT = 1;
const BLK = { x: 84, w: 184, top: 130, max: 74 };

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    if (spec.label) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 20, 26);
      ctx.restore();
    }

    const pk = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (pk <= 0.02) { ctx.textAlign = 'left'; return; }

    const u = frac(t / (spec.loopSec ?? 0.5));
    const fade = ease(clamp(u / 0.06)) * (1 - ease(clamp((u - 0.90) / 0.09)));
    const hot = ease(clamp((u - 0.16) / 0.10));
    const g = ease(clamp((u - 0.24) / 0.40));

    const blkH = BLK.max * g;
    const blkBot = BLK.top + blkH;
    const push = Math.max(0, blkBot + 18 - ROW_Y[2]);

    /* ── 명부의 줄들 ────────────────────────────────── */
    for (let i = 0; i < ROW_Y.length; i++) {
      const isHot = i === HOT;
      const y = i >= 2 ? ROW_Y[i] + push : ROW_Y[i];
      const a = i >= 2 ? fade * pk * clamp(1 - 1.25 * g) : fade * pk;
      if (a <= 0.01) continue;

      /* 🔴 강조색으로 바뀌는 줄은 **한 프레임에 갈아탄다**(교차 페이드 금지).
         같은 좌표에 흰색과 강조색을 겹쳐 흐리면 그 픽셀이 두 색의 혼합이 되고,
         그건 이 트랙에서 팔레트 위반을 만드는 대표적인 자리다. */
      ctx.save();
      ctx.globalAlpha = a;
      if (isHot && hot >= 0.5) {
        setShadow(ctx, GLOW, 8);
        ctx.fillStyle = tone('accent');
        roundRect(ctx, ROW_X, y, ROW_W, ROW_H, 3.5); ctx.fill();
        clearShadow(ctx);
      } else {
        ctx.fillStyle = tone('ink');
        roundRect(ctx, ROW_X, y, ROW_W, ROW_H, 3.5); ctx.fill();
      }
      ctx.restore();
    }

    /* ── 한 사람에게서 불어나 나머지를 덮는 것 ──────── */
    if (blkH > 1) {
      ctx.save();
      ctx.globalAlpha = fade * pk;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      roundRect(ctx, BLK.x, BLK.top, BLK.w, blkH, 8); ctx.fill();
      clearShadow(ctx);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
