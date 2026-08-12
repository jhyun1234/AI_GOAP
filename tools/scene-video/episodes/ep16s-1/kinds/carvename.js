import {
  mono, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW, camAt
} from '../../../engine/lib.js';

/* carvename — 죽고 나면 물어볼 데가 없다. 그래서 아직 서 있는 동안 이름을 건네받아
   무덤에 새긴다.

   원문 근거:
   > "여기서 신경 쓴 건 이름을 언제 넘기느냐였습니다. 오브젝트가 파괴되는 시점에 이름을
   >  읽으면 지연 파괴와 서비스 기록 정리 순서가 얽힙니다. 그래서 죽는 순간, 즉 주민이
   >  아직 살아 있는 함수 안에서 이름을 인자로 넘기게 했습니다."
   > "무덤 표기를 \"{shortName} · Day {day}\" 형식으로 바꿨습니다."

   🔴 「지연 파괴」·「서비스 기록 정리 순서」는 원문에 풀이가 없어 **버렸다**(ADR-V25-13 ③).
   🔴 **이름과 날짜의 값을 글자로 안 적었다.** 원문 형식은 `{shortName} · Day {day}` 인데
   실제 값은 원문에 없다 — 적으면 지어낸 수치가 된다. 그래서 이름 자리와 날짜 자리를
   **강조색 막대**로 두고, 형식에 실제로 들어 있는 가운뎃점과 「Day」만 그린다.
   🔴 색 규약: 이 편에서 처음으로 강조색이 **채워진다.** namefade 에서 꺼졌던 것이
   여기서 돌아온다 — 그것이 이 편의 뒤집힘이다.

   ── 2026-08-12 개정 v3 (역동성 시험) ──────────────────────────────
   🔴 **두 샷으로 갈랐고, 카메라가 이 편에서 유일하게 「작게 → 크게」로 간다.**
     phase 0 (S3a · 3.4초) = 없는 자리를 더듬는다. **배율 0.80 고정** — 화면에서 가장
       작은 구도다. 자막이 「물어볼 데가 없거든요」이고, 그 텅 빔을 크기로 말한다.
     phase 1 (S3b · 3.6초) = 아직 서 있을 때 받아서 새긴다. 0.80 → **1.30 으로 다가간다**
       (0.6~2.0초). 새김이 차오르는 1.10~1.50초와 겹치게 맞췄다. latch 효과음.
   🔑 **이 편의 배율 곡선이 여기서 뒤집힌다.** S1b 2.2 · S2b 2.6 은 「나빠지는 쪽으로
      조여드는」 움직임이었고, S3a 0.80 은 그 조임이 풀린 뒤의 **바닥**이다. 거기서
      1.30 으로 올라가는 것이 이 편의 뒤집힘이다 — 컷이 아니라 **크기가 논지를 진다.**
   🔑 크기 대비를 일부러 벌린 것이기도 하다. v2 에서 모든 샷이 상자를 고르게 채우자
      「채움 편차」가 24.8p → 17.3p 로 오히려 떨어졌다. 다 크면 큰 게 안 보인다.

   🔴 잉크를 세로 107~250(47%)에서 50~265.5(**70%**)로 폈다. 무덤 반지름 30 → 42.

   ⏱ 새김은 `since(0)` 위에 세웠다(효과음 `latch` 의 `delayMs` 와 원점이 같다).
      alive 0.15~0.45 / chip 0.55~1.10 / fall 1.05~1.45 / fill 1.10~1.50.
   🔴 **샷을 갈라도 이 값들은 한 자도 안 바뀐다.** 가르기 전 원점은 `since(1)`(S3 의
      둘째 자막)이었고, 가른 뒤 phase 1 의 첫 자막이 바로 그 줄이다. scene.json 쪽도
      `at` 을 1 → 0 으로 옮겼을 뿐 `delayMs 1100` 은 그대로다.

   겹침 감사(축 A 강조↔흰 / 축 B 흰↔흰):
     축 A · 이름 조각(강조) y 58~70, 글로우 8 까지 50~78.
            아래 = 무덤 바깥 꼭대기 150−(42+1.5 숨)−1.5 = 105 → **27px** /
                   표식 바깥 머리 117−1.5 = 115.5 → **37.5px**.
            새김 줄(강조) 바깥 꼭대기 226.5, 글로우 8 까지 218.5.
            위 = 무덤 바깥 바닥 195 → **23.5px**.
            (빈 칸 점선도 강조색이라 같은 상자를 써도 축 A 에 안 걸린다 — 채워지는 것이
             같은 색인 것이 이 그림의 뜻이다.)
     축 B · 흰 글자가 0개다. 흰 도형끼리는 서 있는 표식의 오른쪽 끝 70.5 와 더듬는 선의
            왼쪽 끝 최댓값 95 → **24.5px**(선이 표식에 안 닿는 것이 요점이다).
            🔑 누운 표식은 오른쪽 끝이 89 로 더듬는 선과 겹칠 좌표지만 **같은 프레임에
            안 뜬다** — 더듬는 선의 알파는 `(1 − alive)` 이고 눕기는 1.05초부터다.

   세로 예산(캔버스 307): 최저 = 새김 줄 글로우 바닥 **265.5**(41.5px 남음).
     최고 = 이름 조각 글로우 꼭대기 **50**.
   가로: 왼쪽 = 표식 41.5 / 오른쪽 = 새김 줄 글로우 319.5. 띠에서 좌 39.5 · 우 32.5.
   phase 0(0.80 고정): 전부 안으로 오므라든다(68.4~290.8) — 밖으로 안 나간다.
   phase 1 끝 배율(1.30 · focus [0.62, 0.62] = 화면 중앙에 (218.2, 190.3)):
     보이는 창 = x 82.8~353.6 · y 72.2~308.4. 무덤(105~195)과 새김 줄(218.5~265.5)이
     화면을 채우고 **누운 표식(41.5~70.5)이 왼쪽으로 나간다** → `checks.edge` 선언 대상.
     🔑 표식이 밖으로 나가는 것이 뜻과 맞는다 — 그때 이야기는 이미 사람에서 무덤으로
        옮겨 갔다(눕기는 1.45초에 끝나고 카메라는 그 뒤에 도착한다).

   계속 도는 것(30fps 기준): 더듬는 선이 55px 폭으로 왕복(2.2rad/s) → **1.3px/프레임**,
   무덤 반지름 숨 1.5px · 4.4rad/s, 새긴 뒤 강조색 줄의 알파 맥동(0.82~1.00 · 3.6rad/s). */

