/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다. */
import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* sixrows — 뒤집힘과 착지(S3). 손으로 180칸을 채우다가 서른여섯 줄만 적기로 했다.

   원문 근거:
   > "성격 여섯 × 목표 서른이면 채워야 할 칸이 180개입니다."
   > "성격 여섯 줄 + 목표 서른 줄 = 서른여섯 줄만 채우면, 그 둘을 곱해서 180칸을
   >  만드는 일은 코드가 하게."
   > "성격 여섯 종이 열다섯 쌍 전부 다른 행동 구성으로 갈라진 것"

   자막 0 : 앞 샷과 **같은 서른 칸**이 깔려 있다가, 통째로 왼쪽으로 무너져 내려
     **여섯 줄짜리 묶음 + 서른 줄짜리 묶음** 둘로 접힌다.
   자막 1 : 접힌 데서 **판이 오른쪽으로 자라** 원래 자리를 도로 채우고,
     그 판이 **여섯 띠로 갈라지며 저마다 길이가 달라진다.**
   라벨을 다 가려도 「사람이 손대는 것은 확 줄었는데 결과는 도로 넓다」가 읽힌다.

   🔴 **「축」·「벡터」·「가중치」를 한 획도 안 그린다**(브리프 §3-C). 이 그림이 말하는
   것은 **사람이 하는 일의 양**뿐이다 — 원문이 스스로 사람 말로 옮긴 그 문장이 전부다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **두 겹으로 갈랐다.**
     ⓐ **시간** : 흰 격자는 `since(0) 0.97초`에 알파 0 이 되고, 강조색 묶음은
        `since(0) 1.10초`에야 켜진다. 둘이 동시에 0보다 큰 프레임이 없다.
     ⓑ **라벨 줄(y 254)은 글자 덩어리 셋이 나눠 쓰는데 셋 다 시간으로 갈린다** :
        흰 라벨 「손으로 180칸」(x 119~233)이 `since(0) 2.00`에 완전히 꺼지고,
        강조색 「36줄만」(x 52~110)은 **2.05** 에, 「나머지는 코드가」(x 182~296)는
        `since(1) 0.25`(= `since(0)` 약 4.06)에 켜진다 — **이 줄에서 흰색과 강조색이
        동시에 0보다 큰 프레임이 없다.** 가로로도 180칸 ↔ 36줄만은 9px 뜬다.
     🔑 제목(sub = 흰색 70%)은 y 24 이고 강조색 판의 위 한계가 y 54 라 30px 뜬다.

   세로 예산(캔버스 307): 최저 = 「열다섯 쌍 전부 갈라짐」 글립 바닥 **286**(기준선 282).
   21px 남는다. 그다음이 아래 라벨 줄 글립 바닥 258 · 판 글로우 바닥 233.5 ·
   격자 바닥 230.3(숨 0.8 + 획 1.5 포함) · 묶음 B 바닥 228.1.
   가로: 격자 오른쪽 **330.3**(21.7px 남는다) · 판 글로우 오른쪽 329.5(22.5px) ·
   격자 왼쪽 21.7 · 묶음 A 왼쪽 24.
   🔴 등장·성장에 **밖에서 밀려 들어오는 이동이 0** 이다 — 격자는 제자리에서 자라고,
   판은 **왼쪽 끝을 고정한 채 폭이 커진다.** 그래서 위 값이 그대로 최댓값이다.

   계속 도는 것: ①접힘(칸 서른 개 **+ 그 안의 표시 180개**가 최대 237px 이동, 0.70초 →
   프레임당 **11.3px**) ②접힌 뒤 **묶음 두 개의 마디 흐름**(60px/초 → 프레임당 **2px**,
   마디 약 144개 × 양끝 → 약 2,000px²/프레임) ③판의 **결 흐름**(54px/초 → 프레임당 **1.8px**,
   결 약 54개 → 약 4,300px²/프레임) ④띠 여섯의 오른쪽 끝 숨(±2px · 7.0rad/s → 0.47px)
   ⑤격자 서른 칸의 상시 숨(±0.8px · 6.6rad/s).
   🔑 ②는 **접힘이 끝난 뒤(`since(0)` 1.45) 판이 자라기 전(`since(0)` 약 4.01)까지의
   2.56초**를 혼자 메우는 자리다.
   ⚠️ **그 2.56초는 「사건이 없는 구간의 길이」이지 정적 판정값이 아니다.** 게이트가 재는 것은
   프레임 간 픽셀 변화이고, 이 구간을 ②가 메우고 있어 **`--fps 30` 실측 최대 1.1s** 로 나온다.
   정본은 실측 쪽이다 — 여기 적은 2.56 은 **설계 여유**로 읽어라(1차 검수가 짚어 준 구분). */

