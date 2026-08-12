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

        [Header("얼마나 넓게 깔리는가 (지형 밴드와 같은 문법)")]
        [Tooltip("바이옴 노이즈가 이 수 **이상**이면 후보가 된다. 0 = 언제나 후보(= 온대).\n" +
                 "🔴 **균등 분할이 아니다** (2026-08-12 실측): 보간 노이즈는 가운데로 쏠려서 " +
                 "0.1 미만이 2.3%·0.9 초과가 2.8%뿐이다. `v × 바이옴수`로 칸을 나누면 **양 끝 " +
                 "바이옴이 굶는다** — 온대 27%·심층산악은 시드에 따라 아예 없었다. 그래서 " +
                 "지형(TerrainTypeSO)이 쓰는 밴드 문법을 그대로 쓴다: 넓이는 여기서 정한다.\n" +
                 "배포 누적 분포: 0.45↑ 57% · 0.6↑ 35% · 0.75↑ 16% · 0.9↑ 3%.")]
        [Range(0f, 1f)] public float AppearsAbove;

        [Tooltip("후보가 여럿일 때 **높은 쪽이 이긴다**. 배포값: 심층산악 30 > 산악 20 > 정글 10 > 온대 0. " +
                 "🔑 심층이 산악을 이겨야 '더 깊은 곳'이 산악에 먹히지 않는다.")]
        public int Priority;

        private void OnValidate()
        {
            if (Palette == null || Palette.Length == 0)
                Debug.LogError($"[BiomeSO] {name}: 팔레트가 비어 있습니다 — 이 바이옴은 무시됩니다.", this);
        }
    }
}
