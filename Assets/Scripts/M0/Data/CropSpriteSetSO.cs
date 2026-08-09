using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 밭 작물 성장 단계 스프라이트 (M2-D, AgentSpriteSetSO 패턴).
    /// 스프라이트를 비우면 FarmPlotView가 색 폴백(연두 새싹 → 노랑 결실)으로 표현한다.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/CropSpriteSet", fileName = "CropSprites")]
    public sealed class CropSpriteSetSO : ScriptableObject
    {
        [Tooltip("재배 중(새싹) 스프라이트")]
        public Sprite Growing;

        [Tooltip("결실(수확 대기) 스프라이트")]
        public Sprite Ripe;

        [Tooltip("작물 스프라이트 스케일 (타일 단위)")]
        public float Scale = 1f;

        [Tooltip("같은 칸에서의 위아래 (WorldSort bias) — 밭 흙(2)보다 위, 서 있는 것(4~)보다 아래. " +
                 "앞뒤는 이 값이 아니라 월드 Y가 정한다.")]
        public int SortingOrder = 3;
    }
}
