/* 📐 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이고, 강조색은 글로우(blur 8)까지 센다.
   반지름 9 짜리 강조색 표식의 「바깥」은 9 + 1.5 + 8 = **18.5**. 상시 숨(±2px)은 여유에
   이미 다 먹여 놓았다. */
import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* reachgap — 어긋난 상태. 부탁은 6타일 안에서 마주쳐야 걸리는데 목수 집은 38타일 밖이다.

   원문 근거:
   > "그런데 부탁이 성립하려면 6타일 안에서 마주쳐야 합니다."
   > "떠돌이 성격의 목수가 기지에서 38타일 밖에 집을 짓습니다."
   > "결과는 집 부탁이 영원히 성립하지 않고, 집 없는 주민이 굶어 죽는 것이었습니다."

   자막 0(6타일 안에서 마주쳐야) : 왼쪽 흰 표식에서 흰 점들이 오른쪽으로 흘러 강조색
     표식에 닿고, 양쪽에 말풍선이 하나씩 뜬다. 아래에는 닿는 거리를 재는 흰 괄호가 깔린다.
   자막 1(목수 집은 38타일 밖) : 강조색 표식이 화면 오른쪽 끝까지 물러나고, 다 물러난 뒤
     **그 자리에서 집으로 바뀐다.** 말풍선이 꺼지고 점들은 괄호 끝까지만 가서 거기서 끊겨
     사라진다 — 그 뒤 오른쪽 절반은 계속 비어 있다.

   🔑 **부탁을 「흐르는 점」으로 그렸다.** 부탁은 눈에 안 보이는 판정인데, 점이 닿거나 못 닿는
   것으로 그리면 ①라벨 없이 성립하고 ②가만히 있는 프레임이 없고 ③S4 의 「다시 닿는다」가
   같은 그림의 뒤집기가 된다.
   🔴 **이 장치는 이 회차의 발명이 아니다** — `ep14s-3/kinds/onepath.js` 가 부탁을 먼저
   흐르는 점으로 그렸고, 거기서는 점에 「부탁」 라벨까지 붙는다. 읽히는 모양은 갈린다:
   그쪽은 **강조색 큰 점이 점선 레일 위를 세로로** 흐르는 깔때기(여럿 → 하나)이고,
   여기는 **흰 작은 점이 레일 없이 가로로** 흐르다 **끊기느냐 닿느냐**를 그린다
   (onepath 에는 끊김이 없다). 1차 검수 §3-F 의 공시 요구를 여기에 적어 둔다.

   🔴 **길이 비를 주장하지 않는다.** 6 : 38 은 6.3배인데 이 캔버스(352px)에서 두 글립과
   글로우를 놓고 그 비를 지키면 마주친 둘이 겹쳐 버린다. 그래서 괄호는 **「닿는 구역」의
   표시**이지 자로 잰 길이가 아니고, 사건은 「괄호 안에 있던 것이 밖으로 나갔다」로 읽힌다.
   수는 라벨이 지고, 라벨은 각각 제 대상에만 붙어 있다(괄호 = 6타일 / 물러나 앉은 집 = 38타일).

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세 겹으로 막았다.**
     ⓐ 세로로. 흰 괄호(track)는 y 191~205 에만 있고, 강조색은 y 150 축에 있다 — 표식은
        글로우·숨을 다 먹어도 아래 끝이 170.5(**20.5px**), 집은 179.5(**11.5px**)다.
        🔑 집은 x 289.5~332.5 라 괄호(x 44~171.5)와 **가로로도 118px** 떨어져 있다.
     ⓑ 점의 끝을 잘라서. 흰 점(반지름 3.5)의 끝은 `min(목수 x − 30, 170)` 이라 자막 0 에서
        오른쪽 끝이 123.5, 목수 글로우 왼쪽 끝이 129.5 — **6.0px** 뜬다.
     ⓒ 말풍선을 위로. 강조색 말풍선은 글로우까지 x 127~173 · y 86~122 이고 흰 괄호의 가장
        위가 191 — **69px** 뜬다. 흰 말풍선은 x 28.5~59.5 라 강조색 글로우(127~)와 **67.5px**
        떨어져 있다.

   세로 예산(캔버스 307): 최저 = 「6타일」 글립 바닥 228. 79px 남는다.
   가로: 오른쪽 = **집 글로우 + 숨 332.5**(여유 17.5px) · 왼쪽 = 흰 말풍선 28.5.

   계속 도는 것 = 점 흐름(90px/s = **프레임당 3.0px**) · 괄호의 점선 흐름
   (60px/s = **프레임당 2.0px**) · 표식 둘의 상시 숨(**프레임당 0.49px**) ·
   자막 1 의 후퇴(156px / 0.55초 = **프레임당 9.5px**) · 그 뒤 표식 → 집 교체
   (0.35초 동안 반지름 9 원이 사라지고 hw 15 오각형이 자란다).
   🔑 후퇴가 끝난 뒤(since(1) 1.85~3.36초)가 이 샷에서 가장 조용한 구간이라, 점 흐름과
   괄호 흐름 둘을 거기서도 계속 돌게 뒀다. */

