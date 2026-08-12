using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 바이옴 1종 = **팔레트 한 벌** (M29 W1, `ADR-M29-1`).
    ///
    /// 🔑 바이옴은 지형 종류·밴드·통행 판정에 손대지 않는다 — `TerrainService.Compute`가
    /// **어느 팔레트를 훑을지**만 고른다. 그래서 바이옴을 늘리는 것 = 이 에셋 1개 +
    /// 씬 배열 등록 · **코드 0줄** (지형이 `TerrainTypeSO`로 그런 것과 같은 문법).
    /// </summary>
    [CreateAssetMenu(menuName = "AI Village/M0/Biome", fileName = "Biome")]
    public sealed class BiomeSO : ScriptableObject
    {
        [Tooltip("화면·로그 표기용 (예: 정글)")]
        public string DisplayName;

        [Tooltip("이 바이옴이 쓰는 지형들. 순서 무관 — 지형의 Priority가 정한다. " +
                 "🔑 온대(배열 0번)의 팔레트가 舊 단일 팔레트와 같으면 마을 주변은 오늘과 같은 판이다.")]
        public TerrainTypeSO[] Palette;

        [Tooltip("이 바이옴이 설 수 있는 최소 기지 거리비 (0 = 마을 옆에도, 1 = 맵 끝에만). " +
                 "**[비례] 분류** (ADR-M28-2) — 맵이 커지면 같은 비율로 멀어진다. " +
                 "미만 거리에서는 온대(배열 0번)로 접힌다 = 초반 안전 보장.")]
        [Range(0f, 1f)] public float MinBaseDistFrac;

        private void OnValidate()
        {
            if (Palette == null || Palette.Length == 0)
                Debug.LogError($"[BiomeSO] {name}: 팔레트가 비어 있습니다 — 이 바이옴은 무시됩니다.", this);
        }
    }
}
