import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* standstill — 검사가 빈손으로 끝나 주민이 굳고, 사다리 맨 아래에 칸 하나를 놨더니
   이번엔 검사가 그 칸에서 못 나온다.

   이 편(4-2편)의 첫 샷이자 콜드 오픈이다. 두 자막이 곧 두 비트다.

   ① stallCue — 왼쪽은 우선순위 사다리다. 칸에 이름이 없다: 원문이 이 시점의 goal 이름을
      대지 않으므로 이름을 지어 넣지 않고 빈 칸(···)으로 둔다. 검사 표식이 위에서 아래로
      계속 내려가는데 어느 칸도 켜지지 않고 ✕만 남긴 채 바닥을 벗어난다. 오른쪽 주민 셋은
      걷다가 발밑에 못이 박히며 멈추고, 아래에 주민의 말이 뜬다. 주민은 고장난 게 아니라
      정상 동작 중이다 — 그래서 멈추는 것은 주민뿐이고 검사는 끝까지 돈다.

   ② leisureCue — 사다리 맨 아래에 '여가' 칸이 붙는다. 주민들은 다시 걷기 시작한다(굳음이
      풀린다). 그런데 내려가던 검사 표식이 그 칸 안에 갇혀 위아래로만 왕복한다.
      **이 왕복이 이 샷의 요지다** — '달성의 끝이 없다'를 라벨이 아니라 움직임의 모양으로
      말한다. 계속 내려가던 것이 갇혀서 되돌아오는 것으로 바뀐다.

   🔴 여기서 점선 목표선을 쓰지 않은 것은 의도다. 다음 샷(sidedoor)이 '목표선 자리가
   비어 있다'를 구조 도식으로 말한다. 두 샷이 같은 기호로 같은 말을 하면 하나는 낭비다.
   S1 은 시간(못 빠져나온다), S2 는 구조(선이 없다)로 갈랐다.

   🔴 재분할(2026-08-04)로 이 파일이 바뀐 자리 — 원본(ep04s S1)의 후반부였던
   플레이어 상자 · 끊긴 명령 경로 · '관람' 도장을 통째로 걷어냈다. 그건 사건 E4(명령과
   거부)의 그림이고 4-1편 것이다. 이 편은 명령을 한 번도 말하지 않는다.

   🔴 못이 자라는 구간을 sk 전체가 아니라 sk>0.5 이후로 밀었다. 원본대로 두면 못이
   자막 시작 +0.8초에 다 박히는데 나레이션의 '굳어요'는 +2.99초라 2.2초가 벌어진다
   (가이드 한도 ±20프레임 = 0.667초). S1 의 thud delayMs 2700 이 이 전제 위에 있다.

   계속 도는 것 = 사다리를 훑는 검사 표식(항상. 후반에는 여가 칸 안을 왕복한다)
                  + 굳기 전·풀린 뒤 주민의 걸음. 전부 t·cue 만의 순수 함수다. */

