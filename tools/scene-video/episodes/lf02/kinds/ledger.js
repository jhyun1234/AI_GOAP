import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* ledger — 🔴 **v2 전면 재설계** (`ADR-V25-15` 라벨 가림 시험 미달).
   v1 은 「수치 칸이 위에서부터 한 줄씩 채워지고 마지막 칸만 강조색으로 잠긴다」였는데,
   라벨을 가리면 **「칸이 채워지고 마지막 칸이 잠긴다」**만 남았다 — 사건이 없고
   글자가 그림 행세를 했다.

   v2 의 사건 = **바닥선 위에 층이 하나씩 얹혀 탑이 되고, 다 쌓인 뒤 맨 위에 빗장이 걸린다.**
   라벨을 전부 가려도 「무언가가 47일에 걸쳐 쌓였고, 마지막에 잠겼다」가 남는다.

   🔴 층 높이는 전부 같다. 수치의 크기를 높이로 옮기면 **커밋 1,061 과 게이트 433 을
   비교하는 그림**이 되는데, 단위가 달라 비교가 성립하지 않는다(지어낸 대비가 된다).
   높이는 「쌓인다」만 말하고 크기는 숫자가 말한다.

   🧊 수치 기준 시각 = **2026-08-10 14:11:52 · `03d0c17d`** (remake-input §6 정본).
   여기 없는 수는 그리지 않는다. 기준 시각은 화면 머리줄에 그대로 적는다.

   연속 모션 = 바닥에서 꼭대기로 훑고 올라가는 집계 띠(층 높이만큼의 면).
   원장은 커밋마다 다시 세어지므로 멈추지 않는다. 층·값보다 **먼저** 그려
   강조색 픽셀 위에 반투명 흰색이 얹히지 않게 한다.

   🔴 화면 글자는 한국어(`ADR-V-10` 2026-08-10 개정). 남은 영어는 날짜·숫자·기호뿐이다. */

const ROWS = [
  { v: '47',      u: '일' },
  { v: '839 / 1,061', u: '커밋 — 게임 파일 / 전부' },
  { v: '22,478',  u: '코드 줄 · 125개 파일' },
  { v: '121 · 34', u: '설정 파일 · 계획 문서' },
  { v: '433',     u: '게이트 — 켜기 전 검사' },
];

const HEAD = '2026-06-24  ->  2026-08-10 14:11 기준';

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    // 머리줄 — 기준 기간과 기준 시각 (숫자·기호라 mono 유지)
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText(HEAD, M, 24);

    const baseY = h - 26;                 // 바닥선 (첫 커밋)
    const layerH = 33, layerGap = 3;
    const towerX = M + 8, towerW = 196;
    const topY = baseY - ROWS.length * layerH;

    /* ── 연속 모션 : 바닥에서 꼭대기로 훑고 올라가는 집계 띠 ──
       띠의 위·아래 끝이 캔버스 안(topY-14 ~ baseY+4)에 갇히도록 시작점을 물린다. */
    {
      const PR = 4.2, k = (t % PR) / PR;
      const bandH = layerH - 4;
      const by = lerp(baseY + 4 - bandH, topY - 14, k);
      ctx.fillStyle = tone('track');
      ctx.fillRect(towerX - 8, by, w - M - (towerX - 8), bandH);
    }

    // 바닥선
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(M, baseY); ctx.lineTo(w - M, baseY); ctx.stroke();

    const kLast = ease(clamp(cue(ROWS.length - 1)));

    for (let i = 0; i < ROWS.length; i++) {
      const k = ease(clamp(cue(i)));
      if (k <= 0.02) continue;
      const last = i === ROWS.length - 1;
      const lit = last && k > 0.5;

      // 층 — 바닥에서부터 i 번째. 얹히면서 살짝 내려앉는다.
      const ly = baseY - (i + 1) * layerH + layerGap;
      const lh = layerH - layerGap * 2;
      const drop = (1 - ease(k)) * -18;

      ctx.save();
      ctx.globalAlpha = clamp(0.25 + 0.75 * k);
      ctx.strokeStyle = lit ? tone('accent') : tone('ink');
      ctx.lineWidth = 3;
      roundRect(ctx, towerX, ly + drop, towerW, lh, 3); ctx.stroke();
      // 층 안의 채움 — 왼쪽부터 찬다
      ctx.fillStyle = lit ? tone('accent') : tone('ink');
      ctx.fillRect(towerX + 8, ly + drop + lh / 2 - 3, (towerW - 16) * ease(k), 6);
      ctx.restore();

      // 값 — 층 오른쪽. 폭을 재서 캔버스 안에 가둔다.
      const vx = towerX + towerW + 20;
      const avail = w - M - vx;
      let fs = 21;
      ctx.font = disp(900, fs);
      while (ctx.measureText(ROWS[i].v).width > avail * 0.62 && fs > 12) {
        fs -= 1; ctx.font = disp(900, fs);
      }
      const vw = ctx.measureText(ROWS[i].v).width;
      ctx.save();
      ctx.globalAlpha = clamp(k);
      ctx.fillStyle = lit ? tone('accent') : tone('ink');
      ctx.fillText(ROWS[i].v, vx, ly + drop + lh / 2 + 7);
      ctx.restore();

      // 단위 — 값 오른쪽. 남는 폭 안에서만 그린다.
      let us = 10;
      ctx.font = disp(700, us);
      const uAvail = w - M - (vx + vw + 12);
      while (ctx.measureText(ROWS[i].u).width > uAvail && us > 8) {
        us -= 1; ctx.font = disp(700, us);
      }
      if (ctx.measureText(ROWS[i].u).width <= uAvail) {
        ctx.save();
        ctx.globalAlpha = clamp(k) * 0.95;
        ctx.fillStyle = tone('sub');
        ctx.fillText(ROWS[i].u, vx + vw + 12, ly + drop + lh / 2 + 6);
        ctx.restore();
      }
    }

    /* ── 빗장 : 마지막 층이 다 얹히면 탑 꼭대기에 걸린다 ── */
    if (kLast > 0.5) {
      const kk = clamp((kLast - 0.5) / 0.5);
      ctx.save();
      ctx.globalAlpha = clamp(kk);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 6;
      const y = topY - 6;
      const x0 = towerX - 10, x1 = towerX + towerW + 10;
      ctx.beginPath();
      ctx.moveTo(lerp((x0 + x1) / 2, x0, ease(kk)), y);
      ctx.lineTo(lerp((x0 + x1) / 2, x1, ease(kk)), y);
      ctx.stroke();
      ctx.restore();
    }
  },
};
