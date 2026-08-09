import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* costfloor — 이 회차에서 유일하게 두 번 쓰는 그림(spec.phase 0 → 1).
   같은 판이어야 "기준선이 무너진 자리에 문이 섰다"가 보이기 때문이다.

   phase 0 (S2, 콜드 오픈): 액션 비용 막대가 늘어선 판에 값 1짜리가 끼어들면
     바닥 기준선이 그 카드까지 끌려 내려가고, 오른쪽 PLAN 칸이 차례로 빈다.
   phase 1 (S19, 회수): 같은 판. 이번엔 기준선 위에 굵은 GATE 막대가 서 있고
     값 1짜리 카드가 내려오다 튕겨 나간다. PLAN 칸은 안 빈다.

   막대 값은 실제 에셋에서 읽었다 — Assets/M0Config/Actions/ 의 BaseCost:
   MineStone 12 · HarvestCrop 8 · BuildCampfire 20 · EatCookedFood 5 ·
   BuildHouse 15 · CookMeal 6, 그리고 TendInjured 8(1→8 로 고친 그 값).
   기준선 5 와 끼어든 1 은 M10 글 50행. 그 밖의 수는 그리지 않는다.

   🔴 3차 개정 (검수 R-2a·R-2b): 두 라벨이 같은 자리에 포개졌다.
   `MIN COST n` 은 (M, fy-7) 에, `GATE` 는 (x0-4, gateY-8) 에 있었는데
   phase 1 에서 kGate=1 이면 `cur = 8` 이라 **fy 와 gateY 가 정의상 같은 값**이 된다
   — 좌표가 아니라 구조가 겹침을 강제하고 있었다(증거 `build/stills/550s.png`:
   「MG̶ATE̶N C̶OST 8 8」). phase 0 에서는 같은 `MIN COST 1` 이 x16 에서 시작해
   막대 0·1 의 획을 관통했다(`48s.png`).
   → **두 라벨을 막대판 오른쪽 바깥(barRight + 16)의 한 열로 옮기고, 하나는 자기 선
   위(gateY − 10) 다른 하나는 자기 선 아래(fy + 14)로 갈랐다.** phase 1 에서는 언제나
   `gateY <= fy`(비용이 클수록 y 가 작다) 이므로 두 baseline 의 간격은 **최소 24px 로
   보장**된다 — 값이 어떻게 움직여도 겹칠 수 없다. 막대판 밖이라 획 관통도 사라진다. */

