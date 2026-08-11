/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다. */
import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 편에서 다룰 장면을 0.5초 루프로 돌린다.

   🔴 재료는 **같은 글의 다음 편(`ep15s-3`)이 맡은 사건**의 원문 문장에서 직접 뽑았다.
   근거 — `tools/blog-automation/published/2026-08-02-unity-goap-m12-trait-vector.html`:
   > "떠돌이 성격의 목수가 기지에서 38타일 밖에 집을 짓습니다."
   > "그런데 부탁이 성립하려면 6타일 안에서 마주쳐야 합니다. 결과는 집 부탁이 영원히
   >  성립하지 않고, 집 없는 주민이 굶어 죽는 것이었습니다."
   ⚠️ 같은 글 안의 다음 편이므로 **막(act)은 안 바뀐다.** 막 전환을 한 마디도 안 썼다.
   🔴 예고 음성 · `outro.next` 카드 · 이 미리보기 **셋이 같은 것을 가리킨다** —
      「목수가 이사 가자 아무도 집을 못 얻는다.」

   0.5초 루프 : 흰 고리(마을) 안에 있던 **강조색 조각(목수)이 고리의 트인 쪽으로 빠져
   나가 멀리 선다** → 마을에서 흰 선(부탁)이 그쪽으로 뻗다가 → **가운데가 벌어져 끊긴다**
   → 꺼지고 되감긴다.

   🖼 라벨을 다 가려도 읽힌다 — 「안에 있던 것 하나가 밖으로 나가고, 그쪽으로 뻗던 줄이
     가운데서 끊어진다.」

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **각도 + 시간 두 겹으로 갈랐다.**
     ⓐ **각도** : 조각이 고리를 통과하므로 고리를 **오른쪽 ±34°를 비운 호**로 그린다.
        호 끝점은 (158.1, 124.28) 과 (158.1, 175.72) 이고 **둥근 캡(lineWidth/2 = 1.5)까지**
        넣으면 y 122.78~125.78 · 174.22~177.22 다. 조각(획 바깥 y 138.5~161.5 · 글로우 blur 5
        까지 **133.5~166.5**) 과 **위아래 똑같이 7.72px** 뜬다(1차 주석의 「위로 6.2px」는
        캡을 안 넣고 잰 값이라 틀렸다). 고리의 나머지 부분은 조각의 이동선(y 150)에서
        20px 이상 떨어진다.
     ⓑ **시간** : 흰 부탁 선(y 148.5~151.5 · x 172~258)은 위상 **0.44** 부터 켜지는데
        조각은 위상 **0.42** 에 이미 오른쪽 끝(중심 286)에 서 있다. 그때 조각의 왼쪽 한계는
        획 바깥 272.5 · **글로우(blur 5)까지 267.5** 이므로 선 오른쪽 끝 258 과 **9.5px** 뜬다.
     ⓒ 마을 안 흰 점 셋(반지름 5 · 획 3 → **바깥 반지름 6.5**)은 전부 조각 글로우 띠
        (y 133.5~166.5) 밖이다 — (96,122)·(98,180) 이 각각 **5px** · **7px**, (132,180) 이 7px.
   ⚠️ 위 세 수는 **2차에서 정정한 값**이다. 1차 주석은 269 / 11px / 6px·8px 이라 전부
     1.5px 씩 낙관적이었다 — **`lineWidth/2` 를 빼먹고 글립 경로만 재서** 났다. 값 자체는
     안전했지만 낙관치가 문서에 남으면 다음 사람이 그 여유를 쓴다.

   🔴 화면에 수를 한 개도 안 올렸다. 「38타일」·「6타일」은 다음 편의 몫이다.
   ⏱ 등장은 예고 자막 `cue(0)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.

   세로 예산(캔버스 307) : 최고 = 고리 위 102.5 · 최저 = 고리 아래 197.5. 109px 남는다.
   가로(캔버스 352) : 왼쪽 = 고리 72.5 · 오른쪽 = 조각 글로우 **304.5**. 47.5px 남는다. */

const VXc = 120, VYc = 150, VR = 46;      // 마을 고리
const GAPD = 34;                          // 트인 각도(±)
const CHIP_X0 = 120, CHIP_X1 = 286;       // 조각이 나가는 거리
const LINE_X0 = 172, LINE_X1 = 258;       // 부탁 선
const BREAK_CX = 223, BREAK_HW = 17;      // 끊기는 자리
const DOTS = [[96, 122], [98, 180], [132, 180]];

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

    const u = frac(t / (spec.loopSec ?? 0.5));
    const slide = ease(clamp(u / 0.42));
    const lineA = ease(clamp((u - 0.44) / 0.10)) * (1 - ease(clamp((u - 0.86) / 0.10)));
    const gap = ease(clamp((u - 0.62) / 0.16));
    /* 🔴 조각의 알파도 **양끝을 다 0 으로** 만든다. 안 그러면 위상이 1 → 0 으로 감길 때
       조각이 286 에서 120 으로 튀는 게 그대로 보여 반 초마다 '팝' 이 난다. */
    const chipA = ease(clamp(u / 0.06)) * (1 - ease(clamp((u - 0.90) / 0.10)));

    /* ── 마을 고리(흰) : 오른쪽이 트여 있다 ── */
    ctx.save();
    ctx.globalAlpha = pk;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineCap = 'round';
    ctx.beginPath();
    ctx.arc(VXc, VYc, VR, GAPD * Math.PI / 180, (360 - GAPD) * Math.PI / 180);
    ctx.stroke();
    for (const [dx, dy] of DOTS) {
      ctx.beginPath(); ctx.arc(dx, dy, 5, 0, Math.PI * 2); ctx.stroke();
    }
    ctx.restore();

    /* ── 부탁 선(흰) : 뻗다가 가운데가 벌어진다 ── */
    if (lineA > 0.01) {
      const hw = BREAK_HW * gap;
      ctx.save();
      ctx.globalAlpha = pk * lineA;
      ctx.fillStyle = tone('ink');
      const a1 = BREAK_CX - hw, b1 = BREAK_CX + hw;
      if (a1 > LINE_X0) ctx.fillRect(LINE_X0, 148.5, a1 - LINE_X0, 3);
      if (LINE_X1 > b1) ctx.fillRect(b1, 148.5, LINE_X1 - b1, 3);
      ctx.restore();
    }

    /* ── 목수 조각(강조색) : 고리 밖으로 미끄러져 나간다 ── */
    const cx = lerp(CHIP_X0, CHIP_X1, slide);
    if (chipA <= 0.01) { ctx.textAlign = 'left'; return; }
    ctx.save();
    ctx.globalAlpha = pk * chipA;
    setShadow(ctx, GLOW, 5);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, cx - 12, VYc - 10, 24, 20, 4); ctx.stroke();
    clearShadow(ctx);
    ctx.restore();

    ctx.textAlign = 'left';
  }
};
