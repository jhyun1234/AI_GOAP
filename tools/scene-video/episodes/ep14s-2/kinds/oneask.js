import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* oneask — 묻는 칸 하나를 빼내자, 값이 처음으로 되튕기지 않는다.

   원문 근거:
   > "선불을 요구하는 성격이 묻는 건 '저 사람이 값을 치를 능력이 있나'이지
   >  '지금 내 손이 비어 있나'가 아닙니다. 그래서 선불 판정에서 수령 공간 조건을 빼냈습니다."
   > "손이 차 있으면 후불로 수락하고…"

   🔴 기본 도형의 네 번째 변주이자 **뒤집는 자리**다. 앞의 세 그림에서 매번 되튕기던 것이
   여기서는 **안 돌아온다** — 되받기가 반복이 아니라 뒤집기인 이유가 이것이다.
   🔴 화면에서 실제로 빠지는 것은 **묻는 칸 한 장**이지 벽이 아니다. 벽(상한 8)은 그대로
   있고 규칙만 바뀌었다는 원문의 뜻을 지키려고, 이 그림에는 벽을 아예 안 그렸다.

   🔴 어려운 낱말을 화면에서 뺐다 — 「선불 판정」·「수령 공간 조건」 대신 원문이 따옴표로
   직접 써 준 두 문장(「값을 치를 능력이 있나」·「지금 내 손이 비어 있나」)을 그대로 올렸다.

   ── 2차 수정 (2026-08-10 검수 반려) ────────────────────────────────
   🔴 **A. 빠지는 물음의 글자가 상자보다 먼저 죽고 있었다.** 옛 식은 `cue(0, 0.15, 0.55)` 에
      글자 알파 `1 − ease(k / 0.30)` 이라, 실측 자막 길이(2.246초)에서 **샷 시작 시점에 이미
      k = 0.108** 이고 최대 알파 **0.145**, ts 0.26초에 0 이었다. 즉 화면 전체가 진입 페이드를
      끝내기도 전에·나레이션이 입을 떼기(0.310초) 전에 사라져서, 시청자가 본 것은
      **「이름 없는 빈 상자가 접힘」**이었다. 이 편의 사건이 「묻는 조건 **하나**를 뺀 것」인데
      **어느 물음이 빠졌는지가 화면에서 없어진 것**이다(`ADR-V25-15` 의 반대 방향 실패 —
      그림은 읽히는데 정보를 지는 글자가 안 읽힌다).
      고친 것 셋(전부 시간표만 · 좌표·자막·`delayMs` 불변):
        ① `cue` span **0.55 → 0.85** — 창을 넓혀 세 사건(글자·상자·띠)이 줄 서게 한다.
        ② 글자 알파 `1 − ease(clamp((k − 0.42) / 0.16))` — **k 0.42 까지 온전**, 0.58 에 0.
        ③ 상자 `1 − ease(clamp((k − 0.50) / 0.24))` — **글자가 다 죽은 뒤에야** 본격적으로 접힌다
           (글자가 0 이 되는 순간 상자 높이가 아직 32.6px 이라 글자가 사각형 밖으로 안 넘친다).
      실측 자막 2.246초 기준(엔진 진입 페이드 ENTER 0.26 · 0.12→1.0 을 곱한 값 —
      kind 안의 알파만 보면 실제보다 밝게 나온다). **마스터가 0.5ms 간격으로 스캔한 정본**:
      · 알파 ≈1.0 구간 **ts 0.249~0.728초(0.479초)** · **알파 0.6 이상 ts 0.138~0.857초(0.719초)**
      · 완전 소멸 1.044초 · **발화 개시(0.310초) 시점 알파 1.0000**
      🔴 초고 주석의 「0.26~0.815 · 0.78초 · 0.135~0.90」은 **낙관적이었다** — ts 0.815 의 실제
      알파는 0.78(1.0 이 아니고), ts 0.90 은 0.41(0.6 이 아니다). k→ts 환산이 아니라
      **알파 판정 자체가** 어긋났다. 🔑 이 수를 옮겨 적을 때는 **엔진 페이드를 곱했는지** 먼저 봐라.
   🔴 **B. 품삯이 목수에 닿으면 「즉시 지급」이 된다.** 원문은 *"손이 차 있으면 **후불로 수락**
      하고, 이미 만들어둔 정산 연기 로직이 나중에 처리합니다"* 다. 게다가 이 편은 S1·S2 에서
      **목수 주머니가 이미 차 있다**고 세워 놨으므로, 닿는 그림은 **내 앞 샷과도 모순**이다
      (4개 든 주머니에 5알이 더 들어갈 칸이 없다). 그래서 `GX_TO` 를 **228 → 200** 으로 내려
      목수 표식 앞 **41px 에서 멈추게** 했다. 화면이 주장하는 것은 「닿았다(지급)」가 아니라
      **「되튕기지 않았다(수락)」**까지다 — 앞 세 그림과의 대비도 그쪽이 정확하다.
      ⚠️ 이 편은 후불 정산을 통째로 버렸으므로(③버리기), 그림도 「나중에 준다」를 **주장하지
      않는다.** 주장을 빼는 것이 목표였지 새 개념을 넣는 것이 아니다.
   ────────────────────────────────────────────────────────────────

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **좌표 + 시간** 두 겹이다.
     ⓐ 좌표 : 흰 카드 A y 52~96 · 흰 라벨 y 176~194 · 흰 표식 y 200~228(x 20~48 / 304~332)
              강조색 품삯 y 207~221 (x 59~263, **글로우 없음** — 왼쪽 표식과 11px,
              오른쪽 표식과 41px 뜬다. 글로우를 걸면 왼쪽 여유가 사라져서 안 걸었다)
              강조색 「수락」 y 250~270 (그 위 흰 것의 최저는 228 → 22px)
     ⓑ 시간 : 빠진 자리의 강조색 띠(y 120~140, 글로우 포함)는 흰 카드 B(y 108~152)와
              같은 상자를 쓴다. 그래서 **카드 B 가 알파 0 이 되는 진행률 0.74 보다 뒤인
              0.78 에서야** 띠가 켜지기 시작한다. 두 알파가 같은 프레임에서 0 을 넘지 않는다.

   🔴 캔버스 밖으로 안 나간다: 최대 x = 카드 오른쪽 300(글로우 없음) · 표식 332
      · 강조색 띠 글로우 308. 세로 최저 = 266 베이스라인(글립 ~270).

   ⏱ 🔴 **멈춰 서는 순간을 `since(1)` 위에 세웠다**(pocketfull 과 같은 이유 — `delayMs` 와
      원점을 맞춰 TTS 재생성에 면역이 되게). 주기 1.5초 · 착지 위상 0.50 이므로
      **첫 착지는 자막 1 시작 + 0.750초**다. 🟢 2차 수정에서 이 값은 **안 건드렸다** —
      위 A 는 `cue(0)` 축이고 B 는 좌표 상수라 둘 다 `sfx.delayMs` 와 무관하다.
   🔴 띠는 `setLineDash` 가 아니라 `fillRect` 조각으로 그리고 위상을 `frac` 으로 양수
      정규화했다 — 긴 가로 점선이 30fps 3패스에서 알파가 갈린 전례가 있다.
   계속 도는 것 = 글자 사라짐 → 카드 B 접힘 → 띠 흐름(여기까지 자막 0, ts 1.91초까지 끊기지
      않는다) → 품삯이 나아가 멈추는 1.5초 루프(자막 1). 자막 0 의 마지막 0.47초는 띠가 흐른다. */

