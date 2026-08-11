import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* noname — 뒤집힘. 안 사라지는 줄 하나가 내려앉아 걸리는데, 그 줄이 아래 셋 중
   누구를 말하는지는 안 적혀 있다.

   원문 근거(M13 「사건과 흔적」 2단계·4단계):
   > "그래서 상태형 줄 하나를 새로 얹었습니다."
   > "조건이 해소되면 줄은 사라지고, 평시에는 이 자리가 완전히 빕니다."
   > "\"굶는 주민 — 식량 0일치\"라는 줄은 누구인지 모르는 정보입니다.
   >  누군지 모르면 개입을 못 합니다."

   🔴 줄 안의 글자는 **원문이 따옴표로 인용한 그 문자열 그대로**다
   (「굶는 주민 — 식량 0일치」). 게임 안 문자열이 한국어이므로 화면도 한국어다
   (`ADR-V-10` 의 「인용」 예외는 영어 로그를 영어로 두라는 것이지 한국어를 영어로
   옮기라는 것이 아니다). 🔴 원문 2단계의 형식 문자열(「■ 굶는 주민 {이름} — 식량 {N}일치」)
   쪽은 **안 썼다** — `{이름}`·`{N}` 자리에 값을 넣으면 지어낸 수치가 된다.

   ⏱ 세 자막에 세 박자가 붙는다.
     · since(0) — 줄이 위에서 내려앉고(0.20→0.55) 양쪽 걸쇠가 물린다(0.55→0.85).
       🔑 **효과음 `latch` 의 delayMs 550 은 이 걸쇠가 물리기 시작하는 시각이다.**
       `cue` 가 아니라 `since` 위에 세웠으므로 자막 길이가 바뀌어도 안 움직인다.
     · since(1) — 아래 사람 셋이 떠오르고(0.10→0.55) 강조색 화살표가 나타나
       셋 사이를 계속 오간다(못 고른다).
     · since(2) — 사람마다 「?」가 차례로 켜지고 화살표는 흐려진다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세로 좌표로 갈랐다.**
     · 강조색 화살표 바닥 152, 글로우(blur 8)까지 160
       ↔ 흰 원 맨 위 = 196 − 27.5(숨 다 먹은 반지름 + 선 반폭) = 168.5. **8.5px** 뜬다.
     · 강조색 줄 바닥 104, 글로우(blur 최대 12)까지 116 ↔ 화살표 맨 위 134(같은 강조색이라
       무관) / 흰 원까지는 52.5px.
     · 걸쇠 왼쪽 x 19~22 · 오른쪽 x 330~333 — 캔버스 좌우 끝에서 19px 안쪽이다.
   흰↔흰(축 B) 최소 간격:
     · 얼굴 글자 바닥 ≈ 208 ↔ 「?」 글립 윗변 ≈ 236 = 28px
     · 원끼리 x 간격 88, 반지름 27.5 → **33px**
     · 「새로 얹은 줄」 라벨(좌 x20, baseline 24)은 아래 무엇과도 100px 이상 떨어져 있다

   세로 예산(캔버스 307): 최저 = 「?」 글립 바닥 ≈ 256. 51px 남는다.
   가로: 19~333. 캔버스 352.

   계속 도는 것(30fps 기준): 줄 안을 40px 폭 하이라이트가 292px 를 2.6초에 훑는다
   → **3.7px/프레임**. + 화살표가 168px 폭을 2.24초 주기로 오간다 → 최대 7.8px/프레임.
   + 원 반지름 숨 1.5px · 4rad/s × 셋. */

