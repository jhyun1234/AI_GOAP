import {
  disp, ease, clamp, lerp, spring,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* reserved — 다가간 한 곳에서만 생기고, 나머지는 계속 비어 있다.

   원문 근거 (`devlog/sessions/2026-08-11.md` 12:06 커밋 본문):
   > "사용자 설계를 채택했다: 판 시작엔 좌표만 예약하고, 주민이 시야x1.5(15칸)에 들면
   >  부화한다. 존재하지 않는 적은 HUD 가 셀 수도 F 가 찾아갈 수도 없다 -- ①②의
   >  구조적 해소다."

   화면 라벨 「잡아 둔 자리」는 원문 **「좌표만 예약하고」의 풀이**다.
   🔴 「예약」·「부화」·「무장」·「회수」·「재무장」·「히스테리시스」는 화면에도 자막에도
   안 쓴다 — 늑대·무덤처럼 게임 안 사물로 **들리는데 게임에 그런 물건이 없는** 낱말이라,
   「게임 안 사물은 어려운 낱말이 아니다」라는 잣대가 여기서 오작동한다.
   🔴 **반경 수치(15칸·20칸)도 안 올렸다.** 같은 로그의 뒤쪽 커밋(15:03 · 시야 10→7)에서
   그 두 값이 10칸·14칸으로 바뀌었다 — 사건 커밋 범위 밖이라 이 편의 서술은 무사하지만,
   수를 올렸으면 그 순간 낡은 수가 됐을 자리다.

   색 규약(이 회차 전체): 흰색 = 판 위에 실제로 있는 것 / 강조색 = 이번 사건에서 새로
   켜지거나 새로 그어진 것. **잡아 둔 자리는 판 위에 아직 아무것도 없는 곳**이라 강조색
   점선이고, 다가가서 생긴 무리는 판에 실제로 선 것이라 흰색이다. 이 색 교대가 곧
   「없던 것이 생겼다」다.

   🔴 겹침 감사(캔버스 352×307 · 선 반폭 1.5 · 글로우 blur 6 포함):
     축 A — 🔴 **첫 자리는 좌표로 못 벌린다(같은 자리에서 강조색이 흰색으로 바뀐다).
       그래서 시간으로 갈랐다:**
         강조 점선 알파 = `1 − ease(clamp((since(0) − 1.30) / 0.36))` → **1.66초에 0**
         흰 무리 알파   = `ease(clamp((since(0) − 1.74) / 0.35))`     → **1.74초에 출발**
       **겹치는 프레임 0** (여유 0.08초 = 30fps 로 2.4프레임). 「거의 안 겹친다」가 아니다.
     축 A — 나머지 세 자리는 좌표로 갈렸다. **숨 진폭(첫 자리 1.5 · 나머지는 마지막
       자막에서 2.6 까지)과 획 반폭 1.5 와 글로우 6 을 다 더해서** 잰다:
         걷는 표식 오른쪽 끝 **151.5** ↔ 첫 원의 가장 부푼 왼쪽 **163**(= 198 − 27.5
         − 1.5 − 6) = **11.5px**. 원끼리는 둘 다 강조색이라 무관.
         흰 라벨 「잡아 둔 자리」 글립 바닥 약 **46** ↔ 가장 높은 원(c2)의 가장 부푼
         꼭대기 **81.9** = **35.9px**.
     축 B(서로 다른 것을 가리키는 흰 글자끼리): 흰 글자가 하나뿐이라 성립하지 않는다.
     흰↔흰(서로 다른 물건): 걷는 표식 오른쪽 **151.5** ↔ 들어선 무리 왼쪽 **181.3**
       = **29.8px**. 마을 네모 바깥 오른쪽 **89.5** ↔ 걷는 표식 왼쪽(출발) **98.5**
       = 9px 인데 **이건 의도한 쌍**이다(자기 마을에서 막 나오는 표식).

   세로 예산: 최저 = c3 의 가장 부푼 바닥 **282.1**. 25px 남는다(바닥 행 y=306 에 잉크 0).
   가로: 최우 = c4 의 가장 부푼 오른쪽 **332.1**. 20px 남는다(가장자리 띠는 바깥 2열).

   ⏱ 틀은 `cue(holdCue)`, 걷기·자리 사라짐·무리 들어섬은 `since(holdCue)`,
     마지막 강조(남은 세 자리가 크게 숨쉬기)는 `since(emptyCue)` 위에 있다.

   계속 도는 것(30fps 기준):
     · 강조 점선 원 넷(둘레 합 4×2π×26 = 653px)의 대시가 `-t × 22` 로 흐른다
     · 원 반지름이 `1.5·sin(...)` 로 숨 쉬고, 마지막 자막에서 진폭이 **2.6 으로 커진다**
     · 걷는 표식이 42px 를 0.95초에 → 1.5px/프레임
     · 잡아 둔 자리 넷이 0.12초 간격으로 하나씩 켜진다(`hold` 0.05~0.76초) */

const TOWN = { x: 16, y: 112, w: 72, h: 72 };
const WALK = { x0: 104, x1: 146, y: 148 };
const SPOTS = [
  { x: 198, y: 148 },
  { x: 292, y: 118 },
  { x: 196, y: 246 },
  { x: 296, y: 240 }
];
const R = 26;
const OFF3 = [[-11, -6], [2, -9], [11, 3]];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const g0 = ease(cue(spec.holdCue ?? 0, 0.15, 0.30));
    if (g0 <= 0.02) { ctx.textAlign = 'left'; return; }

    const hold = since(spec.holdCue ?? 0);
    const gone = ease(clamp((hold - 1.30) / 0.36));   // 첫 자리가 사라진다 (1.66초에 1)
    const born = ease(clamp((hold - 1.74) / 0.35));   // 무리가 들어선다   (1.74초에 출발)
    const push = ease(clamp(since(spec.emptyCue ?? 1) / 0.60));

    /* ── 흰 라벨 ─────────────────────────────────── */
    if (spec.label) {
      let fs = 13; ctx.font = disp(800, fs);
      while (fs > 9 && ctx.measureText(spec.label).width > 200) {
        fs -= 0.5; ctx.font = disp(800, fs);
      }
      const tw = ctx.measureText(spec.label).width;
      ctx.save();
      ctx.globalAlpha = g0;
      ctx.textAlign = 'center'; ctx.fillStyle = tone('ink');
      ctx.fillText(spec.label, clamp(176, 14 + tw / 2, w - 14 - tw / 2), 42);
      ctx.restore();
    }

    /* ── 마을 네모 (흰색) ───────────────────────────── */
    const bre = 1.2 * (1 + Math.sin(t * 5.4));
    ctx.save();
    ctx.globalAlpha = g0;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, TOWN.x + bre, TOWN.y + bre, TOWN.w - bre * 2, TOWN.h - bre * 2, 9);
    ctx.stroke();
    ctx.restore();

    /* ── 잡아 둔 자리 넷 (강조색 점선) ───────────────
       첫 자리만 표식이 다가오면 사라진다. 나머지 셋은 끝까지 빈 채로 숨만 쉰다. */
    SPOTS.forEach((s, i) => {
      const a = g0 * (i === 0 ? (1 - gone) : 1) * ease(clamp((hold - 0.05 - i * 0.12) / 0.35));
      if (a <= 0.01) return;
      const amp = 1.5 + 1.1 * (i === 0 ? 0 : push);
      const rr = R + amp * Math.sin(t * (4.6 + i * 0.6) + i * 1.7);
      ctx.save();
      ctx.globalAlpha = a;
      setShadow(ctx, FAIL_GLOW, 6);
      ctx.strokeStyle = tone('fail'); ctx.lineWidth = 3;
      ctx.setLineDash([9, 8]); ctx.lineDashOffset = -t * 22;
      ctx.beginPath(); ctx.arc(s.x, s.y, rr, 0, Math.PI * 2); ctx.stroke();
      ctx.setLineDash([]);
      clearShadow(ctx);
      ctx.restore();
    });

    /* ── 다가가자 그 자리에만 들어선 무리 (흰색) ────── */
    if (born > 0.01) {
      const sc = spring(born);
      OFF3.forEach(([ox, oy], i) => {
        const br = 0.8 * (1 + Math.sin(t * (6.4 + i * 0.5) + i * 1.2));
        ctx.save();
        ctx.globalAlpha = g0 * born;
        ctx.translate(SPOTS[0].x + ox * sc, SPOTS[0].y + oy * sc + br);
        ctx.scale(sc, sc);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3 / sc; ctx.lineJoin = 'round';
        roundRect(ctx, -3.5, -8, 7, 16, 3.5);
        ctx.stroke();
        ctx.restore();
      });
    }

    /* ── 마을에서 나와 다가가는 표식 (흰색) ─────────── */
    const wk = ease(clamp((hold - 0.30) / 0.95));
    ctx.save();
    ctx.globalAlpha = g0;
    ctx.translate(lerp(WALK.x0, WALK.x1, wk), WALK.y + 0.7 * Math.sin(t * 7.2));
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, -4, -9, 8, 18, 4);
    ctx.stroke();
    ctx.restore();

    ctx.textAlign = 'left';
  }
};
