import {
  disp, mono, ease, easeOut, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, camAt
} from '../../../engine/lib.js';

/* onecount — **화면 앞의 사람이 「누가 죽었지?」하고 묻는데, 화면이 내놓는 답은 숫자
   하나뿐이다.** 숫자가 1 올라가고, 물음표는 그대로 남는다.

   원문 근거:
   > "누가 죽었냐고? 사망 카운터 +1. 내가 가진 건 그게 전부인데."
   > "Day 45에 주민 일곱 명이 죽었습니다."

   ── 2026-08-12 개정 v5 (사용자 판정) ──────────────────────────────
   사용자: 「+1 만 나오는 애니메이션은 진짜 정말 안 좋아」 ·
          「'누가 죽었냐고 물으면 답이 하나였어요 / 죽은 사람 수만 1 올라가고 끝이었죠' 가
           무슨 의미인지 모르겠어」 ·
          「게임을 플레이하는 사람의 모습을 만들어서 위에 ? 애니메이션도 넣어봐」

   🔴 v4 의 「+1 도장이 무덤을 눌러 납작하게 만든다」를 버렸다. 임팩트는 있었지만
      **묻는 사람이 화면에 없어서** 「답이 하나였다」의 *답*도 *묻는 사람*도 안 보였다.
      자막의 두 문장은 둘 다 **사람의 곤란**에 대한 것인데 그림에는 사람이 없었다.
   🔴 그래서 구도를 바꿨다: **왼쪽에 플레이어, 오른쪽에 게임 화면.**
      플레이어 머리 위의 큰 「?」가 이 샷의 주인공이고, 그 물음표는 **끝까지 안 풀린다.**
      숫자가 올라가는 것을 보고도 물음표가 남는 것 — 그게 이 편의 진단이다.
   🔴 **숫자를 실제로 보여 준다.** v4 까지는 「+1」이라는 기호만 있어서 무엇이 1 올라가는지
      알 수 없었다. 지금은 화면 안에 **죽은 사람 수**가 서 있고, 「+1」이 무덤에서 떠올라
      그 숫자에 꽂히며 6 → 7 로 바뀐다.
      ⚠️ 값 6 → 7 의 근거: 원문 「Day 45에 주민 일곱 명이 죽었습니다」 + 「사망 카운터 +1」.
         일곱 번째가 올라가는 순간이라 원문 안에 있는 수다. **원문에 없는 누적 총계는
         여전히 안 적는다.**

   🔴 색 규약: 이 샷에는 **강조색이 한 점도 없다.** 이 편에서 강조색은 「이름」인데
      여기서 남는 것은 이름이 아니라 수뿐이다. 물음표도 흰색이다 — 크기로 세운다.
   🔴 화면 유리는 `destination-out` 으로 **파낸다.** 배경색으로 칠하면 흰 베젤과의 경계에서
      팔레트 위반 색이 생긴다(v4 에서 실제로 다섯 샷이 걸렸다).

   phase (샷 분할):
     phase 0 (S2a) = 사람이 화면을 보고 묻는다. 「?」가 튀어 오른다.
                     자막 「누가 죽었냐고 물으면 답이 하나였어요」
     phase 1 (S2b) = 「+1」이 무덤에서 떠올라 숫자에 꽂히고 7 로 바뀐다. 물음표는 그대로.
                     자막 「죽은 사람 수만 1 올라가고, 끝이었죠」
     카메라는 숫자로 다가갔다가 **마지막에 물음표로 되돌아온다** — 「숫자가 올라가도 나는
     여전히 모른다」가 카메라의 문장이다.

   ⏱ 「?」의 도약과 「+1」의 비행은 `since(0)` 위에 세웠다 — `since` 는 자막이 시작된 뒤
      흐른 초라 `delayMs` 와 원점이 같다. 🔴 `sfx.delayMs 285`(sweep · 에너지 정점 0.555초)는
      **phase 0 에 있다.** 그래서 「?」가 튀어 오르는 정점을 0.555초에 맞췄다 —
      묻는 동작과 소리의 정점이 같은 프레임이다.

   겹침 감사(축 A 강조↔흰 / 축 B 흰↔흰):
     축 A · 강조색 0개 — 잴 것이 없다.
     축 B · 흰 글자 간격: 라벨 내림선 84 → 「?」 글립 꼭대기 97 = **13px**.
            「?」 baseline 134 → 사람 머리 꼭대기 154 = **20px**.
            화면 안 숫자(x 236 · baseline 210)와 「+1」(x 178 → 214 에서 사라짐)은
            x 가 갈려 최소 **22px** 뜬다.
            흰 도형끼리는 사람 오른쪽 끝 85 와 모니터 왼쪽 116 이 **31px** 뜬다.
   세로 예산(캔버스 307): 최저 = 모니터 발 266(41px 남음). 최고 = 라벨 글립 꼭대기 **71**.
   가로: 사람 왼쪽 39 / 모니터 오른쪽 340. 바깥 2px 띠에서 좌 37 · 우 12 뜬다.
   phase 1 카메라(1.7 로 숫자에 붙었다가 1.25 로 물음표 복귀): 모니터 좌우와 사람이 번갈아
     밖으로 나간다 → `checks.edge` 선언 대상.

   계속 도는 것: 물음표가 아주 느리게 좌우로 기운다(2.4초 주기 · ±3도). 정적 3초 검사를
   넘기는 최소치이고 「아직도 모르겠다」와 뜻이 맞는, 이 샷에서 유일하게 허용한 루프다. */

