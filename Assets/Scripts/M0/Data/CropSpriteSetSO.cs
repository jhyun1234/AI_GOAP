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

        [Tooltip("정렬 순서 — 밭 마커보다 위, 주민보다 아래")]
        public int SortingOrder = 3;
    }
}
