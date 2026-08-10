import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* nowline — 지금 상태 칸 셋.
   🔴 **v2 에서 세 번째 칸의 내용이 통째로 바뀌었다 — 라벨만 고치면 화면이 거짓말을 한다.**
   v1 은 「늑대·곰은 아직 색 원이고, 그림을 못 구했다(사람 몫)」였는데 그 사실이 죽었다:
   M24 W1(커밋 `665f79cc`, 2026-08-10 02:57)이 **늑대·곰을 지웠고**, W6(`83fea176`)이
   새 종족 넷에 **그림 배선까지 끝냈다**. 그래서 세 번째 칸은 「비어 있는 칸」이 아니라
   **「바뀐 칸」**이다 — 동그라미 하나가 지워지고 그 자리에 표식 넷이 들어선다.

   구조(칸 셋 · 가운데 칸의 도구 호 · 왼쪽 칸의 이어지는 조각)는 v1 그대로 살렸다.

   `ADR-V25-15` — 라벨을 전부 가려도 읽힌다:
   「왼쪽 둘은 채워졌고, 오른쪽 칸에서는 동그라미가 지워지고 표식 넷이 대신 들어선다.」

   연속 모션 = 가운데 칸에서 계속 휘둘러지는 도구 호. 「정지 그림의 시대를 끝냈다」를
   말하는 샷이 멈추면 자기모순이다.

   문자열 출처: `Docs/M22_3차_망루함정_실행명세서.md`(완료) · `Docs/M23_비주얼_실행명세서.md` ·
   `Docs/종족침공축_브레인스토밍_기록_2026-08-09.md` 9~22행 · `Docs/M24_1차_종족침공축_실행명세서.md` §W1·§W6 ·
   종족 이름 넷은 `Assets/M0Config/Threats/Race_*.asset` 의 `DisplayName` 실측. */

/* 🔴 2차 정정 (검수 C1): `천사` → `타락천사`.
   1차본이 「폭 때문에 줄였다」고 적었는데 실측이 그 사유를 깬다 — 라벨 실제 폭 334 device px,
   허용 폭(`cw − 16`) 497 px, 「타락」 두 글자를 더해도 400 px 다. 게다가 아래 `ns` 축소 루프가
   8px 까지 줄일 수 있어 **폭은 애초에 구속 조건이 아니었다.**
   정본은 `Assets/M0Config/Threats/Race_Demon.asset:15 DisplayName: "타락천사"` 이고 자막도
   「타락천사」라고 부른다 — 화면만 「천사」면 적이 아닌 것으로 읽힌다.
   🟢 `고블린`·`오크`·`기사단` 은 `무리`·`약탈` 만 뗀 일관된 줄임이라 그대로 둔다. */
