import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* handoff — 마무리. 판단 기준 블록이 사람 칸을 떠나 저장소 칸에 놓이고, 거기서
   세션 칸들이 차례로 같은 높이까지 채워진다. 「사람이 없어도 된다」가 아니라
   「사람이 남긴 것이 있어야 이어진다」를 순서로만 말한다 — 출발점이 사람 칸이다.

   🔴 2차 개정 (검수 R1): 1차본은 좌하단에서 취소선 문구 「AI가 코드를 대신 써 준다」·
   라벨 `CHANNEL ENTRANCE`·「기술은 만점, 재미는 0점」 셋이 같은 자리에 포개졌다
   (증거 `build/stills/775s.png`). 셋을 세로로 쌓으면서 y 간격을 4px 밖에 안 준 것이
   원인이다 → **취소선 문구는 왼쪽 칸, 정문 블록은 오른쪽 칸으로 가로 분리**하고
   정문 블록 안에서만 라벨 위·헤드라인 아래로 24px 간격을 뒀다.

   🔴 2차 개정 (검수 C6): 연속 모션이 반지름 5px 인계 표식뿐이라 문턱 아래였다 →
   **48px 폭 인계 띠**로 바꿨다(세션 칸보다 먼저 그린다).

   문자열 출처: 번외 글 28·21행 · M11 글 70행. 유도 대상 lf01 은 uploads.json 등재 확인. */

const NS = 5;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kNot = ease(cue(0));     // 코드를 대신 써 주는 게 핵심이 아니다
    const kWho = ease(cue(1));     // 무엇을 만들지 정하는 사람
    const kLeave = ease(cue(2));   // 저장소에 남겨 둬야
    const kDoor = ease(cue(3));    // 채널 정문 영상
    const kJudge = ease(cue(4));   // 기술은 만점, 재미는 0점

    // ── 위 : 사람 칸 → 저장소 칸 → 세션 칸 ──────────
    const hy = 44, hh = 54;
    const hx = M, hw = 128;
    const rx = M + hw + 48, rw = 146;
    const sx0 = rx + rw + 34;
    const cwd = (w - M - sx0 - (NS - 1) * 7) / NS;
    const sy = hy, sh = hh;

    // 연속 모션 : 인계 띠 (칸보다 먼저 — 강조색 위에 흰색을 얹지 않는다)
    const PR = 2.0;
    const idx = Math.floor(t / PR) % NS;
    const ph2 = (t % PR) / PR;
    {
      const target = sx0 + idx * (cwd + 7) + cwd / 2;
      const fx = lerp(rx + rw + 6, target, ease(Math.min(1, ph2 / 0.45)));
      ctx.fillStyle = tone('track');
      ctx.fillRect(fx - 24, sy - 14, 48, sh + 28);
    }

    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('사람', hx, hy - 10);
    ctx.fillText('저장소', rx, hy - 10);
    ctx.fillText('다음 세션들', sx0, hy - 10);

    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, hx, hy, hw, hh, 4); ctx.stroke();
    ctx.strokeStyle = kLeave > 0.2 ? tone('accent') : tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, rx, hy, rw, hh, 4); ctx.stroke();

    // 사람 칸 안 — 무엇을 만들지 정한다
    if (kWho > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kWho);
      ctx.font = disp(900, 14); ctx.fillStyle = tone('ink');
      const s = '무엇을 만들지';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, hx + (hw - sw) / 2, hy + 33);
      ctx.restore();
    }

    // 인계선
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(rx + rw + 4, sy + sh / 2); ctx.lineTo(sx0 - 6, sy + sh / 2);
    ctx.stroke();

    for (let i = 0; i < NS; i++) {
      const cx = sx0 + i * (cwd + 7);
      const on = kLeave > 0.35;
      const active = on && i === idx && ph2 > 0.45;
      ctx.strokeStyle = on ? (active ? tone('accent') : tone('track')) : tone('track');
      ctx.lineWidth = active ? 4 : 3;
      roundRect(ctx, cx, sy, cwd, sh, 3); ctx.stroke();
      if (on) {
        const fill = active ? 1 : 0.72;
        ctx.fillStyle = active ? tone('accent') : tone('sub');
        ctx.fillRect(cx + 6, sy + sh - 8 - (sh - 22) * fill, cwd - 12, (sh - 22) * fill);
      }
    }

    // 판단 기준 블록 — 사람 칸에서 저장소 칸으로 옮겨 앉는다
    {
      const k = ease(clamp(kLeave));
      const bw = 104, bh = 24;
      const sxA = hx + (hw - bw) / 2, sxB = rx + (rw - bw) / 2;
      const bx = lerp(sxA, sxB, k);
      const by = hy + hh + 16 - 14 * Math.sin(Math.PI * k);
      ctx.save(); ctx.globalAlpha = clamp(0.35 + 0.65 * Math.max(kWho, kLeave));
      ctx.strokeStyle = k > 0.5 ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, bx, by, bw, bh, 3); ctx.stroke();
      ctx.font = disp(800, 12);
      ctx.fillStyle = k > 0.5 ? tone('accent') : tone('ink');
      const s = '판단 기준';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, bx + (bw - sw) / 2, by + 17);
      ctx.restore();
    }

    /* ── 아래 : 왼쪽 = 핵심이 아니었던 것 / 오른쪽 = 정문 ──
       🔴 셋을 같은 자리에 쌓지 않는다(R1). 가로로 두 칸, 오른쪽 칸 안에서만 세로 24px. */
    const by2 = h - 62;
    if (kNot > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kNot);
      const bw = 234;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, M, by2, bw, 28, 3); ctx.stroke();
      let fs = 13; ctx.font = disp(800, fs);
      const s = 'AI가 코드를 대신 써 준다';
      while (ctx.measureText(s).width > bw - 24 && fs > 9) {
        fs -= 1; ctx.font = disp(800, fs);
      }
      const swid = ctx.measureText(s).width;
      ctx.fillStyle = tone('sub');
      ctx.fillText(s, M + 12, by2 + 19);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(M + 9, by2 + 15); ctx.lineTo(M + 15 + swid, by2 + 15);
      ctx.stroke();
      ctx.restore();
    }

    if (kDoor > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kDoor);
      const dx = M + 262;
      ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('채널 정문 영상', dx, by2 + 8);
      if (kJudge > 0.05) {
        ctx.save(); ctx.globalAlpha = clamp(kJudge);
        const s = '기술은 만점, 재미는 0점';
        let fs = 17;
        ctx.font = disp(900, fs);
        while (ctx.measureText(s).width > w - M - dx && fs > 10) {
          fs -= 1; ctx.font = disp(900, fs);
        }
        ctx.fillStyle = tone('accent');
        ctx.fillText(s, dx, by2 + 34);
        ctx.restore();
      }
      ctx.restore();
    }
  },
};
