import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 편 기능의 0.5초 테스트 장면을 루프로 돌린다.

   🔴 **다음 편은 `ep13s` 이고, 작성 시점에 아직 분할 전이다**(`state/schedule.json` 의
   `order` 에 `ep13s` 한 줄 · `episodes/ep13s` 폴더 없음). 그래서 사건 단위가 아니라
   **밀스톤 층위**로만 그렸다 — 그 글의 1단계는 늑대가 아니라 부상이라, 「늑대가 나타난다」로
   그리면 13-1편이 부상 편이 될 때 또 어긋난다.

   근거는 `tools/blog-automation/published/2026-07-28-unity-goap-m10-wolf-injury-wanderer.html`:
   > "제가 만드는 마을에 이번 회차에서 처음으로 무덤이 생겼습니다."
   > "…마을에 처음으로 잃는 일이 생겼습니다."
   > "주민은 근처에 위협이 있으면 하던 일을 놓고 안전한 곳으로 피신합니다."
   > "무덤은 지워지지 않습니다. 주민이 죽으면 시체는 사라지지만 무덤은 그 자리에 계속 남습니다."

   **0.5초 루프** — ①오른쪽 화면 밖에서 강조색 THREAT 쐐기가 지면 아래 길로 들어와 왼쪽으로
   지나간다 ②그것이 마을 가장자리 주민의 x 에 닿는 순간 그 **흰 표식이 완전히 꺼지고**
   ③같은 자리에서 **강조색 무덤이 솟는다** ④나머지 표식은 반대쪽으로 물러났다 돌아온다
   ⑤무덤이 먼저 꺼지고 그 뒤에 표식이 돌아오며 되감긴다.

   🔴 이 회차의 문법을 물려받되 도형은 안 물려받는다. 물려받은 문장은 하나 —
   **「흰 것이 꺼진 자리에 강조색 자국이 남는다」**(본문 S3 에서 밭 칸이 주저앉고 강조색
   점선 윤곽만 남던 그 문장). 다음 편에서는 그 흰 것이 밭이 아니라 **사람**이다.
   그래서 색을 이렇게 나눴다: **흰색 = 서 있는 사람 / 강조색 = 그들에게 오는 것과 남는 자국.**

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — 두 겹으로 막았다.
     ⓐ **좌표** : 주민 표식은 지면선 **위**(중심 194 · r 8 → 186~202),
        THREAT 쐐기는 지면선 **아래**(216~236)로만 다닌다. 14px 떨어진다.
     ⓑ **시간** : 무덤은 표식이 있던 바로 그 x 에 서므로 알파 구간을 붙여 놓지 않았다 —
        표식은 위상 0.34 에 알파 0, 무덤은 0.38 에야 시작. 되감을 때도 무덤이 0.95 에
        완전히 꺼진 **뒤** 0.955 부터 표식이 돌아온다.

   🔴 **화면에 숫자를 한 개도 안 올렸다.** 주민 표식 개수는 `spec.marks` 로만 있고 세는
      라벨이 없다 — 그 시점의 주민 수가 원문에 없어서 「7명」이라 적으면 지어낸 수치가 된다.
      왼쪽 끝만 흐리게 그려 "여기서 끊긴 게 아니라 이어진다"로 읽히게 했고, 오른쪽 끝은
      흐리게 하지 않았다 — 위협이 들어오는 **마을의 가장자리**라 거기가 실제로 끝이다.
   🔴 수치는 전부 `spec` 경유다(`marks` · `hitIndex` · `loopSec`). kind 파일 상수로 박아 두면
      그 수가 원문의 어느 문장에서 왔는지 `scene.json` 만 보고는 알 수 없다.

   ⏱ 등장(cue) 과 루프(t) 를 갈랐다 — 등장은 예고 자막 `cue(0)` 에 물리고, 루프만 `t` 로 돈다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다. 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.

   세로 예산(캔버스 307) — 창 40~262 · GRAVE 라벨 154 · 무덤 166~206 · 표식 186~202 ·
   지면선 206 · 쐐기 216~236 · THREAT 라벨 252. 최저는 창 바닥 262(stroke 포함 263.5). */

