import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* trace — 결과는 같은데, 위에 남는 것이 다르다.

   원문: "완료 선언 이후에 되돌아가 편의 기능 하나를 포기하는 결정을 내리지 않았다면,
   M8은 '돌아가지만 아무 이유가 없는' 시스템으로 끝났을 겁니다."

   🔴 이 회차의 기본 도형이 여기서 **결론**으로 선다. 집 두 채가 나란히 서고,
   **둘은 완전히 똑같이 생겼다** — 돌아가긴 하니까. 다른 것은 그 위다.
   왼쪽 집 위는 빈 윤곽 셋이 점선으로만 남아 있고, 오른쪽 집 위에는 주민 · 요청 · 목수가
   실제로 채워진 채 집까지 선으로 이어진다. 그 사슬이 곧 「이유」다.

   🔴 왼쪽을 실패로 그리지 않았다. 왼쪽 집도 똑같이 빗금이 차 있다 —
   원문이 말하는 것은 '안 돌아간다'가 아니라 '돌아가는데 아무 이유가 없다'이기 때문이다.
   여기를 무너뜨리거나 비워 두면 원문과 다른 말이 된다.

   🔴 사슬은 **위에서 아래로** 채워진다(주민 → 요청 → 목수 → 집). 원인의 순서다.

   🟢 **2026-08-08 4단 구조 개정 — 아래쪽 「삯이 목수를 쫓는」 트랙을 통째로 뺐다.**
   그건 다음 편 예고였고, 예고는 이제 아웃트로 샷(`nextpeek`)이 진다. 그러면서 이 샷은
   **자막 한 줄짜리 판정 샷**이 됐고, 아웃트로 카드도 더는 이 그림을 안 덮는다
   (카드는 마지막 샷 위에 뜬다). 그래서 페이오프를 뒤로 밀 걱정 없이 c0 안에서 끝난다 —
   집 두 채가 c0 0.05~0.31, 사슬이 0.30~0.70 이다.
   🔴 예고를 여기 남겨 두면 **같은 것을 두 그림이 말하게 된다**(nextpeek 과 겹친다).
   두 곳에 적으면 갈린다는 원칙 그대로다.

   🔴 화면에 숫자가 없다. 집이 둘인 것은 대조를 위한 배치이고 셀 값이 아니다.

   계속 도는 것 = 오른쪽 사슬을 타고 내려가는 강조색 하이라이트 · 왼쪽 빈 윤곽의 점선 ·
   두 집의 빗금. */

const SLOT = { vill: 122, cap: 158, carp: 196 };
const HOUSE = { cy: 236, w: 46, h: 38 };
const LXP = 96, RXP = 256;