const VX = 44, AY = 150;               // 마을 사람 · 두 표식이 선 축
const CP0 = 150, CP1 = 306;            // 목수 : 닿는 구역 안 → 화면 오른쪽 끝
const BR_Y = 198, BR_X0 = 44, BR_X1 = 170;   // 닿는 거리 괄호
const DOT0 = 60, DOT_CAP = BR_X1;      // 점은 괄호의 오른쪽 끝을 못 넘는다
const PITCH = 12;

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

    /* 말풍선 — 안에 무엇이 들었는지는 안 그린다. 이 샷에서 말풍선이 지는 뜻은
       「둘이 마주쳤다」 하나이고, 무엇을 부탁하는지는 자막이 이미 말한다. */
    const bubble = (x, y, hw, hh, color, alpha, glow) => {
      if (alpha <= 0.01) return;
      ctx.save();
      ctx.globalAlpha = alpha;
      if (glow) setShadow(ctx, GLOW, 8);
      ctx.strokeStyle = color; ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(x - hw, y - hh);
      ctx.lineTo(x + hw, y - hh);
      ctx.lineTo(x + hw, y + hh);
      ctx.lineTo(x - hw * 0.25, y + hh);
      ctx.lineTo(x - hw * 0.55, y + hh + 7);
      ctx.lineTo(x - hw * 0.55, y + hh);
      ctx.lineTo(x - hw, y + hh);
      ctx.closePath(); ctx.stroke();
      if (glow) clearShadow(ctx);
      ctx.restore();
    };

    const st = ease(clamp(t / 0.28));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    const bx = i => 2 * Math.sin(t * 7.4 + i * 1.7);
    const by = i => 2 * Math.cos(t * 6.1 + i * 2.3);

    /* ── 목수의 후퇴 ────────────────────────────────
       `cue` 가 아니라 `since()` 위에 세운다 — `delayMs` 와 원점이 같아져 자막 길이가
       바뀌어도 소리와 그림이 어긋나지 않는다. */
    const s1 = since(spec.leaveCue ?? 1);
    const mv = s1 >= 0 ? ease(clamp((s1 - 1.00) / 0.55)) : 0;
    const cpx = lerp(CP0, CP1, mv);
    /* 닿는 구역의 오른쪽 끝이 x 170 이므로, 목수가 그 선을 넘어가는 순간 링크가 끊긴다. */
    const linkA = 1 - ease(clamp((cpx - 162) / 16));

    /* ── 닿는 거리 괄호 ─────────────────────────────
       `setLineDash` 를 안 쓴다(긴 점선이 30fps 3패스에서 알파가 갈린 전례).
       가로선은 fillRect 조각으로 놓고 위상은 양수로 정규화한다. */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.fillStyle = tone('track');
    const ph = ((t * 60) % PITCH + PITCH) % PITCH;
    for (let s = -PITCH; s < BR_X1 - BR_X0; s += PITCH) {
      const a = Math.max(0, s + ph), b = Math.min(BR_X1 - BR_X0, s + ph + 7);
      if (b <= a) continue;
      ctx.fillRect(BR_X0 + a, BR_Y - 1.5, b - a, 3);
    }
    ctx.fillRect(BR_X0 - 1.5, BR_Y - 7, 3, 14);
    ctx.fillRect(BR_X1 - 1.5, BR_Y - 7, 3, 14);
    ctx.restore();

    ctext(spec.reachLabel ?? '', (BR_X0 + BR_X1) / 2, 224, 800, 13.5, tone('sub'), 120,
      st * ease(clamp((since(spec.reachCue ?? 0) - 0.55) / 0.45)));

    /* ── 부탁 = 흐르는 점 ───────────────────────────
       점이 갈 수 있는 끝은 「목수 바로 앞」이거나 「괄호 끝」 중 가까운 쪽이다.
       목수가 괄호 밖으로 나가면 자동으로 뒤엣것이 이겨서 점이 허공에서 끊긴다. */
    const dotEnd = Math.min(cpx - 30, DOT_CAP);
    const room = Math.max(0, dotEnd - DOT0);
    const NP = Math.ceil((DOT_CAP - DOT0) / PITCH) + 1;
    const SPAN = NP * PITCH;
    if (room > 6) {
      ctx.save();
      ctx.fillStyle = tone('ink');
      for (let i = 0; i < NP; i++) {
        const s = (((i * PITCH + t * 90) % SPAN) + SPAN) % SPAN;
        if (s > room) continue;
        const a = clamp(s / 9) * clamp((room - s) / 11);
        if (a <= 0.02) continue;
        ctx.globalAlpha = st * a;
        ctx.beginPath();
        ctx.arc(DOT0 + s, AY, 3.5, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.restore();
    }

    /* ── 말풍선 둘 — 닿아 있는 동안에만 ───────────── */
    const talk = st * linkA * ease(clamp((since(spec.reachCue ?? 0) - 0.75) / 0.40));
    const pulse = 1 + 0.06 * Math.sin(t * 4.6);
    bubble(VX, 112, 14 * pulse, 10 * pulse, tone('ink'), talk, false);
    /* 강조색 말풍선은 목수를 따라간다 — 링크가 끊기기 전(cpx ≤ 178)에만 보이므로
       글로우 오른쪽 끝이 202.5 를 못 넘고, 흰 괄호(y 191~205)와는 y 로 69px 떨어져 있다. */
    bubble(cpx, 104, 15 * pulse, 10 * pulse, tone('accent'), talk, true);

    /* ── 마을 사람 ──────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(VX + bx(0), AY + by(0), 9, 0, Math.PI * 2); ctx.stroke();
    ctx.restore();

    /* ── 목수 → 그 자리의 집 ───────────────────────
       🔴 1차 정정 §3-D. 자막이 「목수 **집**은 38타일 밖」이라고 말하는데 화면에는 사람만
       있었다 — 라벨이 사람에게 붙어 말과 그림이 서로 다른 것을 가리켰다. 물러남이 끝난 뒤
       (since(1) 1.50~1.85초) 표식이 **제자리에서** 집으로 바뀐다. 위치가 CP1(306)로 고정이고
       교체가 시작되는 since(1) 1.50 에 표식도 이미 거의 거기 있다 —
       `mv = ease((1.50 − 1.00)/0.55) = ease(0.909) ≈ 0.977` → **x ≈ 302**(옛 주석의 304.5 는
       2차 검수가 잡은 오기다). 남은 4px 은 교체 0.35초 동안 지워지는 원 쪽에서만 움직이므로
       **집이 옮겨 다니는 프레임은 없다.**
       🔑 SH 가 세운 그 집이고 S4 가 당겨 오는 것도 이 집이다 — 다만 **좌표가 같은 것은
       S1 ↔ S4 뿐**이다(둘 다 (306,150)·hh 18 / SH 는 (300,152)·hh 19). 이어지는 것은 이야기다. */
    const swap = s1 >= 0 ? ease(clamp((s1 - 1.50) / 0.35)) : 0;

    if (st * (1 - swap) > 0.01) {
      ctx.save();
      ctx.globalAlpha = st * (1 - swap);
      setShadow(ctx, GLOW, 8);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(cpx + bx(1), AY + by(1), 9, 0, Math.PI * 2); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    }

    if (st * swap > 0.01) {
      /* 다른 샷과 같은 오각형 집. hw 15 · hh 18 이라 획 바깥이 x 289.5~322.5 ·
         y 130.5~169.5, 글로우까지 330.5, 숨을 먹여도 332.5 다(캔버스 352). */
      const sc = 0.5 + 0.5 * swap;
      const hw = 15 * sc, hh = 18 * sc;
      const hx = CP1 + bx(1), hy = AY + by(1);
      ctx.save();
      ctx.globalAlpha = st * swap;
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

    /* 「38타일」은 목수가 다 물러난 뒤에 뜬다 — 아직 가는 중인 자리에 거리를 적으면
       그 수가 어디까지의 거리인지 안 맞는다. 집 아래로 내려 달아(기준선 198) 집 글로우
       바닥 179.5 와 7.5px 뜬다. */
    ctext(spec.farLabel ?? '', CP1, 198, 900, 13.5, tone('accent'), 100,
      st * ease(clamp((s1 - 1.45) / 0.40)));

    ctx.textAlign = 'left';
  }
};
