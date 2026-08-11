import {
  disp, ease, clamp,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* namejump — 착지. 줄이 사람 수만큼 서고, 그중 하나를 누르자 강조색이 지도 위의
   표식 하나까지 이어져 네모로 붙잡는다. 이 회차에서 강조색이 **처음으로 사람에게 닿는다.**

   원문 근거(M13 「사건과 흔적」 4단계·5단계):
   > "그래서 마을 단위 집계를 버리고 개인 단위로 이름을 한 줄씩 나열하게 바꿨습니다."
   > "그래서 상태줄의 줄을 클릭하면 카메라가 그 주민에게 점프하고 동시에 선택까지
   >  되도록 했습니다."
   > "주민이 선택돼 있는 동안 카메라가 부드럽게 따라붙게 했습니다."  ← 네모가 표식과
   >  함께 계속 흔들리는 근거(추적)

   🔴 **이름을 글자로 안 적었다.** 원문이 준 것은 형식(`{이름}`)뿐이고 실제 이름 값은
   이 글 어디에도 없다 — 지어내면 없는 문자열이 된다. 그래서 「이름」은 **길이가 서로
   다른 강조색 줄 셋**으로만 그린다(길이가 다르다 = 서로 다른 사람이다).
   🔑 `ep15s-5` 의 nextpeek 이 같은 이유로 이름·날짜를 글자로 안 적었다. 같은 처리를 따랐다.

   ⏱ 이 샷은 자막이 **한 줄**이라 전부 `since(0)` 위에 있다.
      (🔴 한 줄 샷에서 `cue(1)` 을 쓰면 엔진이 조용히 `cue(0)` 으로 접는다 — 아예 안 썼다.)
     · 0.10→0.66  줄 셋이 차례로 선다
     · 0.75→0.95  누르는 자리에 강조색 고리가 퍼진다
     · 0.90→1.30  강조색 점선이 지도의 표식 하나까지 뻗는다
     · 1.30→1.60  네모 넷이 표식을 붙잡는다
       🔑 **효과음 `tick` 의 delayMs 1300 은 이 붙잡기가 시작되는 시각이다.**
       `since` 원점이 `delayMs` 와 같으므로 자막 길이가 바뀌어도 안 움직인다.
     · 그 뒤 — 표식과 네모가 **같은 값으로** 함께 흔들린다(추적 카메라).

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **가로 좌표로 갈랐다.**
     · 강조색 줄의 오른쪽 최대 x = 26 + 118 = 144, 글로우(blur 최대 10)까지 154
       ↔ 흰 표식의 가장 왼쪽 = 240 − 11.5(반지름 + 선 반폭) − 5(흔들림) = 223.5. **69.5px**.
     · 강조색 네모의 오른쪽 최대 = 240 + 5 + 3 + 31.2(등장 순간의 확대 반지름) = 279.2,
       글로우까지 287.2 ↔ 둘째 표식의 왼쪽 = 312 − 11.5 − 5 = 295.5. **8.3px** 뜬다.
     · 강조색 점선의 끝은 표식 중심에서 **30px** 앞에서 멈춘다. 글로우 8 을 빼도 22px 이고
       표식 바깥선은 최대 14.5px 이라 **7.5px** 뜬다.
     · 흰 라벨 「이름 줄」의 글립 아래끝 ≈ 59 ↔ 첫 줄의 글로우 위끝 = 78 − 10(blur 최대
       7 + 3 숨) = 68. **9px** 뜬다.
   흰↔흰(축 B) 최소 간격:
     · 「이름 줄」 라벨(좌 x26, baseline 56)과 「마을」 라벨(중앙 x276, baseline 250) —
       정렬도 baseline 도 갈라 놓았다.
     · 셋째 표식 바닥 = 206 + 3.5 + 11.5 = 221 ↔ 「마을」 글립 윗변 ≈ 238. **17px**.
     · 표식끼리 최소 중심 거리 = (240,98)↔(252,206) 로 108.7px, 바깥선 반지름 14.5 → 79.7px.

   세로 예산(캔버스 307): 최저 = 「마을」 글립 바닥 ≈ 253. 54px 남는다.
   가로: 26~328.5. 캔버스 352.

   계속 도는 것(30fps 기준): 표식 셋과 네모가 5px·3.5px 진폭으로 계속 흔들리고
   (1.1·1.6rad/s), 강조색 점선은 도착 뒤에도 밝기 물결이 **표식 쪽으로 흘러간다**
   (5rad/s · 점 8개). 줄 셋의 글로우는 2.6rad/s 로 3px 숨 쉰다. */

const ROW_X = 26, ROW_H = 14, ROW_Y0 = 78, ROW_GAP = 46;
const ROW_W = [104, 84, 118];
const DOTS = [[240, 98], [312, 152], [252, 206]];
const TARGET = 0;
const LINK_FROM = [142, 85];
const STOP_SHORT = 30, LINK_GAP = 9;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, since }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const s = since(0);
    const appear = ease(clamp((s - 0.10) / 0.24));
    if (appear <= 0.01) { ctx.textAlign = 'left'; return; }

    const click = ease(clamp((s - 0.75) / 0.20));
    const fly = ease(clamp((s - 0.90) / 0.40));
    const grip = ease(clamp((s - 1.30) / 0.30));

    const drift = i => [5 * Math.sin(t * 1.1 + i * 1.7), 3.5 * Math.sin(t * 1.6 + i * 2.3)];

    if (spec.listLabel) {
      ctx.save();
      ctx.globalAlpha = appear; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.listLabel, ROW_X, 56);
      ctx.restore();
    }

    /* ── 이름을 한 줄씩 ─────────────────────────────── */
    for (let i = 0; i < ROW_W.length; i++) {
      const k = ease(clamp((s - 0.10 - i * 0.16) / 0.24));
      if (k <= 0.01) continue;
      const y = ROW_Y0 + i * ROW_GAP;
      ctx.save();
      ctx.globalAlpha = k;
      setShadow(ctx, GLOW, 7 + 3 * (0.5 + 0.5 * Math.sin(t * 2.6 + i * 1.1)));
      ctx.fillStyle = tone('accent');
      roundRect(ctx, ROW_X, y, ROW_W[i] * k, ROW_H, 7); ctx.fill();
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 지도 위의 사람들 ───────────────────────────── */
    for (let i = 0; i < DOTS.length; i++) {
      const [dx, dy] = drift(i);
      ctx.save();
      ctx.globalAlpha = appear;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(DOTS[i][0] + dx, DOTS[i][1] + dy, 10, 0, Math.PI * 2);
      ctx.stroke();
      ctx.restore();
    }
    if (spec.mapLabel) {
      ctx.save();
      ctx.globalAlpha = appear; ctx.textAlign = 'center';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.mapLabel, 276, 250);
      ctx.restore();
    }

    /* ── 눌렀다 ─────────────────────────────────────── */
    if (click > 0.01 && click < 0.999) {
      ctx.save();
      ctx.globalAlpha = Math.sin(Math.PI * click);
      setShadow(ctx, GLOW, 8);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.arc(ROW_X + ROW_W[0] / 2, ROW_Y0 + ROW_H / 2, 9 + 22 * click, 0, Math.PI * 2);
      ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 그 사람에게 가는 길이 이어진다 ─────────────── */
    const [tdx, tdy] = drift(TARGET);
    const tx = DOTS[TARGET][0] + tdx, ty = DOTS[TARGET][1] + tdy;
    if (fly > 0.01) {
      const vx = tx - LINK_FROM[0], vy = ty - LINK_FROM[1];
      const L = Math.hypot(vx, vy);
      const usable = Math.max(0, L - STOP_SHORT);
      const ux = vx / L, uy = vy / L;
      ctx.save();
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      for (let d = 0, k = 0; d <= usable * fly; d += LINK_GAP, k++) {
        /* 도착 뒤에도 밝기 물결이 표식 쪽으로 흘러간다 — 「따라붙는 중」 */
        ctx.globalAlpha = fly * (0.55 + 0.45 * (0.5 + 0.5 * Math.sin(t * 5 - k * 0.9)));
        ctx.fillRect(LINK_FROM[0] + ux * d - 3, LINK_FROM[1] + uy * d - 3, 6, 6);
      }
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 네모가 그 사람을 붙잡는다 (그리고 같이 움직인다) ── */
    if (grip > 0.01) {
      const R = 26 * (1 + 0.20 * (1 - grip));
      const arm = 9;
      ctx.save();
      ctx.globalAlpha = grip;
      setShadow(ctx, GLOW, 8);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.lineCap = 'round';
      for (let sx = -1; sx <= 1; sx += 2) {
        for (let sy = -1; sy <= 1; sy += 2) {
          const cx = tx + sx * R, cy = ty + sy * R;
          ctx.beginPath();
          ctx.moveTo(cx - sx * arm, cy); ctx.lineTo(cx, cy); ctx.lineTo(cx, cy - sy * arm);
          ctx.stroke();
        }
      }
      clearShadow(ctx);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