const BAR = { x: 26, y: 62, w: 300, h: 42 };
const FIG_Y = 196, FIG_R = 26, FIG_X = [88, 176, 264];
const CARET_Y = 152, CARET_SWING = 84;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, since }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const s0 = since(0), s1 = since(1), s2 = since(2);
    const land = ease(clamp((s0 - 0.20) / 0.35));
    const lock = ease(clamp((s0 - 0.55) / 0.30));
    if (land <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = land; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ── 해소될 때까지 안 사라지는 줄 ───────────────── */
    /* 🔴 내려앉는 거리는 16px 이다. 26px 로 잡았더니 등장 첫 순간에 줄의 글로우(blur 최대 12)가
       위쪽 흰 라벨의 글립 아래끝(≈27)까지 번졌다 — 강조색이 흰 픽셀을 덮는 축 A 위반이다.
       16px 이면 가장 높이 떴을 때도 글로우 위끝이 32.5 라 **5.5px** 뜬다. */
    const by = BAR.y - 16 * (1 - land);
    ctx.save();
    ctx.globalAlpha = land;
    setShadow(ctx, GLOW, 8 + 4 * (0.5 + 0.5 * Math.sin(t * 2.3)));
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    roundRect(ctx, BAR.x, by, BAR.w, BAR.h, 10); ctx.stroke();
    clearShadow(ctx);

    /* 줄 안을 훑고 지나가는 하이라이트 — 「이 줄은 계속 살아 있다」 */
    ctx.save();
    roundRect(ctx, BAR.x, by, BAR.w, BAR.h, 10); ctx.clip();
    const hx = BAR.x - 40 + frac(t / 2.6) * (BAR.w + 40);
    ctx.globalAlpha = land * 0.22;
    ctx.fillStyle = tone('accent');
    ctx.fillRect(hx, by, 40, BAR.h);
    ctx.restore();

    if (spec.lineText) {
      ctx.textAlign = 'center';
      let fs = 16; ctx.font = disp(800, fs);
      while (fs > 10 && ctx.measureText(spec.lineText).width > BAR.w - 34) {
        fs -= 0.5; ctx.font = disp(800, fs);
      }
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.lineText, BAR.x + BAR.w / 2, by + BAR.h / 2 + 6);
    }
    ctx.restore();

    /* 양쪽에서 물리는 걸쇠 */
    if (lock > 0.01) {
      const off = 8 * (1 - lock);
      ctx.save();
      ctx.globalAlpha = lock;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(19 + off, by + 4, 3, BAR.h - 8);
      ctx.fillRect(330 - off, by + 4, 3, BAR.h - 8);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 그런데 아래 셋 중 누구인지 모른다 ──────────── */
    const rise = ease(clamp((s1 - 0.10) / 0.45));
    if (rise > 0.01) {
      for (let i = 0; i < FIG_X.length; i++) {
        const r = FIG_R + 1.5 * Math.sin(t * 4 + i * 1.3);
        const y = FIG_Y + 14 * (1 - rise);
        ctx.save();
        ctx.globalAlpha = rise;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.arc(FIG_X[i], y, r, 0, Math.PI * 2); ctx.stroke();
        ctx.textAlign = 'center';
        ctx.font = disp(800, 15); ctx.fillStyle = tone('ink');
        ctx.fillText('·_·', FIG_X[i], y + 5);
        ctx.restore();

        /* 「?」 — 누구인지 모른다 */
        const q = ease(clamp((s2 - 0.10 - i * 0.16) / 0.28));
        if (q > 0.01) {
          ctx.save();
          ctx.globalAlpha = q; ctx.textAlign = 'center';
          ctx.font = disp(900, 20 + 2 * q * (0.5 + 0.5 * Math.sin(t * 2.7 + i)));
          ctx.fillStyle = tone('ink');
          ctx.fillText('?', FIG_X[i], 254);
          ctx.restore();
        }
      }
    }

    /* 셋 사이를 계속 오가는 화살표 — 한 번도 안 멈춘다 */
    const cA = ease(clamp((s1 - 0.30) / 0.35)) * (1 - 0.6 * ease(clamp(s2 / 0.5)));
    if (cA > 0.01) {
      const cx = 176 + CARET_SWING * Math.sin(Math.max(0, s1) * 2.8);
      ctx.save();
      ctx.globalAlpha = cA;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.beginPath();
      ctx.moveTo(cx - 11, CARET_Y - 18);
      ctx.lineTo(cx + 11, CARET_Y - 18);
      ctx.lineTo(cx, CARET_Y);
      ctx.closePath(); ctx.fill();
      clearShadow(ctx);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
