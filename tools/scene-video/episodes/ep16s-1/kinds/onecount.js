import {
  disp, mono, ease, easeOut, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, castShadow, DEPTH, camAt, speedLines
} from '../../../engine/lib.js';

/* onecount — **책상 앞에 앉은 사람이 「누가 죽었지?」하고 묻는데, 화면이 내놓는 답은
   숫자 하나뿐이다.** 숫자가 1 올라가고, 물음표는 끝까지 안 풀린다.

   원문 근거:
   > "누가 죽었냐고? 사망 카운터 +1. 내가 가진 건 그게 전부인데."
   > "Day 45에 주민 일곱 명이 죽었습니다."

   ── 2026-08-12 개정 v6 (사용자 지시 · 참고 이미지) ─────────────────
   사용자: 「컴퓨터를 바라보는 자세로 다시 만들어 줘. 그냥 떡하니 사람 모양만 만들지 말고,
          점점 입체감이 있게 바꿔야 해」 + 모니터 앞에 앉아 몸을 기울인 인물 사진 한 장.

   🔴 v5 는 사람을 **정면 실루엣으로 세워** 모니터 옆에 놓았다. 「사람이 있다」는 읽혔지만
      **「그 화면을 들여다보고 있다」는 안 읽혔다** — 자세가 없으니 시선도 없었다.
      지금은 참고 자세대로 **앉아서 몸을 앞으로 기울이고 팔을 책상으로 뻗은 3/4 뒷모습**이다.
      🔑 손가락은 안 그린다(`MINIMAL_DESIGN_LANGUAGE:838`). 손목이 **키보드에 가려지게**
         구도를 짜서 규칙과 그림이 싸우지 않게 했다 — 못 그리는 것은 가리면 된다.

   🔴 **입체감은 원근이 아니라 셋으로 만든다.**
      ① 가림 — 책상이 사람 하반신을, 키보드가 손목을, 사람이 의자를 덮는다.
         깊이를 만드는 것은 결국 「무엇이 무엇을 덮는가」다.
      ② 명도 위계 — `DEPTH[]` 로 가까울수록 밝다:
         사람/책상 앞 모서리(흰색) → 키보드·모니터 앞면 → 책상 상판 → 모니터 옆면·의자.
      ③ 바닥 그림자 — `castShadow` 로 모니터 발과 컵이 놓인 자리를 만든다.
      ④ 팔은 몸통보다 **한 칸 밝게 + 어두운 테두리**를 깔아 겹친 자리에서 분리하고,
         몸통에는 **등 쪽 코어 섀도**를 깎아 원통으로 세운다. 평평한 흰 실루엣은
         종잇조각으로 읽힌다 — 명도가 한 번 꺾여야 몸이 된다. 목선도 한 번 끊는다.
         이게 없으면 팔이 몸통에 먹혀 실루엣이 뭉갠 덩어리가 된다.
      🔴 회색은 전부 무채색이라 팔레트 규약을 안 깬다. `DESIGN_GUIDE.md:95` 대로
         0x50 아래 어두운 fill 은 안 쓴다(검정과 구분이 안 된다).

   🔴 책상 상판은 **사다리꼴**이다. 직사각형이면 정면도라 깊이가 죽는다.
      앞변(y 252)이 뒷변(y 222)보다 넓어 시점이 위에서 내려다보는 것으로 읽힌다.
   🔴 모니터도 3/4 로 돌렸다 — 사람 쪽(오른쪽) 변이 길고 왼쪽에 옆면 두께가 보인다.

   🔴 **숫자를 실제로 보여 준다**(v4 의 「+1」 기호만 띄우던 실패를 고친 것 유지).
      화면 안에 죽은 사람 수가 서 있고 「+1」이 무덤에서 떠올라 꽂히며 6 → 7 이 된다.
      ⚠️ 값 6 → 7 의 근거: 원문 「주민 일곱 명이 죽었습니다」 + 「사망 카운터 +1」.
         일곱 번째가 올라가는 순간이라 원문 안의 수다. 원문에 없는 누적 총계는 안 적는다.

   🔴 색 규약: 이 샷에는 **강조색이 한 점도 없다.** 이 편에서 강조색은 「이름」인데
      여기서 남는 것은 수뿐이다. 물음표도 흰색이고 크기로 세운다.
   🔴 화면 유리는 `destination-out` 으로 **파낸다.** 배경색으로 칠하면 흰 베젤과의 경계에서
      팔레트 위반 색이 생긴다(v4 에서 실제로 다섯 샷이 걸렸다).

   phase (샷 분할):
     phase 0 (S2a) = 사람이 화면을 들여다보고 묻는다. 「?」가 튀어 오른다.
                     자막 「누가 죽었냐고 물으면 답이 하나였어요」
     phase 1 (S2b) = 「+1」이 꽂히고 숫자가 7 로. 물음표는 그대로 남아 한 번 흔들린다.
                     자막 「죽은 사람 수만 1 올라가고, 끝이었죠」
     카메라는 화면으로 다가갔다가 **마지막에 물음표로 되돌아온다.**

   ⏱ 「?」의 도약과 「+1」의 비행은 `since(0)` 위에 세웠다 — `delayMs` 와 원점이 같다.
      🔴 `sfx.delayMs 285`(sweep · 정점 0.555초)는 phase 0 에 있다. 「?」가 튀어 오르는
         정점을 0.555초에 맞췄다 — 묻는 동작과 소리의 정점이 같은 프레임이다.

   겹침 감사(축 A 강조↔흰 / 축 B 흰↔흰):
     축 A · 강조색 0개 — 잴 것이 없다.
     축 B · 흰 글자 셋: 라벨 내림선 66 → 「?」 글립 꼭대기 79 = **13px**.
            「?」 baseline 116 → 사람 머리 꼭대기(타원 위) 87 … 「?」는 머리 **오른쪽 위**라
            x 가 갈린다(? x 306 · 머리 x 250, 반지름 25 → 오른쪽 끝 275) → **31px**.
            화면 안 숫자(x 111 · baseline 186)와 「+1」(x 66 → 92 에서 사라짐) → **19px**.
            흰 도형끼리는 팔에 어두운 테두리를 깔아 몸통과 갈랐다(위 ④).
   세로 예산(캔버스 307): 최저 = 책상 앞변 **252**(55px 남음). 최고 = 라벨 글립 꼭대기 **53**.
   가로: 책상 앞변 4~348. 바깥 2px 띠에서 좌우 2 뜬다 — 책상은 **화면 밖으로 이어지는 면**이라
     `checks.edge` 선언 대상이다(잘린 글자가 아니라 방이 계속된다는 뜻).

   계속 도는 것: 물음표가 아주 느리게 기운다(2.4초 주기 · ±3도). 정적 3초 검사를 넘기는
   최소치이고 「아직도 모르겠다」와 뜻이 맞는, 이 샷에서 유일하게 허용한 루프다. */

