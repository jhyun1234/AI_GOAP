import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 회차 기능의 0.5초 테스트 장면을 루프로 돌린다.

   🔴 재료는 이 글이 아니라 **다음 회차 원문(M14 「가을과 겨울」)**에서 직접 뽑았다.
   근거 문장 — `tools/blog-automation/published/2026-08-06-unity-goap-m14-autumn-winter.html`:
   > "게으름뱅이 주민이 자기 집도 있고, 집 옆에 자기 밭도 있는데, 그 밭에 아무것도 안 심습니다.
   >  그러다 겨울이 오고, 곳간이 비고, 굶어 죽습니다. 저는 이걸 '준비된 자의 아사'라고 부르며
   >  수수께끼로 남겨 뒀습니다."
   > "Play로 확인한 결과는 정확히 의도대로였습니다. 식량이 남아 있는데도 밭에 나가 심는 주민이
   >  로그에 찍혔고 … 그리고 게으름뱅이는 집도 밭도 있는데 여전히 안 심고 겨울에 굶어 죽었습니다."
   → 화면에서 움직이는 것 = **같은 순간에 어떤 주민은 밭으로 가고 어떤 주민은 안 간다.**

   🔴 이 글(M13) 끝의 「다음 개발일지에서는 …」 문단은 **블로그의 다음 글** 예고라 근거로
   쓰지 않았다(ep02s·ep11s·ep12s 가 그 형태로 세 번 틀렸다). 이번 건은 그 문단이 **우연히
   같은 방향**을 가리켜서 더 위험했다 — 그래서 정본을 M14 원문 파일 쪽으로 못박는다.

   🔴 `ep17s` 는 아직 분할 전이라 **밀스톤 층위**로만 그렸다. 막(act)은 한 마디도 안 썼다.
   🔴 예고 음성 · `outro.next` 카드 · 이 미리보기 셋이 같은 것을 가리킨다:
      **집도 밭도 있는데 밭에 아무것도 안 심는 주민.**

   0.5초 루프 : 왼쪽 표식이 자기 밭 칸 위로 걸어가고 그 칸이 아래에서부터 강조색으로 차오른다.
   오른쪽 표식은 자기 밭 옆에 그대로 서 있고 그 칸은 끝까지 빈다. → 되감긴다.

   🔴 **수를 글자로 안 적었다.** M14 원문이 허용하는 수(3일치 · 겨울 3번 · Day 44 · 최대 8명 ·
   넷/넷)가 있지만, 올리면 그 수가 이 편의 사건인 것처럼 읽힌다. 미리보기가 지는 것은 **동작**이다.

   🔴 본문의 도형(집·밭 한 쌍 · 판 틀 · 물음표)을 통째로 끌고 오지 않았다 — 여기 남는 것은
   **밭 칸 둘**뿐이고, 이 편이 「갖췄는데 왜」였다면 다음 편은 「갖췄는데 안 한다」라 축이 다르다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **가로 + 세로 두 겹으로 갈랐다.**
     가로: 강조색 채움 x 62~122 · 글로우(blur 6) **56~128** / 왼쪽 칸 획의 안쪽 면
       x **45.5**·**138.5** → 양쪽 **10.5px**.
     세로: 채움 y 164~186 · 글로우 **158~192** / 칸 획 안쪽 면 **147.5**·**200.5** →
       **10.5px** · **8.5px**. 🔴 **이 회차에서 가장 좁은 자리(8.5px)** 다.
     흰 왼쪽 표식 바닥 **120.5** 와 채움 글로우 꼭대기 158 은 37.5px.
   흰 글자끼리(축 B): 흰 글자가 「다음 편」 하나뿐이라 성립하지 않는다.

   ⏱ 등장은 예고 자막 `cue(0)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.
      0.5초 루프는 예고 자막 구간(겨냥 약 4.1초)에서 **최소 8바퀴** 돈다.

   세로 예산(캔버스 307): 최저 = 밭 칸 아래 획 바깥 203.5. 103px 남는다.
   가로: 오른쪽 칸 오른 획 바깥 309.5. 좌 42.5 · 우 42.5 남는다.

   계속 도는 것(30fps 기준):
     · 밭 칸 둘의 둘레 2×2×(96+56) = **608px** 이 숨을 쉰다.
       `bre = 1.2·(1 + sin(10.4 t))` → 진폭 1.2 · ω 10.4 → **0.416px/프레임** → 약 **253px²**
     · 왼쪽 표식이 36px 를 0.175초에 → **6.9px/프레임**
     · 채움이 22px 를 0.15초에 → 4.9px/프레임 × 폭 60 */

const LP = [44, 146, 96, 56];
const RP = [212, 146, 96, 56];
const FILL_X = 62, FILL_W = 60, FILL_BOT = 186, FILL_H = 22;
const MARK_Y = 104;

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

    const bre = 1.2 * (1 + Math.sin(t * 10.4));

    /* ── 밭 칸 둘 (흰색) ─────────────────────────── */
    for (const p of [LP, RP]) {
      ctx.save();
      ctx.globalAlpha = pk;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, p[0] + bre, p[1] + bre, p[2] - bre * 2, p[3] - bre * 2, 6);
      ctx.stroke();
      ctx.restore();
    }

    const u = frac(t / (spec.loopSec ?? 0.5));
    const vis = ease(clamp(u / 0.06)) * (1 - ease(clamp((u - 0.88) / 0.12)));
    const walk = ease(clamp((u - 0.05) / 0.35));
    const plant = ease(clamp((u - 0.42) / 0.30));

    /* ── 왼쪽 : 밭으로 나간다 → 칸이 찬다 ─────────── */
    const mark = (x, alpha) => {
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.translate(x, MARK_Y);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, -6, -15, 12, 30, 6);
      ctx.stroke();
      ctx.restore();
    };
    mark(lerp(56, 92, walk), pk * vis);

    const fh = FILL_H * plant;
    if (fh > 0.5) {
      ctx.save();
      ctx.globalAlpha = pk * vis;
      setShadow(ctx, GLOW, 6);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(FILL_X, FILL_BOT - fh, FILL_W, fh);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 오른쪽 : 그대로 서 있고, 칸은 끝까지 빈다 ── */
    mark(224, pk);

    ctx.textAlign = 'left';
  }
};