const WIN_Y0 = 40, WIN_Y1 = 262;
const GROUND = 206;
const HEAD_R = 8, HEAD_CY = 194;
const LANE_Y = 226, LANE_H = 10;
const STONE_W = 22, STONE_H = 40;
const HIT_PH = 0.22;       // 쐐기가 마을 가장자리에 닿는 위상

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

    /* ── 구획 이름 (창 밖) ─────────────────────────── */
    if (spec.label) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 12, 26);
    }

    /* ── 등장 : 미리보기 창이 열린다 ────────────────── */
    const ak = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (ak <= 0.02) { ctx.textAlign = 'left'; return; }

    const inset = lerp(16, 0, ak);
    ctx.save();
    ctx.globalAlpha = clamp(ak * 1.6);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([16, 10]);
    ctx.lineDashOffset = -((t * 26) % 26);
    roundRect(ctx, 16 + inset, WIN_Y0 + inset, w - 32 - inset * 2, WIN_Y1 - WIN_Y0 - inset * 2, 10);
    ctx.stroke();
    ctx.restore();

    if (ak < 0.5) { ctx.textAlign = 'left'; return; }
    const inner = clamp((ak - 0.5) / 0.5);

    /* 🔴 창 안쪽으로 클립한다 — 이게 「미리보기 창」이라는 뜻이면서, 동시에
       **좌우 가장자리 검사를 안 건드리는 방법**이다. 위협은 설계상 화면 밖에서 들어오는데,
       클립이 없으면 쐐기가 캔버스 바깥 2px 띠를 몇 프레임씩 칠해 `가장자리 잘림` 이 센다.
       `checks.edge` 로 예외 선언을 하는 길도 있지만, **잘린 것처럼 보이지 않게 만드는 쪽이
       예외를 다는 쪽보다 낫다** — 쐐기가 창 테두리 밑으로 미끄러져 들어오는 것으로 읽힌다.
       클립 경계는 테두리 stroke(3px, 경로 ±1.5)의 안쪽 변에 맞췄다. */
    ctx.save();
    ctx.beginPath();
    ctx.rect(18, WIN_Y0 + 2, w - 36, WIN_Y1 - WIN_Y0 - 4);
    ctx.clip();

    /* ── 0.5초 테스트 장면 ─────────────────────────── */
    const ph = frac(t / (spec.loopSec ?? 0.5));

    const n = Math.max(2, spec.marks ?? 7);
    const hit = clamp(spec.hitIndex ?? (n - 1), 0, n - 1) | 0;
    const gap = Math.min(40, (w - 92) / (n - 1));
    const x0 = w / 2 - gap * (n - 1) / 2;
    const tx = x0 + hit * gap;

    /* 쐐기 — 오른쪽 밖에서 들어와 가장자리에 닿고, 그대로 왼쪽으로 빠진다 */
    const wx = ph <= HIT_PH
      ? lerp(w + 34, tx, ease(ph / HIT_PH))
      /* 🔴 나가는 끝점을 −80 으로 둔 것은 아래 그리기 가드(wx > −60)보다 더 나가야
         쐐기 팔(wx+27)까지 화면 밖으로 빠지기 때문이다. −44 면 위상 0.8~1.0 동안
         왼쪽 가장자리에 걸려 있는다. */
      : lerp(tx, -80, ease(clamp((ph - HIT_PH) / (0.80 - HIT_PH))));

    const fell = clamp((ph - HIT_PH) / 0.12);          // 표식이 꺼진다 (0.34 에 0)
    const rise = ease(clamp((ph - 0.38) / 0.20));      // 무덤이 선다 (0.58 에 완성)
    const gOut = clamp((ph - 0.90) / 0.05);            // 무덤이 먼저 꺼진다 (0.95)
    const back = clamp((ph - 0.955) / 0.045);          // 그 뒤에 표식이 돌아온다
    const flee = ease(clamp((ph - 0.08) / 0.16)) * (1 - ease(clamp((ph - 0.72) / 0.20)));

    /* ── 지면선 ────────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = inner;
    ctx.setLineDash([9, 8]);
    ctx.lineDashOffset = (t * 16) % 17;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(32, GROUND); ctx.lineTo(w - 32, GROUND); ctx.stroke();
    ctx.restore();

    /* ── 지면 위 : 주민 표식 (흰색) ─────────────────── */
    for (let i = 0; i < n; i++) {
      const isHit = i === hit;
      const a = isHit ? clamp((1 - fell) + back) : 1;
      if (a <= 0.01) continue;
      const x = x0 + i * gap + (isHit ? 0 : -7 * flee);
      ctx.save();
      ctx.globalAlpha = inner * a * (i === 0 ? 0.45 : 1);
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(x, HEAD_CY, HEAD_R, 0, Math.PI * 2); ctx.fill();
      ctx.restore();
    }

    /* ── 남는 자국 : 무덤 (강조색) ──────────────────── */
    const ga = rise * (1 - gOut);
    if (ga > 0.01) {
      const hgt = STONE_H * rise;
      const top = GROUND - hgt;
      const r = Math.min(STONE_W / 2, hgt / 2);
      ctx.save();
      ctx.globalAlpha = clamp(ga * 1.4);
      setShadow(ctx, GLOW, 16);
      ctx.fillStyle = tone('accent');
      ctx.beginPath();
      ctx.moveTo(tx - STONE_W / 2, GROUND);
      ctx.lineTo(tx - STONE_W / 2, top + r);
      ctx.arcTo(tx - STONE_W / 2, top, tx, top, r);
      ctx.arcTo(tx + STONE_W / 2, top, tx + STONE_W / 2, top + r, r);
      ctx.lineTo(tx + STONE_W / 2, GROUND);
      ctx.closePath();
      ctx.fill();
      clearShadow(ctx);
      ctx.restore();

      if (spec.graveLabel && rise > 0.6) {
        ctx.save();
        ctx.globalAlpha = clamp((rise - 0.6) / 0.3) * (1 - gOut) * inner;
        ctx.textAlign = 'center';
        ctx.font = disp(800, fit(spec.graveLabel, 800, 11.5, 72, 8));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.graveLabel, tx, 154);
        ctx.restore();
      }
    }

    /* ── 지면 아래 : 위협이 지나가는 길 (강조색) ────── */
    if (wx > -60 && wx < w + 60) {
      ctx.save();
      ctx.globalAlpha = inner;
      setShadow(ctx, GLOW, 12);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      /* 왼쪽을 향한 쐐기 — 지면선 아래로만 다닌다 */
      ctx.beginPath();
      ctx.moveTo(wx + 15, LANE_Y - LANE_H);
      ctx.lineTo(wx - 15, LANE_Y);
      ctx.lineTo(wx + 15, LANE_Y + LANE_H);
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(wx + 27, LANE_Y - LANE_H * 0.6);
      ctx.lineTo(wx + 3, LANE_Y);
      ctx.lineTo(wx + 27, LANE_Y + LANE_H * 0.6);
      ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    }

    if (spec.threatLabel) {
      ctx.save();
      ctx.globalAlpha = inner * 0.9;
      ctx.textAlign = 'left';
      ctx.font = disp(800, fit(spec.threatLabel, 800, 12, 120, 8));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.threatLabel, 32, 252);
      ctx.restore();
    }

    ctx.restore();          // 창 클립 해제
    ctx.textAlign = 'left';
  }
};
