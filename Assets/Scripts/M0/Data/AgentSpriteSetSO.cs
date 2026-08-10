using System;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 주민 스프라이트 세트 (Kenmi Player 시트 등) — 아트 교체는 이 에셋만 바꾸면 된다.
    /// 방향 3종(정면/측면/후면) × 상태 2종(대기/걷기) + 행동 몸짓(M23-W1). 왼쪽은 측면 flipX.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/AgentSpriteSet", fileName = "AgentSpriteSet")]
    public sealed class AgentSpriteSetSO : ScriptableObject
    {
        /// <summary>행동 몸짓 한 벌 (M23-W1) — {Kind, 3방} append-only 배열의 항목
        /// (TraitValue {축,값} 규약 동형: 미등록 Kind = 대기/걷기 폴백 = 중립).</summary>
        [Serializable]
        public struct ActionAnim
        {
            public AnimKind Kind;
            public Sprite[] Down;
            public Sprite[] Side;
            public Sprite[] Up;
        }

        public Sprite[] IdleDown;
        public Sprite[] IdleSide;
        public Sprite[] IdleUp;
        public Sprite[] WalkDown;
        public Sprite[] WalkSide;
        public Sprite[] WalkUp;

        [Tooltip("행동 몸짓 칸 (M23-W1, ADR-M23-1). 미등록 Kind는 대기/걷기 폴백 (중립).")]
        public ActionAnim[] Actions;

        [Tooltip("애니메이션 프레임 속도 (fps). 제안치 8 — 관찰 후 튜닝.")]
        public float FramesPerSecond = 8f;

        [Tooltip("그림 규격 정규화 배율 — 시트 셀 크기가 달라도 화면에서 같은 크기로 서게 하는 값. " +
                 "PPU 16 기준 32px=2유닛·48px=3·64px=4 → 0.75/0.5/0.375 가 전부 1.5유닛으로 수렴한다. " +
                 "⚠️ 팩 세트(Race_*)는 tools/pixel-art/slice-pack.mjs 가 **기계로 다시 쓴다** — " +
                 "여기 손으로 적은 값은 재슬라이스에 지워진다. 연출용 크기 조절은 ThreatSO.SizeMult 로.")]
        public float Scale = 0.75f;

        [Tooltip("측면 스프라이트의 기본 방향이 오른쪽이면 true (왼쪽 이동 시 flipX).")]
        public bool SideFacesRight = true;

        [Tooltip("개체 구분 틴트 강도 (0=원본색). 제안치 0.15.")]
        [Range(0f, 1f)] public float TintStrength = 0.15f;

        [Tooltip("그림이 셀 안에서 **어디부터 시작하는가** (0~1) — 셀 아래쪽 빈 칸의 비율. " +
                 "−1 = 미측정(중립: 셀 중앙 정렬 = 배선 전과 동일).\n" +
                 "🔑 이 아래끝은 곧 **구운 그림자의 아래끝**이다 (팩 시트에 그림자가 들어 있다 — " +
                 "실측 2026-08-10). 그래서 이 값에 맞춰 세우는 것은 그림쟁이가 찍어 둔 접지선에 " +
                 "맞추는 것과 같다 — 발끝을 눈대중으로 잡는 것보다 정확하다.\n" +
                 "🔑 왜 필요한가: 팩 셀은 무기 휘두름·날개를 담느라 여백이 넓고 그 여백이 시트마다 " +
                 "다르다. 셀을 기준으로 세우면 **발이 제각각인 높이에 선다** — 실측 2026-08-10에 " +
                 "오크만 발이 타일 중심 1.06 아래(다른 종족 0.37~0.56)라 반 칸 앞으로 튀어나와 " +
                 "그려지고 있었다. 이 값이 있으면 모든 종족의 발이 같은 선에 선다.\n" +
                 "측정은 slice-pack.mjs 가 픽셀을 훑어서 한다 (사람이 눈대중으로 적을 값이 아니다).")]
        public float ArtBottomNorm = -1f;

        [Tooltip("그림이 셀 높이의 몇 할을 쓰는가 (0~1). −1 = 미측정. " +
                 "실측 배포값: 주민 0.63 · 고블린 0.5 · 오크 0.52 · 기사단 0.46 · 타락천사 0.42.\n" +
                 "⚠️ **구운 그림자를 포함한 높이**다 — 캐릭터 키가 아니다. 그림자를 뺀 실제 키는 " +
                 "주민 0.84 · 고블린 0.70 · 기사단 1.25 · 오크 1.44 · 타락천사 1.44 유닛 " +
                 "(실측 2026-08-10). 크기를 눈으로 비교할 땐 이쪽 숫자를 본다.\n" +
                 "쓰임: 런타임 그림자를 켤 때의 폭 파생 (지금은 전부 꺼져 있다 — ShadowWidthTiles 참조).")]
        public float ArtHeightNorm = -1f;

        [Tooltip("런타임 발밑 그림자 폭 (타일). **기본 0 = 끔**, 그리고 배포 시트는 전부 꺼져 있어야 한다.\n" +
                 "🔴 왜 꺼져 있나 (실측 2026-08-10): **팩 시트에 그림자가 이미 구워져 있다** — " +
                 "다섯 시트 모두 맨 아래 행이 순수 검정 알파 100/255 에 몸통보다 넓다. 켜면 그림자가 " +
                 "두 겹이 된다.\n" +
                 "🔴 舊 툴팁은 '팩 건물·나무는 그림자가 있는데 주민만 없다'(2026-08-09)고 적고 있었고 " +
                 "그건 **틀렸다**. 당시 주민 몸이 반 칸 어긋나 있어서 구운 그림자가 발이 아니라 " +
                 "왼쪽 아래에 떨어져 있었을 뿐이다 — 필요했던 것은 그림자가 아니라 정렬이었다.\n" +
                 "그래도 코드 경로를 남겨 둔 이유: ①구운 그림자가 없는 시트가 들어올 수 있고 " +
                 "②낮/밤 축이 생기면 구운 그림자는 시간대로 못 바꾼다. 그때 이 값을 켜면 된다 " +
                 "(폭은 ArtHeightNorm 에서 파생되므로 숫자는 스위치 역할만 한다).")]
        public float ShadowWidthTiles;

        [Tooltip("발밑 그림자 진하기 (0~1). 짙으면 픽셀아트가 탁해진다 — 제안치 0.28.")]
        [Range(0f, 1f)] public float ShadowAlpha = 0.28f;

        [Tooltip("발밑 그림자의 세로 눌림 (1 = 원, 0.35 = 납작한 타원). 위에서 비스듬히 본 바닥.")]
        [Range(0.1f, 1f)] public float ShadowFlatten = 0.4f;

        [Tooltip("발밑 그림자의 세로 **미세 보정** (타일). 기준선은 이제 코드가 안다 " +
                 "(SpriteViewRig.FootBaselineTiles = 모든 서 있는 것의 발이 놓이는 선) — 이 값은 " +
                 "거기서 얼마나 더 밀지만 정한다. 0 = 발 바로 밑.\n" +
                 "⚠️ 舊 기본값 −0.12 는 주민 몸이 반 칸 어긋나 있던 시절에 맞춰 둔 값이라 " +
                 "정렬을 고친 지금은 근거가 없다 (2026-08-10).")]
        public float ShadowOffsetTiles;

        /// <summary>이 세트의 아무 프레임 하나 (없으면 null) — **크기·피벗을 그림에게 물어보는 창구**다.
        /// 배선 코드가 "32px일 것이다"를 가정하면 셀 크기가 다른 시트에서 반드시 어긋난다
        /// (M23 관통 교훈: 표현 수치를 코드에 두면 그림과 어긋난다).</summary>
        public Sprite AnyFrame()
        {
            Sprite s = First(IdleDown) ?? First(IdleSide) ?? First(IdleUp)
                    ?? First(WalkDown) ?? First(WalkSide) ?? First(WalkUp);
            if (s != null || Actions == null) return s;
            foreach (ActionAnim a in Actions)
            {
                s = First(a.Down) ?? First(a.Side) ?? First(a.Up);
                if (s != null) return s;
            }
            return null;
        }

        private static Sprite First(Sprite[] frames)
        {
            if (frames == null) return null;
            foreach (Sprite s in frames) if (s != null) return s;
            return null;
        }
    }
}
