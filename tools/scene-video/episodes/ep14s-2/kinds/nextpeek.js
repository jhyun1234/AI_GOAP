import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 편(14-3편) 사건의 0.5초 미리보기 루프.

   🔴 재료는 이 편의 대목이 아니라 **다음 편이 맡은 대목**에서 뽑았다. 같은 원문 안이지만
   8단계 절이다:
   > "목수는 마을에서 유일하게 집이 없는 주민이었습니다. 항상 그랬습니다."
   > "집이 생기는 경로는 '누군가가 목수에게 부탁하기' 하나뿐인데, 부탁 스캔은 자기 자신을
   >  대상에서 제외하고 상대가 목수 직업이기를 요구합니다. 마을에 목수가 한 명이면
   >  그가 부탁을 걸 상대가 세상에 존재하지 않습니다."
   🔴 원문 끝의 「다음 이야기 예고」 문단(성격 성향 벡터)은 **블로그의 다음 글** 예고라
      쓰지 않았다 — 이 계열로 ep02s·ep11s·ep12s 가 세 번 틀렸다.
   🔴 미리보기·예고 음성·`outro.next` 카드 셋이 같은 것을 가리킨다: **목수만 집이 없다.**

   **0.5초 루프** — 아래 표식 넷 위로 강조색 집이 왼쪽부터 하나씩 솟는데, 세 번째 표식
   위만 빈 터로 남는다. 루프가 몇 번을 돌아도 그 자리는 한 번도 안 채워진다.

   🔴 본문의 도형(칸·알·되튕김)을 여기로 끌고 오지 않았다 — 다음 편의 사건은 「자리가
   모자라다」가 아니라 「걸 상대가 없다」라 축이 다르다. 물려받은 문장은 색 규약 하나뿐이다:
   **흰색 = 지금 있는 것 / 강조색 = 다음 편이 바꾸는 것.**

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — 좌표로 갈랐다.
     · 강조색 집 : y 158~200 (글로우 148~210) · x 22~82 / 105~165 / 270~330.
     · 빈 터(흰색 45%) : x 197~237, y 186~200 → 좌우 강조색 글로우와 32px·33px 뜬다.
     · 흰 표식 : y 224~248 → 강조색 글로우 아래끝 210 과 14px 뜬다.
     · 흰 「목수」 : y 254~272.

   🔴 화면에 수를 한 개도 안 올렸다. 표식은 `spec.marks` 로만 있고 세는 라벨이 없다.
   🔴 캔버스 밖으로 안 나간다: 최소 x 22 · 최대 x 330 · 세로 최저 268 베이스라인(글립 ~272).

   ⏱ 등장은 예고 자막 `cue(0)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다. */

const MARK_Y = 236, MARK_R = 12;
const HOUSE_BOT = 200, HOUSE_EAVE = 176, HOUSE_TOP = 158, HOUSE_HW = 20;
const PLOT_T = 186;

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
      ctx.fillText(txt, clamp(x, 12 + tw / 2, w - 12 - tw / 2), y);
      ctx.restore();
    };

    if (spec.label) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 20, 26);
      ctx.restore();
    }

    const pk = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (pk <= 0.02) { ctx.textAlign = 'left'; return; }

    const n = Math.max(3, spec.marks ?? 4);
    const ci = Math.min(n - 1, Math.max(0, spec.carpIndex ?? 2));
    const mx = i => 52 + i * (w - 104) / (n - 1);

    const u = frac(t / (spec.loopSec ?? 0.5));
    const out = 1 - ease(clamp((u - 0.84) / 0.14));

    /* ── 남의 집은 하나씩 선다 ─────────────────────── */
    let built = 0;
    for (let i = 0; i < n; i++) {
      if (i === ci) continue;
      const rise = ease(clamp((u - (0.05 + 0.15 * built)) / 0.16));
      built++;
      const a = rise * out * pk;
      if (a <= 0.01) continue;
      const x = mx(i), dy = lerp(16, 0, rise);
      ctx.save();
      ctx.globalAlpha = a;
      setShadow(ctx, GLOW, 10);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, x - 16, HOUSE_EAVE + dy, 32, HOUSE_BOT - HOUSE_EAVE, 4); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(x - HOUSE_HW, HOUSE_EAVE + dy);
      ctx.lineTo(x, HOUSE_TOP + dy);
      ctx.lineTo(x + HOUSE_HW, HOUSE_EAVE + dy);
      ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 목수 자리는 빈 터로 남는다 ────────────────── */
    {
      const x = mx(ci);
      const a = pk * (0.40 + 0.25 * (0.5 + 0.5 * Math.sin(2 * Math.PI * u)));
      ctx.save();
      ctx.globalAlpha = a;
      ctx.fillStyle = tone('sub');
      /* 점선은 fillRect 조각으로 — 긴 setLineDash 가 30fps 3패스에서 갈린 전례가 있다 */
      for (let s = 0; s < 5; s++) {
        ctx.fillRect(x - 20 + s * 8.5, PLOT_T, 5, 3);
        ctx.fillRect(x - 20 + s * 8.5, HOUSE_BOT - 3, 5, 3);
      }
      ctx.fillRect(x - 20, PLOT_T, 3, HOUSE_BOT - PLOT_T);
      ctx.fillRect(x + 17, PLOT_T, 3, HOUSE_BOT - PLOT_T);
      ctx.restore();
    }

    /* ── 주민들 ───────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = pk;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    for (let i = 0; i < n; i++) {
      ctx.beginPath(); ctx.arc(mx(i), MARK_Y, MARK_R, 0, Math.PI * 2); ctx.stroke();
    }
    ctx.restore();

    ctext(spec.carpLabel, mx(ci), 268, 800, 13, tone('ink'), 76, pk);

    ctx.textAlign = 'left';
  }
};
