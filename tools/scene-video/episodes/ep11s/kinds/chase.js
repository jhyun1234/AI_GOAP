import {
  disp, ease, clamp, lerp, rnd,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* chase — 같은 속도로 쫓으면 간격은 상수다. 그리고 타이머가 닫히면 품삯이 없어진다.

   원문: "그런데 목수와 의뢰인의 걷는 속도가 똑같습니다. 앞서 걷는 사람을 같은 속도로 쫓으면,
   간격은 영원히 좁혀지지 않습니다." / "그렇게 60초가 지나면 보고 심부름은 타임아웃으로
   취소됐고, 그 순간 목수가 받아야 할 보상이 그냥 사라졌습니다."

   🔴 이 회차 네 샷이 공유하는 기본 도형은 **두 표식 사이의 간격**이다. 여기서 그 간격을
   상수로 세워 두고, 뒤 샷들이 같은 간격을 재조준(S2)·해제(S3)·소멸(S4)의 축으로 쓴다.
   막대도 그래프도 아니다 — 이 글의 사건이 「거리가 안 줄어든다」이기 때문이다.

   ① walkCue — 트랙이 깔리고 목수·의뢰인이 걷는다. **표식은 화면에 고정하고 바닥이 흐른다.**
      쫓아가는 그림인데 표식을 움직이면 화면 밖으로 나가 잘리고, 무엇보다 「간격이 안 변한다」가
      안 읽힌다. 흐르는 것은 발밑이고, 안 변하는 것은 둘 사이 화살표 길이다.
      두 표식의 위아래 바운스는 **같은 위상**이다 — 그게 같은 속도다.
   ② timeoutCue — 위쪽 60초 바가 채워지고, 꽉 차는 순간 목수 머리 위 품삯 캡슐이
      아래로 빠지며 조각으로 흩어진다. 조각이 다 꺼진 뒤에야 빈 점선 윤곽이 남는다.

   🔴 흰 것과 강조색을 같은 픽셀에 겹치지 않는다. 떨어지는 캡슐(강조색)은 46px 만 내려가고
   그전에 알파가 0 이 되며, 조각도 세로로 30px 까지만 퍼진다 — 흰 표식(y 164 위)에 안 닿는다.
   빈 윤곽(흰 계열)은 조각이 완전히 꺼진 뒤(fall 0.70)에 켜진다.

   🔴 화면에 올린 수는 **60** 하나뿐이고 원문에 두 번 실재한다. 간격·보상액·발생 빈도는
   원문에 값이 없어 화살표 길이로만 말한다.

   계속 도는 것 = 트랙 점선 흐름 · 바닥 눈금 흐름 · 두 표식의 같은 위상 바운스 ·
   품삯 캡슐 **바깥** 점선 후광의 흐름(안쪽이 아니다 — 아래 반려 수정 참조). */

const BAR_Y = 56, BAR_H = 12;
const CAP_Y = 116, CAP_W = 96, CAP_H = 28;
const TRK_Y = 186;
const TK0 = 198, TK1 = 208;
const NAME_Y = 226;
const GAP_L = 250, GAP_A = 264;

function walker(ctx, x, bob) {
  ctx.fillStyle = tone('ink');
  ctx.beginPath(); ctx.arc(x, TRK_Y - 13 + bob, 9, 0, Math.PI * 2); ctx.fill();
  ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(x, TRK_Y - 5 + bob); ctx.lineTo(x, TRK_Y - 1);
  ctx.stroke();
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

    const carpX = w * 0.30, cliX = w * 0.66;
    const c0 = cue(spec.walkCue ?? 0, 0.15, 0.7);
    /* span 0.9 — 타임아웃 문장이 길어서 0.7 이면 「사라졌어요」를 말하기 한참 전에
       그림이 끝나 버린다. 창을 넓혀 말과 그림이 같이 도착하게 한다. */
    const c1 = cue(spec.timeoutCue ?? 1, 0.15, 0.9);

    const appear = ease(clamp(c0 / 0.5));
    const timer = ease(clamp(c1 / 0.72));
    const fall = ease(clamp((c1 - 0.72) / 0.26));
    const bob = Math.sin(t * 7.4) * 3;

    if (appear <= 0.004) { ctx.textAlign = 'left'; return; }

    /* ── 60초 타이머 ──────────────────────────────── */
    const bx0 = 44, bx1 = w - 44;
    ctx.globalAlpha = appear;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, bx0, BAR_Y, bx1 - bx0, BAR_H, BAR_H / 2); ctx.stroke();
    if (timer > 0.004) {
      ctx.save();
      roundRect(ctx, bx0, BAR_Y, bx1 - bx0, BAR_H, BAR_H / 2); ctx.clip();
      ctx.fillStyle = tone('ink');
      ctx.fillRect(bx0, BAR_Y, (bx1 - bx0) * timer, BAR_H);
      ctx.restore();
    }
    if (spec.timerLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(900, fit(spec.timerLabel, 900, 11, 150, 8));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.timerLabel, bx0, BAR_Y - 8);
    }
    ctx.globalAlpha = 1;

    /* ── 트랙과 바닥 눈금 — 발밑이 흐른다 ──────────── */
    ctx.globalAlpha = appear;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([14, 10]);
    ctx.lineDashOffset = (t * 34) % 24;
    ctx.beginPath(); ctx.moveTo(18, TRK_Y); ctx.lineTo(w - 18, TRK_Y); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;

    const step = 34, sl = (t * 32) % step;
    for (let i = -1; i * step + 18 - sl < w - 14; i++) {
      const x = 18 + i * step - sl;
      if (x < 16 || x > w - 16) continue;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(x, TK0); ctx.lineTo(x - 6, TK1); ctx.stroke();
    }
    ctx.globalAlpha = 1;

    /* ── 두 표식 — 같은 위상으로 흔들린다 = 같은 속도 ─ */
    ctx.globalAlpha = appear;
    walker(ctx, carpX, bob);
    walker(ctx, cliX, bob);
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('sub');
    if (spec.carpLabel) {
      ctx.font = disp(900, fit(spec.carpLabel, 900, 11, 110, 8));
      ctx.fillText(spec.carpLabel, carpX, NAME_Y);
    }
    if (spec.cliLabel) {
      ctx.font = disp(900, fit(spec.cliLabel, 900, 11, 110, 8));
      ctx.fillText(spec.cliLabel, cliX, NAME_Y);
    }
    ctx.globalAlpha = 1;

    /* ── 간격 — 길이가 한 번도 안 변한다 ───────────── */
    const gA = ease(clamp((c0 - 0.24) / 0.5)) * appear;
    if (gA > 0.02) {
      ctx.globalAlpha = gA;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(carpX, GAP_A); ctx.lineTo(cliX, GAP_A); ctx.stroke();
      for (const [x, s] of [[carpX, 1], [cliX, -1]]) {
        ctx.beginPath(); ctx.moveTo(x, GAP_A - 6); ctx.lineTo(x, GAP_A + 6); ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(x + s * 9, GAP_A - 5); ctx.lineTo(x, GAP_A); ctx.lineTo(x + s * 9, GAP_A + 5);
        ctx.stroke();
      }
      if (spec.gapLabel) {
        ctx.textAlign = 'center';
        ctx.font = disp(900, fit(spec.gapLabel, 900, 12, 90, 8));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.gapLabel, (carpX + cliX) / 2, GAP_L);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 품삯 캡슐 — 타이머가 닫히면 빠진다 ────────── */
    const capA = appear * clamp(1 - fall * 1.5);
    if (capA > 0.02) {
      const cy = CAP_Y + 46 * fall;
      const x0 = carpX - CAP_W / 2;
      /* 🔴 **계속 도는 것을 캡슐 바깥으로 뺐다** (2026-08-08 반려 수정).
         초안은 캡슐 **안쪽 전면**에 강조색 빗금을 흘렸는데, 같은 강조색 라벨(REWARD)을
         빗금이 계속 훑어 글자가 안 읽혔다. 0~9.6초 내내 그 상태였다.
         🔑 이건 내가 ledger.js 주석에 스스로 적어 둔 규칙(「빗금을 글자 아래에 깔지 않는다」)
         위반이고, 같은 회차 안에 정답이 둘 있었다(S3 원장 카드 · S4 아래 빚 카드는 빗금을
         글자 없는 바닥 띠에만 넣어 선명하다). 캡슐은 28px 라 그 띠를 넣을 자리가 없으므로
         움직임을 **테두리 밖 점선 후광**으로 옮긴다 — 글자를 한 번도 안 지나간다. */
      ctx.globalAlpha = capA * 0.55;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([10, 8]);
      ctx.lineDashOffset = -(t * 26) % 18;
      roundRect(ctx, x0 - 5, cy - CAP_H / 2 - 3, CAP_W + 10, CAP_H + 6, (CAP_H + 6) / 2);
      ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = capA;
      setShadow(ctx, GLOW, 14);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, x0, cy - CAP_H / 2, CAP_W, CAP_H, CAP_H / 2); ctx.stroke();
      clearShadow(ctx);
      if (spec.rewardLabel) {
        ctx.textAlign = 'center';
        ctx.font = disp(900, fit(spec.rewardLabel, 900, 15, CAP_W - 26, 9));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.rewardLabel, carpX, cy + 5);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 흩어지는 조각 ────────────────────────────── */
    const frA = clamp(fall * 4) * clamp((0.68 - fall) * 4);
    if (frA > 0.02) {
      ctx.globalAlpha = frA;
      ctx.fillStyle = tone('accent');
      for (let i = 0; i < 7; i++) {
        const a0 = (rnd(i * 31 + 5) - 0.5) * 2.2;
        const d = (26 + rnd(i * 17 + 9) * 34) * clamp(fall * 1.4);
        const fx = carpX + Math.sin(a0) * d;
        const fy = CAP_Y + Math.cos(a0) * d * 0.5;
        ctx.fillRect(fx - 2.5, fy - 2.5, 5, 5);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 빈 자리 — 조각이 다 꺼진 뒤에만 켠다 ──────── */
    const gh = ease(clamp((fall - 0.70) / 0.30));
    if (gh > 0.02) {
      ctx.globalAlpha = gh * 0.7;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.setLineDash([9, 8]);
      ctx.lineDashOffset = -(t * 16) % 17;
      roundRect(ctx, carpX - CAP_W / 2, CAP_Y - CAP_H / 2, CAP_W, CAP_H, CAP_H / 2);
      ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
