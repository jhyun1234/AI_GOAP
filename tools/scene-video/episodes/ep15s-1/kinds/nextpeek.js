/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다. */
import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 편 사건의 0.5초 테스트 장면을 루프로 돌린다.

   🔴 재료는 **다음 편(`ep15s-2`)이 맡은 사건의 원문 문장**에서 직접 뽑았다.
   > "배가 어느 정도 고픈 주민에게 늑대가 접근하면 배고픔이 이깁니다."
   > "\"배고파. 밥. 밥. 밥. …어? 방금 뭐가 나를 물었지?\"
   >  화면에서 이건 굶주린 주민이 늑대 앞에 굳어 선 채로 물리는 장면으로 보입니다."
   ⚠️ 이 글 끝의 「다음 개발일지에서는…」 문단은 **블로그의 다음 글** 예고라 근거로
   쓰지 않았다(ep02s·ep11s·ep12s 가 그 형태로 세 번 틀렸다). 같은 글의 다음 **편**이므로
   정본은 이 글 안의 「굶주림 앞에 성격 없음」 절이다.
   🔴 같은 글의 다음 편이라 **막(act)이 바뀌지 않는다** — 막 이름을 한 마디도 안 썼다.
   🔴 예고 음성 · `outro.next` 카드 · 이 미리보기 셋이 같은 것을 가리킨다:
      **굶주린 주민이 늑대 앞에서 도망을 안 간다.**

   0.5초 루프 : 오른쪽에서 큰 표식(강조색)이 미끄러져 다가오는데, 왼쪽 작은 표식(흰색)은
   **반대쪽 점(밥)으로만 계속 팔을 뻗다가 그 자리에서 굳는다**(테두리가 겹으로 두꺼워진다).

   🔴 본문의 도형(하루 띠 · 격자 · 접히는 묶음)을 여기로 끌고 오지 않았다 —
   다음 편의 사건은 「하루가 갈라지는가」가 아니라 **「무엇을 먼저 하는가」**라 축이 다르다.
   색 규약도 물려받지 않는다: 본문에서 강조색은 「성격이 닿은 것」이었지만
   여기서는 **강조색 = 다가오는 위험 / 흰색 = 그걸 안 보고 있는 주민**이다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **가로 좌표**로 갈랐다.
     큰 표식(accent)은 중심이 296 → 218 을 오가고 반너비 22 · 글로우 blur 10 이라
     왼쪽 한계가 **186**. 흰 것은 주민 원 오른쪽 끝 **110** 이 최대다(76px 뜬다).
     🔑 바닥선(track = 흰색 12%)은 **x 30~170 에서 끊었다** — 끝에서 끝까지 그으면
     큰 표식의 글립을 관통한다(전례가 있다).

   🔴 화면에 수를 한 개도 안 올렸다.

   ⏱ 등장은 예고 자막 `cue(0)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.
      0.5초 루프는 `t` 로 돌아 **예고 자막 구간에서 이미 다 보이고**, 그 구간은 카드 앞이라
      정적도 같이 해결된다.

   세로 예산(캔버스 307): 최저 = 큰 표식 글로우 바닥 207. 100px 남는다.
   가로: 큰 표식 오른쪽 끝 318 · 글로우까지 328(24px 남는다) · 밥 점 왼쪽 40.
   계속 도는 것 = 0.5초 루프(큰 표식이 매 루프 78px 이동 → 프레임당 약 **7.2px**)
   + 뻗는 팔(최대 6.9px/프레임) + 글로우 맥동. */

const GY = 200;                       // 바닥
const MAN_X = 100, MAN_Y = 180, MAN_R = 10;
const FOOD_X = 46, FOOD_R = 6;
const REACH = 46;
const WOLF_X0 = 296, WOLF_X1 = 218, WOLF_HW = 22, WOLF_HH = 17;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    if (spec.label) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 20, 26);
      ctx.restore();
    }

    const pk = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (pk <= 0.02) { ctx.textAlign = 'left'; return; }

    const u = frac(t / (spec.loopSec ?? 0.5));
    const near = ease(clamp(u / 0.72));            // 다가옴
    const frozen = ease(clamp((u - 0.74) / 0.10)); // 굳음
    const wolfA = ease(clamp(u / 0.08)) * (1 - ease(clamp((u - 0.86) / 0.14)));

    /* ── 바닥 : x 30~170 에서 끊는다(강조색 글립을 안 지나가게) ── */
    ctx.save();
    ctx.globalAlpha = pk; ctx.fillStyle = tone('track');
    ctx.fillRect(30, GY - 1.5, 140, 3);
    ctx.restore();

    /* ── 밥(흰 점) ────────────────────────────────────── */
    const fp = 0.5 + 0.5 * Math.sin(t * 12);
    ctx.save();
    ctx.globalAlpha = pk;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(FOOD_X, MAN_Y, FOOD_R + fp, 0, Math.PI * 2); ctx.stroke();
    ctx.restore();

    /* ── 주민 : 밥 쪽으로만 팔을 뻗다가 굳는다 ────────── */
    const reach = REACH * (0.5 + 0.5 * Math.sin(t * 9)) * (1 - frozen) + REACH * 0.86 * frozen;
    ctx.save();
    ctx.globalAlpha = pk;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineCap = 'round';
    ctx.beginPath();
    ctx.moveTo(MAN_X - MAN_R, MAN_Y);
    ctx.lineTo(MAN_X - MAN_R - reach * 0.75, MAN_Y);
    ctx.stroke();
    ctx.beginPath(); ctx.arc(MAN_X, MAN_Y, MAN_R, 0, Math.PI * 2); ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(MAN_X, MAN_Y + MAN_R); ctx.lineTo(MAN_X, GY - 2);
    ctx.stroke();
    /* 굳음 — 테두리가 겹으로 두꺼워진다(흰색 그대로다. 강조색을 안 쓴다) */
    if (frozen > 0.01) {
      ctx.globalAlpha = pk * frozen;
      ctx.beginPath(); ctx.arc(MAN_X, MAN_Y, MAN_R + 5, 0, Math.PI * 2); ctx.stroke();
    }
    ctx.restore();

    /* ── 다가오는 큰 표식(강조색) ───────────────────── */
    if (wolfA > 0.01) {
      const wx = lerp(WOLF_X0, WOLF_X1, near);
      const wp = 0.5 + 0.5 * Math.sin(t * 8.5);
      ctx.save();
      ctx.globalAlpha = pk * wolfA;
      setShadow(ctx, GLOW, 7 + 3 * wp);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(wx - WOLF_HW, MAN_Y);
      ctx.lineTo(wx, MAN_Y - WOLF_HH);
      ctx.lineTo(wx + WOLF_HW, MAN_Y - WOLF_HH * 0.35);
      ctx.lineTo(wx + WOLF_HW * 0.55, MAN_Y + WOLF_HH);
      ctx.lineTo(wx - WOLF_HW * 0.45, MAN_Y + WOLF_HH * 0.7);
      ctx.closePath(); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