const CARD_T_A = 52, CARD_T_B = 108, CARD_H = 44;
const SLOT_Y = 128, SLOT_H = 4;
const LANE_Y = 214, MARK_R = 14;
const DOT_R = 7, DOT_DX = 14;
/* 착지 시 오른쪽 끝 = 200 + 4×14 + 7 = 263. 목수 표식 왼쪽 304 와 41px 떨어진다 —
   「닿았다(즉시 지급)」로 안 읽히게 한 값이다(위 2차 수정 B). */
const GX_FROM = 66, GX_TO = 200;
const ARRIVE = 0.50;                 // 착지 위상 — sfx.delayMs 의 근거 (2차에서 불변)
/* 빠지는 물음의 시간표 — 글자가 상자보다 먼저 죽지 않게 하는 세 상수 (2차 수정 A) */
const TXT_OFF = 0.42, TXT_SPAN = 0.16;   // 글자: k 0.42 까지 온전 → 0.58 에 0
const BOX_OFF = 0.50, BOX_SPAN = 0.24;   // 상자: k 0.50 부터 접힘 → 0.74 에 0
const SLOT_OFF = 0.78, SLOT_SPAN = 0.22; // 띠  : 상자가 다 없어진 뒤에 켜진다

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (!txt || alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 12 + tw / 2, w - 12 - tw / 2), y);
      ctx.restore();
    };

    const CL = 52, CR = w - 52, CWD = CR - CL;
    const LX = 34, RX = w - 34;

    if (spec.title) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* span 0.85 — 창을 넓혀 「글자 죽음 → 상자 접힘 → 띠 켜짐」 셋이 줄 서게 한다.
       0.55 였을 때는 셋이 겹쳐서 글자가 상자보다 먼저 죽었다(2차 수정 A). */
    const k = cue(spec.dropCue ?? 0, 0.15, 0.85);
    const st = ease(clamp(k / 0.14));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 남는 물음 ─────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, CL, CARD_T_A, CWD, CARD_H, 10); ctx.stroke();
    ctx.restore();
    ctext(spec.keepAsk, w / 2, CARD_T_A + 28, 800, 13.5, tone('ink'), CWD - 26, st);

    /* ── 빠지는 물음 : 읽히고 나서 납작하게 접힌다 ─── */
    /* 🔴 순서가 뜻이다 — **글자가 먼저 다 읽히고, 그 다음에 상자가 접힌다.**
       거꾸로 하면 「이름 없는 빈 상자가 접히는」 그림이 되고 이 편의 대비가 사라진다. */
    const sy = 1 - ease(clamp((k - BOX_OFF) / BOX_SPAN));
    if (sy > 0.01) {
      const h = CARD_H * sy, cy = CARD_T_B + CARD_H / 2;
      ctx.save();
      ctx.globalAlpha = st * sy;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, CL, cy - h / 2, CWD, Math.max(3, h), Math.min(10, h / 2)); ctx.stroke();
      ctx.restore();
      ctext(spec.dropAsk, w / 2, cy + 5, 800, 13.5, tone('ink'), CWD - 26,
        st * (1 - ease(clamp((k - TXT_OFF) / TXT_SPAN))));
    }

    /* ── 빠진 자리 : 흐르는 강조색 띠 ──────────────── */
    const ba = ease(clamp((k - SLOT_OFF) / SLOT_SPAN));
    if (ba > 0.01) {
      const SEG = 14, PER = 24;
      const ph = frac(t / 1.1) * PER;
      ctx.save();
      ctx.globalAlpha = ba;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      for (let x = CL - PER + ph; x < CR; x += PER) {
        const a = Math.max(x, CL), b = Math.min(x + SEG, CR);
        if (b > a) ctx.fillRect(a, SLOT_Y, b - a, SLOT_H);
      }
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 두 사람 ───────────────────────────────────── */
    const mark = (x, face, label) => {
      ctx.save();
      ctx.globalAlpha = st;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(x, LANE_Y, MARK_R, 0, Math.PI * 2); ctx.stroke();
      ctx.restore();
      ctext(face, x, LANE_Y + 4, 800, 11, tone('ink'), MARK_R * 2 - 8, st);
      ctext(label, x, 190, 800, 12.5, tone('ink'), 76, st);
    };
    mark(LX, spec.clientFace, spec.clientLabel);
    mark(RX, spec.carpFace, spec.carpLabel);

    /* ── 품삯이 처음으로 안 되튕긴다 : 자막 1 ──────── */
    /* 🔴 목수에 **닿지 않는다**(2차 수정 B). 앞의 세 그림과 갈리는 것은 「도착했다」가 아니라
       「되밀려 돌아오지 않는다」이고, 원문의 그 순간도 지급이 아니라 **수락**이다. */
    const s1 = since(spec.payCue ?? 1);
    if (s1 >= 0) {
      const wa = ease(clamp(s1 / 0.30));
      const P = spec.loopSec ?? 1.5;
      const u = frac(s1 / P);
      const m = ease(clamp((u - 0.08) / 0.42));
      const gx = lerp(GX_FROM, GX_TO, m);
      const ar = 1 - ease(clamp(Math.abs(u - ARRIVE) / 0.10));
      const a = wa * ease(clamp(u / 0.06)) * (1 - ease(clamp((u - 0.80) / 0.16)));
      const n = Math.max(1, spec.wage ?? 5);
      if (a > 0.01) {
        ctx.save();
        ctx.globalAlpha = a;
        ctx.fillStyle = tone('accent');
        for (let j = 0; j < n; j++) {
          ctx.beginPath(); ctx.arc(gx + j * DOT_DX, LANE_Y, DOT_R, 0, Math.PI * 2); ctx.fill();
        }
        ctx.restore();
      }
      ctext(spec.verdict, w / 2, 266, 900, 17, tone('accent'), w - 120,
        ease(clamp((s1 - 0.55) / 0.45)) * (0.78 + 0.22 * ar));
    }

    ctx.textAlign = 'left';
  }
};
