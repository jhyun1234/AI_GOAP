import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 회차 기능의 0.5초 테스트 장면을 루프로 돌린다.

   🔴 재료는 이 글이 아니라 **다음 회차 원문(M12 성격 축 · 성향 벡터)**에서 직접 뽑았다.
   이 글 끝의 「다음 이야기 예고」 문단은 **블로그의 다음 글** 예고라 근거로 쓰지 않았다
   (ep02s·ep11s·ep12s 가 그 형태로 세 번 틀렸다). 이번 건은 그 문단이 **우연히 같은 방향을
   가리켜서** 더 위험했다 — 그래서 정본을 M12 원문 쪽으로 못박는다.
   근거 문장 — `tools/blog-automation/published/2026-08-02-unity-goap-m12-trait-vector.html`:
   > "이름표와 말풍선 말투는 여섯 가지인데, 하루의 실제 일과는 겹칩니다. 나무를 하러 가는
   >  순서도, 밭에 가는 타이밍도, 여가를 즐기는 빈도도 여섯이 같습니다."
   > "결과는 성격 여섯 종이 열다섯 쌍 전부 다른 행동 구성으로 갈라진 것"

   🔴 `ep15s` 는 아직 분할 전이라 **밀스톤 층위**로만 그렸다. 막(act)은 한 마디도 안 썼다.
   🔴 예고 음성 · `outro.next` 카드 · 이 미리보기 셋이 같은 것을 가리킨다:
      **성격 여섯이 사실은 겹쳐 있었다는 이야기.**

   0.5초 루프 : 여섯 줄이 거의 한 줄로 겹쳐 있다가(흰색) → 꺼지고 → 같은 자리에서
   **여섯 갈래로 부채처럼 갈라진다**(강조색) → 꺼지고 되감긴다.

   🔴 본문의 도형(가운데 불 하나와 그 둘레의 여섯)을 여기로 끌고 오지 않았다 —
   다음 회차의 사건은 **집이 서는 자리**가 아니라 **하루가 갈라지는가**라 축이 다르다.
   그래서 색 규약도 물려받지 않는다: 본문에서는 강조색이 「불」이었지만 여기서는
   **흰색 = 지금 겹쳐 있는 것 / 강조색 = 다음 회차가 갈라 놓는 것**이다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **시간으로 갈랐다.** 두 무리가 같은 좌표를
   지나므로(부채가 겹친 상태에서 시작한다) 겹칠 수 있는데, 흰 다발은 위상 0.34 에 알파 0
   이고 강조색은 0.40 에야 켜진다. 되감을 때도 강조색이 0.92 에 완전히 꺼진 뒤 흰 다발이
   0.94 부터 돌아온다.

   🔴 화면에 수를 한 개도 안 올렸다. 여섯은 `spec.lanes` 로 자리만 있고 세는 라벨이 없다.

   ⏱ 등장은 예고 자막 `cue(0)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.

   세로 예산(캔버스 307): 최저 = 갈라진 맨 아래 줄 204. 103px 남는다.
   가로: 오른쪽 끝 300. 캔버스 352, 여유 52px. */

const X0 = 66, X1 = 300, MY = 150;
const OPEN = 21;              // 갈라졌을 때 이웃 줄 사이 간격
const BUNDLE = 0.14;          // 겹쳐 있을 때의 벌어짐 — 「거의 한 줄」

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
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

    const n = Math.max(2, spec.lanes ?? 6);
    const u = frac(t / (spec.loopSec ?? 0.5));

    /* 흰 다발(겹침) 과 강조색 부채(갈라짐) — 알파가 동시에 0 보다 큰 프레임이 없다 */
    const bundleA = (1 - ease(clamp((u - 0.26) / 0.08))) + ease(clamp((u - 0.94) / 0.06));
    const fanA = ease(clamp((u - 0.40) / 0.10)) * (1 - ease(clamp((u - 0.86) / 0.06)));
    const split = lerp(BUNDLE, 1, ease(clamp((u - 0.40) / 0.42)));

    const fan = (spread, color, alpha, glow) => {
      if (alpha <= 0.01) return;
      ctx.save();
      ctx.globalAlpha = alpha;
      if (glow) setShadow(ctx, GLOW, 9);
      ctx.strokeStyle = color; ctx.lineWidth = 3; ctx.lineCap = 'round';
      for (let i = 0; i < n; i++) {
        const off = (i - (n - 1) / 2) * OPEN * spread;
        ctx.beginPath();
        ctx.moveTo(X0, MY);
        ctx.lineTo(X1, MY + off);
        ctx.stroke();
      }
      if (glow) clearShadow(ctx);
      ctx.restore();
    };

    fan(BUNDLE, tone('ink'), pk * clamp(bundleA), false);
    fan(split, tone('accent'), pk * fanA, true);

    /* 왼쪽 매듭 — 여섯이 같은 하루에서 출발한다는 표시. 오른쪽 끝 x 53 이고 강조색
       부채의 글로우(blur 9)가 닿는 왼쪽 한계가 x 57 이라 4px 뜬다. */
    ctx.save();
    ctx.globalAlpha = pk;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(X0 - 22, MY, 9, 0, Math.PI * 2); ctx.stroke();
    ctx.restore();

    ctx.textAlign = 'left';
  }
};