/* 명도 위계 — 가까울수록 밝다 */
const C_PERSON = DEPTH[0];      // 흰색 — 주인공
const C_ARM_EDGE = DEPTH[2];    // 팔 테두리(몸통과 분리)
const C_KEY = DEPTH[1];
const C_MON_FACE0 = DEPTH[1], C_MON_FACE1 = DEPTH[2];
const C_MON_SIDE = DEPTH[3];
const C_DESK0 = DEPTH[2], C_DESK1 = DEPTH[3];
const C_DESK_EDGE = DEPTH[0];
const C_CHAIR = DEPTH[3];

/* 책상 — 사다리꼴(앞이 넓다) */
const DESK = { fy: 252, fx0: 4, fx1: 348, by: 222, bx0: 56, bx1: 296 };
/* 모니터 앞면 — 3/4. 사람 쪽(오른쪽) 변이 길다 */
const MF = { lx: 50, rx: 172, lt: 104, lb: 196, rt: 92, rb: 208 };
const GLASS = { lx: 58, rx: 164, lt: 112, lb: 188, rt: 101, rb: 199 };
const TOMB_Y = 158, TOMB_W = 12, TOMB_H = 26;
const TOMB_X = [66, 81, 96, 111, 126, 141, 156];
const NUM = { x: 111, y: 186 };
const FIG = { hx: 250, hy: 116 };            // 머리 중심
const Q = { x: 306, y: 116 };                // 물음표 baseline

