import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 회차 기능의 0.5초 테스트 장면을 루프로 돌린다.

   🔴 재료는 이 글이 아니라 **다음 회차 원문(M13 「사건과 흔적」)**에서 직접 뽑았다.
   근거 문장 — `tools/blog-automation/published/2026-08-04-unity-goap-m13-events-and-traces.html`:
   > "죽은 자리에는 회색 원이 하나씩 남습니다. 무덤입니다. 그런데 그 원에는 아무것도 안
   >  적혀 있습니다. 화면을 가득 채운 회색 원 일곱 개를 보면서, 저는 그중 누가 제가 아침에
   >  지켜보던 그 주민인지 알 수 없었습니다."
   > "1단계 — 무덤에 이름을 새겼다 (A). 무덤 표기를 \"{shortName} · Day {day}\" 형식으로 바꿨습니다."

   🔴 이 글(M12) 끝의 「다음 개발일지에서는 …」 문단은 **블로그의 다음 글** 예고라 근거로
   쓰지 않았다(ep02s·ep11s·ep12s 가 그 형태로 세 번 틀렸다). 이번 건은 그 문단이 **우연히
   같은 방향**을 가리켜서 더 위험했다 — 그래서 정본을 M13 원문 쪽으로 못박는다.

   🔴 `ep16s` 는 아직 분할 전이라 **밀스톤 층위**로만 그렸다. 막(act)은 한 마디도 안 썼다.
   🔴 예고 음성 · `outro.next` 카드 · 이 미리보기 셋이 같은 것을 가리킨다:
      **무덤에 이름이 없던 마을 이야기.**

   0.5초 루프 : 아무것도 안 적힌 흰 동그라미 셋이 놓여 있고 → 동그라미마다 그 아래에
   **강조색 글자 줄이 하나씩 켜진다**(새겨진다) → 꺼지고 되감긴다.

   🔴 **이름과 날짜를 글자로 안 적었다.** 원문 형식은 `{shortName} · Day {day}` 인데,
   실제 이름이나 날짜 값은 원문에 없다 — 지어내면 없는 수치가 된다. 그래서 새겨지는 것을
   **글자 줄(막대)** 로만 그린다. 라벨을 다 가려도 「빈 것에 무언가가 새겨진다」가 읽힌다.

   🔴 본문의 도형(가득 찬 계단 · 두 칸 대비)을 여기로 안 끌고 왔다 — 다음 회차의 사건은
   「안쪽이 가득 찼다」가 아니라 「남은 흔적에 이름이 없다」라 축이 다르다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세로 좌표로 갈랐다.**
     동그라미(ink) 바닥 = 150 + 22(숨을 다 먹은 반지름) + 1.5(선 반폭) = **173.5**.
     글자 줄(accent)은 y 187~193 이고 글로우(blur 8)까지 179. **5.5px** 뜬다.
     (1차본 주석은 선 반폭을 안 세서 「7px」이라고 적었다 — 검수 재계산값이 맞다.)

   ⏱ 등장은 예고 자막 `cue(0)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.

   세로 예산(캔버스 307): 최저 = 글자 줄 바닥 193 + 글로우 8 = 201. 106px 남는다.
   가로: 74~278. 캔버스 352, 좌 74 · 우 74 남는다.

   계속 도는 것(30fps 기준): 0.5초 루프 + 동그라미 반지름 숨 진폭 2px · 9rad/s →
   **0.6px/프레임** × 둘레 126px × 셋. */

const CXS = [96, 176, 256];
const CY = 150, BAR_Y = 187, BAR_W = 44, BAR_H = 6;

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

    for (let i = 0; i < CXS.length; i++) {
      /* 빈 무덤 — 아무것도 안 적혀 있다 */
      const r = 20 + 2 * (0.5 + 0.5 * Math.sin(t * 9 + i * 1.2));
      ctx.save();
      ctx.globalAlpha = pk;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(CXS[i], CY, r, 0, Math.PI * 2); ctx.stroke();
      ctx.restore();

      /* 새겨지는 글자 줄 */
      const on = ease(clamp((u - (0.32 + i * 0.11)) / 0.09))
               * (1 - ease(clamp((u - 0.90) / 0.08)));
      if (on * pk <= 0.01) continue;
      ctx.save();
      ctx.globalAlpha = pk * on;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(CXS[i] - BAR_W / 2, BAR_Y, BAR_W, BAR_H);
      clearShadow(ctx);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