const FIG_X = 56, CY = 150;
const GR_X = 240, GR_R = 42;
const ROW = { x: 170, y: 228, w: 140, h: 28 };
const CHIP_Y = 58, CHIP_W = 34, CHIP_H = 12;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    camAt(ctx, w, h, spec, t);
    ctx.textBaseline = 'alphabetic';

    const ph = spec.phase ?? 0;
    /* 무덤과 빈 칸은 두 phase 에 걸쳐 계속 있다. phase 1 에서는 컷과 함께 이미 서 있다. */
    const g0 = ph === 0 ? ease(cue(0, 0.15, 0.30)) : 1;
    if (g0 <= 0.01) { ctx.textAlign = 'left'; return; }

    const s1 = ph === 0 ? -1 : since(0);
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
      roundRect(ctx, FIG_X - 13, CY - 33, 26, 66, 13); ctx.stroke();
      ctx.setLineDash([]);
      ctx.restore();
    }
    if (alive > 0.01) {
      ctx.save();
      ctx.globalAlpha = g0 * alive;
      ctx.translate(FIG_X, CY);
      ctx.rotate(fall * Math.PI / 2);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, -13, -33, 26, 66, 13); ctx.stroke();
      ctx.restore();
    }

    /* ── 더듬어 봐도 물어볼 데가 없다 (흰 점선, 왕복) ── */
    const probe = g0 * (1 - alive);
    if (probe > 0.01) {
      const reach = 40 + 55 * (0.5 + 0.5 * Math.sin(t * 2.2));
      ctx.save();
      ctx.globalAlpha = probe * 0.75;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.setLineDash([8, 8]);
      ctx.lineDashOffset = -((t * 40) % 16);
      ctx.beginPath();
      ctx.moveTo(GR_X - GR_R - 8, CY);
      ctx.lineTo(GR_X - GR_R - 8 - reach, CY);
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
      ctx.font = mono(700, 14);
      const wordW = ctx.measureText(word).width;
      const B1 = 48, B2 = 26, DOT = 5, GAP = 10;
      const total = B1 + GAP + DOT + GAP + wordW + 8 + B2;
      let x = GR_X - total / 2;
      const pulse = 0.82 + 0.18 * (0.5 + 0.5 * Math.sin(t * 3.6));
      ctx.save();
      ctx.globalAlpha = g0 * fill * pulse;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(x, ROW.y + 9, B1, 10); x += B1 + GAP;
      ctx.fillRect(x, ROW.y + 11, DOT, DOT); x += DOT + GAP;
      ctx.textAlign = 'left';
      ctx.fillText(word, x, ROW.y + 19); x += wordW + 8;
      ctx.fillRect(x, ROW.y + 9, B2, 10);
      clearShadow(ctx);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
