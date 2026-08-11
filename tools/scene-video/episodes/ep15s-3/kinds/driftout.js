/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이고, 강조색은 글로우(blur 8)까지
   센다. 즉 반지름 9 짜리 강조색 표식의 「바깥」은 9 + 1.5 + 8 = **18.5** 다.
   상시 숨(±2px)은 여유 계산에 이미 다 먹여 놓았다. */
import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* driftout — 훅. 성격이 목수를 마을 밖으로 내보냈다.

   원문 근거:
   > "떠돌이 성격의 목수가 기지에서 38타일 밖에 집을 짓습니다."
   > "떠돌이는 모험 +90 단일 축입니다."

   1.6초 루프 : 점선 고리 안에 흰 표식 넷이 서 있고, 그 안에 있던 강조색 표식 하나가
   고리의 **열린 쪽**으로 미끄러져 나가 오른쪽 끝까지 가서 그 자리에 자기 집을 세운다.
   집이 서면 표식은 집으로 바뀐다(둘이 겹쳐 서 있지 않는다).
   🔴 주기가 2.4 → **1.6초**로 바뀐 이유는 「목수」 라벨의 읽는 창이다 — 아래 루프 주석 참조.

   라벨을 다 가려도 「하나가 무리에서 빠져나가 저 멀리 혼자 자리를 잡는다」가 읽힌다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **고리를 열어서** 갈랐다.
     ⓐ 마을 고리(track)를 40°~320° 만 그리고 오른쪽 ±40° 를 비웠다. 그 비운 자리가
        목수(accent)가 나가는 통로다. 고리 끝점은 (153, 193.1)·(153, 110.9) 이고 통로는
        y 152 이므로 세로 거리 41.1 — 강조색 바깥 18.5 + track 획 절반 1.5 + 숨 2 = 22 를
        빼면 **19.1px** 뜬다.
     ⓑ 통로에 가장 가까운 주민은 (104,112) 이고 목수 출발점 (128,152) 과 46.6px 떨어져 있다.
        양쪽 숨 4 를 먹이고 두 글립의 바깥 합(18.5 + 10.5 = 29)을 빼면 **13.6px** 뜬다.
     ⓒ 「마을」(sub)은 고리 아래(기준선 238), 「목수」(accent)는 오른쪽 위(기준선 118)라
        서로 만나지 않는다. 「목수」는 목수가 x 200 을 지난 뒤에만 뜬다.

   세로 예산(캔버스 307): 최저 = 「마을」 글립 바닥 242. 65px 남는다.
   가로: 오른쪽 = 집 글로우 + 숨까지 326(여유 24px) · 왼쪽 = 고리 38.5.

   계속 도는 것 = 고리 점선 회전(54°/s × r64 = 60px/s = **프레임당 2.0px**) ·
   표식 다섯의 상시 숨(2px · 7.4rad/s = **프레임당 0.49px**) · 루프 안의 목수 이동
   (172px / 0.48초 = **프레임당 11.9px**). 주기가 짧아져 이동 구간이 샷에서 차지하는 비율이
   30% → 45% 로 늘었다(정적 예산은 좋아지는 방향이다).
   🔑 마을(고리·주민)은 루프를 안 탄다 — 루프로 사라졌다 나타나면 마을이 매번 새로
   생기는 것처럼 읽힌다. 되풀이되는 것은 **목수가 나가는 일** 하나뿐이다. */

const CVX = 104, CVY = 152, RING = 64;
const OPEN = 40;                       // 고리를 비운 각도(오른쪽 ±40°)
const PX0 = 128, PX1 = 300, PY = 152;  // 목수가 지나는 통로
const RAD = Math.PI / 180;

