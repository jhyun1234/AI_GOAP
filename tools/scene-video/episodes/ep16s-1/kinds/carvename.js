import {
  mono, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* carvename — 죽고 나면 물어볼 데가 없다. 그래서 아직 서 있는 동안 이름을 건네받아
   무덤에 새긴다.

   원문 근거:
   > "여기서 신경 쓴 건 이름을 언제 넘기느냐였습니다. 오브젝트가 파괴되는 시점에 이름을
   >  읽으면 지연 파괴와 서비스 기록 정리 순서가 얽힙니다. 그래서 죽는 순간, 즉 주민이
   >  아직 살아 있는 함수 안에서 이름을 인자로 넘기게 했습니다."
   > "무덤 표기를 \"{shortName} · Day {day}\" 형식으로 바꿨습니다."

   🔴 「지연 파괴」·「서비스 기록 정리 순서」는 원문에 풀이가 없어 **버렸다**(ADR-V25-13 ③).
   대신 바로 뒤 문장이 사람 말로 준 것(「아직 살아 있는 …」)만 ①풀어쓰기로 옮겼다 —
   자막은 「죽고 나면 이름을 물어볼 데가 없거든요」다. 그림도 같은 순서를 그린다:
   먼저 **없는 자리를 더듬어 보고**(cue 0), 그 다음 **아직 서 있을 때 건네받는다**(cue 1).

   🔴 **이름과 날짜의 값을 글자로 안 적었다.** 원문 형식은 `{shortName} · Day {day}` 인데
   실제 값은 원문에 없다 — 적으면 지어낸 수치가 된다(브리프 §6 형식 문자열 경고).
   그래서 이름 자리와 날짜 자리를 **강조색 막대**로 두고, 형식에 실제로 들어 있는
   가운뎃점과 「Day」만 그린다. 「Day」는 게임이 찍는 표기의 인용이라 영어로 둔다
   (ADR-V-10 예외 ①). 가운뎃점은 폰트 폴백을 피하려고 글자가 아니라 4×4 네모로 그렸다.

   🔴 색 규약: 이 편에서 처음으로 강조색이 **채워진다.** namefade 에서 꺼졌던 것이
   여기서 돌아온다 — 그것이 이 편의 뒤집힘이다.

   ⏱ 새김은 `since(1)` 위에 세웠다(효과음 `latch` 의 `delayMs` 와 원점이 같다).
      alive 0.15~0.45 / chip 0.55~1.10 / fall 1.05~1.45 / fill 1.10~1.50.
   🔴 c 계열의 창 인자(cue(askCue, 0.15, 0.30))와 발동 문턱은 이 파일에서만 정한다.

   겹침 감사(축 A 강조↔흰 / 축 B 흰↔흰):
     축 A · 이름 조각(강조) y 107~117, 글로우 8 까지 99~125.
            아래 = 표식 바깥 머리 168−26−1.5 = 140.5 → **15.5px** /
                   무덤 바깥 꼭대기 168−(30+1.5 숨)−1.5 = 135 → **10px**.
            새겨진 줄(강조)의 바깥 꼭대기 = 226−1.5 = 224.5, 글로우 8 까지 216.5.
            위 = 무덤 바깥 바닥 168+31.5+1.5 = 201 → **15.5px**.
            (빈 칸 점선도 강조색이므로 같은 상자를 써도 축 A 에 안 걸린다 — 채워지는 것이
             같은 색인 것이 이 그림의 뜻이다.)
     축 B · 흰 글자가 0개다. 흰 도형끼리는 서 있는 표식의 오른쪽 끝 84+10+1.5 = 95.5 와
            더듬는 선의 왼쪽 끝 최댓값 110 → **14.5px**(선이 표식에 닿지 않는 것이 요점이다).
            🔑 누운 표식은 오른쪽 끝이 111.5 라 더듬는 선과 겹칠 좌표에 있지만,
            **둘은 같은 프레임에 안 뜬다** — 더듬는 선의 알파는 `g0 × (1 − alive)` 이고
            눕기(`fall`)는 `since(1)` 1.05초부터라 그때 `alive` 는 이미 1 이다.

   세로 예산(캔버스 307): 최저 = 새김 상자 바깥 바닥 249.5 + 글로우 8 = **257.5**. 49px 남는다.
   가로: 왼쪽 = 누운 표식 84−26−1.5 = 56.5 / 오른쪽 = 상자 306+1.5 + 글로우 8 = **315.5**.
   바깥 2px 띠에서 좌 54.5 · 우 34.5 뜬다.

   계속 도는 것(30fps 기준): 더듬는 선이 60px 폭으로 왕복(2.2rad/s) → **1.4px/프레임**,
   무덤 반지름 숨 1.5px · 4.4rad/s, 새긴 뒤 강조색 줄의 알파 맥동(0.82~1.00 · 3.6rad/s). */

const FIG_X = 84, CY = 168;
const GR_X = 246, GR_R = 30;
const ROW = { x: 186, y: 226, w: 120, h: 22 };
const CHIP_Y = 107, CHIP_W = 26, CHIP_H = 10;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const g0 = ease(cue(spec.askCue ?? 0, 0.15, 0.30));
    if (g0 <= 0.01) { ctx.textAlign = 'left'; return; }

    const s1 = since(spec.carveCue ?? 1);
    const alive = ease(clamp((s1 - 0.15) / 0.30));
    const chip = ease(clamp((s1 - 0.55) / 0.55));
    const fall = ease(clamp((s1 - 1.05) / 0.40));
    const fill = ease(clamp((s1 - 1.10) / 0.40));

    /* ── 무덤 (흰색) ───────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = g0;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.arc(GR_X, CY, GR_R + 1.5 * (0.5 + 0.5 * Math.sin(t * 4.4)), 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();

    /* ── 주민 표식 — 없는 자리(점선) → 서 있다(실선) → 눕는다 ── */
    const ghost = g0 * (1 - alive);
    if (ghost > 0.01) {
      ctx.save();
      ctx.globalAlpha = ghost * 0.8;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.setLineDash([7, 7]);
      ctx.lineDashOffset = -((t * 22) % 14);
      roundRect(ctx, FIG_X - 10, CY - 26, 20, 52, 10); ctx.stroke();
      ctx.setLineDash([]);
      ctx.restore();
    }
    if (alive > 0.01) {
      ctx.save();
      ctx.globalAlpha = g0 * alive;
      ctx.translate(FIG_X, CY);
      ctx.rotate(fall * Math.PI / 2);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, -10, -26, 20, 52, 10); ctx.stroke();
      ctx.restore();
    }

    /* ── 더듬어 봐도 물어볼 데가 없다 (흰 점선, 왕복) ── */
    const probe = g0 * (1 - alive);
    if (probe > 0.01) {
      const reach = 40 + 60 * (0.5 + 0.5 * Math.sin(t * 2.2));
      ctx.save();
      ctx.globalAlpha = probe * 0.75;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.setLineDash([8, 8]);
      ctx.lineDashOffset = -((t * 40) % 16);
      ctx.beginPath();
      ctx.moveTo(GR_X - GR_R - 6, CY);
      ctx.lineTo(GR_X - GR_R - 6 - reach, CY);
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.restore();
    }

    /* ── 이름 조각이 건너간다 (강조색) ─────────────── */
    const chipA = ease(clamp(chip / 0.12)) * (1 - ease(clamp((chip - 0.86) / 0.14)));
    if (g0 * chipA > 0.01) {
      const cx = lerp(FIG_X, GR_X, chip);
      ctx.save();
      ctx.globalAlpha = g0 * chipA;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(cx - CHIP_W / 2, CHIP_Y, CHIP_W, CHIP_H);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 새김 자리 : 빈 점선 → 「막대 · Day 막대」 ──── */
    ctx.save();
    ctx.globalAlpha = g0 * (1 - fill) * 0.9;
    if (ctx.globalAlpha > 0.01) {
      setShadow(ctx, GLOW, 8);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([9, 7]);
      ctx.lineDashOffset = -((t * 30) % 16);
      ctx.strokeRect(ROW.x, ROW.y, ROW.w, ROW.h);
      ctx.setLineDash([]);
      clearShadow(ctx);
    }
    ctx.restore();

    if (fill > 0.01) {
      const word = spec.dayWord ?? 'Day';
      ctx.font = mono(700, 12);
      const wordW = ctx.measureText(word).width;
      const B1 = 40, B2 = 22, DOT = 4, GAP = 8;
      const total = B1 + GAP + DOT + GAP + wordW + 7 + B2;
      let x = GR_X - total / 2;
      const pulse = 0.82 + 0.18 * (0.5 + 0.5 * Math.sin(t * 3.6));
      ctx.save();
      ctx.globalAlpha = g0 * fill * pulse;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(x, ROW.y + 7, B1, 8); x += B1 + GAP;
      ctx.fillRect(x, ROW.y + 9, DOT, DOT); x += DOT + GAP;
      ctx.textAlign = 'left';
      ctx.fillText(word, x, ROW.y + 15); x += wordW + 7;
      ctx.fillRect(x, ROW.y + 7, B2, 8);
      clearShadow(ctx);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
