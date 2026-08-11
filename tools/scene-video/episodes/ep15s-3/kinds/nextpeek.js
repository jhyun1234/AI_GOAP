/* 📐 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이고, 강조색은 글로우(blur 7)까지 센다.
   불꽃의 「바깥」은 세로 9(맥동 최대 11.2) + 1.5 + 7 = **19.7**, 가로 6 + 1.5 + 7 = **14.5** 다. */
import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 편에서 다룰 장면의 0.5초 미리보기를 루프로 돈다.

   🔴 재료는 **같은 글의 다음 편(`ep15s-4`)이 맡은 사건**에서 뽑았다. 근거 문장 —
   `tools/blog-automation/published/2026-08-02-unity-goap-m12-trait-vector.html`:
   > "화면에서는 집 주변에 모닥불이 격자로 빼곡히 깔리고 자리 없음 경고가 반복되는
   >  장면이었습니다."
   > "반경이 만원이 되어 밭 지을 자리까지 잡아먹습니다."
   ⚠️ 이 글 끝의 "다음 개발일지에서는 …" 문단은 **블로그의 다음 글** 예고라 근거로 쓰지
   않았다(ep02s·ep11s·ep12s 가 그 형태로 세 번 틀렸다). 여기서는 그 문단이 **5편 다음의
   회차**를 가리켜 자리가 두 칸 뒤였다.
   🔴 예고 음성 · `outro.next` 카드 · 이 미리보기 셋이 같은 것을 가리킨다:
      **집 둘레가 모닥불로 뒤덮이는 이야기.**

   0.5초 루프 : 가운데 흰 집 하나를 둘러싸고 강조색 불꽃이 안쪽 칸부터 하나씩 켜져
   격자를 다 채우고, 처음부터 되풀이된다. 멈추는 프레임이 없다.

   🔴 본문의 도형(가로 거리 축 · 흐르는 부탁 점)을 여기로 끌고 오지 않았다 — 다음 편의
   사건은 **거리**가 아니라 **수가 멈추지 않고 늘어나는 것**이라 축이 다르다. 색 규약도
   물려받지 않는다: 본문에서는 강조색이 「목수」였지만 여기서는 **흰색 = 집 /
   강조색 = 다음 편이 뒤덮는 것**이다.

   🔴 화면에 수를 한 개도 안 올렸다. 칸 수는 자리로만 있고 세는 라벨이 없다 —
   원문이 모닥불 개수를 세어 주지 않으므로 수를 붙이면 그게 지어낸 수치가 된다.

   ⏱ 등장은 예고 자막 `cue(0)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **격자 간격**으로 갈랐다. 집(획 포함
   x 160.5~191.5 · y 130.5~167.5 · 숨 ±1.5)에 가장 가까운 불꽃 넷의 여유는
   위 **3.3px** · 아래 **5.3px** · 좌우 **6.5px** 다.

   세로 예산(캔버스 307): 최저 = 아래 칸 불꽃 257.7. 49px 남는다.
   가로: 오른쪽 = 304.5(여유 45.5px) · 왼쪽 = 47.5.

   계속 도는 것 = 불꽃이 켜지며 자라는 것(한 칸이 0.059초에 다 자라므로 **프레임당 5.1px**,
   30fps 한 프레임마다 네댓 개가 자라는 중이다) · 불꽃 맥동(2.2px · 8rad/s =
   **프레임당 0.29px**) · 집의 상시 숨 · 0.5초마다의 전면 리셋. */

const DX = 38, DY = 44, MY = 150;
const CELLS = [];
for (let i = -3; i <= 3; i++) {
  for (let j = -2; j <= 2; j++) {
    if (i === 0 && j === 0) continue;                       // 집이 앉은 칸
    CELLS.push({ i, j, d: Math.hypot(i * DX, j * DY) });
  }
}
/* 안쪽부터 바깥으로 — 「집 주변에 깔린다」가 방향으로 읽히게. 정렬 기준을 셋 다 적어
   같은 거리에서도 순서가 흔들리지 않게 했다(결정성). */
CELLS.sort((a, b) => a.d - b.d || a.i - b.i || a.j - b.j);

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    if (spec.label) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 20, 26);
      ctx.restore();
    }

    const pk = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (pk <= 0.02) { ctx.textAlign = 'left'; return; }

    const cx = w / 2;
    const u = frac(t / (spec.loopSec ?? 0.5));
    const n = CELLS.length;

    /* ── 불꽃이 칸마다 하나씩 켜진다 ──────────────── */
    ctx.save();
    ctx.globalAlpha = pk;
    setShadow(ctx, GLOW, 7);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    CELLS.forEach((c, k) => {
      const g = clamp((u * n - k) / 4);                     // 한 칸이 0.059초에 다 자란다
      if (g <= 0.02) return;
      const x = cx + c.i * DX, y = MY + c.j * DY;
      const hh = (9 + 2.2 * (0.5 + 0.5 * Math.sin(t * 8 + k * 0.9))) * g;
      const hw = 6 * g;
      ctx.beginPath();
      ctx.moveTo(x, y - hh);
      ctx.lineTo(x + hw, y + hh * 0.25);
      ctx.lineTo(x, y + hh);
      ctx.lineTo(x - hw, y + hh * 0.25);
      ctx.closePath(); ctx.stroke();
    });
    clearShadow(ctx);
    ctx.restore();

    /* ── 가운데 집 — 자리를 다 뺏기고도 그 자리에 있다 ── */
    const br = 1.5 * Math.sin(t * 7.4);
    ctx.save();
    ctx.globalAlpha = pk;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    ctx.beginPath();
    ctx.moveTo(cx - 14, MY + 16 + br);
    ctx.lineTo(cx - 14, MY - 5 + br);
    ctx.lineTo(cx, MY - 18 + br);
    ctx.lineTo(cx + 14, MY - 5 + br);
    ctx.lineTo(cx + 14, MY + 16 + br);
    ctx.closePath(); ctx.stroke();
    ctx.restore();

    ctx.textAlign = 'left';
  }
};
