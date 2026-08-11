import {
  disp, ease, clamp, spring,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* bandsborn — 멀리서 가만히 있는 것들을, 위쪽 계기가 세고 있었다.

   원문 근거 (`devlog/sessions/2026-08-11.md`):
   > (00:36 W5) "판 시작부터 들판에 사는 것들을 놓을 수 있게 한다. 웨이브가 아니라
   >  원래 거기 살던 것들이라 예고도 예산도 없고 퇴장도 없다."
   > (12:06 ①) "상주가 태어나자마자 도착해 영원히 Staying 이라 HUD 가 습격으로 셌다.
   >  마을 근처에 오지도 않았는데."
   > (13:44) "14마리 밀집 관측은 HUD 총계 표기였음이 확정됐다 -- 앵커 4곳은 실제로
   >  흩어져 있다."

   🔴 **수의 소유가 화면 안에서 지켜진다.** 올라가는 강조색 숫자 바로 아래 라벨이
   「마을 습격 중」이라, 14 는 **화면이 센 총계**로 읽히지 한 무리의 크기로 안 읽힌다.
   이 편의 1번 오배치 경보가 「14마리 무리가 몰려왔다」인데, 그래서 무리는 **처음부터
   네 곳에 흩어져** 선다 — 원문이 부정한 그 오해를 화면이 먼저 부정한다.
   🔴 표식을 4·4·3·3 으로 나눈 것은 원문의 세 인용(자리 **4곳** · 무리 **3~5** ·
   총계 **14**)을 동시에 만족시키는 배치다. **개수를 글자로 적은 곳은 없다.**
   🔴 「상주」·「웨이브」·「Staying」·「HUD」는 화면에도 자막에도 안 쓴다.

   색 규약(이 회차 전체): **흰색 = 판 위에 실제로 있는 것 / 강조색 = 이번 사건에서
   새로 켜지거나 새로 그어진 것.** 무리도 마을도 흰색이고, 그것을 세는 계기만 강조색이다.

   🔴 겹침 감사(캔버스 352×307 · 선 반폭 1.5 · 숨 진폭 · 글로우 blur 10 포함):
     (등장 스프링이 1.05 까지 커지므로 아래 값은 전부 **스프링 최대치로** 잰 것이다.)
     축 A: 강조 숫자 글립 바닥 약 50 · 글로우 **60** ↔ 흰 라벨 글립 꼭대기 **72.5**
       = **12.5px**. 숫자 글로우 좌우는 145~207 이지만 세로로 이미 갈렸다
       (숫자 글로우 바닥 60 ↔ 가장 높은 무리의 꼭대기 **117.6** = **57.6px**).
     축 B(서로 다른 것을 가리키는 흰 글자끼리): 흰 글자가 「마을 습격 중」 하나뿐이라
       성립하지 않는다.
     흰↔흰(서로 다른 물건): 마을 네모 바깥 오른쪽 **93.5** ↔ 첫 무리 왼쪽 **167.2**
       = **73.7px** — 이 빈 칸이 곧 「마을 근처에 오지도 않았는데」다.
       무리끼리 최소 **56.7px**(A2 바닥 167.9 ↔ A4 꼭대기 224.65).

   세로 예산: 최저 = A3 무리 바닥 **260.7**. 46px 남는다(바닥 행 y=306 에 잉크 0).
   가로: 최우 = A4 오른쪽 **316.7**. 35.3px 남는다(좌우 가장자리 띠는 바깥 2열).

   ⏱ 무리는 `since(bornCue)`, 계기는 `since(countCue)` 위에 있다 — `cue` 와 달리
   자막 길이가 바뀌어도 자리가 안 움직인다.

   계속 도는 것(30fps 기준):
     · 무리 14개가 각자 위상이 다른 숨을 쉰다(진폭 0.8 · ω 6.6~8.0) → 개당 약
       0.19px/프레임 × 14
     · 마을 사람 둘이 반지름 16 궤도를 `t × 0.7` rad/s 로 돈다 → 0.37px/프레임
     · 마을 네모 둘레 304px 가 숨을 쉰다(진폭 1.3 · ω 5.8) → 0.25px/프레임 */

const TOWN = { x: 16, y: 116, w: 76, h: 76 };
const BANDS = [
  { x: 186, y: 138, n: 4 },
  { x: 296, y: 148, n: 4 },
  { x: 194, y: 246, n: 3 },
  { x: 300, y: 244, n: 3 }
];
const OFF4 = [[-13, -8], [0, -10], [13, -6], [3, 8]];
const OFF3 = [[-11, -6], [2, -9], [11, 3]];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const g0 = ease(cue(spec.bornCue ?? 0, 0.15, 0.30));
    if (g0 <= 0.02) { ctx.textAlign = 'left'; return; }

    /* ── 마을 네모 + 사람 둘 (흰색) ─────────────────── */
    const bre = 1.3 * (1 + Math.sin(t * 5.8));
    ctx.save();
    ctx.globalAlpha = g0;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, TOWN.x + bre, TOWN.y + bre, TOWN.w - bre * 2, TOWN.h - bre * 2, 9);
    ctx.stroke();
    ctx.restore();

    const tcx = TOWN.x + TOWN.w / 2, tcy = TOWN.y + TOWN.h / 2;
    for (let i = 0; i < 2; i++) {
      const a = t * 0.7 + i * Math.PI;
      ctx.save();
      ctx.globalAlpha = g0;
      ctx.translate(tcx + Math.cos(a) * 16, tcy + Math.sin(a) * 16);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, -4, -9, 8, 18, 4);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 들판 네 자리에서 무리가 튀어나온다 (흰색) ────
       튀어나온 뒤로는 **자리를 안 뜬다** — 이 샷의 사건이 「가만히 있었다」이므로
       걸어 다니게 하면 안 된다. 숨만 쉰다. */
    const born = since(spec.bornCue ?? 0);
    BANDS.forEach((b, bi) => {
      const k = ease(clamp((born - 0.35 - bi * 0.30) / 0.28));
      if (k <= 0.01) return;
      const sc = spring(k);
      const offs = b.n === 4 ? OFF4 : OFF3;
      offs.forEach(([ox, oy], mi) => {
        const br = 0.8 * (1 + Math.sin(t * (6.6 + mi * 0.45) + bi * 1.3 + mi));
        ctx.save();
        ctx.globalAlpha = g0 * k;
        ctx.translate(b.x + ox * sc, b.y + oy * sc + br);
        ctx.scale(sc, sc);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3 / sc; ctx.lineJoin = 'round';
        roundRect(ctx, -3.5, -8, 7, 16, 3.5);
        ctx.stroke();
        ctx.restore();
      });
    });

    /* ── 위쪽 계기 (강조색 숫자 + 흰 라벨) ───────────
       숫자는 0 에서 시작해 원문의 총계까지 올라가 멈춘다. */
    const cnt = since(spec.countCue ?? 1);
    const ca = ease(clamp((cnt - 0.10) / 0.35));
    if (ca > 0.02) {
      const target = spec.count ?? 0;
      const n = Math.round(target * ease(clamp((cnt - 0.25) / 0.70)));
      const txt = String(n);
      ctx.save();
      ctx.globalAlpha = g0 * ca;
      ctx.textAlign = 'center';
      ctx.font = disp(900, 36);
      setShadow(ctx, GLOW, 10);
      ctx.fillStyle = tone('accent');
      const tw = ctx.measureText(txt).width;
      ctx.fillText(txt, clamp(176, 16 + tw / 2, w - 16 - tw / 2), 48);
      clearShadow(ctx);
      ctx.restore();

      if (spec.countLabel) {
        let fs = 13; ctx.font = disp(800, fs);
        while (fs > 9 && ctx.measureText(spec.countLabel).width > 200) {
          fs -= 0.5; ctx.font = disp(800, fs);
        }
        const lw = ctx.measureText(spec.countLabel).width;
        ctx.save();
        ctx.globalAlpha = g0 * ca;
        ctx.textAlign = 'center'; ctx.fillStyle = tone('ink');
        ctx.fillText(spec.countLabel, clamp(176, 14 + lw / 2, w - 14 - lw / 2), 82);
        ctx.restore();
      }
    }

    ctx.textAlign = 'left';
  }
};