const GROUND = 272;
const FIG = { x: 62, h: 118, w: 46 };
const Q = { x: 62, y: 134 };                       // 물음표 baseline
const MON = { x: 116, y: 88, w: 224, h: 140 };     // 모니터 바깥 상자
const GLASS = { x: 124, y: 96, w: 208, h: 124 };   // 파낸 화면
const TOMB_Y = 170, TOMB_W = 21, TOMB_H = 38;
const TOMB_X = [140, 169.3, 198.7, 228, 257.3, 286.7, 316];
const NUM = { x: 236, y: 210 };                    // 죽은 사람 수

function stone(ctx, x, y, w, h) {
  const r = w / 2;
  ctx.beginPath();
  ctx.moveTo(x - r, y); ctx.lineTo(x - r, y - h + r);
  ctx.arc(x, y - h + r, r, Math.PI, 0);
  ctx.lineTo(x + r, y); ctx.closePath();
}

/** 사람 실루엣 — 머리 + 어깨. 얼굴·손은 안 그린다(MINIMAL_DESIGN_LANGUAGE:838). */
function person(ctx, x, y, hgt, wid) {
  const hr = wid * 0.26;
  const bodyTop = y - hgt + hr * 2 + 1;
  ctx.beginPath(); ctx.arc(x, y - hgt + hr, hr, 0, Math.PI * 2); ctx.closePath(); ctx.fill();
  ctx.beginPath();
  ctx.moveTo(x - wid * 0.50, bodyTop + 10);
  ctx.quadraticCurveTo(x, bodyTop - 9, x + wid * 0.50, bodyTop + 10);
  ctx.lineTo(x + wid * 0.40, y); ctx.lineTo(x - wid * 0.40, y);
  ctx.closePath(); ctx.fill();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    camAt(ctx, w, h, spec, t);
    ctx.textBaseline = 'alphabetic';

    const ph = spec.phase ?? 0;
    const st = ph === 0 ? ease(cue(0, 0.15, 0.20)) : 1;
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }
    const s0 = since(0);

    /* ── 사람 ─────────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.fillStyle = tone('ink');
    person(ctx, FIG.x, GROUND, FIG.h, FIG.w);
    ctx.restore();

    /* ── 모니터 ───────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.fillStyle = tone('ink');
    roundRect(ctx, MON.x, MON.y, MON.w, MON.h, 10); ctx.fill();
    ctx.fillRect(222, MON.y + MON.h, 12, 30);                        // 목
    roundRect(ctx, 198, MON.y + MON.h + 30, 60, 8, 4); ctx.fill();   // 발

    ctx.globalCompositeOperation = 'destination-out';                // 유리는 파낸다
    ctx.fillStyle = '#000';
    roundRect(ctx, GLASS.x, GLASS.y, GLASS.w, GLASS.h, 5); ctx.fill();
    ctx.restore();

    /* ── 화면 안 : 무덤 일곱 + 죽은 사람 수 ────────── */
    ctx.save();
    ctx.globalAlpha = st * 0.92;
    ctx.beginPath(); ctx.rect(GLASS.x, GLASS.y, GLASS.w, GLASS.h); ctx.clip();
    ctx.fillStyle = tone('ink');
    for (const x of TOMB_X) { stone(ctx, x, TOMB_Y, TOMB_W, TOMB_H); ctx.fill(); }

    /* 🔴 v4 의 실패를 여기서 고친다 — 「+1」기호만 띄우면 무엇이 1 올라가는지 알 수 없다. */
    const tick = ph === 1 ? clamp((s0 - 1.28) / 0.06) : 0;
    const numPop = 1 + 0.22 * tick * (1 - tick) * 4;                 // 꽂히는 순간 한 번 커진다
    ctx.save();
    ctx.translate(NUM.x, NUM.y);
    ctx.scale(numPop, numPop);
    ctx.font = mono(700, 30);
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('ink');
    ctx.fillText(tick > 0.5 ? '7' : '6', 0, 0);
    ctx.restore();

    /* 「+1」 — 무덤에서 떠올라 그 숫자에 꽂힌다 */
    if (ph === 1) {
      const fly = clamp((s0 - 0.55) / 0.75);
      if (fly > 0.005 && fly < 1) {
        const k = fly < 0.16 ? -0.30 * Math.sin(fly / 0.16 * Math.PI) : easeOut((fly - 0.16) / 0.84);
        const cx = lerp(178, NUM.x - 24, k);
        const cy = lerp(TOMB_Y - TOMB_H - 6, NUM.y - 9, clamp(k)) - Math.sin(clamp(k) * Math.PI) * 16;
        ctx.globalAlpha = st * (1 - clamp((fly - 0.86) / 0.14));
        ctx.font = mono(700, 20);
        ctx.textAlign = 'center'; ctx.fillStyle = tone('ink');
        ctx.fillText('+1', cx, cy);
      }
    }
    ctx.restore();

    /* ── 물음표 — 이 샷의 주인공. 끝까지 안 풀린다 ──
       phase 0 에서 튀어 오르고(정점 0.555초 = sweep 정점), 그 뒤로는 아주 느리게 기운다. */
    const qk = ph === 0 ? clamp((s0 - 0.30) / 0.42) : 1;
    if (qk > 0.01) {
      const popk = ph === 0 ? easeOut(qk) + 0.30 * Math.sin(Math.PI * qk) * (1 - qk) * 2 : 1;
      /* 숫자가 꽂히는 순간 한 번 흔들린다 — 답이 안 됐다는 뜻이다 */
      const shake = ph === 1
        ? Math.sin((s0 - 1.28) * 26) * clamp((s0 - 1.28) / 0.06) * clamp((2.0 - s0) / 0.6)
        : 0;
      const tilt = Math.sin(t * 2.6) * 0.052 + shake * 0.09;
      ctx.save();
      ctx.globalAlpha = st * clamp(popk);
      ctx.translate(Q.x, Q.y);
      ctx.rotate(tilt);
      ctx.scale(popk, popk);
      ctx.font = disp(900, 56);
      ctx.textAlign = 'center'; ctx.fillStyle = tone('ink');
      ctx.fillText('?', 0, 0);
      ctx.restore();

      if (spec.askLabel) {
        ctx.save();
        ctx.globalAlpha = st * clamp((popk - 0.5) / 0.5) * 0.85;
        ctx.font = disp(800, 13);
        ctx.textAlign = 'center'; ctx.fillStyle = tone('sub');
        ctx.fillText(spec.askLabel, Q.x, 84);
        ctx.restore();
      }
    }

    ctx.textAlign = 'left';
  }
};
