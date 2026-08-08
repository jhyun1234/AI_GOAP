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
    }
}