const RACES = ['고블린', '오크', '기사단', '타락천사'];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    /* v2 자막 6줄에 물린다:
       0 울타리·문 + 몸짓 / 1 늑대 그림에서 막혔다 / 2 사서 쓰려니 안 됐다 /
       3 **지웠다** / 4 **지금 노리는 건 종족 넷** / 5 그림이 기획을 바꿨다.
       🔑 「지우기」와 「채우기」를 다른 줄에 물린 이유: 한 줄에 몰면 X 와 표식 넷이 같은
       0.4초 안에 겹쳐 일어나 무엇이 무엇을 대신했는지가 안 읽힌다. */
    const kBuild = ease(cue(0));
    const kMotion = ease(clamp((cue(0) - 0.45) / 0.55));
    const kStuck = ease(cue(1));
    const kFail = ease(cue(2));
    const kErase = ease(cue(3));
    const kRace = ease(cue(4));
    const kJudge = ease(cue(5));

    const gap = 16;
    const cw = (w - M * 2 - gap * 2) / 3;
    const cy = 52, ch = h - cy - 44;

    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('지금', M, 34);

    const CARDS = [
      { k: '마을에 선 것', on: kBuild },
      { k: '보이게 된 것', on: kMotion },
      { k: '바뀐 것', on: Math.max(kStuck, kRace) },
    ];

    for (let i = 0; i < 3; i++) {
      const cx = M + i * (cw + gap);
      const k = CARDS[i].on;
      const swapped = i === 2 && kRace > 0.3;

      ctx.strokeStyle = k > 0.1 ? (i === 2 && !swapped ? tone('ink') : tone('accent')) : tone('track');
      ctx.lineWidth = 3;
      if (i === 2 && !swapped) ctx.setLineDash([8, 6]);
      roundRect(ctx, cx, cy, cw, ch, 4); ctx.stroke();
      ctx.setLineDash([]);

      // 제목 (폭을 재서 칸 안에)
      let fs = 17; ctx.font = disp(900, fs);
      while (ctx.measureText(CARDS[i].k).width > cw - 22 && fs > 10) {
        fs -= 1; ctx.font = disp(900, fs);
      }
      const sw = ctx.measureText(CARDS[i].k).width;
      ctx.fillStyle = k > 0.1 ? (i === 2 && !swapped ? tone('ink') : tone('accent')) : tone('sub');
      ctx.fillText(CARDS[i].k, cx + (cw - sw) / 2, cy + 28);

      // 0번 : 이어진 줄 (조각이 붙어 하나가 된다) + 문 한 칸
      if (i === 0 && k > 0.05) {
        const n = 6, sy = cy + ch / 2 + 6;
        const bw = (cw - 40) / n;
        for (let j = 0; j < n; j++) {
          const kj = clamp((k - j * 0.09) / 0.4);
          if (kj <= 0.02) continue;
          ctx.fillStyle = tone('accent');
          const jx = cx + 20 + j * bw;
          ctx.fillRect(jx, sy - 8 * ease(kj), Math.max(3, bw - 3), 16 * ease(kj));
        }
        if (k > 0.6) {
          ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
          const dx = cx + 20 + 3 * bw;
          ctx.strokeRect(dx - 2, sy - 12, bw + 1, 24);
        }
      }

      // 1번 : 계속 휘둘러지는 도구 호 (연속 모션)
      if (i === 1) {
        const mx = cx + cw / 2, my = cy + ch / 2 + 12;
        const sw2 = Math.sin(t * 3.2);
        const a0 = -2.5 + 0.9 * sw2;
        ctx.strokeStyle = k > 0.1 ? tone('accent') : tone('sub');
        ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.arc(mx, my, 26, a0, a0 + 1.1);
        ctx.stroke();
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(mx, my);
        ctx.lineTo(mx + 24 * Math.cos(a0 + 0.55), my + 24 * Math.sin(a0 + 0.55));
        ctx.stroke();
        ctx.beginPath(); ctx.arc(mx, my + 14, 7, 0, Math.PI * 2); ctx.stroke();
      }

      /* 2번 : 동그라미 하나(늑대·곰의 색 원)가 지워지고 표식 넷이 들어선다.
         🔴 지워지는 것과 들어서는 것이 **같은 자리**를 쓰지 않도록 위아래로 갈랐다 —
         동그라미는 칸 위쪽 절반, 표식 넷은 아래쪽 절반이다. */
      if (i === 2) {
        const mx = cx + cw / 2, my = cy + 62;

        /* 색 원 — X 가 먼저 그어지고(kErase 0 → 0.55) 그 다음 원과 X 가 함께 사라진다
           (0.55 → 1). 사라지는 것과 들어서는 것이 시간에서도 갈린다. */
        const kx = clamp(kErase / 0.55);
        const circAlpha = clamp(1 - Math.max(0, (kErase - 0.55) / 0.45));
        if (circAlpha > 0.02) {
          ctx.save(); ctx.globalAlpha = circAlpha;
          ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
          ctx.beginPath(); ctx.arc(mx, my, 15, 0, Math.PI * 2); ctx.stroke();
          if (kx > 0.03) {
            ctx.globalAlpha = circAlpha * clamp(kx);
            ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
            const r = 15 * ease(kx);
            ctx.beginPath();
            ctx.moveTo(mx - r, my - r); ctx.lineTo(mx + r, my + r);
            ctx.moveTo(mx + r, my - r); ctx.lineTo(mx - r, my + r);
            ctx.stroke();
          }
          ctx.restore();
        }

        // 표식 넷 — 그 자리를 대신 채운다
        if (kRace > 0.03) {
          const bw = 26, bg = 8, n = RACES.length;
          const tw = n * bw + (n - 1) * bg;
          const tx = cx + (cw - tw) / 2, ty = cy + ch - 62;
          for (let j = 0; j < n; j++) {
            const kj = clamp((kRace - j * 0.12) / 0.45);
            if (kj <= 0.02) continue;
            ctx.save(); ctx.globalAlpha = clamp(kj);
            ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
            const bx = tx + j * (bw + bg);
            roundRect(ctx, bx, ty, bw, 26, 3); ctx.stroke();
            ctx.fillStyle = tone('accent');
            ctx.fillRect(bx + 6, ty + 8 + (10 - 10 * ease(kj)), bw - 12, 10 * ease(kj));
            ctx.restore();
          }
          // 이름 넷 — 표식 아래 한 줄. 폭을 재서 칸 안에 가둔다.
          const names = RACES.join(' · ');
          let ns = 11;
          ctx.font = disp(700, ns);
          while (ctx.measureText(names).width > cw - 16 && ns > 8) {
            ns -= 1; ctx.font = disp(700, ns);
          }
          const nw = ctx.measureText(names).width;
          ctx.save(); ctx.globalAlpha = clamp(kRace);
          ctx.fillStyle = tone('sub');
          ctx.fillText(names, cx + (cw - nw) / 2, ty + 46);
          ctx.restore();
        }
      }
    }

    /* 판정 한 줄 — 그림이 기획을 바꿨다. 칸 밖 바닥줄이라 어느 칸도 안 덮는다. */
    if (kJudge > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kJudge);
      ctx.font = disp(900, 15); ctx.fillStyle = tone('ink');
      const s = '그림이 기획을 바꿨다';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, Math.min(M, w - M - sw), h - 14);
      ctx.restore();
    }
  },
};
