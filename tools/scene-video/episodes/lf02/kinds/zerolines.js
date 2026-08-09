import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* zerolines — 왼쪽 에셋 칸 하나가 오른쪽 콘텐츠 칸 하나를 켠다. 그 사이 아래에 있는
   코드 계기는 바늘이 왼쪽 끝에서 떨기만 하고 한 번도 안 움직인다.
   🔴 lf01 의 `zerocode` 와 같은 사실을 다루지만 그림이 다르다 — 저쪽은 인스펙터 칸을
   채우면 주민이 걸어가는 장면이고, 이쪽은 에셋 칸 ↔ 콘텐츠 칸의 1:1 대응 판이다.
   라벨도 `CODE WRITTEN` 이 아니라 `CODE ADDED` 로 갈라 뒀다.
   연속 모션 = 켜진 콘텐츠 램프가 다음 칸으로 계속 옮겨 가는 것. 콘텐츠는 계속 늘어난다.
   문자열 출처: Docs/CLAUDE.md 콘텐츠 추가 비용표(계절·재해·위협 = 에셋 1개, 코드 0줄) ·
   published/2026-07-16-unity-goap-m2-m3-production-housing.html 8행(.cs 한 줄도 없음). */

const PAIRS = [
  { a: 'SeasonSO', b: '계절' },
  { a: 'DisasterSO', b: '재해' },
  { a: 'ThreatSO', b: '위협' },
  { a: 'CookMeal.asset', b: '요리' },
];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kPrompt = ease(cue(0));  // 프롬프트가 답이 아니다
    const kStruct = ease(cue(1));  // 구조를 먼저 만든다
    const kSeason = ease(cue(2));  // 계절 하나 = 에셋 하나
    const kMore = ease(cue(3));    // 재해도 위협도
    const kCook = ease(cue(4));    // 요리는 .cs 0줄

    const rowH = 34, y0 = 44;
    const aw = 156, bw = 96;
    const ax = M, bx = M + aw + 66;

    /* 🔴 2차 개정 (검수 C6): 연속 모션이 반지름 6px 램프 점멸과 1.6px 바늘 떨림뿐이라
       문턱 아래였다. → 에셋 칸 ↔ 콘텐츠 칸 짝을 **행째로 훑고 내려가는 판독 띠**를
       세운다. 칸보다 먼저 그려 강조색 위에 반투명 흰색이 얹히지 않게 한다. */
    {
      const BR = 3.1, bk = (t % BR) / BR;
      const by = lerp(y0 - 12, y0 + PAIRS.length * (rowH + 6) + 2, bk);
      ctx.fillStyle = tone('track');
      ctx.fillRect(ax - 8, by, (bx + bw + 8) - (ax - 8), rowH + 4);
    }

    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('ASSET FILE', ax, 32);
    ctx.fillText('CONTENT', bx, 32);

    // 켜진 램프가 도는 위상
    const PR = 2.4;
    const lit = Math.floor(t / PR) % PAIRS.length;

    const kOf = i => i === 0 ? kSeason : (i < 3 ? clamp((kMore - (i - 1) * 0.2) / 0.5) : kCook);

    for (let i = 0; i < PAIRS.length; i++) {
      const ry = y0 + i * (rowH + 6);
      const k = ease(kOf(i));
      const on = k > 0.1;

      // 에셋 칸
      ctx.strokeStyle = on ? tone('accent') : tone('track');
      ctx.lineWidth = 3;
      roundRect(ctx, ax, ry, aw, rowH, 3); ctx.stroke();
      let fs = 13; ctx.font = mono(700, fs);
      while (ctx.measureText(PAIRS[i].a).width > aw - 20 && fs > 8) {
        fs -= 1; ctx.font = mono(700, fs);
      }
      ctx.fillStyle = on ? tone('accent') : tone('sub');
      ctx.fillText(PAIRS[i].a, ax + 10, ry + 22);

      // 이음선
      ctx.strokeStyle = on ? tone('accent') : tone('track'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(ax + aw + 6, ry + rowH / 2);
      ctx.lineTo(bx - 8, ry + rowH / 2);
      ctx.stroke();
      if (on) {
        ctx.fillStyle = tone('accent');
        ctx.beginPath();
        ctx.moveTo(bx - 8, ry + rowH / 2);
        ctx.lineTo(bx - 16, ry + rowH / 2 - 5);
        ctx.lineTo(bx - 16, ry + rowH / 2 + 5);
        ctx.closePath(); ctx.fill();
      }

      // 콘텐츠 칸 + 램프
      const glow = on && i === lit;
      ctx.strokeStyle = on ? (glow ? tone('accent') : tone('ink')) : tone('track');
      ctx.lineWidth = glow ? 4 : 3;
      roundRect(ctx, bx, ry, bw, rowH, 3); ctx.stroke();
      ctx.font = disp(900, 15);
      ctx.fillStyle = on ? (glow ? tone('accent') : tone('ink')) : tone('sub');
      const sw = ctx.measureText(PAIRS[i].b).width;
      ctx.fillText(PAIRS[i].b, bx + 14, ry + 23);
      if (on) {
        ctx.fillStyle = glow ? tone('accent') : tone('track');
        ctx.beginPath();
        ctx.arc(bx + bw - 16, ry + rowH / 2, 6, 0, Math.PI * 2);
        ctx.fill();
      }
    }

    // ── 오른쪽 : 코드 계기 ─────────────────────────
    {
      const gx = bx + bw + 40, gw = w - M - gx;
      const gy = y0 + 34, gh = 74;
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('CODE ADDED', gx, gy - 12);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, gx, gy, gw, gh, 4); ctx.stroke();

      // 눈금
      for (let i = 0; i <= 4; i++) {
        const tx = gx + 14 + (gw - 28) * (i / 4);
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(tx, gy + gh - 22); ctx.lineTo(tx, gy + gh - 14);
        ctx.stroke();
      }
      // 바늘 — 왼쪽 끝에서 떨기만 한다 (자막과 무관하게 계속)
      const jitter = 1.6 * Math.sin(t * 5.1);
      const nx = gx + 14 + jitter;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(nx, gy + gh - 16); ctx.lineTo(nx, gy + 24);
      ctx.stroke();

      ctx.font = disp(900, 30); ctx.fillStyle = tone('ink');
      const z = '0';
      ctx.fillText(z, gx + 24, gy + 34);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('LINES', gx + 24 + ctx.measureText(z).width + 22, gy + 30);
    }

    // ── 아래 : 판정 ────────────────────────────────
    if (kPrompt > 0.05 || kStruct > 0.05) {
      const k = Math.max(kPrompt, kStruct);
      ctx.save(); ctx.globalAlpha = clamp(k);
      ctx.font = disp(900, 15); ctx.fillStyle = tone('ink');
      const s = kStruct > 0.3 ? '코드를 안 써도 늘어나는 구조' : '프롬프트가 답이 아니다';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, M, h - 12);
      ctx.restore();
    }
  },
};