const SEATS = [[66, 126], [52, 170], [86, 196], [104, 112]];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, since }) {
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

    /* 상시 숨 — 사건이 아니라 「살아 있다」는 표시라 `t` 로 돈다(사건은 자막에 물린다).
       진폭 2px · 7.4rad/s = 30fps 한 프레임에 0.49px. */
    const bx = i => 2 * Math.sin(t * 7.4 + i * 1.7);
    const by = i => 2 * Math.cos(t * 6.1 + i * 2.3);

    const st = ease(clamp(t / 0.28));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ── 마을 고리 — 오른쪽이 열린 점선 ──────────────
       `setLineDash` 를 쓰지 않는다(긴 점선이 30fps 3패스에서 알파가 갈린 전례). 1° 짜리
       호 조각을 직접 그리고 위상은 양수로 정규화한다. */
    const ph = ((t * 54) % 14 + 14) % 14;
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3; ctx.lineCap = 'butt';
    for (let a = OPEN; a < 360 - OPEN; a += 1) {
      const m = (((a + ph) % 14) + 14) % 14;
      if (m >= 8) continue;
      ctx.beginPath();
      ctx.arc(CVX, CVY, RING, a * RAD, (a + 1.1) * RAD);
      ctx.stroke();
    }
    ctx.restore();

    /* ── 마을 사람 넷 ────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    SEATS.forEach(([x, y], i) => {
      ctx.beginPath();
      ctx.arc(x + bx(i), y + by(i), 9, 0, Math.PI * 2);
      ctx.stroke();
    });
    ctx.restore();

    ctext(spec.villageLabel ?? '', CVX, 238, 800, 13, tone('sub'), 120, st);

    /* ── 목수 : 마을 안 → 열린 쪽으로 → 밖에서 집을 세운다 ──
       루프의 원점을 훅 자막에 물린다 — 이 샷은 자막이 하나뿐이라 값으로는 `t` 와 같지만,
       「무엇에 맞춰 도는가」를 코드에 남겨 둔다(계약 2).
       🔴 **1차 검수 §4-B 를 여기서 고쳤다.** 옛 주기 2.4초에서는 「목수」 라벨의 밝은 구간이
       `t mod 2.4 ∈ [0.59, 2.23]` 이라, 훅 wav 에서 「목수」가 발화되는 **2.4~2.9초가 통째로
       어두운 구간**에 들어갔다. 주기를 **1.6초**로 줄이고 어두운 꼬리를 0.05 로 좁혔다:
       밝은 구간이 `t mod 1.6 ∈ [0.39, 1.55]` 가 되어 2.4(→0.80)와 2.9(→1.30)가 **둘 다 안**이다.
       🔑 주기를 줄이는 쪽을 고른 이유 — 위상을 밀면 샷이 「목수가 이미 나가 있는 상태」로
       열리고(φ ≥ 0.6 이어야 맞는다), 한 번만 도는 일회성으로 바꾸면 뒤에 3초짜리 조용한
       꼬리가 생긴다. 주기 단축은 **움직임을 오히려 늘리면서** 창을 덮는 유일한 손잡이다. */
    const u = frac(Math.max(0, since(spec.leaveCue ?? 0)) / (spec.loopSec ?? 1.6));
    const la = ease(clamp(u / 0.03)) * (1 - ease(clamp((u - 0.95) / 0.05)));
    const mv = ease(clamp((u - 0.10) / 0.30));      // 172px / 0.48초 = 프레임당 11.9px
    const hs = ease(clamp((u - 0.40) / 0.10));      // 집이 자란다
    const mk = 1 - ease(clamp((u - 0.40) / 0.07));  // 표식이 집에 자리를 넘긴다

    const cx = lerp(PX0, PX1, mv) + bx(4);
    const cy = PY + by(4);

    if (st * la * mk > 0.01) {
      ctx.save();
      ctx.globalAlpha = st * la * mk;
      setShadow(ctx, GLOW, 8);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(cx, cy, 9, 0, Math.PI * 2); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    }

    if (st * la * hs > 0.01) {
      /* 오각형 집 — 몸통 네 점 + 지붕 꼭대기를 한 경로로 긋는다.
         hw 15 · hh 19 이므로 획 바깥이 x 283.5~316.5 · y 131.5~172.5, 글로우까지 324.5,
         숨을 먹여도 326 이다(캔버스 352). */
      const hw = 15 * (0.4 + 0.6 * hs), hh = 19 * (0.4 + 0.6 * hs);
      const hx = PX1 + bx(5) * 0.75, hy = PY + by(5) * 0.75;
      ctx.save();
      ctx.globalAlpha = st * la * hs;
      setShadow(ctx, GLOW, 8);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(hx - hw, hy + hh);
      ctx.lineTo(hx - hw, hy - hh * 0.2);
      ctx.lineTo(hx, hy - hh);
      ctx.lineTo(hx + hw, hy - hh * 0.2);
      ctx.lineTo(hx + hw, hy + hh);
      ctx.closePath(); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    }

    /* 「목수」는 목수가 마을을 확실히 벗어난 뒤에만 뜬다 — 무리 안에 있을 때 띄우면
       흰 표식들 위에 강조색 글자가 얹힌다. 라벨이 **주인을 따라간다**(x = cx): 알파가 0 을
       벗어나는 것은 mv 0.35(= x 188)부터이고, 그 자리에서 고리(중심 104,152 · r 64)까지
       거리가 79.6px 라 track 을 안 문다. */
    ctext(spec.carpenterLabel ?? '', cx, 118, 900, 13, tone('accent'), 90,
      st * la * ease(clamp((mv - 0.35) / 0.20)));

    ctx.textAlign = 'left';
  }
};
