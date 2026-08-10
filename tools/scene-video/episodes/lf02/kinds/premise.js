import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* premise — 명세가 딛고 선 전제 일곱 칸을 코드가 하나씩 열어 본다.
   그중 둘이 X 로 뒤집히고, 그 두 칸 위에 얹혀 있던 설계 층이 함께 기운다.
   숫자는 M15 글 25·70행의 「일곱 개 중 둘」뿐이다 — 어느 전제였는지는 원문이
   본문에 안 적었으므로 칸에 이름을 붙이지 않는다(번호만).
   연속 모션 = 칸을 여는 코드 커서 대괄호. 확인은 명세마다 매번 다시 도는 절차다.

   🔴 **v2 — v1 이 남긴 지뢰 8을 구조로 닫았다.**
   v1 은 세로 기둥 일곱 개가 라벨 줄(`PREMISE x7` · 판정 줄, baseline `cy - 10`)을
   그대로 관통했고, **일곱 개가 전부 글자 사이 공백에 떨어진 덕분에** 판독이 살아 있었다
   (writer.md V-2 — 「지금은 운이 좋았다」). 문자열이 한 글자만 바뀌면 획을 먹는데,
   라벨을 한국어로 옮기는 이번 작업이 정확히 그 조건이었다.
   → **기둥이 지나는 띠(y 78 ~ cy)에서 글자를 전부 치웠다.** 머리 라벨은 위 머리줄(y 26),
   판정 줄은 칸 아래(`cy + ch + 20`)로 옮겼다. 좌표를 맞추는 대신 **글자와 기둥이
   같은 y 구간을 쓰지 않게** 해서, 앞으로 문자열이 어떻게 바뀌어도 다시 안 뚫린다.

   🔴 v2 — 설계 층 안의 「이 파일은 한 건짜리였네」를 지웠다. 그 문자열은 자막을 그대로
   복사한 것이었다(v1 검수 C8 이 `identity` 에서 잡은 것과 같은 종류). 대신 마지막 자막에서
   **층이 더 기울고 흐려지는 것**으로 갚는다 — 글자 없이 읽히는 쪽이 `ADR-V25-15` 다. */

const N = 7;
const WRONG = [4, 6];   // 일곱 칸 중 두 칸 (자리는 임의 — 수만 원문이다)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kOpen = ease(cue(1));    // 하나씩 코드를 열어 확인
    const kFall = ease(cue(2));    // 전제 하나가 틀리면 그 위가 무너진다
    const kX = ease(cue(3));       // 둘이 틀렸다
    const kAfter = ease(cue(4));   // 확인 없이 갔으면

    const gap = 7;
    const cw = (w - M * 2 - gap * (N - 1)) / N;
    const cy = h - 104, ch = 56;

    // 머리줄 — 기둥이 지나는 띠 **밖**이다 (v2)
    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('전제 7개', M, 26);

    // ── 위 : 설계 층 ───────────────────────────────
    {
      /* 🔴 기울기는 v1 값(0.055)에서 더 키우지 않는다 — 판이 가로로 563px 이라
         0.03 만 더해도 왼쪽 끝이 y 20 까지 올라가 캔버스 머리(엔진 HUD 진행 막대가
         y 13~17 을 덮는다)에 닿는다. kAfter 는 **기울기 대신 가라앉음과 흐려짐**으로 받는다. */
      const dy = 44 + 6 * ease(kAfter), dh = 34;
      const tilt = lerp(0, 0.055, ease(kX));
      ctx.save();
      ctx.globalAlpha = clamp(1 - 0.4 * kAfter);
      ctx.translate(w / 2, dy + dh / 2);
      ctx.rotate(tilt);
      ctx.translate(-w / 2, -(dy + dh / 2));
      ctx.strokeStyle = kX > 0.4 ? tone('ink') : tone('accent');
      ctx.lineWidth = 3;
      roundRect(ctx, M + 18, dy, w - M * 2 - 36, dh, 3); ctx.stroke();
      ctx.font = disp(900, 13);
      ctx.fillStyle = kX > 0.4 ? tone('ink') : tone('accent');
      ctx.fillText('설계', M + 30, dy + 23);
      ctx.restore();
    }

    // 설계와 전제를 잇는 기둥 (이 띠 안에는 글자를 두지 않는다 — v2)
    for (let i = 0; i < N; i++) {
      const cx = M + i * (cw + gap) + cw / 2;
      const broken = WRONG.includes(i) && kX > 0.4;
      ctx.strokeStyle = broken ? tone('track') : tone('sub');
      ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(cx, 78);
      ctx.lineTo(cx, broken ? 92 : cy);
      ctx.stroke();
    }

    // ── 아래 : 전제 일곱 칸 ────────────────────────
    for (let i = 0; i < N; i++) {
      const cx = M + i * (cw + gap);
      const kIn = clamp((kOpen - i * 0.1) / 0.42);
      const bad = WRONG.includes(i);
      const kBad = bad ? clamp((kX - (i === WRONG[0] ? 0 : 0.22)) / 0.45) : 0;

      ctx.strokeStyle = kIn > 0.1 ? (kBad > 0.4 ? tone('ink') : tone('accent')) : tone('track');
      ctx.lineWidth = 3;
      roundRect(ctx, cx, cy, cw, ch, 3); ctx.stroke();

      ctx.font = mono(700, 10);
      ctx.fillStyle = tone('sub');
      ctx.fillText(String(i + 1), cx + 6, cy + 15);

      if (kIn > 0.15 && kBad < 0.4) {
        // 확인됨 : 체크
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        const mx = cx + cw / 2, my = cy + ch / 2 + 6;
        ctx.beginPath();
        ctx.moveTo(mx - 10, my - 2); ctx.lineTo(mx - 3, my + 6); ctx.lineTo(mx + 11, my - 11);
        ctx.stroke();
      }
      if (kBad > 0.4) {
        ctx.save(); ctx.globalAlpha = clamp((kBad - 0.4) / 0.5);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
        const mx = cx + cw / 2, my = cy + ch / 2 + 4;
        ctx.beginPath();
        ctx.moveTo(mx - 10, my - 10); ctx.lineTo(mx + 10, my + 10);
        ctx.moveTo(mx + 10, my - 10); ctx.lineTo(mx - 10, my + 10);
        ctx.stroke();
        ctx.restore();
      }
    }

    // ── 연속 모션 : 칸을 여는 코드 커서 ─────────────
    {
      const PR = 3.5, k = (t % PR) / PR;
      const idx = Math.min(N - 1, Math.floor(k * N));
      const cx = M + idx * (cw + gap);
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      const pad = 5;
      ctx.beginPath();
      ctx.moveTo(cx - pad + 8, cy - pad); ctx.lineTo(cx - pad, cy - pad);
      ctx.lineTo(cx - pad, cy + ch + pad); ctx.lineTo(cx - pad + 8, cy + ch + pad);
      ctx.moveTo(cx + cw + pad - 8, cy - pad); ctx.lineTo(cx + cw + pad, cy - pad);
      ctx.lineTo(cx + cw + pad, cy + ch + pad); ctx.lineTo(cx + cw + pad - 8, cy + ch + pad);
      ctx.stroke();
    }

    // 판정 줄 — 칸 **아래**. 기둥이 지나는 띠 밖이다 (v2)
    if (kFall > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kFall);
      ctx.font = disp(700, 11); ctx.fillStyle = tone('sub');
      const s = '전제 하나가 틀리면 그 위가 통째로 무너진다';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, Math.min(M, w - M - sw), cy + ch + 24);
      ctx.restore();
    }
  },
};
