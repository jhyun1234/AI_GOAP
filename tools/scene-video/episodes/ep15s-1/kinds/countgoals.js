/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다. */
import {
  disp, ease, clamp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* countgoals — 진단(S2). 세어 봤더니 성격이 닿는 자리가 셋뿐이었다.

   원문 근거:
   > "게임 안의 목표(goal)는 서른 개인데, 성격 보정이 걸린 목표는 셋뿐이었습니다."
   > "그중 여섯 성격이 다 같이 값을 가진 건 겨울 비축 딱 하나였고, 나머지 둘은
   >  게으름뱅이 전용 페널티였습니다."

   자막 0 : 서른 칸이 깔리고 그중 **딱 세 칸에만 불이 들어온다.**
   자막 1 : 세 칸 중 **둘이 떨어져 나가고 한 칸만 남는다.**
   라벨을 다 가려도 「가득한 것 중에 켜진 게 거의 없다, 그리고 더 줄어든다」가 읽힌다.

   🔴 **격자를 6 × 5 = 30 칸으로 실제로 맞췄다.** 세면 서른이 나온다 — 라벨의 수와
   실물이 어긋나면 그 라벨이 근거가 아니라 장식이 된다.
   🔴 **「나머지 둘은 게으름뱅이 전용」은 그리지 않는다.** 원문에 있지만 이 편에서
   그 자리가 지는 뜻(사실상 하나였다)은 「하나만 남는다」로 이미 다 선다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **칸 사이 간격**으로 갈랐다.
     꺼진 칸 = 흰 테두리(3px), 켜진 칸 = 강조색 테두리(3px) + 글로우 blur 최대 **5**.

     🔴 **1차본의 「가로 13px · 세로 9px」은 틀린 산수였다** — 간격에서 글로우만 빼고
     **획 반폭(1.5씩 둘)과 상시 숨(0.8씩 둘)을 안 뺐다.** 정직하게 다시 적는다.
     한 축의 여유 = `간격 − 글로우 − 획 반폭×2 − 숨×2`:
       · 세로 = 16 − 5 − 3 − 1.6 = **6.4px**
       · 가로 = 20 − 5 − 3 − 1.6 = **10.4px**
     검산(세로, 절대 좌표): 위 칸의 흰 획 바깥 아래 끝 = `by − 16 + 0.8 + 1.5 = by − 13.7` /
     아래 칸의 강조색 도달 위 끝 = `by − 0.8 − 1.5 − 5 = by − 7.3` → **6.4px** 뜬다.
     🔑 훑기 마디(`fillRect(px−2, py−2, 4, 4)`)는 `clearShadow` 뒤에 그려 **글로우가 없고**
     테두리에서 2px 만 나가므로(`by − 2.8`) 위 값을 못 넘는다.
     🔑 켜지는 자리(7 · 14 · 22)는 6열 격자에서 **서로 상하좌우로 안 닿는 칸**이다
     (7 = 2행2열 · 14 = 3행3열 · 22 = 4행5열). 대각은 **가로 여유 10.4px 만으로** 안전하다 —
     두 상자가 겹치려면 두 축이 **동시에** 겹쳐야 하는데 가로가 이미 안 겹친다.

   세로 예산(캔버스 307): 최저 = 아래 라벨 글립 바닥 **286**(기준선 282). 21px 남는다.
   그다음이 떨어진 칸의 바닥 256.3(206 + 22 + 26 + 숨 0.8 + 획 1.5) · 위 라벨 글립 바닥 252 ·
   격자 5행 바닥 230.3.
   가로: 격자 오른쪽 **330.3**(21.7px 남는다 — 숨 0.8 과 획 1.5 를 다 먹은 순간) · 왼쪽 21.7.
   🔴 등장에 **이동이 0** 이다(칸이 제자리에서 자란다). 떨어지는 두 칸만 아래로 26px
   내려가는데 그 최저점이 256.3 이라 라벨 바닥(286)을 안 넘는다.
   🔑 떨어지는 칸(강조색, 글로우 최대 **5**)과 위 라벨(흰색)은 **가로로도 안 만난다** —
   22번 칸은 x 240~274 이고 「목표 30개」 글자는 x 133~219 다.

   계속 도는 것: ①격자 서른 칸의 상시 숨(진폭 1.6px · 6.6rad/s → 프레임당 **0.35px**,
   서른 칸 × 둘레 112px 이라 합이 크다) ②켜진 칸의 **테두리 훑기**(둘레 112px 를
   96px/초로 도는 마디 셋 → 프레임당 **3.2px**) ③켜진 칸의 글로우 맥동.
   🔑 ①②는 사건이 아니라 상시 운동이라 `t` 로 돈다(사건은 자막에 물린다). */

const COLS = 6, ROWS = 5;
const CW = 34, CH = 22, GX = 20, GY = 16;
const X0 = 24, Y0 = 54;                 // 격자 x 24~328 · y 54~228
/* 🔴 두 라벨의 y 를 34px 벌렸다 — 위(흰색)와 아래(강조색)가 다른 색이라 글립 사이가
   최소 15px 은 떠야 한다(초고는 28px 간격이라 9px 밖에 안 떴다). */
const LAB_A_Y = 248, LAB_B_Y = 282;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    /* 테두리 위의 한 점 — 훑기 마디를 놓을 자리를 구한다(결정적).
       🔴 **1차 반려를 고친 자리.** 네 번째 변에 들어가기 전에 아래 변의 길이(`wd`)를 빼야
       하는데 `ht` 를 빼고 있었다. 세 변이 `wd + ht + wd = 90` 을 먹는데 78 만 빼서,
       p 90~112 구간(둘레의 20%)의 마디가 **칸 위로 최대 11.5px 솟았다** — 켜진 칸 셋이
       둥근 네모가 아니라 **위로 꼬리가 난 「b」 모양**으로 보였고, 그 꼬리가 위 칸(흰색)까지
       올라가 아래 「간격」 주석의 근거가 통째로 무너져 있었다.
       🔑 세 변의 소비 길이는 `wd + ht + wd` 다. 마지막 분기는 남은 `ht` 만큼만 올라간다. */
    const perim = (x, y, wd, ht, p) => {
      const P = 2 * (wd + ht);
      p = ((p % P) + P) % P;
      if (p < wd) return [x + p, y];          // 위 변 →
      p -= wd;
      if (p < ht) return [x + wd, y + p];     // 오른 변 ↓
      p -= ht;
      if (p < wd) return [x + wd - p, y + ht];// 아래 변 ←
      p -= wd;
      return [x, y + ht - p];                 // 왼 변 ↑
    };

    const c0 = cue(spec.countCue ?? 0, 0.15, 0.60);
    const st = ease(clamp(c0 / 0.10));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const cols = spec.cols ?? COLS, rows = spec.rows ?? ROWS;
    const lit = spec.litIdx ?? [];
    const sole = spec.soleIdx ?? lit[0];

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ⏱ 소멸은 `cue` 가 아니라 `since()`(그 자막이 시작된 뒤 흐른 초) 위에 세웠다.
       `cue` 창은 `lead + rel × span` 이라 실측 `rel` 이 있어야 시각이 나오고, 그러면
       `sfx.delayMs` 를 실측 뒤 다시 풀어야 한다. `since()` 는 자막 시작을 0 으로 두므로
       **자막 길이가 바뀌어도, pauseAfter 를 줄여도 안 움직인다.** */
    const s1 = Math.max(0, since(spec.soleCue ?? 1));
    const fall = ease(clamp((s1 - 0.25) / 0.45));

    /* ── 서른 칸 ────────────────────────────────────── */
    const march = t * 96;
    for (let i = 0; i < cols * rows; i++) {
      const col = i % cols, row = Math.floor(i / cols);
      const bx = X0 + col * (CW + GX), by = Y0 + row * (CH + GY);
      const brt = 0.8 * Math.sin(t * 6.6 + i * 0.83);       // 상시 숨 ±0.8px
      const isLit = lit.includes(i);
      const drops = isLit && i !== sole;

      let a = st * ease(clamp((c0 - 0.04 - (col + row) * 0.012) / 0.14));
      let dy = 0;
      if (drops) { a *= (1 - fall); dy = 26 * fall; }
      if (a <= 0.01) continue;

      const x = bx - brt, y = by - brt + dy;
      const wd = CW + brt * 2, ht = CH + brt * 2;

      if (isLit) {
        /* 🔴 켜지기 전에는 이 칸도 **흰 테두리**다. 흰색과 강조색이 같은 좌표를
           나눠 쓰므로 여기서는 좌표로 못 가른다 — **시간으로 갈랐다.**
           흰 테두리는 c0 0.36 에 알파 0 이 되고 강조색은 c0 0.38 에야 켜진다.
           둘이 동시에 0보다 큰 프레임이 없다. */
        const offK = 1 - ease(clamp((c0 - 0.26) / 0.10));
        const litK = ease(clamp((c0 - 0.38) / 0.16));
        if (offK > 0.01) {
          ctx.save();
          ctx.globalAlpha = a * offK * 0.85;
          ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
          roundRect(ctx, x, y, wd, ht, 4); ctx.stroke();
          ctx.restore();
        }
        if (litK > 0.01) {
          const puff = 0.5 + 0.5 * Math.sin(t * 5.5 + i);
          ctx.save();
          ctx.globalAlpha = a * litK;
          /* 🔴 글로우 최대를 7 → **5** 로 내렸다. 1차본 주석이 「세로 9px 남는다」고 적었는데
             그 산수가 획 반폭(1.5 × 2)과 숨(0.8 × 2)을 빼먹은 값이었다 — 실제 여유는 4.4px
             이었다. 아래 「간격」 주석에 정직한 산수를 다시 적었고, 그 여유를 6.4px 로 넓히려고
             글로우를 한 칸 줄였다. 맥동은 그대로라 정적 기여는 안 줄어든다. */
          setShadow(ctx, FAIL_GLOW, 3 + 2 * puff);
          ctx.strokeStyle = tone('fail'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
          roundRect(ctx, x, y, wd, ht, 4); ctx.stroke();
          clearShadow(ctx);
          /* 테두리 훑기 — 마디 셋이 둘레를 돈다(정적 구간 대책 ②) */
          const P = 2 * (wd + ht);
          ctx.fillStyle = tone('fail');
          for (let s = 0; s < 3; s++) {
            for (let d = 0; d < 16; d += 2) {
              const [px, py] = perim(x, y, wd, ht, march + s * P / 3 + d);
              ctx.fillRect(px - 2, py - 2, 4, 4);
            }
          }
          ctx.restore();
        }
      } else {
        ctx.save();
        ctx.globalAlpha = a * 0.85;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
        roundRect(ctx, x, y, wd, ht, 4); ctx.stroke();
        ctx.restore();
      }
    }

    /* ── 라벨 셋 ────────────────────────────────────────
       🔴 아래 자리의 두 라벨은 **시간으로 갈랐다** — 같은 y 를 나눠 쓰므로
       하나가 완전히 꺼진 뒤에야 다음이 켜진다(둘 다 0 인 구간 0.06초). */
    if (spec.allLabel) {
      ctext(spec.allLabel, X0 + (cols * (CW + GX) - GX) / 2, LAB_A_Y,
        900, 15, tone('ink'), 300, st);
    }
    if (spec.litLabel) {
      const outB = ease(clamp((s1 - 0.05) / 0.23));
      ctext(spec.litLabel, X0 + (cols * (CW + GX) - GX) / 2, LAB_B_Y,
        900, 15, tone('fail'), 300,
        st * ease(clamp((c0 - 0.38) / 0.16)) * (1 - outB));
    }
    if (spec.soleLabel) {
      ctext(spec.soleLabel, X0 + (cols * (CW + GX) - GX) / 2, LAB_B_Y,
        900, 15, tone('fail'), 300, st * ease(clamp((s1 - 0.34) / 0.34)));
    }

    ctx.textAlign = 'left';
  }
};