const BARS = [12, 8, 20, 5, 15, 6];
const MAXC = 20;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const P1 = (spec.phase | 0) === 1;
    const M = 16;

    // ── 판 좌표 ────────────────────────────────────
    const base = h - 66;                 // 막대 바닥
    const top = 44;                      // 막대 최대 높이 기준선
    const span = base - top;             // 20 에 해당하는 높이
    const yOf = c => base - (c / MAXC) * span;
    const slotW = 44, barW = 26, x0 = M + 8;
    const barRight = x0 + 6 * slotW + barW;   // 막대판 오른쪽 끝
    const labX = barRight + 16;               // 두 선 라벨이 함께 사는 열 (판 바깥)

    // ── 진행률 ────────────────────────────────────
    const kNew = ease(P1 ? 1 : cue(3));        // 1짜리 카드가 판에 든다
    const kFloor = ease(P1 ? 1 : cue(4));      // 기준선이 끌려 내려간다
    const kBreak = ease(P1 ? 0 : cue(5));      // PLAN 칸이 빈다
    const kFix = P1 ? ease(cue(1)) : 0;        // 1 → 8
    const kGate = P1 ? ease(cue(3)) : 0;       // 게이트가 선다
    const kPlan = P1 ? ease(cue(4)) : 0;       // PLAN 칸이 다시 찬다

    /* ── 계속 도는 것 : 판을 훑는 탐색 띠 (플래너는 언제나 뒤지고 있다) ──
       🔴 2차 개정 (검수 C6): 1차본은 3px 선이라 프레임 간 변화량이 0.00051 로
       문턱(0.0008) 아래였다. 폭 46px 의 **면**으로 바꿨다 — 통과 선례인 `antipattern`
       의 40px 점검 띠와 같은 처방이다. 막대·게이트보다 **먼저** 그려서 강조색 픽셀 위에
       반투명 흰색이 얹히지 않게 한다. */
    {
      const PR = 3.2, k = (t % PR) / PR;
      const sx = lerp(x0 - 18, x0 + 6 * slotW + barW + 18, k);
      ctx.fillStyle = tone('track');
      ctx.fillRect(sx - 23, top - 10, 46, base + 14 - (top - 10));
    }

    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('ACTION COST', M, 24);

    /* 게이트·기준선이 앉는 높이. 막대 루프보다 **위**에서 잡는다 (4차 정정 C14) —
       값 라벨이 이 선을 피해야 하기 때문이다.
       🔴 C14: phase 1 에서 게이트 막대(lineWidth 6 → gateY±3)와 기준선(±1.5)이 y 125.6 에
       서는데, CookMeal(6) 막대의 값 라벨 baseline 이 `yOf(6) − 6 = 133.2` 라 **글자 위쪽
       2.4px 이 막대 안에 들어가 `6` 의 갈고리가 지워지고 `o` 로 읽혔다**(증거 `550s.png`).
       1·2차 스틸과 픽셀이 같은, 처음부터 있던 자리다.
       → phase 1 에서만 값 라벨 baseline 을 `gateY − 12` 위로 물린다. 선보다 낮게 내려갈
       수 없으므로 어떤 막대 값에서도 선이 글자를 먹지 못한다. phase 0 은 기준선이 5→1 로
       **내려가** 이 자리에 선이 없으므로 손대지 않는다(`20s`·`48s` 깨끗). */
    const gateY = yOf(P1 ? 8 : 5);

    // ── 기존 막대 여섯 ─────────────────────────────
    for (let i = 0; i < BARS.length; i++) {
      const c = BARS[i], bx = x0 + i * slotW, by = yOf(c);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, bx, by, barW, base - by, 3); ctx.stroke();
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      const s = String(c), sw = ctx.measureText(s).width;
      const ly = P1 ? Math.min(by - 6, gateY - 12) : by - 6;
      ctx.fillText(s, bx + (barW - sw) / 2, ly);
    }

    // ── 새로 들어오는 액션 ─────────────────────────
    {
      const bx = x0 + 6 * slotW;
      const val = P1 ? lerp(1, 8, kFix) : 1;
      const by = yOf(val);
      const drop = (1 - kNew) * -46;                 // 위에서 내려온다
      // gateY 는 위(막대 루프 앞)에서 잡았다 — C14 참조
      // phase 1 : 게이트가 서면 카드가 튕긴다 (자막과 무관하게 계속)
      let bounceY = 0;
      if (P1 && kGate > 0.5) {
        const BR = 2.4, k = (t % BR) / BR;
        bounceY = k < 0.55 ? lerp(-52, 0, ease(k / 0.55)) : lerp(0, -52, ease((k - 0.55) / 0.45));
      }
      ctx.save();
      ctx.globalAlpha = clamp(0.25 + 0.75 * kNew);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, bx, by + drop + bounceY, barW, Math.max(6, base - by), 3); ctx.stroke();
      ctx.restore();
      ctx.font = mono(700, 11); ctx.fillStyle = tone('accent');
      const s = P1 ? (kFix > 0.5 ? '8' : '1') : '1';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, bx + (barW - sw) / 2, by + drop + bounceY - 6);
      ctx.font = mono(700, 9); ctx.fillStyle = tone('sub');
      ctx.fillText('TendInjured', bx - 22, base + 16);
      if (P1 && kGate > 0.02) {
        ctx.save(); ctx.globalAlpha = kGate;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 6;
        ctx.beginPath(); ctx.moveTo(x0 - 6, gateY); ctx.lineTo(bx + barW + 8, gateY); ctx.stroke();
        ctx.font = mono(700, 10); ctx.fillStyle = tone('accent');
        ctx.fillText('GATE', labX, gateY - 10);   // 자기 선 **위** (3차 개정 R-2a)
        ctx.restore();
      }
    }

    // ── 기준선 ────────────────────────────────────
    {
      const from = 5, to = P1 ? 8 : 1;
      const cur = P1 ? lerp(5, 8, kGate) : lerp(from, to, kFloor);
      const fy = yOf(cur);
      ctx.setLineDash([9, 6]);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(M, fy); ctx.lineTo(barRight + 10, fy); ctx.stroke();
      ctx.setLineDash([]);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('ink');
      const s = `MIN COST ${Math.round(cur)}`;
      ctx.fillText(s, labX, fy + 14);          // 자기 선 **아래** (3차 개정 R-2a·R-2b)
    }

    // ── 오른쪽 : PLAN 칸 넷 ─────────────────────────
    {
      const px = w - M - 232, py = 78, cw = 52, ch = 30, gap = 8;
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('PLAN', px, py - 10);
      for (let i = 0; i < 4; i++) {
        const cx = px + i * (cw + gap);
        const kOut = clamp((kBreak - i * 0.14) / 0.4);
        const kIn = P1 ? clamp((kPlan - i * 0.14) / 0.4) : 1;
        const alive = P1 ? kIn : 1 - kOut;
        ctx.strokeStyle = alive > 0.5 ? tone('accent') : tone('track');
        ctx.lineWidth = 3;
        roundRect(ctx, cx, py, cw, ch, 3); ctx.stroke();
        if (alive > 0.5) {
          ctx.fillStyle = tone('accent');
          ctx.fillRect(cx + 7, py + ch / 2 - 2, cw - 14, 4);
        } else {
          ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
          ctx.beginPath();
          ctx.moveTo(cx + 14, py + 9); ctx.lineTo(cx + cw - 14, py + ch - 9);
          ctx.moveTo(cx + cw - 14, py + 9); ctx.lineTo(cx + 14, py + ch - 9);
          ctx.stroke();
        }
      }
      /* 판정 줄 — 3차 개정: `py + ch + 30`(y 138) 이 새로 옮긴 `MIN COST n`
         (phase 1 에서 y 139.6)과 같은 가로줄에 놓여 두 문자열이 나란히 붙어 보였다.
         x 는 안 겹치지만(390 vs 401) 한 줄로 읽히므로 18px 내려 행을 갈랐다. */
      ctx.font = mono(700, 12);
      if (!P1 && kBreak > 0.4) {
        ctx.save(); ctx.globalAlpha = clamp((kBreak - 0.4) / 0.4);
        ctx.fillStyle = tone('ink');
        ctx.fillText('NoSolutionFound', px, py + ch + 48);
        ctx.restore();
      }
      if (P1 && kGate > 0.4) {
        ctx.save(); ctx.globalAlpha = clamp((kGate - 0.4) / 0.4);
        ctx.fillStyle = tone('accent');
        ctx.fillText('TEST FAILS FIRST', px, py + ch + 48);
        ctx.restore();
      }
    }
  },
};
