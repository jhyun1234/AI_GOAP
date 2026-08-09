import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* refusal — 이 영상의 논지. 촌장의 명령이 주민 앞에서 되돌아온다.
   거부를 '벽'이 아니라 **되돌아오는 토큰**으로 그린다. 벽으로 그리면 주민이 막힌
   물건처럼 보이는데, 이 게임에서 거부하는 쪽은 주민 본인이라서 토큰이 튕겨야 맞다.
   보상(REWARD)이 붙은 토큰만 통과한다 — 원문의 '보상을 걸고 다시 설득'이 그 그림이다.

   🔴 `episodes/ep04s-1/kinds/refusal.js` 와 이름이 같고 소재도 같지만 **다른 그림이다**
   (검수 의견 20 에 대한 답). 갈리는 축이 셋이다:
     ① 묻는 것이 다르다. ep04s-1 은 **왜 거부하는가** — 주민 옆에 상태 판정 행 둘
        (피로·굶주림 문턱)을 세우고 말풍선 둘을 띄우는 **판정 표**가 중심이다.
        여기는 **거부 다음에 무엇이 있는가** — 판정 행이 아예 없고, 대신 보상 층과
        상태 띠(ORDER → REFUSED → +REWARD → ACCEPTED)가 중심이다.
     ② 횟수가 다르다. ep04s-1 의 칩은 **한 번** 다가왔다 밀려나고 끝난다(사건).
        여기 토큰은 1.9초 주기로 **끝없이** 되돌아온다 — 거부가 사건이 아니라 규칙이라는
        뜻이고, 그래서 이 샷은 자막이 넷째 줄로 넘어가도 계속 돈다.
     ③ 무대가 다르다. ep04s-1 은 세로 한 축에 주민을 고정하고 위에서 아래로 읽힌다.
        여기는 가로 축 양 끝에 촌장과 주민을 세워 **둘 사이의 왕복**으로 읽힌다. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const M = 16;
    const cy = h * 0.50;
    const ax = M + 52;                  // 촌장
    const bx = w - M - 66;              // 주민
    const gate = bx - 46;               // 거부가 일어나는 자리

    const persuade = ease(cue(2));      // 보상이 걸린 정도
    const thesis = ease(cue(0));        // "거부할 수 있다"

    // ── 양 끝 기둥 ────────────────────────────────
    ctx.lineWidth = 3;
    ctx.strokeStyle = tone('ink');
    roundRect(ctx, ax - 34, cy - 30, 68, 60, 5); ctx.stroke();
    ctx.font = disp(800, 12); ctx.fillStyle = tone('ink');
    let tw = ctx.measureText('CHIEF').width;
    ctx.fillText('CHIEF', ax - tw / 2, cy + 4);
    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    tw = ctx.measureText('ORDER').width;
    ctx.fillText('ORDER', ax - tw / 2, cy + 19);

    ctx.strokeStyle = persuade > 0.6 ? tone('accent') : tone('ink');
    ctx.beginPath(); ctx.arc(bx, cy, 30, 0, Math.PI * 2); ctx.stroke();
    ctx.font = disp(800, 15);
    ctx.fillStyle = persuade > 0.6 ? tone('accent') : tone('ink');
    const face = persuade > 0.6 ? '^-^' : 'ㅡ_ㅡ';
    tw = ctx.measureText(face).width;
    ctx.fillText(face, bx - tw / 2, cy + 5);
    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    tw = ctx.measureText('VILLAGER').width;
    ctx.fillText('VILLAGER', bx - tw / 2, cy + 46);

    // ── 궤도 ──────────────────────────────────────
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(ax + 40, cy); ctx.lineTo(bx - 34, cy); ctx.stroke();

    // 거부선 — 설득되면 열려서 짧아진다
    const gh = lerp(34, 4, persuade);
    ctx.strokeStyle = persuade > 0.6 ? tone('track') : tone('ink');
    ctx.beginPath(); ctx.moveTo(gate, cy - gh); ctx.lineTo(gate, cy + gh); ctx.stroke();

    // ── 명령 토큰 — 1.9초 주기로 계속 날아간다 ──────
    const P = 1.9, k = (t % P) / P;
    const x0 = ax + 44, xEnd = bx - 34;
    let tx, back = false;
    if (persuade > 0.6) {
      tx = lerp(x0, xEnd, ease(k));                 // 통과
    } else if (k < 0.5) {
      tx = lerp(x0, gate - 12, ease(k / 0.5));      // 가다가
    } else {
      tx = lerp(gate - 12, x0, ease((k - 0.5) / 0.5)); // 되돌아온다
      back = true;
    }
    const r = 11;
    ctx.fillStyle = persuade > 0.6 ? tone('accent') : tone('ink');
    ctx.beginPath(); ctx.arc(tx, cy, r, 0, Math.PI * 2); ctx.fill();
    // 진행 방향 꼬리
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(tx + (back ? r + 4 : -r - 4), cy);
    ctx.lineTo(tx + (back ? r + 22 : -r - 22), cy);
    ctx.stroke();

    // 보상 칩 — 설득 뒤에는 토큰에 붙어 같이 간다
    if (persuade > 0.05) {
      ctx.font = disp(800, 10);
      const lab = 'REWARD';
      const lw = ctx.measureText(lab).width;
      const bxp = clamp((tx - lw / 2 - 7), M, w - M - lw - 14);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, bxp, cy - 40, lw + 14, 19, 3); ctx.stroke();
      ctx.fillStyle = tone('accent');
      ctx.fillText(lab, bxp + 7, cy - 26);
    }

    // ── 주민이 거부할 수 있는 이유 ──────────────────
    const planner = ease(cue(3));
    if (planner > 0.05) {
      ctx.globalAlpha = clamp(planner);
      ctx.font = mono(700, 10);
      const lab = 'OWN PLANNER';
      const lw = ctx.measureText(lab).width;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, bx - lw / 2 - 8, cy + 54, lw + 16, 19, 3); ctx.stroke();
      ctx.fillStyle = tone('accent');
      ctx.fillText(lab, bx - lw / 2, cy + 67);
      ctx.globalAlpha = 1;
    }

    // ── 상태 띠 ──────────────────────────────────
    // 🔴 자막을 화면에 옮겨 적지 않는다. 여기 있는 것은 문장이 아니라 상태 이름이다.
    const states = ['ORDER', 'REFUSED', '+REWARD', 'ACCEPTED'];
    const at = persuade > 0.6 ? 3 : persuade > 0.15 ? 2 : (thesis > 0.35 ? 1 : 0);
    ctx.font = disp(800, 12);
    let sx = M + 4;
    for (let i = 0; i < states.length; i++) {
      const sw = ctx.measureText(states[i]).width + 18;
      const on = i === at;
      ctx.globalAlpha = i <= at ? 1 : 0.34;
      ctx.strokeStyle = on ? tone('accent') : tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, sx, h - 34, sw, 24, 3); ctx.stroke();
      ctx.fillStyle = on ? tone('accent') : tone('sub');
      ctx.fillText(states[i], sx + 9, h - 17);
      ctx.globalAlpha = 1;
      if (i < states.length - 1) {
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(sx + sw + 4, h - 22); ctx.lineTo(sx + sw + 16, h - 22);
        ctx.stroke();
      }
      sx += sw + 20;
    }
  },
};