/** 사다리꼴 경로 */
function quad(ctx, x0, y0, x1, y1, x2, y2, x3, y3) {
  ctx.beginPath();
  ctx.moveTo(x0, y0); ctx.lineTo(x1, y1); ctx.lineTo(x2, y2); ctx.lineTo(x3, y3);
  ctx.closePath();
}

function stone(ctx, x, y, w, h) {
  const r = w / 2;
  ctx.beginPath();
  ctx.moveTo(x - r, y); ctx.lineTo(x - r, y - h + r);
  ctx.arc(x, y - h + r, r, Math.PI, 0);
  ctx.lineTo(x + r, y); ctx.closePath();
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

    ctx.save();
    ctx.globalAlpha = st;

    /* ── ① 의자 등받이 (가장 뒤) ───────────────────── */
    ctx.fillStyle = C_CHAIR;
    roundRect(ctx, 288, 140, 34, 104, 9); ctx.fill();

    /* ── ② 모니터 (3/4) ───────────────────────────── */
    castShadow(ctx, 111, 220, 42, 7, 0.40);
    ctx.fillStyle = C_MON_SIDE;                                   // 왼쪽 옆면 = 두께
    quad(ctx, MF.lx, MF.lt, MF.lx - 15, MF.lt + 8, MF.lx - 15, MF.lb - 4, MF.lx, MF.lb);
    ctx.fill();
    const mg = ctx.createLinearGradient(MF.lx, 0, MF.rx, 0);      // 앞면 — 오른쪽이 가깝다
    mg.addColorStop(0, C_MON_FACE1); mg.addColorStop(1, C_MON_FACE0);
    ctx.fillStyle = mg;
    quad(ctx, MF.lx, MF.lt, MF.rx, MF.rt, MF.rx, MF.rb, MF.lx, MF.lb);
    ctx.fill();
    ctx.fillStyle = C_MON_SIDE;                                   // 목 + 발
    ctx.fillRect(102, MF.lb - 2, 16, 22);
    roundRect(ctx, 84, MF.lb + 16, 54, 8, 4); ctx.fill();

    ctx.globalCompositeOperation = 'destination-out';             // 유리는 파낸다
    ctx.fillStyle = '#000';
    quad(ctx, GLASS.lx, GLASS.lt, GLASS.rx, GLASS.rt, GLASS.rx, GLASS.rb, GLASS.lx, GLASS.lb);
    ctx.fill();
    ctx.globalCompositeOperation = 'source-over';

    /* ── ③ 화면 안 : 무덤 일곱 + 죽은 사람 수 ──────── */
    ctx.save();
    quad(ctx, GLASS.lx, GLASS.lt, GLASS.rx, GLASS.rt, GLASS.rx, GLASS.rb, GLASS.lx, GLASS.lb);
    ctx.clip();
    ctx.transform(1, -0.085, 0, 1, 0, 9);                         // 화면도 3/4 로 기운다
    ctx.fillStyle = DEPTH[0];
    for (const x of TOMB_X) { stone(ctx, x, TOMB_Y, TOMB_W, TOMB_H); ctx.fill(); }

    const tick = ph === 1 ? clamp((s0 - 1.28) / 0.06) : 0;
    /* 숫자에 꽂히는 순간 화면 안에서만 집중선이 튄다 — 반전은 안 쓴다.
       이 편에서 반전은 '무덤·이름'의 것이고, 여기서 일어나는 일은 사건이 아니라 집계다. */
    if (tick > 0.01 && tick < 1) speedLines(ctx, NUM.x * 2, 260, NUM.x, NUM.y - 8, tick, { n: 14, seed: 97, r0: 16, alpha: 0.5 });
    const numPop = 1 + 0.26 * tick * (1 - tick) * 4;              // 꽂히는 순간 한 번 커진다
    ctx.save();
    ctx.translate(NUM.x, NUM.y);
    ctx.scale(numPop, numPop);
    ctx.font = mono(700, 26);
    ctx.textAlign = 'center'; ctx.fillStyle = DEPTH[0];
    ctx.fillText(tick > 0.5 ? '7' : '6', 0, 0);
    ctx.restore();

    if (ph === 1) {                                               // 「+1」이 무덤에서 떠올라 꽂힌다
      const fly = clamp((s0 - 0.55) / 0.75);
      if (fly > 0.005 && fly < 1) {
        const k = fly < 0.16 ? -0.30 * Math.sin(fly / 0.16 * Math.PI) : easeOut((fly - 0.16) / 0.84);
        const cx = lerp(66, NUM.x - 19, k);
        const cy = lerp(TOMB_Y - TOMB_H - 5, NUM.y - 7, clamp(k)) - Math.sin(clamp(k) * Math.PI) * 13;
        ctx.globalAlpha = 1 - clamp((fly - 0.86) / 0.14);
        ctx.font = mono(700, 15);
        ctx.textAlign = 'center'; ctx.fillStyle = DEPTH[0];
        ctx.fillText('+1', cx, cy);
      }
    }
    ctx.restore();

    /* ── ④ 사람 — 앉아서 화면 쪽으로 몸을 기울인다 ── */
    ctx.fillStyle = C_PERSON;
    ctx.save();
    ctx.translate(FIG.hx, FIG.hy); ctx.rotate(-0.20);             // 고개가 화면 쪽으로
    ctx.beginPath(); ctx.ellipse(0, 0, 25, 29, 0, 0, Math.PI * 2); ctx.fill();
    ctx.restore();
    const torso = () => {                                         // 등이 굽은 몸통
      ctx.beginPath();
      ctx.moveTo(228, 150);
      ctx.quadraticCurveTo(212, 188, 204, 232);                   // 가슴 → 배 (화면 쪽으로)
      ctx.lineTo(302, 240);                                       // 엉덩이 — 책상이 덮는다
      ctx.quadraticCurveTo(310, 182, 280, 146);                   // 등 (뒤로 둥글게)
      ctx.closePath();
    };
    torso(); ctx.fill();

    /* 코어 섀도 — 등 쪽 가장자리를 어둡게 깎아 원통으로 세운다.
       평평한 흰 실루엣은 종잇조각으로 읽힌다. 명도가 한 번 꺾여야 몸이 된다. */
    ctx.save();
    torso(); ctx.clip();
    const cg = ctx.createLinearGradient(250, 0, 312, 0);
    cg.addColorStop(0, 'rgba(255,255,255,0)');
    cg.addColorStop(1, 'rgba(120,120,120,1)');
    ctx.fillStyle = cg;
    ctx.fillRect(240, 140, 80, 108);
    ctx.restore();

    /* 목 — 머리와 어깨 사이를 한 번 끊는다. 붙여 두면 덩어리가 된다. */
    ctx.save();
    ctx.strokeStyle = C_ARM_EDGE; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(232, 146); ctx.quadraticCurveTo(252, 156, 272, 145); ctx.stroke();
    ctx.restore();

    /* 팔 — 어깨에서 책상으로. 어두운 테두리를 먼저 깔아 몸통과 분리한다 */
    const armPath = () => {
      ctx.beginPath();
      ctx.moveTo(236, 162); ctx.quadraticCurveTo(206, 196, 170, 230);
    };
    ctx.lineCap = 'round'; ctx.lineJoin = 'round';
    ctx.strokeStyle = C_ARM_EDGE; ctx.lineWidth = 26; armPath(); ctx.stroke();
    ctx.strokeStyle = C_PERSON; ctx.lineWidth = 19; armPath(); ctx.stroke();

    /* ── ⑤ 책상 — 사다리꼴. 사람 하반신을 덮는다 ──── */
    const dg = ctx.createLinearGradient(0, DESK.by, 0, DESK.fy);
    dg.addColorStop(0, C_DESK0); dg.addColorStop(1, C_DESK1);
    ctx.fillStyle = dg;
    quad(ctx, DESK.bx0, DESK.by, DESK.bx1, DESK.by, DESK.fx1, DESK.fy, DESK.fx0, DESK.fy);
    ctx.fill();
    ctx.fillStyle = C_DESK_EDGE;                                  // 앞 모서리 = 가장 가깝다
    quad(ctx, DESK.fx0, DESK.fy - 5, DESK.fx1, DESK.fy - 5, DESK.fx1, DESK.fy, DESK.fx0, DESK.fy);
    ctx.fill();

    /* ── ⑥ 키보드 — 손목을 덮는다(손가락을 안 그리는 이유) ── */
    castShadow(ctx, 182, 250, 56, 5, 0.30);
    ctx.fillStyle = C_KEY;
    quad(ctx, 142, 234, 226, 234, 240, 250, 128, 250);
    ctx.fill();
    ctx.strokeStyle = C_MON_SIDE; ctx.lineWidth = 2.5;
    for (const [y, x0, x1] of [[239, 148, 220], [245, 140, 228]]) {
      ctx.beginPath(); ctx.moveTo(x0, y); ctx.lineTo(x1, y); ctx.stroke();
    }

    /* ── ⑦ 머그컵 ─────────────────────────────────── */
    castShadow(ctx, 268, 242, 15, 4, 0.32);
    ctx.fillStyle = C_KEY;
    roundRect(ctx, 256, 222, 24, 20, 3); ctx.fill();
    ctx.strokeStyle = C_KEY; ctx.lineWidth = 3.5;
    ctx.beginPath(); ctx.arc(283, 231, 6, -1.2, 1.2); ctx.stroke();

    ctx.restore();

    /* ── ⑧ 물음표 — 이 샷의 주인공. 끝까지 안 풀린다 ── */
    const qk = ph === 0 ? clamp((s0 - 0.30) / 0.42) : 1;
    if (qk > 0.01) {
      const popk = ph === 0 ? easeOut(qk) + 0.30 * Math.sin(Math.PI * qk) * (1 - qk) * 2 : 1;
      const shake = ph === 1
        ? Math.sin((s0 - 1.28) * 26) * clamp((s0 - 1.28) / 0.06) * clamp((2.0 - s0) / 0.6)
        : 0;
      ctx.save();
      ctx.globalAlpha = st * clamp(popk);
      ctx.translate(Q.x, Q.y);
      ctx.rotate(Math.sin(t * 2.6) * 0.052 + shake * 0.09);
      ctx.scale(popk, popk);
      ctx.font = disp(900, 54);
      ctx.textAlign = 'center'; ctx.fillStyle = DEPTH[0];
      ctx.fillText('?', 0, 0);
      ctx.restore();

      if (spec.askLabel) {
        ctx.save();
        ctx.globalAlpha = st * clamp((popk - 0.5) / 0.5) * 0.85;
        ctx.font = disp(800, 13);
        ctx.textAlign = 'center'; ctx.fillStyle = tone('sub');
        ctx.fillText(spec.askLabel, Q.x, 66);
        ctx.restore();
      }
    }

    ctx.textAlign = 'left';
  }
};
