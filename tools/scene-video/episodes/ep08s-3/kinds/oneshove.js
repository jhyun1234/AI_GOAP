import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* oneshove — 신호 하나가 열을 같은 양만큼 밀어 올려 전부 한 선을 넘긴다.

   원문: "겨울 예고가 뜨는 순간, 주민 전원이 동시에 비축에 뛰어들었습니다."

   🔴 이 편의 기본 도형은 **문턱선 — 하나의 밀어올림과, 그것이 넘기는 선**이다.
   여기서 문턱은 '전부 넘는 선'으로 나오고, 마지막 샷(apart)에서 같은 선이
   '한 쪽은 넘고 한 쪽은 안 넘는 선'으로 뒤집힌다.

   🔴 출발 높이를 일부러 **전부 다르게** 뒀다. 이 편의 어긋남은 '값이 같다'가 아니라
   **'밀림이 같다'**이기 때문이다 — 개인차는 이미 있는데(성격도 직업도 있다) 겨울 신호가
   모두에게 똑같은 크기로 얹히니 결과가 하나로 붙는다. 열이 처음부터 같은 높이였다면
   그건 다음 편이 고칠 이야기가 아니라 앞 편들이 이미 다룬 이야기가 된다.

   🔴 상승분(RISE)은 열에게 **완전히 같은 값**이다. 이 한 줄이 원문의 "성격별 가중치가
   없었다"에 해당한다 — S3(weightknob)에서 이 값에 손잡이가 붙는다.

   🔴 문턱선을 막대보다 **먼저** 그리고 막대는 배경색으로 채운다. 흰 선이 초록 빗금 위에
   얹히면 두 색이 같은 픽셀에서 섞이고, 그건 형제 편이 검수에서 잡힌 자리다.
   막대가 자라면서 선을 삼키는 것이 오히려 이 샷의 뜻이기도 하다.

   🔴 라벨 자리는 막대밭 **바깥**(왼쪽 여백)이고, 문턱선 라벨은 선 **위**에 올린다.
   ①막대가 최대 158 까지 자라므로 막대밭 안에 글자를 두면 반드시 덮이고,
   ②라벨을 선과 같은 높이(LINE + 4)에 두면 **점선이 글자를 취소선처럼 관통한다**
   (1차 검수 R3 — 8.5초 내내 그 상태였다). apart.js 는 처음부터 LINE − 8 이었는데
   이 파일만 빠뜨렸다. 두 파일이 같은 규칙을 쓴다.

   🔴 등장은 전부 cue 에 물렸다. 첫 자막이 「게임 속 주민이 **겨울** 온다고 미리 대비하면」
   이므로 **겨울 신호도 첫 자막에 물린다**(자막 앞 구간 = 막대·문턱선, 뒤 구간 = 신호 하강).
   신호를 둘째 자막으로 보내면 「겨울」을 부른 지 3초 뒤에 WINTER 가 뜬다(1차 검수 C3).
   둘째 자막이 여는 것은 신호가 아니라 **밀림**이다.
   t 로 도는 것은 막대 안쪽 빗금과 두 선의 dash 흐름뿐이고 이것들은 문턱을 가지지 않는다. */

