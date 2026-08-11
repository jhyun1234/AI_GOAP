import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone, depthGrad
} from '../../../engine/lib.js';

/* rosterflood — 훅. 이름이 죽 늘어선 명부가 있는데, 한 줄 밑에서 같은 기록이
   한 칸씩 쌓아 올라오며 나머지 이름을 아래로 밀어내고 자리를 다 차지한다.

   원문 근거:
   > "이 덕분에 전멸 회고 화면이 통계에서 명부로 바뀌었습니다. … 그 아래에 사람 수만큼
   >  줄이 뜹니다."
   > "회고 명부의 각 줄 아래에 연대기를 병기했더니, 게으름뱅이 한 명의 '명령 거부(피로)'
   >  아홉 연발이 명부를 통째로 덮었습니다."

   🔴 이 편의 색 규약(다섯 샷 공통): **흰색 = 사람(이름 줄) / 강조색 = 그 사람에게 남은 기록.**
   훅에서 세우고 S2·S3 이 그대로 받는다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세로 좌표로 갈랐다.**
     이름 줄은 34px 간격(100·134·168·202·236, 높이 12)이고 기록 칸은 그 사이 22px 틈에
     산다(151~163, 간격 20). 그래서 위로 row1 바닥 146 에서 **5px**, 아래로 row2 꼭대기
     168 에서 **5px** 뜬다. 기록이 한 칸 자랄 때마다 밀리는 이름 줄도 같은 보폭(20px)으로
     내려가므로 이 5px 는 끝까지 유지된다(칸 k 바닥 163+20k / 밀린 줄 꼭대기 168+20k+20f
     → 간격 5+20f).
     🔑 그래서 **기록 칸에는 글로우를 안 걸었다** — blur 6 이면 이 5px 를 그대로 먹는다.
     깊이는 `depthGrad` 로 낸다(3색 규약 안, lib.js 주석 참조).

   ⏱ 등장만 자막에 물리고(`cue(0)`), 되풀이는 `t` 에 물렸다. 훅은 한 줄짜리 샷이라
   `cue(1)` 이 `cue(0)` 으로 접힌다 — 그래서 인덱스를 하나만 쓴다.

   세로 예산(캔버스 307): 패널 48~272. 기록 다섯 칸의 마지막이 231~243.
   밀려난 이름 줄은 y+12 > 266 이면 안 그린다. 아래로 최소 29px 남는다.
   가로: 26~326. 좌우 26px 씩 남는다.

   계속 도는 것(30fps 기준): 2.6초 루프에서 차오르는 구간(u 0.20~0.75)에 이름 줄 셋이
   **1.0px/프레임**으로 내려가고 기록 칸은 폭이 최대 **10px/프레임**으로 늘어난다.
   되감기(u 0.86~1.0)에는 같은 것이 반대로 움직인다. 아주 멈추는 구간은 u 0.75~0.86
   (**0.29초**)뿐이고 그때도 이름 줄과 기록 칸이 서로 다른 박자로 밝아졌다 어두워진다. */

const PX = 26, PY = 48, PW = 300, PH = 224;
const ROW_X = 42, ROW_H = 12, ROW_P = 34, ROW_Y0 = 100;
const NAME_W = [104, 120, 86, 132, 96];
const BAR_X = 42, BAR_W = 176, BAR_H = 12, BAR_P = 20, BAR_Y0 = 151;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const pk = ease(cue(spec.panelCue ?? 0, 0.15, 0.30));
    if (pk <= 0.02) { ctx.textAlign = 'left'; return; }

    /* 되풀이 — 차올랐다가 되감긴다. 전부 nf 하나의 함수라 어느 시각으로 seek 해도 같다. */
    const u = frac(t / (spec.loopSec ?? 2.6));
    const nf = 5 * ease(clamp((u - 0.20) / 0.55)) * (1 - ease(clamp((u - 0.86) / 0.14)));

    /* ── 명부 패널 ── */
    ctx.save();
    ctx.globalAlpha = pk;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, PX, PY, PW, PH, 12); ctx.stroke();
    ctx.restore();

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = pk; ctx.textAlign = 'left';
      ctx.font = disp(800, 13); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, ROW_X, 72);
      ctx.restore();
    }

    ctx.save();
    ctx.globalAlpha = pk * 0.34;
    ctx.fillStyle = tone('ink');
    ctx.fillRect(ROW_X, 82, 268, 3);
    ctx.restore();

    /* ── 이름 줄 다섯 ── 위 둘은 제자리, 아래 셋은 기록과 같은 보폭으로 밀려나며 사라진다 */
    for (let i = 0; i < 5; i++) {
      const pushed = i >= 2;
      const y = ROW_Y0 + i * ROW_P + (pushed ? nf * BAR_P : 0);
      const fade = pushed ? 1 - ease(clamp((nf - (i - 2) * 0.9) / 1.4)) : 1;
      const a = pk * fade * (0.86 + 0.14 * (0.5 + 0.5 * Math.sin(t * 2.6 - i * 0.7)));
      if (a <= 0.02 || y + ROW_H > 266) continue;
      ctx.save();
      ctx.globalAlpha = a;
      ctx.fillStyle = depthGrad(ctx, ROW_X, y, ROW_X + NAME_W[i], y + ROW_H, 'ink');
      roundRect(ctx, ROW_X, y, NAME_W[i], ROW_H, 6); ctx.fill();
      ctx.restore();
    }

    /* ── 한 사람의 기록이 한 칸씩 쌓인다 (왼쪽부터 써 내려가듯 폭이 늘어난다) ── */
    for (let k = 0; k < 5; k++) {
      const kf = ease(clamp(nf - k));
      const w = BAR_W * kf;
      if (w <= 2) continue;
      const y = BAR_Y0 + k * BAR_P;
      const a = pk * (0.78 + 0.22 * (0.5 + 0.5 * Math.sin(t * 3.4 - k * 0.8)));
      ctx.save();
      ctx.globalAlpha = a;
      ctx.fillStyle = depthGrad(ctx, BAR_X, y, BAR_X + BAR_W, y + BAR_H, 'accent');
      roundRect(ctx, BAR_X, y, w, BAR_H, 6); ctx.fill();
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
