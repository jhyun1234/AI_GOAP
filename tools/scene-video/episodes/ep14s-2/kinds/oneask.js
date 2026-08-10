import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* oneask — 묻는 칸 하나를 빼내자, 값이 처음으로 건너간다.

   원문 근거:
   > "선불을 요구하는 성격이 묻는 건 '저 사람이 값을 치를 능력이 있나'이지
   >  '지금 내 손이 비어 있나'가 아닙니다. 그래서 선불 판정에서 수령 공간 조건을 빼냈습니다."
   > "손이 차 있으면 후불로 수락하고…"

   🔴 기본 도형의 네 번째 변주이자 **뒤집는 자리**다. 앞의 세 그림에서 매번 되튕기던 것이
   여기서 처음으로 끝까지 건너간다 — 되받기가 반복이 아니라 뒤집기인 이유가 이것이다.
   🔴 화면에서 실제로 빠지는 것은 **묻는 칸 한 장**이지 벽이 아니다. 벽(상한 8)은 그대로
   있고 규칙만 바뀌었다는 원문의 뜻을 지키려고, 이 그림에는 벽을 아예 안 그렸다.

   🔴 어려운 낱말을 화면에서 뺐다 — 「선불 판정」·「수령 공간 조건」 대신 원문이 따옴표로
   직접 써 준 두 문장(「값을 치를 능력이 있나」·「지금 내 손이 비어 있나」)을 그대로 올렸다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **좌표 + 시간** 두 겹이다.
     ⓐ 좌표 : 흰 카드 A y 52~96 · 흰 라벨 y 176~194 · 흰 표식 y 200~228(x 20~48 / 304~332)
              강조색 품삯 y 207~221 (x 59~291, **글로우 없음** — 왼쪽 표식과 11px,
              오른쪽 표식과 13px 뜬다. 글로우를 걸면 그 여유가 사라져서 안 걸었다)
              강조색 「수락」 y 250~270 (그 위 흰 것의 최저는 228 → 22px)
     ⓑ 시간 : 빠진 자리의 강조색 띠(y 120~140, 글로우 포함)는 흰 카드 B(y 108~152)와
              같은 상자를 쓴다. 그래서 **카드 B 가 알파 0 이 되는 진행률 0.55 보다 뒤인
              0.70 에서야** 띠가 켜지기 시작한다. 두 알파가 같은 프레임에서 0 을 넘지 않는다.

   🔴 캔버스 밖으로 안 나간다: 최대 x = 카드 오른쪽 300(글로우 없음) · 표식 332
      · 강조색 띠 글로우 308. 세로 최저 = 266 베이스라인(글립 ~270).

   ⏱ 🔴 **건너가는 순간을 `since(1)` 위에 세웠다**(pocketfull 과 같은 이유 — `delayMs` 와
      원점을 맞춰 TTS 재생성에 면역이 되게). 주기 1.5초 · 도착 위상 0.50 이므로
      **첫 도착은 자막 1 시작 + 0.750초**다.
   🔴 띠는 `setLineDash` 가 아니라 `fillRect` 조각으로 그리고 위상을 `frac` 으로 양수
      정규화했다 — 긴 가로 점선이 30fps 3패스에서 알파가 갈린 전례가 있다.
   계속 도는 것 = 카드 B 접힘(자막 0) → 띠 흐름 → 품삯 왕복 루프(자막 1). */

const CARD_T_A = 52, CARD_T_B = 108, CARD_H = 44;
const SLOT_Y = 128, SLOT_H = 4;
const LANE_Y = 214, MARK_R = 14;
const DOT_R = 7, DOT_DX = 14;
const GX_FROM = 66, GX_TO = 228;   // 도착 시 오른쪽 끝 = 228 + 4×14 + 7 = 291 (표식 왼쪽 304 와 13px)
const ARRIVE = 0.50;                 // 도착 위상 — sfx.delayMs 의 근거

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

    const k = cue(spec.dropCue ?? 0, 0.15, 0.55);
    const st = ease(clamp(k / 0.14));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 남는 물음 ─────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, CL, CARD_T_A, CWD, CARD_H, 10); ctx.stroke();
    ctx.restore();
    ctext(spec.keepAsk, w / 2, CARD_T_A + 28, 800, 13.5, tone('ink'), CWD - 26, st);

    /* ── 빠지는 물음 : 납작하게 접히며 사라진다 ────── */
    const sy = 1 - ease(clamp(k / 0.55));
    if (sy > 0.01) {
      const h = CARD_H * sy, cy = CARD_T_B + CARD_H / 2;
      ctx.save();
      ctx.globalAlpha = st * sy;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, CL, cy - h / 2, CWD, Math.max(3, h), Math.min(10, h / 2)); ctx.stroke();
      ctx.restore();
      ctext(spec.dropAsk, w / 2, cy + 5, 800, 13.5, tone('ink'), CWD - 26,
        st * (1 - ease(clamp(k / 0.30))));
    }

    /* ── 빠진 자리 : 흐르는 강조색 띠 ──────────────── */
    const ba = ease(clamp((k - 0.70) / 0.30));
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

    /* ── 품삯이 처음으로 건너간다 : 자막 1 ─────────── */
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