const COLS = 6, ROWS = 5;
const CW = 34, CH = 22, GX = 20, GY = 16;
const GX0 = 24, GY0 = 54;               // 격자 x 24~328 · y 54~228 (앞 샷과 같은 자리)
/* 칸 안의 여섯(3열 × 2행) — 6 × 30 = **180**. 묶음이 20 × 12 라 34 × 22 칸의 획 안쪽
   (31 × 19)에 가로 5.5px · 세로 3.5px 여유로 들어간다. 한 변 4px 은 최종 렌더(×2.755)에서
   11px 이라 충분히 보인다. */
const SUB = 4, SUB_GAP = 4;

const BA_X = 24, BA_W = 52, BA_N = 6, BA_H = 6, BA_P = 15, BA_Y = 96;    // 성격 6줄
const BB_X = 86, BB_W = 52, BB_N = 30, BB_H = 3, BB_P = 5.9, BB_Y = 54;  // 목표 30줄
const FOLD_X = 81, FOLD_Y = 141;        // 격자가 빨려 드는 자리(두 묶음의 한가운데)

/* 🔴 판의 오른쪽 끝을 328 → **322** 로 당겼다. 획(1.5)과 글로우(6)를 다 세면 329.5 라
   캔버스(352)까지 22.5px 이 남는다 — 328 이면 16.5px 이라 얇았다. */