const SCAN = 2.6;   // 사다리를 훑고 내려가는 주기
const TRAP = 1.7;   // 여가 칸 안에 갇힌 검사가 한 번 왕복하는 주기
const WALK = 1.9;   // 주민 걸음
const BOB = 1.35;   // 주민 상하 흔들림

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const sk = ease(cue(spec.stallCue ?? 0, 0.15, 0.85));
    const gk = ease(cue(spec.leisureCue ?? 1, 0.15, 0.5));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* 굳음 정도. 자막 0 의 후반에 박히고, 여가 칸이 들어오면 풀린다. */
    const fz = clamp((sk - 0.5) / 0.4) * (1 - clamp((gk - 0.15) / 0.5));

    const lx = 8, lw = 150, rowH = 20, pitch = 26, top = 26;
    const rows = spec.rows || 4;
    const bot = top + (rows - 1) * pitch + rowH;      // 124
    const gy = 134, gh = 28;                           // 여가 칸

    /* ── 사다리 ───────────────────────────────────── */
    ctx.textAlign = 'left';
    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.ladderLabel || '우선순위 사다리', lx, 16);

    for (let i = 0; i < rows; i++) {
      const y = top + i * pitch;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, lx, y, lw, rowH, 3); ctx.stroke();

      ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
      ctx.globalAlpha = 0.6;
      ctx.fillText(spec.rowMark || '···', lx + 9, y + 14);
      ctx.globalAlpha = 1;

      // 어느 칸도 켜지지 않는다 — 검사가 지나간 자리에 ✕ 만 남는다
      const passed = clamp(sk * (rows + 1) - i);
      if (passed > 0.05) {
        const k = clamp(passed * 1.5);
        const mx = lx + lw - 16, my = y + rowH / 2, r = 5 * k;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.globalAlpha = 0.9 * k;
        ctx.beginPath();
        ctx.moveTo(mx - r, my - r); ctx.lineTo(mx + r, my + r);
        ctx.moveTo(mx + r, my - r); ctx.lineTo(mx - r, my + r);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 여가 칸 — 사다리 맨 아래 ──────────────────── */
    if (gk > 0.02) {
      const s = Math.min(1, spring(clamp(gk * 1.7)));
      ctx.globalAlpha = clamp(gk * 1.8);
      setShadow(ctx, GLOW, 14, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      roundRect(ctx, lx + (lw - lw * s) / 2, gy + (gh - gh * s) / 2, lw * s, gh * s, 3);
      ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'left';
      ctx.font = disp(800, 13); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.leisure || '여가', lx + 11, gy + 19);
      ctx.globalAlpha = 1;
    }

    /* ── 계속 도는 것 — 검사 표식 ───────────────────
       앞: 위에서 아래로 훑고 바닥을 벗어나 다시 위에서.
       뒤: 여가 칸 안에 갇혀 위아래로 왕복한다. 두 궤적을 trap 으로 섞는다. */
    {
      const trap = clamp((gk - 0.25) / 0.4);
      const freeY = lerp(top - 12, bot + 20, frac(t / SCAN));
      const trapY = gy + 6 + (gh - 12) * (0.5 + 0.5 * Math.sin(frac(t / TRAP) * Math.PI * 2));
      const y = lerp(freeY, trapY, trap);
      const half = lerp(lw / 2 + 4, lw / 2 - 9, trap);
      const cxm = lx + lw / 2;

      const pulse = 0.55 + 0.45 * Math.sin(Math.PI * frac(t / SCAN));
      ctx.globalAlpha = lerp(pulse, 0.9, trap);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(cxm - half, y); ctx.lineTo(cxm + half, y);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 언제까지인가 ─────────────────────────────── */
    if (gk > 0.45 && spec.question) {
      const k = clamp((gk - 0.45) / 0.4);
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.question, 800, 12.5, w - 16);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.question, lx, 188);
      ctx.globalAlpha = 1;
    }

    /* ── 주민 ─────────────────────────────────────── */
    const nV = spec.villagers || 3;
    const vx0 = 208, vgap = 50, vcy = 84;
    for (let i = 0; i < nV; i++) {
      const walk = Math.sin(frac(t / WALK + i / nV) * Math.PI * 2) * 7 * (1 - fz);
      const cx = vx0 + i * vgap + walk;
      const cy = vcy + Math.sin(frac(t / BOB + i / 3) * Math.PI * 2) * 2.5 * (1 - fz);

      ctx.lineWidth = 3;
      ctx.strokeStyle = fz > 0.45 ? tone('sub') : tone('ink');
      ctx.beginPath(); ctx.arc(cx, cy, 15, 0, Math.PI * 2); ctx.stroke();

      // 굳음 — 발밑에 못이 박힌다
      if (fz > 0.02) {
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.moveTo(cx - 13 * fz, cy + 23); ctx.lineTo(cx + 13 * fz, cy + 23);
        ctx.stroke();
      }
    }

    /* ── 주민의 말 ────────────────────────────────── */
    const q = spec.quote || '';
    if (sk > 0.12 && q) {
      const k = clamp((sk - 0.12) / 0.45);
      const cy0 = 206, chh = 42;
      ctx.globalAlpha = k;
      setShadow(ctx, GLOW, 12, 0);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      roundRect(ctx, 8, cy0, w - 16, chh, 4); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'left';
      const fs = fit(q, 800, 14, w - 40, 10);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(q, 20, cy0 + 27);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
