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

        [Tooltip("표시 스케일. Kenmi 32px/16ppu = 2유닛이라 1타일 감각에 맞게 축소. 제안치 0.75.")]
        public float Scale = 0.75f;

        [Tooltip("측면 스프라이트의 기본 방향이 오른쪽이면 true (왼쪽 이동 시 flipX).")]
        public bool SideFacesRight = true;

        [Tooltip("개체 구분 틴트 강도 (0=원본색). 제안치 0.15.")]
        [Range(0f, 1f)] public float TintStrength = 0.15f;

        [Tooltip("발밑 그림자 크기 (타일 폭). 0 = 그림자 없음 (중립 — 배선 전과 동일). " +
                 "왜 필요한가: 팩 건물·나무는 그림자가 구워져 나오는데 주민만 없어서, 집 앞에 서면 " +
                 "**경계가 사라진다** (사용자 Play 지적 2026-08-09). 앞뒤는 정렬이 정하고, " +
                 "'땅에 발을 붙이고 있다'는 그림자가 정한다.")]
        public float ShadowWidthTiles = 0.62f;

        [Tooltip("발밑 그림자 진하기 (0~1). 짙으면 픽셀아트가 탁해진다 — 제안치 0.28.")]
        [Range(0f, 1f)] public float ShadowAlpha = 0.28f;

        [Tooltip("발밑 그림자의 세로 눌림 (1 = 원, 0.35 = 납작한 타원). 위에서 비스듬히 본 바닥.")]
        [Range(0.1f, 1f)] public float ShadowFlatten = 0.4f;

        [Tooltip("발밑 그림자의 세로 위치 보정 (타일). 발 위치는 시트마다 달라 에셋이 정한다.")]
        public float ShadowOffsetTiles = -0.12f;
    }
}