const PN_X = 150, PN_X1 = 322, PN_Y = 54, PN_H = 172;
const RATIO = [1.00, 0.72, 0.90, 0.55, 0.96, 0.66];   // 갈라진 뒤 여섯 띠의 길이

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

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

    /* 🔴 setLineDash 를 안 쓴다(결정성 — 긴 점선이 30fps 3패스에서 알파가 갈린 전례).
       마디를 fillRect 조각으로 놓고 위상은 `((x % P) + P) % P` 로 양수 정규화한다. */
    const dashRow = (x0, x1, y, h, seg, per, off, alpha) => {
      if (alpha <= 0.01) return;
      const ph = ((off % per) + per) % per;
      ctx.save();
      ctx.globalAlpha = alpha; ctx.fillStyle = tone('accent');
      for (let x = x0 - per + ph; x < x1; x += per) {
        const a = Math.max(x0, x), b = Math.min(x1, x + seg);
        if (b > a) ctx.fillRect(a, y, b - a, h);
      }
      ctx.restore();
    };

    const c0 = cue(spec.foldCue ?? 0, 0.15, 0.55);
    const st = ease(clamp(c0 / 0.10));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const cols = spec.cols ?? COLS, rows = spec.rows ?? ROWS;

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ⏱ 세 국면 전부 `cue` 가 아니라 `since()` 위에 세웠다 — `delayMs` 와 원점이 같아
       **자막 길이가 바뀌어도, pauseAfter 를 줄여도 시각이 안 움직인다.**
       (등장 알파 `st` 만 `cue` 를 쓴다 — 그건 시각이 아니라 페이드라 상관없다.) */
    const s0 = Math.max(0, since(spec.foldCue ?? 0));
    const s1 = Math.max(0, since(spec.growCue ?? 1));
    const fold = ease(clamp((s0 - 0.35) / 0.70));        // 격자가 접힌다
    const gridA = 1 - ease(clamp((s0 - 0.35) / 0.62));   // 0.97초에 0
    const bun = ease(clamp((s0 - 1.10) / 0.35));         // 1.10초에야 켜진다
    const grow = ease(clamp((s1 - 0.20) / 0.90));        // 판이 자란다

    /* ── ① 서른 칸(흰색) : 제자리에서 자랐다가 왼쪽으로 무너져 접힌다 ──
       🔴 **1차 검수 지적을 고친 자리(§3-3).** 앞 샷과 같은 **서른 칸**인데 이 샷의 라벨은
       「손으로 180칸」이라, 칸을 세면 30 이 나오는데 글자는 180 을 말하고 있었다 —
       내 자신의 규칙(「라벨의 수와 실물이 어긋나면 그 라벨이 근거가 아니라 장식이 된다」)에
       걸린다. → **칸마다 작은 표시 여섯**을 넣었다. 6 × 30 = **180**. 이제 세면 180 이 나온다.
       🔑 그리고 이게 이 편의 뒤집힘을 더 정확히 그린다 — 원문이 말하는 180 은
       **「성격 여섯 × 목표 서른」**이고, 화면에서도 **한 목표(칸) 안에 성격 여섯 몫**이 있다.
       접힘 때 그 180 개가 통째로 왼쪽으로 빨려 들어가 36 줄로 접힌다.
       🔑 S2 와 **자리·크기는 한 픽셀도 안 바꿨다**(같은 판이라는 것이 읽혀야 한다).
       달라진 것은 칸 안이 비어 있는가(S2 = 몇 칸에 값이 있나)와
       여섯으로 나뉘어 있는가(S3 = 손으로 몇 개를 채워야 하나)뿐이다. */
    if (gridA > 0.01) {
      for (let i = 0; i < cols * rows; i++) {
        const col = i % cols, row = Math.floor(i / cols);
        const hx = GX0 + col * (CW + GX), hy = GY0 + row * (CH + GY);
        const brt = 0.8 * Math.sin(t * 6.6 + i * 0.83);
        const k = ease(clamp((fold - (col + row) * 0.02) / 0.55));
        const sc = 1 - 0.7 * k;
        const cx = lerp(hx + CW / 2, FOLD_X, k), cy = lerp(hy + CH / 2, FOLD_Y, k);
        const wd = (CW + brt * 2) * sc, ht = (CH + brt * 2) * sc;
        const a = st * gridA * ease(clamp((c0 - 0.04 - (col + row) * 0.012) / 0.14));
        if (a <= 0.01) continue;
        ctx.save();
        ctx.globalAlpha = a * 0.9;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
        roundRect(ctx, cx - wd / 2, cy - ht / 2, wd, ht, 4); ctx.stroke();
        /* 칸 안의 여섯 — 3열 × 2행. 묶음 20 × 12 가 칸 획 안쪽(31 × 19) 한복판에 앉는다
           (가로 5.5px · 세로 3.5px 여유). 접힐 때 `sc` 를 같이 먹어 칸과 함께 줄어든다. */
        ctx.fillStyle = tone('ink');
        const gw = (3 * SUB + 2 * SUB_GAP) * sc, gh = (2 * SUB + SUB_GAP) * sc;
        for (let s = 0; s < 6; s++) {
          const sx = cx - gw / 2 + (s % 3) * (SUB + SUB_GAP) * sc;
          const sy = cy - gh / 2 + Math.floor(s / 3) * (SUB + SUB_GAP) * sc;
          ctx.fillRect(sx, sy, SUB * sc, SUB * sc);
        }
        ctx.restore();
      }
    }

    /* ── ② 두 묶음(강조색) : 사람이 실제로 적는 줄 ────────
       마디가 60px/초로 흐른다 — 접힘이 끝난 뒤 판이 자라기 전까지의 구간을
       이 흐름이 혼자 메운다(정적 구간 대책 ②). */
    if (bun > 0.01) {
      const flow = t * 60;
      for (let i = 0; i < BA_N; i++) {
        const y = BA_Y + i * BA_P;
        const kk = bun * ease(clamp((bun - i * 0.03) / 0.5));
        dashRow(BA_X, BA_X + BA_W, y, BA_H, 8, 14, -flow - i * 5, st * kk);
      }
      for (let i = 0; i < BB_N; i++) {
        const y = BB_Y + i * BB_P;
        const kk = bun * ease(clamp((bun - i * 0.008) / 0.5));
        dashRow(BB_X, BB_X + BB_W, y, BB_H, 8, 14, -flow - i * 3, st * kk);
      }
    }

    /* ── ③ 판(강조색) : 왼쪽 끝을 고정한 채 오른쪽으로 폭이 커진다 ──
       그다음 여섯 띠로 갈라지며 저마다 길이가 달라진다. */
    const splitK = ease(clamp((cue(spec.growCue ?? 1, 0.12, 0.34) - 0.45) / 0.25));
    if (grow > 0.01) {
      const gap = 8 * splitK;
      const bh = (PN_H - gap * 5) / 6;
      const full = PN_X1 - PN_X;
      const graze = t * 54;
      for (let i = 0; i < 6; i++) {
        const y = PN_Y + i * (bh + gap);
        const brt = 2 * Math.sin(t * 7.0 + i * 1.3);
        const len = full * grow * lerp(1, RATIO[i], splitK) + brt;
        if (len < 6) continue;
        ctx.save();
        ctx.globalAlpha = st;
        setShadow(ctx, GLOW, 6);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
        roundRect(ctx, PN_X, y, len, bh, 4); ctx.stroke();
        clearShadow(ctx);
        ctx.restore();
        /* 결 — 판이 채워져 있다는 표시이자 이 샷의 상시 운동이다 */
        const ph = ((graze % 20) + 20) % 20;
        ctx.save();
        ctx.globalAlpha = st * 0.8; ctx.fillStyle = tone('accent');
        for (let x = PN_X - 20 + ph; x < PN_X + len; x += 20) {
          const a = Math.max(PN_X + 4, x), b = Math.min(PN_X + len - 4, x + 6);
          if (b > a) ctx.fillRect(a, y + 4, b - a, bh - 8);
        }
        ctx.restore();
      }
    }

    /* ── 라벨 넷 ──────────────────────────────────────── */
    /* 🔴 **1차 검수 지적을 고친 자리(§3-4).** 1차본은 「36줄만」(x 81)과 「손으로 180칸」
       (x 190)이 **같은 줄에 0.85초 동안 함께** 떠 있었다 — 왼→오로 읽으면 **after → before**
       라 자막(before → after)과 순서가 뒤집혀 보였다.
       🔴 **좌우를 맞바꾸는 대신 시간으로 갈랐다.** 맞바꾸면 「손으로 180칸」이 묶음(=after
       상태의 물건) 위에 앉고 「36줄만」이 빈 오른쪽에 앉아 **자리가 뜻과 어긋난다.**
       지금은 둘이 **한 프레임도 같이 안 뜬다**: 180칸은 `since(0)` 2.00 에 완전히 꺼지고
       36줄만은 2.05 에야 켜진다. 순서를 눈이 아니라 **시간**이 준다.
       ⏱ 자막 0 = 「손으로 백여든 칸 채우다 서른여섯 줄로 바꿨어요.」(**실측 rel 3.780**).
       🔴 **어절 시각은 wav 실측만 쓴다**(20ms RMS 포락선 · 음절 핵 기준 · 2차 검수 측정):
         「백여든」 **0.82** · 「채우다」 **1.40** · 「서른여섯」 **1.90** · 「줄로」 **2.32**.
         ⚠️ 1차본은 `dur` 을 글자 수로 균등 배분해 0.93 · 1.85 · 2.38 · 3.04 로 적었다 —
         **최대 0.72초 틀렸다.** `dur`(3.660초) 안에 **후행 무음이 0.56초** 들어 있어
         균등 배분은 반드시 뒤로 밀린다.
         · 「손으로 180칸」 : 샷 시작부터 떠 있다가 1.60~2.00 에 꺼진다
           → 읽는 창(α ≥ 0.6) **1.603초**(ts 0.145 ~ 1.748), 「채우다」(1.40)를 말할 때까지 떠 있다
         · 「36줄만」 : 2.05~2.40 에 켜진다 → α 0.6 이 **2.271**,
           「서른여섯」(1.90)보다 **0.37초 늦다**(한도 0.67 · 여유 0.30). 늦는 쪽이라 안전하다
       🔑 가운데(x 176)로 옮긴 것도 뜻이다 — 180칸은 **접히기 전 판 전체**를 가리키므로
       격자 한복판이 제자리다(1차본의 x 190 은 「36줄만」을 피하려던 임시 자리였다). */
    if (spec.beforeLabel) {
      ctext(spec.beforeLabel, 176, 254, 900, 15, tone('ink'), 200,
        st * (1 - ease(clamp((s0 - 1.60) / 0.40))));
    }
    if (spec.afterLabel) {
      ctext(spec.afterLabel, 81, 254, 900, 15, tone('accent'), 110,
        st * ease(clamp((s0 - 2.05) / 0.35)));
    }
    /* 🔴 **2차 검수 지적으로 늦춘 자리.** 1차·2차본은 `since(1)` 0.25~0.35 라 알파 0.6 이
       0.471 이었는데, 「코드가」의 **wav 실측 발화 개시가 1.12**(내가 어절을 `dur` 로 균등
       배분해 0.5 로 잡았던 것이 0.62초 틀렸다) — **0.649초 일러 한도 0.67초까지 여유가
       0.02초**였다. 통과하긴 했지만 같은 방식이면 다음 편에서 넘는다.
       → 램프를 **0.62~0.98** 로 옮겼다: 알파 0.6 = **0.847** · 1.0 = 0.98
       → 「코드가」(1.12)보다 **0.27초 이르다**(여유 0.40초). 읽는 창 3.27초.
       🔑 뜻으로도 이쪽이 낫다 — 판이 자라는 구간이 `since(1)` 0.20~1.10 이라
       **라벨이 완성되는 0.98 이 판이 다 자라는 순간**이다. */
    if (spec.restLabel) {
      ctext(spec.restLabel, 239, 254, 900, 15, tone('accent'), 190,
        st * ease(clamp((s1 - 0.62) / 0.36)));
    }
    if (spec.pairLabel) {
      ctext(spec.pairLabel, 176, 282, 900, 15, tone('accent'), 260, st * splitK);
    }

    ctx.textAlign = 'left';
  }
};
