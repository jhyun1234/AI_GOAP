import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, camAt
} from '../../../engine/lib.js';

/* onecount — 일곱 개의 죽음이 전부 한 칸으로 흘러 들어가고, 그 칸에는 「+1」 하나만
   되풀이해서 켜진다.

   원문 근거:
   > "누가 죽었냐고? 사망 카운터 +1. 내가 가진 건 그게 전부인데."
   ← 원문이 옛 코드의 속마음을 **사람 말로 옮겨 준** 대목이라, 「사망 카운터」라는 낱말을
      안 쓰고도 뜻이 선다(ADR-V25-13 ①풀어쓰기). 화면 라벨 「내가 가진 것」도 같은 문장에서
      글자 그대로 왔다. 화면의 「+1」은 원문 문자열 그대로다 — 총합(7 같은 수)은 원문에
      없으므로 **누적값을 숫자로 적지 않는다.**

   🔴 색 규약: 이 샷에는 **강조색이 한 점도 없다.** 이 편에서 강조색은 「이름」인데,
   여기서 남는 것은 이름이 아니라 수뿐이기 때문이다.

   ── 2026-08-12 개정 v3 (역동성 시험) ──────────────────────────────
   🔴 **두 샷으로 갈랐다(`spec.phase`).** 그리고 **카메라를 서로 반대로 걸었다.**
     phase 0 (S2a · 3.2초) = 무덤 클로즈업에서 **확 빠지며**(1.75 → 1.0) 일곱이 다시
       드러나고, 그대로 한 칸으로 쓸려 내려간다. sweep 효과음.
     phase 1 (S2b · 3.9초) = 그 칸으로 **가장 세게 파고든다**(1.0 → 2.6). 이 편 최대 배율.
   🔑 앞 샷(S1b)이 2.2 로 끝나므로 S2a 를 1.75 에서 시작해 **컷을 사이에 둔 줌 연결**을
      만든다 — 컷 직후 배율이 비슷해 튀지 않고, 이어서 빠지니 「일곱이었구나」가 온다.
   🔑 phase 1 의 최대 배율이 「내가 가진 건 그게 전부」와 같은 자리인 것이 요점이다.
      화면에 남는 것도 그 칸 하나뿐이어야 한다 — 그래서 위의 무덤·줄기를 아예 안 그린다.

   🔴 잉크가 세로 89~270(59%)이던 것을 62~288(**74%**)로 폈다. 반지름 15 → 19,
   칸 48 → 60 높이. 일곱을 한 줄로 두려면 반지름이 19 를 못 넘는다(간격 계산은 아래).

   ⏱ 흘러내림은 `since(0)` 위에 세웠다 — **`cue` 가 아니라 `since` 인 것이 효과음의
   전제다.** `since` 는 자막이 시작된 뒤 흐른 초라 `delayMs` 와 원점이 같다.
   🔴 **샷을 갈라도 이 원점은 안 움직인다** — `since(0)` 의 0 은 「그 샷의 첫 자막」이고,
      가른 뒤 phase 0 의 첫 자막은 가르기 전 S2 의 첫 자막과 같은 줄이다. 그래서
      `sfx.delayMs 285` 를 한 자도 안 고쳤다(`at` 만 1 → 0 으로 옮긴 샷이 S3 다).
   🔑 카메라 무브(0 → 0.9초)와 줄기 흘러내림(0.20 → 0.91초)이 **같은 창**에 있다.
      우연이 아니라 맞춘 것이다 — 빠지는 동안 줄기가 자라야 둘이 한 사건으로 읽힌다.

   겹침 감사(축 A 강조↔흰 / 축 B 흰↔흰):
     축 A · 강조색 0개 — 잴 것이 없다.
     축 B · 흰 글자 둘의 최소 간격 = 「+1」 바닥 244(baseline · 내림선 없는 글자)와
            「내가 가진 것」 글립 꼭대기 284−10 = 274 → **30px**. 그 사이를 칸 바깥
            아래변(y 261.5)이 가른다. 정렬도 둘 다 가운데(x 176)라 좌우로 어긋나 붙지 않는다.
            폭 상한도 걸었다 — 라벨은 200px 를 넘으면 글자 크기가 줄어든다.
            흰 도형끼리 = 동그라미 바깥 바닥 106.7 과 줄기 시작 109 는 **의도한 쌍**이다.
            같은 줄 동그라미 사이는 48.67−43.4 = **5.3px** 로 역시 의도한 쌍이다.

   세로 예산(캔버스 307): 최저 = 라벨 내림선 약 **288**(19px 남음).
     최고 = 동그라미 바깥 꼭대기 84−19−1.2−1.5 = **62.3**.
   가로: 동그라미 왼쪽 30−21.7 = **8.3** / 오른쪽 322+21.7 = **343.7**. 띠에서 좌우 6.3.
   phase 0 시작 배율(1.75 · focus [0.5, 0.27] = 화면 중앙에 y 82.9):
     보이는 창 = x 75.4~276.6 · y −4.8~170.6 → 바깥 동그라미 둘이 밖으로 나간다.
   phase 1 최대 배율(2.6 · focus [0.5, 0.795] = 화면 중앙에 y 244):
     보이는 창 = x 108~244 · y 185~303 → 칸(101~251)의 좌우가 잘리고 라벨은 안에 남는다.
   → 두 샷 다 `checks.edge` 선언 대상이다.

   계속 도는 것(30fps 기준): 줄기의 점선이 초당 46px 로 내려간다 → **1.5px/프레임**.
   「+1」은 0.62초마다 켜졌다 꺼진다 — phase 1 은 무브가 끝난 뒤 이것 하나로 정적을 막는다
   (0.62초 주기라 3초 검사에 근처도 못 간다). */

