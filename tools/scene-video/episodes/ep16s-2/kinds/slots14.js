import {
  disp, ease, clamp, frac, rnd,
  fitCanvas, mkCanvas, tone, roundRect
} from '../../../engine/lib.js';

/* slots14 — 진단. 알림이 뜨는 자리 열넷이 제각기 반짝였다 꺼지고, 아래 큰 칸에는
   아무것도 안 쌓인다.

   원문 근거(M13 「사건과 흔적」 2단계):
   > "이 항목을 시작하면서 기존 알림이 발생하는 지점을 전수 조사했습니다. 열네 곳이
   >  나왔는데, 세어 보니 상태형 알림이 0개였습니다. 전부 순간 사건이었습니다."
   > "\"식량 부족\"과 \"치료 필요\"는 해소될 때까지 남아 있어야 의미가 있으니까요."

   🔴 **수 둘을 글자가 아니라 형태로도 지게 했다.** 칸을 정확히 열넷 그리므로 세어서
   확인할 수 있고, 큰 칸에는 아무것도 안 쌓이므로 「0」이 라벨 없이도 읽힌다.
   (글자로도 올린 것은 라벨 「알림이 뜨는 자리 14곳」과 큰 칸 안의 「0」 둘뿐이고,
   둘 다 원문 문장에 있는 수다 — 브리프 §6 의 「14곳」·「0개」.)

   🔴 **이 샷에는 강조색이 한 픽셀도 없다. 그게 이 샷의 논지다** — 이 회차에서
   강조색은 「그 사람에게 가 닿는 것」이고, 지금은 닿는 것이 아무것도 없다.
   따라서 축 A(강조↔흰) 겹침은 **구조적으로 0**이다.

   흰↔흰(축 B) 최소 간격:
     · 라벨 글립 바닥 ≈ 27 ↔ 칸 윗줄 44.5 = 17.5px
     · 칸 아랫줄 115.5 ↔ 떨어지는 점 맨 위 118 = 2.5px — **의도한 쌍**이다(칸에서 떨어져
       나온 점이라 붙어 보여야 한다). 서로 다른 것끼리의 최소는 아래 둘이다.
     · 떨어지는 점 바닥(최대) 158 ↔ 큰 칸 윗줄 162.5 = 4.5px
       (점은 y 152 에서 이미 알파 0 이라 실제로는 겹칠 일이 없다)
     · 「0」 글립 윗변 ≈ 182 ↔ 큰 칸 윗줄 165.5 = 16.5px
     · 큰 칸 아랫줄 253.5 ↔ 「계속 남는 알림」 글립 윗변 ≈ 265 = 11.5px
   같은 baseline 에 둘을 놓은 자리는 없다. 라벨은 좌·중앙 정렬을 나눠 썼다
   (제목 = 좌 x20 / 칸 라벨 = 중앙 176).

   세로 예산(캔버스 307): 최저 = 「계속 남는 알림」 글립 바닥 ≈ 277. 30px 남는다.
   가로: 18~334(칸 오른쪽 끝). 캔버스 352, 좌 18 · 우 18 남는다.

   계속 도는 것(30fps 기준): 칸 열넷이 2.4초 주기로 제각기 반짝이고(각 칸 40×30),
   그때마다 흰 점이 118→152 를 0.62초에 지난다 → **1.8px/프레임** × 열넷.
   「0」은 3.1rad/s 로 크기가 1.5px 숨 쉰다. */

const COLS = 7, CW = 40, CH = 30, GX = 6, GY = 8;
const X0 = 18, Y0 = 46;
const BOX = { x: 46, y: 164, w: 260, h: 88 };
const FALL_TOP = 118, FALL_LEN = 34;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const g = ease(cue(spec.gridCue ?? 0, 0.12, 0.40));
    const z = ease(cue(spec.zeroCue ?? 1, 0.15, 0.35));
    if (g <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = g; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ── 알림이 뜨는 자리 열넷 ──────────────────────── */
    const n = spec.slots ?? 14;
    for (let i = 0; i < n; i++) {
      const col = i % COLS, row = (i / COLS) | 0;
      const x = X0 + col * (CW + GX);
      const y = Y0 + row * (CH + GY);
      const ap = g * clamp((g - i * 0.02) / 0.4);
      if (ap <= 0.01) continue;

      ctx.save();
      ctx.globalAlpha = ap * 0.35;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, x, y, CW, CH, 6); ctx.stroke();
      ctx.restore();

      /* 반짝했다 꺼진다 — 칸마다 위상이 다르다(rnd 는 씨앗이 같으면 같은 값) */
      const ph = frac(t / 2.4 + rnd(i * 17 + 3));
      const fl = ease(clamp(ph / 0.035)) * (1 - ease(clamp((ph - 0.09) / 0.06)));
      if (fl > 0.01) {
        ctx.save();
        ctx.globalAlpha = ap * fl;
        ctx.fillStyle = tone('ink');
        roundRect(ctx, x + 5, y + 5, CW - 10, CH - 10, 4); ctx.fill();
        ctx.restore();
      }

      /* 떨어져 나오지만 큰 칸에 닿기 전에 사라진다 */
      const p = clamp((ph - 0.10) / 0.26);
      /* 🔴 켜질 때도 꺼질 때도 알파가 튀면 안 된다 — 양쪽을 다 부드럽게 잡는다 */
      const sa = ap * ease(clamp(p / 0.08)) * (1 - ease(p)) * 0.85;
      if (sa > 0.01) {
        ctx.save();
        ctx.globalAlpha = sa;
        ctx.fillStyle = tone('ink');
        ctx.fillRect(x + CW / 2 - 3, FALL_TOP + p * FALL_LEN, 6, 6);
        ctx.restore();
      }
    }

    /* ── 계속 남는 알림 — 끝까지 비어 있다 ──────────── */
    if (z > 0.01) {
      ctx.save();
      ctx.globalAlpha = z * 0.35;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, BOX.x, BOX.y, BOX.w, BOX.h, 10); ctx.stroke();
      ctx.restore();

      const fs = 58 + 1.5 * Math.sin(t * 3.1);
      ctx.save();
      ctx.globalAlpha = z; ctx.textAlign = 'center';
      ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.zeroText ?? '0', BOX.x + BOX.w / 2, 224);
      ctx.restore();

      if (spec.stayLabel) {
        ctx.save();
        ctx.globalAlpha = z; ctx.textAlign = 'center';
        ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.stayLabel, BOX.x + BOX.w / 2, 274);
        ctx.restore();
      }
    }

    ctx.textAlign = 'left';
  }
};