function hatch(ctx, x, y, bw, bh, k, t, color) {
  if (k <= 0.004) return;
  const fh = bh * k, fy = y + bh - fh;
  ctx.save();
  ctx.beginPath(); ctx.rect(x, fy, bw, fh); ctx.clip();
  ctx.strokeStyle = color; ctx.lineWidth = 3;
  const step = 13, off = (t * 11) % step;
  for (let d = -bh - step; d < bw + step; d += step) {
    ctx.beginPath();
    ctx.moveTo(x + d + off, y + bh);
    ctx.lineTo(x + d + off + bh, y);
    ctx.stroke();
  }
  ctx.restore();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const lx = w * (LXP / 352), rx = w * (RXP / 352);
    const hTop = HOUSE.cy - HOUSE.h / 2;

    const c0 = cue(spec.traceCue ?? 0, 0.15, 0.75);

    const house = ease(clamp((c0 - 0.05) / 0.26));
    const chain = ease(clamp((c0 - 0.30) / 0.40));

    /* ── 라벨 ─────────────────────────────────────── */
    const la = clamp(c0 * 5);
    if (la > 0.02) {
      ctx.globalAlpha = la;
      ctx.textAlign = 'center';
      if (spec.autoLabel) {
        ctx.font = disp(800, fit(spec.autoLabel, 800, 13, 130, 9));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.autoLabel, lx, 92);
      }
      if (spec.askLabel) {
        ctx.font = disp(900, fit(spec.askLabel, 900, 13, 130, 9));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.askLabel, rx, 92);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 왼쪽 — 위가 비어 있다 ─────────────────────── */
    if (la > 0.02) {
      ctx.globalAlpha = la * 0.9;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([6, 7]);
      ctx.lineDashOffset = (t * 12) % 13;
      ctx.beginPath();
      ctx.moveTo(lx, SLOT.vill); ctx.lineTo(lx, hTop);
      ctx.stroke();
      ctx.beginPath(); ctx.arc(lx, SLOT.vill, 7, 0, Math.PI * 2); ctx.stroke();
      ctx.beginPath(); ctx.arc(lx, SLOT.carp, 12, 0, Math.PI * 2); ctx.stroke();
      roundRect(ctx, lx - 33, SLOT.cap - 12, 66, 24, 12); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 오른쪽 — 사슬이 위에서부터 채워진다 ────────
       강조색 선을 먼저 긋고 흰 표식을 나중에 얹는다(겹치는 자리의 z 순서). */
    if (chain > 0.004) {
      const y1 = lerp(SLOT.vill, hTop, ease(clamp(chain / 0.92)));
      ctx.globalAlpha = clamp(chain * 4) * 0.5;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      ctx.beginPath(); ctx.moveTo(rx, SLOT.vill); ctx.lineTo(rx, y1); ctx.stroke();
      ctx.globalAlpha = 1;

      /* 사슬을 타고 내려가는 하이라이트 — 🔴 흰색을 겹치지 않는다.
         반투명 흰색을 강조색 위에 얹으면 팔레트 밖의 옅은 민트가 나온다.
         밝기 차이만으로 충분하므로 같은 강조색의 알파로만 가른다. */
      const runY = SLOT.vill + ((t * 62) % (hTop - SLOT.vill + 40)) - 40;
      const s0 = Math.max(SLOT.vill, runY), s1 = Math.min(y1, runY + 34);
      if (s1 > s0) {
        ctx.globalAlpha = clamp(chain * 4);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
        ctx.beginPath(); ctx.moveTo(rx, s0); ctx.lineTo(rx, s1); ctx.stroke();
        ctx.globalAlpha = 1;
      }

      /* 요청 캡슐 — 강조색 */
      const ka = clamp((chain - 0.28) / 0.22);
      if (ka > 0.02 && spec.capsuleLabel) {
        ctx.globalAlpha = ka;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        roundRect(ctx, rx - 33, SLOT.cap - 12, 66, 24, 12); ctx.stroke();
        ctx.textAlign = 'center';
        ctx.font = disp(900, fit(spec.capsuleLabel, 900, 11, 54, 8));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.capsuleLabel, rx, SLOT.cap + 4);
        ctx.globalAlpha = 1;
      }

      /* 주민 · 목수 — 흰 것을 마지막에 */
      ctx.globalAlpha = clamp(chain * 5);
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(rx, SLOT.vill, 7, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;

      const ca = clamp((chain - 0.56) / 0.22);
      if (ca > 0.02) {
        ctx.globalAlpha = ca;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.arc(rx, SLOT.carp, 12, 0, Math.PI * 2); ctx.stroke();
        ctx.setLineDash([7, 6]);
        ctx.lineDashOffset = -(t * 16) % 13;
        ctx.beginPath(); ctx.arc(rx, SLOT.carp, 19, 0, Math.PI * 2); ctx.stroke();
        ctx.setLineDash([]); ctx.lineDashOffset = 0;
        ctx.globalAlpha = 1;
      }
    }

    /* ── 집 두 채 — 똑같이 생겼다 ─────────────────── */
    for (const cxh of [lx, rx]) {
      const x = cxh - HOUSE.w / 2;
      hatch(ctx, x, hTop, HOUSE.w, HOUSE.h, house, t, tone('ink'));
      ctx.globalAlpha = clamp(house * 2.4);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, x, hTop, HOUSE.w, HOUSE.h, 5); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
