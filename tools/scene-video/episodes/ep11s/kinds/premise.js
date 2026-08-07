import {
  disp, ease, clamp, lerp, rnd,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* premise — 위층을 아무리 다시 조준해도, 금이 가는 건 아래층이다.

   원문: "이동 도중에 상대가 움직이면 경로를 다시 잡고, 상대에게 가장 가까운 걸을 수 있는
   칸을 다시 계산하는 식이었습니다." / "재조준을 아무리 빨리 해봐야, 상대는 그만큼 앞으로 가
   있으니까요." / "제가 고치려던 건 알고리즘이었는데, 진짜 원인은 한 겹 아래에 있었습니다."

   🔴 이 샷의 뼈대는 **한 겹 아래**라는 원문의 말 그대로다. 화면이 위아래 두 층이고,
   위층은 S1 과 같은 트랙(간격이 그대로)이며 아래층이 「쫓아가서 건네준다」라는 판이다.
   ① aimCue — 목수에서 조준선이 네 번 뻗는데 **매번 같은 자리에서 멈춘다.** 멈춘 자리에
      짧은 눈금이 하나씩 쌓여 넉 줄이 된다 — 다시 조준한 횟수는 눈에 보이되 숫자로 쓰지 않는다
      (원문에 시도 횟수가 없다). 조준선 끝과 의뢰인 사이는 끝까지 비어 있다.
   ② crackCue — 위층이 흐려지고, 아래 판이 좌우로 벌어진다. 벌어진 **검은 틈 안에서만**
      강조색 균열이 위에서 아래로 번진다. 판 위의 글자도 함께 찢어진다.

   🔴 균열을 판 위에 긋지 않는다. 강조색 선을 흰 판 위에 얹으면 두 색이 같은 픽셀에서 섞이고,
   그 혼합은 팔레트 게이트가 hue 152 로 읽어 그냥 통과시킨다 — 기계가 못 잡는 자리다.
   그래서 **틈이 먼저 열리고 그 틈 안에서만** 균열을 그린다(균열의 좌우 흔들림 폭을 틈 폭에
   비례시켜 어느 시점에도 판을 침범하지 않는다: jit = min(4.2, off × 0.42), lw 3 →
   최대 확장 off × 0.42 + 1.5 < off. 그리고 off < 3 이면 균열 알파가 0 이다).

   🔴 **2026-08-08 반려 수정 — 위 문단은 초안에서 「의도」였을 뿐 코드가 지키지 않았다.**
   두 가지가 동시에 틀렸다. ①클립 두 조각을 맞닿게 잡고 각각 ∓off 로 옮겨서 **원본 2×off 띠가
   두 번 그려졌다**(한글 한 글자 폭이 20px, off 10 → 정확히 한 글자 복제). ②그래서 화면에는
   **틈이 0px** 이었고 균열이 흰 글자를 세로로 훑었다. 검수가 판 윗변 y=190 행을 샘플해
   x 155~185 가 전부 R=255 임을 보고 잡았다 — 강조색이 흰색을 **불투명하게** 덮어 혼합
   픽셀이 안 생기므로 팔레트 게이트는 5개 시각 전부 통과시켰다.
   🔑 남길 교훈: **「겹치지 않게 설계했다」는 주석은 근거가 아니다.** 좌표를 실제로 풀어 봐야
   한다. 지금은 클립 경계를 ∓off 만큼 밀었고(아래 참조) 금 자리도 낱말 사이 공백으로 옮겼다.

   🔴 판 위 문자열은 한국어다. 「쫓아가서 건네준다」는 코드 식별자가 아니라 원문이 따옴표로
   묶어 부른 **서술**이라 영어로 옮기면 원문에 없는 말이 된다. 축 이름(ALGORITHM · PREMISE ·
   RETARGET)만 영어다.

   계속 도는 것 = 바닥 눈금 흐름 · 두 표식의 같은 위상 바운스 · 조준선 끝의 진행 ·
   틈이 열린 뒤 균열을 따라 흐르는 점선. */

const TRK_Y = 128;
const TK0 = 142, TK1 = 150;
const SLAB_Y = 190, SLAB_H = 78;
const AIMS = [0.10, 0.30, 0.50, 0.70];

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

    const carpX = w * 0.26, cliX = w * 0.68, stopX = cliX - 36;
    const c0 = cue(spec.aimCue ?? 0, 0.15, 0.75);
    const c1 = cue(spec.crackCue ?? 1, 0.15, 0.8);

    const appear = ease(clamp(c0 / 0.4));
    const upA = appear * (1 - 0.55 * ease(clamp((c1 - 0.10) / 0.45)));
    const bob = Math.sin(t * 7.4) * 3;

    /* ── 위층 : 알고리즘 ──────────────────────────── */
    if (upA > 0.02) {
      ctx.globalAlpha = upA;

      if (spec.algoLabel) {
        ctx.textAlign = 'left';
        ctx.font = disp(900, fit(spec.algoLabel, 900, 12, 130, 8));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.algoLabel, 22, 74);
      }

      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([14, 10]);
      ctx.lineDashOffset = (t * 34) % 24;
      ctx.beginPath(); ctx.moveTo(18, TRK_Y); ctx.lineTo(w - 18, TRK_Y); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      const step = 34, sl = (t * 32) % step;
      for (let i = -1; i * step + 18 - sl < w - 14; i++) {
        const x = 18 + i * step - sl;
        if (x < 16 || x > w - 16) continue;
        ctx.beginPath(); ctx.moveTo(x, TK0); ctx.lineTo(x - 6, TK1); ctx.stroke();
      }

      /* 다시 조준 — 네 번 뻗고 네 번 같은 자리에서 멈춘다 */
      for (let i = 0; i < AIMS.length; i++) {
        const f = AIMS[i];
        const run = ease(clamp((c0 - f) / 0.15));
        if (run <= 0.002) continue;
        const alive = clamp((c0 - f) * 9) * clamp((f + 0.34 - c0) * 5);
        const tip = lerp(carpX + 12, stopX, run);
        if (alive > 0.02) {
          ctx.globalAlpha = upA * alive;
          ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
          ctx.beginPath(); ctx.moveTo(carpX + 12, TRK_Y); ctx.lineTo(tip, TRK_Y); ctx.stroke();
          ctx.beginPath();
          ctx.moveTo(tip - 8, TRK_Y - 5); ctx.lineTo(tip, TRK_Y); ctx.lineTo(tip - 8, TRK_Y + 5);
          ctx.stroke();
          ctx.globalAlpha = upA;
        }
        /* 멈춘 자리에 눈금이 하나씩 쌓인다.
           🔴 문턱으로 켜지 않는다 — run 이 0.86 을 넘는 동안 알파로 올라온다.
           하드 컷이 없어야 seek 이 어느 시각으로 뛰어도 그림이 이어진다. */
        const mark = ease(clamp((run - 0.86) / 0.14));
        if (mark > 0.02) {
          ctx.globalAlpha = upA * mark * 0.5;
          ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
          const sx = stopX - i * 4;
          ctx.beginPath(); ctx.moveTo(sx, TRK_Y - 11); ctx.lineTo(sx, TRK_Y + 11); ctx.stroke();
          ctx.globalAlpha = upA;
        }
      }

      if (spec.retargetLabel) {
        ctx.textAlign = 'center';
        ctx.font = disp(800, fit(spec.retargetLabel, 800, 11, 120, 8));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.retargetLabel, (carpX + stopX) / 2, TRK_Y - 20);
      }

      ctx.fillStyle = tone('ink');
      for (const x of [carpX, cliX]) {
        ctx.beginPath(); ctx.arc(x, TRK_Y - 13 + bob, 8, 0, Math.PI * 2); ctx.fill();
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(x, TRK_Y - 6 + bob); ctx.lineTo(x, TRK_Y - 1); ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }

    /* ── 아래층 : 전제 ────────────────────────────── */
    const slabA = ease(clamp(c0 / 0.5));
    if (slabA <= 0.02) { ctx.textAlign = 'left'; return; }

    const off = 10 * ease(clamp((c1 - 0.10) / 0.30));
    const prog = ease(clamp((c1 - 0.22) / 0.48));

    /* 🔴 **갈라지는 자리를 인용문의 낱말 사이 공백에 맞춘다** (2026-08-08 반려 수정).
       고정값 w × 0.48 을 쓰던 초안은 글자 한복판을 지나서 「쫓아가서 **서** 건네준다」처럼
       한 글자가 깨져 보였다. 여기서는 실제로 그릴 글꼴로 폭을 재서 **공백의 한가운데**를
       금 자리로 삼는다 — 그러면 어느 글자도 안 잘리고 낱말 두 덩이가 통째로 벌어진다.
       measureText 는 같은 글꼴·같은 문자열이면 같은 값이라 결정성은 그대로다. */
    const qfs = spec.quote ? fit(spec.quote, 900, 21, w - 96, 12) : 0;
    let crackX = w * 0.5;
    if (spec.quote) {
      const gi = spec.quote.indexOf(' ');
      if (gi > 0) {
        ctx.font = disp(900, qfs);
        const total = ctx.measureText(spec.quote).width;
        const pre = ctx.measureText(spec.quote.slice(0, gi)).width;
        const sp = ctx.measureText(spec.quote.slice(gi, gi + 1)).width;
        crackX = (w / 2 - total / 2) + pre + sp / 2;
      }
    }
    crackX = Math.max(w * 0.32, Math.min(w * 0.68, crackX));

    const drawSlab = () => {
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, 26, SLAB_Y, w - 52, SLAB_H, 8); ctx.stroke();
      if (spec.premiseLabel) {
        ctx.textAlign = 'left';
        ctx.font = disp(900, fit(spec.premiseLabel, 900, 11, 120, 8));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.premiseLabel, 42, SLAB_Y + 24);
      }
      if (spec.quote) {
        ctx.textAlign = 'center';
        ctx.font = disp(900, qfs);
        ctx.fillStyle = tone('ink');
        ctx.fillText(spec.quote, w / 2, SLAB_Y + 56);
      }
    };

    /* 🔴 **클립 경계를 벌어진 만큼 밀어 준다** (2026-08-08 반려 수정).
       초안은 클립을 [0, crackX) / [crackX, w) 로 **맞닿게** 잡고 각각 ∓off 로 옮겼다.
       그러면 원본 x ∈ [crackX−off, crackX+off) 인 2×off 짜리 띠가 **두 번 그려지고**
       (한 글자 복제), 화면에는 **틈이 0px** 이라 균열이 흰 글자 위를 지나게 된다.
       ⚠️ 그 겹침은 강조색이 흰색을 불투명하게 덮어 혼합 픽셀이 안 생기므로
       **팔레트 게이트가 못 잡는다** — 기계가 아니라 사람이 잡은 결함이다.
       고쳐 쓴 규칙: 왼쪽은 화면 x < crackX−off 에만, 오른쪽은 화면 x ≥ crackX+off 에만 그린다.
       그러면 원본 x < crackX 는 왼쪽에, x ≥ crackX 는 오른쪽에 **정확히 한 번씩** 들어가고
       화면 [crackX−off, crackX+off) 는 진짜로 빈다. off = 0 이면 이음매가 딱 맞는다. */
    ctx.globalAlpha = slabA;
    ctx.save();
    ctx.beginPath();
    ctx.rect(0, SLAB_Y - 24, Math.max(0, crackX - off), SLAB_H + 48); ctx.clip();
    ctx.translate(-off, 0); drawSlab();
    ctx.restore();
    ctx.save();
    ctx.beginPath();
    ctx.rect(crackX + off, SLAB_Y - 24, Math.max(0, w - crackX - off), SLAB_H + 48); ctx.clip();
    ctx.translate(off, 0); drawSlab();
    ctx.restore();
    ctx.globalAlpha = 1;

    /* ── 균열 — 열린 틈 안에서만 그린다 ────────────── */
    const crackA = clamp((off - 3.0) / 3.5) * slabA;
    if (crackA > 0.02 && prog > 0.01) {
      const jit = Math.min(4.2, off * 0.42);
      const y0 = SLAB_Y - 8, y1 = SLAB_Y + SLAB_H + 8;
      const N = 6;
      const pts = [];
      for (let j = 0; j <= N; j++) {
        pts.push({
          x: crackX + (rnd(j * 19 + 7) - 0.5) * 2 * jit,
          y: lerp(y0, y1, j / N)
        });
      }
      const endY = lerp(y0, y1, prog);
      ctx.globalAlpha = crackA;
      setShadow(ctx, GLOW, 12);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([11, 7]);
      ctx.lineDashOffset = -(t * 20) % 18;
      ctx.beginPath();
      ctx.moveTo(pts[0].x, pts[0].y);
      for (let j = 1; j <= N; j++) {
        if (pts[j].y <= endY) { ctx.lineTo(pts[j].x, pts[j].y); continue; }
        const k = (endY - pts[j - 1].y) / (pts[j].y - pts[j - 1].y || 1e-6);
        ctx.lineTo(lerp(pts[j - 1].x, pts[j].x, k), endY);
        break;
      }
      ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