const XS = [30, 78.7, 127.3, 176, 224.7, 273.3, 322];
const CY = 84, R = 19;
const BOX = { x: 101, y: 200, w: 150, h: 60 };

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    camAt(ctx, w, h, spec, t);
    ctx.textBaseline = 'alphabetic';

    const ph = spec.phase ?? 0;
    const st = ph === 0 ? ease(cue(0, 0.15, 0.25)) : 1;
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    /* ── phase 0 : 일곱 개의 무덤이 한 칸으로 흘러 내려간다 ── */
    if (ph === 0) {
      for (let i = 0; i < XS.length; i++) {
        ctx.save();
        ctx.globalAlpha = st;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.arc(XS[i], CY, R + 1.2 * (0.5 + 0.5 * Math.sin(t * 4.2 + i * 0.7)), 0, Math.PI * 2);
        ctx.stroke();
        ctx.restore();
      }

      const s0 = since(0);
      ctx.save();
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineCap = 'butt';
      ctx.setLineDash([8, 8]);
      ctx.lineDashOffset = -((t * 46) % 16);
      for (let i = 0; i < XS.length; i++) {
        const k = ease(clamp((s0 - 0.20 - i * 0.075) / 0.26));
        if (k <= 0.01) continue;
        const x0 = XS[i], y0 = CY + R + 6;
        const x1 = 176 + (i - 3) * 13, y1 = BOX.y;
        ctx.globalAlpha = st * 0.85;
        ctx.beginPath();
        ctx.moveTo(x0, y0);
        ctx.lineTo(lerp(x0, x1, k), lerp(y0, y1, k));
        ctx.stroke();
      }
      ctx.setLineDash([]);
      ctx.restore();
    }

    /* ── 내가 가진 것 — 칸 하나 ─────────────────────
       phase 0 에서는 줄기가 도착한 뒤(0.75초) 열리고, phase 1 에서는 컷과 함께 이미 있다. */
    const box = ph === 0 ? ease(clamp((since(0) - 0.75) / 0.30)) : 1;
    if (box > 0.01) {
      ctx.save();
      ctx.globalAlpha = st * box;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, BOX.x, BOX.y, BOX.w, BOX.h, 10);
      ctx.stroke();
      ctx.restore();
    }

    /* 「+1」 — phase 1 의 유일한 사건. 되풀이해서 켜졌다 꺼진다 */
    const open = ph === 0 ? 0 : ease(clamp(since(0) / 0.30));
    if (open > 0.01) {
      const u = frac(t / 0.62);
      const on = ease(clamp(u / 0.10)) * (1 - ease(clamp((u - 0.55) / 0.14)));
      if (on > 0.01) {
        ctx.save();
        ctx.globalAlpha = st * open * on;
        ctx.textAlign = 'center'; ctx.fillStyle = tone('ink');
        ctx.font = mono(700, 34);
        ctx.fillText(spec.markLabel ?? '+1', 176, 244);
        ctx.restore();
      }
    }

    if (spec.haveLabel) ctext(spec.haveLabel, 176, 284, 900, 14, tone('sub'), 200, st * box);

    ctx.textAlign = 'left';
  }
};