const H0 = [34, 58, 22, 46, 30, 62, 26, 50, 38, 42];  // 출발 높이 — 제각각
const RISE = 96;                                       // 밀어올림 — 열에게 모두 같다

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

    const N = Math.min(spec.people ?? 10, H0.length);
    const GX = 88, X1 = w - 16;          // 막대밭. 왼쪽 16~88 은 라벨 자리로 비워 둔다
    const BASE = 252, LINE = 140;
    const slot = (X1 - GX) / N;
    const bw = Math.min(15, Math.max(8, slot - 9));

    /* 첫 자막을 앞뒤로 나눠 쓴다 — 앞 구간에 막대와 문턱선, 뒤 구간(「겨울 온다고」)에 신호. */
    const r0 = cue(spec.standCue ?? 0, 0.15, 0.6);
    const sk = ease(clamp(r0 / 0.55));                  // 열이 제각각 서 있다
    const sig = ease(clamp((r0 - 0.30) / 0.50));        // 겨울 신호가 내려온다
    const c1 = cue(spec.shoveCue ?? 1, 0.10, 0.85);
    const push = ease(clamp((c1 - 0.30) / 0.45));       // 열이 밀려 올라간다

    const flow = -(t * 15) % 13;                        // 빗금 흐름(계속)

    /* 막대 안쪽 빗금 — 이 샷에서 면적이 가장 큰 움직임 */
    const hatch = (x, y, bwid, bh, color) => {
      if (bh < 4) return;
      ctx.save();
      ctx.beginPath(); ctx.rect(x, y, bwid, bh); ctx.clip();
      ctx.strokeStyle = color; ctx.lineWidth = 3; ctx.globalAlpha = 0.5;
      for (let k = -bh - 13; k < bwid + 13; k += 13) {
        const o = k + flow;
        ctx.beginPath();
        ctx.moveTo(x + o, y + bh); ctx.lineTo(x + o + bh, y);
        ctx.stroke();
      }
      ctx.restore();
    };

    /* ── 문턱선 ──────────────────────────────────────
       막대보다 먼저 그린다. 넘은 뒤에는 막대가 이 선을 가린다. */
    const la = clamp(sk * 2.2);
    if (la > 0.02) {
      ctx.globalAlpha = la * 0.85;
      ctx.setLineDash([10, 8]);
      ctx.lineDashOffset = -(t * 12) % 18;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(16, LINE); ctx.lineTo(X1, LINE); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;

      if (spec.lineLabel) {
        const over = push > 0.94;
        ctx.globalAlpha = la;
        ctx.textAlign = 'right';
        const fs = fit(spec.lineLabel, 800, 11, 64, 8);
        ctx.font = disp(800, fs);
        ctx.fillStyle = over ? tone('accent') : tone('sub');
        /* 🔴 선 **위**(LINE − 9)에 올린다. 같은 높이에 두면 점선이 글자를 관통한다.
           대문자뿐이라 baseline 아래로 내려가는 획이 없어 9px 이면 안 닿는다. */
        ctx.fillText(spec.lineLabel, GX - 8, LINE - 9);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 막대 열 개 ──────────────────────────────────
       출발 높이는 제각각, 상승분은 모두 같다. */
    for (let i = 0; i < N; i++) {
      const app = ease(clamp(sk * 3.0 - i * 0.16));
      if (app <= 0.02) continue;
      const h = H0[i] * app + RISE * push;
      if (h < 3) continue;
      const x = GX + i * slot + (slot - bw) / 2;
      const top = BASE - h;
      const over = top < LINE;
      const col = over ? tone('accent') : tone('ink');

      ctx.fillStyle = tone('bg');
      ctx.fillRect(x, top, bw, h);

      hatch(x, top, bw, h, col);

      ctx.strokeStyle = col; ctx.lineWidth = 3;
      if (over) setShadow(ctx, GLOW, 8, 0);
      ctx.beginPath(); ctx.rect(x, top, bw, h); ctx.stroke();
      clearShadow(ctx);
    }

    /* 바닥선 — 열이 같은 땅을 딛고 있다 */
    if (sk > 0.02) {
      ctx.globalAlpha = clamp(sk * 2.4) * 0.8;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(GX - 6, BASE); ctx.lineTo(X1, BASE); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 겨울 신호 ───────────────────────────────────
       위에서 내려와 열 개의 머리 위에 한 번에 닿는다. */
    if (sig > 0.02) {
      const sy = lerp(36, 88, sig);
      ctx.globalAlpha = clamp(sig * 2.2);
      ctx.setLineDash([13, 9]);
      ctx.lineDashOffset = (t * 16) % 22;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(GX, sy); ctx.lineTo(X1, sy); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      // 닿는 자국 — 열 개 머리 위에 하나씩
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      for (let i = 0; i < N; i++) {
        const cx = GX + i * slot + slot / 2;
        ctx.beginPath();
        ctx.moveTo(cx, sy); ctx.lineTo(cx, sy + 9 * sig);
        ctx.stroke();
      }

      if (spec.signalLabel) {
        ctx.textAlign = 'left';
        const fs = fit(spec.signalLabel, 900, 13, 62, 9);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.signalLabel, 16, sy + 5);
      }
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
