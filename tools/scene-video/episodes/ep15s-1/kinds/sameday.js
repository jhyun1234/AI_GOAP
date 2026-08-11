/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다.
   경로 좌표를 적을 땐 `(경로)` 라고 표시한다. */
import {
  disp, ease, clamp,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* sameday — 훅(SH)과 증상(S1). 이름표는 여섯인데 하루가 똑같다.

   원문 근거:
   > "화면에서 벌어지던 일이 딱 그랬습니다. 이름표와 말풍선 말투는 여섯 가지인데,
   >  하루의 실제 일과는 겹칩니다."
   > "나무를 하러 가는 순서도, 밭에 가는 타이밍도, 여가를 즐기는 빈도도 여섯이 같습니다."
   > "겨울이 다가올 때만 잠깐 갈라지고, 봄이 오면 다시 하나로 합쳐집니다."

   위쪽 = 말풍선 여섯 개(저마다 다른 표정). 아래 = 하루 띠 여섯 줄(무늬가 똑같다).
   여섯 줄의 마디가 **세로 기둥으로 딱 맞아떨어지는 것**이 이 그림의 전부다.
   라벨을 다 가려도 「위는 다 다른데 아래는 한 줄을 복사한 것 같다」가 읽힌다.

   🔑 **SH 와 S1 이 같은 kind 를 쓰는 것은 돌려막기가 아니라 설계다.**
   훅은 「같다」만 보여주고(`blipAt` 없음), S1 은 같은 그림 안에서 `blipAt` 로
   **한 번 어긋났다 도로 맞는 것**을 더한다. 원문이 그 둘을 한 문단에 붙여 쓴다.
   여기서 그림을 새로 세우면 시청자가 다른 마을로 읽는다.
   🔴 **그 「한 번」은 자막 1 안에서만 일어난다** — 자막 0 을 말하는 동안(「…똑같고요」)
   여섯 줄은 **끝까지 기둥으로 맞아 있다.** 1차본은 `frac(t/2.0)` 루프라 세 번 터졌고
   첫 발생이 자막 0 한복판이었다(아래 「어긋남」 절).

   🔴 **강조색이 이 파일에 한 개도 없다.** 이 회차의 색 규약은
   「강조색 = 성격이 실제로 닿은 것」인데, 이 두 샷은 성격이 아무 데도 안 닿은 상태다.
   그래서 흰색과 강조색이 같은 픽셀을 나눠 가질 일이 **구조적으로 없다**
   (바닥선은 track = 흰색 12% 라 흰색 계열이다).

   세로 예산(캔버스 307): 최저 = 여섯째 띠 아래 가장자리 **262**
   (LANE_Y0 104 + 5×LANE_H 30 = 254, + 숨 2, + SEG_H/2 6). 45px 남는다.
   그다음이 「겨울」 글립 바닥 94(기준선 90).
   가로: 띠 오른쪽 끝 **330**(22px 남는다) · 말풍선 오른쪽 끝 330 · 왼쪽 끝 24.
   🔴 등장에 **이동이 0** 이다(말풍선은 제자리에서 자라고 띠는 폭이 커진다) —
   밖에서 밀려 들어오는 요소가 없으므로 위 값이 그대로 최댓값이다.

   계속 도는 것: ①마디 무늬가 72px/초로 흐른다(30fps 프레임당 **2.4px**,
   여섯 줄 × 마디 약 7개 × 양끝 = 84개 모서리가 매 프레임 옮겨 앉는다)
   ②말풍선 여섯의 상시 숨(진폭 2px · 7.4rad/s → 프레임당 **0.49px**).
   🔑 ①②는 **사건이 아니라 상시 운동**이라 `t` 로 돈다. **사건(어긋남)은 자막에 물린다** —
   이 구분을 1차본이 어겨서 반려됐다. 어긋남이 `t` 로 돌면 「말이 A 를 부를 때 화면이 B」가
   구조적으로 생긴다. 상시 운동은 **뜻을 지지 않으므로** `t` 로 돌아도 안전하다. */

const LANE_X0 = 24, LANE_X1 = 330;
const LANE_Y0 = 104, LANE_H = 30;      // 104 · 134 · 164 · 194 · 224 · 254
const SEG_H = 12;                      // 마디 높이 (y ± 6)
const BUB_W = 44, BUB_H = 26, BUB_Y = 40;   // 말풍선 상자 (y 40~66, 꼬리 72)

/* 마디 무늬 — 한 묶음(126px) 안에 폭이 다른 마디 셋. 여섯 줄이 **같은 묶음**을 쓰므로
   세로 기둥이 딱 맞아떨어진다. 그게 이 샷이 말하려는 전부다. */
const GROUP = [[0, 30], [46, 18], [84, 24]];
const GP = 126;
const FLOW = 72;                       // px/초

/* S1 의 벌어짐 — 줄마다 다른 가로 어긋남. 최대 |33| 이고 띠 밖은 잘라내므로
   가장자리 예산에 영향이 없다. */
const FANOFF = [-33, 19, -11, 33, -22, 8];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    /* 캔버스 글자는 폭을 재서 안쪽에 가둔다 — 폭 제한은 상자가 아니라 **글자**에 건다. */
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

    const c0 = cue(spec.sameCue ?? 0, 0.15, 0.90);
    const st = ease(clamp(c0 / 0.10));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const n = Math.max(2, spec.lanes ?? 6);
    const LEN = LANE_X1 - LANE_X0;

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ── 어긋남(S1 전용) ────────────────────────────────
       🔴 **1차 반려를 고친 자리.** 예전에는 `frac(t / loopSec)` 로 돌았다 — 자막과 무관한
       루프라 갈라짐이 샷 상대 시각 **1.00 · 3.00 · 5.00** 세 번 터졌고, **첫 발생(1.03~1.53)이
       통째로 자막 0 안**에 있었다. 그 순간 음성은 「밭에 가는 때도 여가도 똑같고요」인데
       화면은 여섯 줄이 ±33px 어긋나고 「겨울」 라벨이 알파 1.0 이었다 —
       **말이 A 를 부르는데 화면이 정반대 B 를 켜고 있었다**(ep02s `tripwire`·ep00i `runmark`
       와 같은 형태). 발화 개시(자막1 시작 + wav 선행 무음 0.456 = ts 2.985)보다 1.95초 일렀고
       가이드 한도는 0.67초다.
       → **자막 1 에 물렸다.** `since(blipCue)` 는 그 자막이 시작된 뒤 흐른 초라
       자막 0 동안에는 음수 → 0 이고, 봉투가 열리는 것은 자막 1 이 시작한 뒤뿐이다.

       ⏱ 자막 1 = 「겨울에만 잠깐 갈라졌다 합쳐져요.」(**실측 rel 2.882** · 선행 무음 0.456).
       🔴 **어절 시각은 wav 실측만 쓴다**(2차 검수 20ms RMS 포락선 · 음절 핵 기준):
       「겨울」 **0.456** · 「갈라졌다」 **1.30** · 「합쳐져요」 **1.90**.
       ⚠️ 1차본은 `dur` 을 글자 수로 균등 배분해 1.52 · 2.19 로 적었다 — `dur` 에 **후행 무음이
       0.56초** 들어 있어 균등 배분은 반드시 뒤로 밀린다.
         · 열림 0.30~0.54  → 알파 0.6 이 **0.452**
           (검수 60fps 열거 실측 = abs 2.967 vs 「겨울」 발화 개시 2.985 → **0.018초 차**)
         · 닫힘 2.00~2.42  → 「합쳐져요」(**1.90**)보다 **0.10초 늦게** 시작해 그 말이 끝날 즈음
           도로 맞는다. 말이 먼저 나오고 그림이 따라 닫히는 순서다
       🔑 `cue` 가 아니라 `since` 를 쓴 이유는 `sfx.delayMs` 와 같다 — **자막 길이가 바뀌어도
       이 시각이 안 움직인다.** (이 샷에는 sfx 가 없지만 규약을 같게 둔다.) */
    const bp = spec.blipAt;
    const sb = Math.max(0, since(spec.blipCue ?? 1));
    const fan = bp == null ? 0
      : ease(clamp((sb - 0.30) / 0.24)) * (1 - ease(clamp((sb - 2.00) / 0.42)));

    /* ── 하루 띠 여섯 줄 ────────────────────────────────
       🔴 setLineDash 를 안 쓴다(결정성 — 긴 점선이 30fps 3패스에서 알파가 갈린 전례).
       마디를 fillRect 조각으로 직접 놓고 위상은 양수로 정규화한다. */
    const ph = ((t * FLOW) % GP + GP) % GP;
    for (let i = 0; i < n; i++) {
      const idle = 2.0 * (0.5 + 0.5 * Math.sin(t * 7.4 + i * 1.7));
      const y = LANE_Y0 + i * LANE_H + idle;
      const shift = -ph + fan * FANOFF[i % FANOFF.length];

      /* 띠 바닥선 — 폭이 자란다(밖에서 밀려 들어오지 않는다) */
      const half = LEN / 2 * st;
      ctx.save();
      ctx.globalAlpha = st * 0.9; ctx.fillStyle = tone('track');
      ctx.fillRect(LANE_X0 + LEN / 2 - half, y - 1.5, half * 2, 3);
      ctx.restore();

      ctx.save();
      ctx.globalAlpha = st; ctx.fillStyle = tone('ink');
      for (let g = -2 * GP; g < LEN + GP; g += GP) {
        for (let m = 0; m < GROUP.length; m++) {
          const x0 = LANE_X0 + LEN / 2 - half + g + GROUP[m][0] + shift;
          const a = Math.max(LANE_X0 + LEN / 2 - half, x0);
          const b = Math.min(LANE_X0 + LEN / 2 + half, x0 + GROUP[m][1]);
          if (b > a) ctx.fillRect(a, y - SEG_H / 2, b - a, SEG_H);
        }
      }
      ctx.restore();
    }

    /* ── 말풍선 여섯 : 저마다 다른 표정 ──────────────────
       🔴 이름표를 안 붙인다. 성격 이름을 올리는 순간 「이 표정이 이 성격이다」라는
       **원문에 없는 분류 주장**이 된다. 여기서 이것들이 지는 뜻은
       「저마다 다른 말풍선」 하나뿐이다. */
    const faces = spec.faces ?? [];
    const pitch = (LANE_X1 - LANE_X0 - BUB_W) / (n - 1);
    for (let i = 0; i < n; i++) {
      const idle = 2.0 * (0.5 + 0.5 * Math.sin(t * 7.4 + i * 1.7));
      const cx = LANE_X0 + BUB_W / 2 + i * pitch;
      const by = BUB_Y - idle;
      const sc = 0.86 + 0.14 * ease(clamp((c0 - i * 0.012) / 0.12));   // 제자리에서 자란다
      const bw = BUB_W * sc, bh = BUB_H * sc;
      ctx.save();
      ctx.globalAlpha = st;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, cx - bw / 2, by + (BUB_H - bh) / 2, bw, bh, 5); ctx.stroke();
      /* 꼬리 — 「말풍선」으로 읽히게 하는 최소한의 획 */
      ctx.beginPath();
      ctx.moveTo(cx - 8, by + BUB_H - (BUB_H - bh) / 2);
      ctx.lineTo(cx - 3, by + BUB_H + 6 - (BUB_H - bh) / 2);
      ctx.lineTo(cx + 2, by + BUB_H - (BUB_H - bh) / 2);
      ctx.stroke();
      ctx.restore();
      if (faces[i]) ctext(faces[i], cx, by + 18, 800, 14, tone('ink'), BUB_W - 8, st);
    }

    /* ── 「겨울」 : 어긋나 있는 동안에만 뜬다(S1 전용) ────────
       알파가 어긋남과 **같은 봉투**를 탄다. 이제 그 봉투가 자막 1 위에 있으므로
       라벨이 뜨는 시각 = 「겨울」을 말하는 시각이다(알파 0.6 이 since(1) **0.452**,
       발화 개시 0.456 — 오차 0.004초. 가이드 한도 0.67초).
       읽는 창(알파 ≥ 0.6) = since(1) 0.452 ~ 2.152 = **1.70초** (2자에 넉넉하다).
       🔴 1차본은 이 라벨이 **자막 0 위에서도** 알파 1.0 으로 떴다 — 그때 말은 「똑같고요」였다.
       🔑 그리고 이 라벨은 **사건을 지지 않는다** — 어긋났다 도로 맞는 것은 라벨 없이 읽히고,
       「겨울」은 그 자리에 이름을 붙일 뿐이다. */
    if (spec.coldLabel && bp != null) {
      ctext(spec.coldLabel, LANE_X0 + LEN / 2, 90, 900, 14, tone('ink'), 90, st * fan);
    }

    ctx.textAlign = 'left';
  }
};
