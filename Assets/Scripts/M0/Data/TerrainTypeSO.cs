using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 지형 1종의 성질 (M26-1차 W2) — **코드에 지형 표를 두지 않는다** (`ADR-M0-2`).
    /// 지형을 늘리는 것 = 에셋 1개 · 코드 0줄 (`ADR-M25-2`의 정신을 지형에 재사용).
    ///
    /// 🔑 **비용과 속도가 한 에셋에 같이 산다** (ADR-T-6): 경로 비용만 올리면 주민이 돌아갈 뿐
    /// 느려지는 걸 아무도 못 보고, 속도만 낮추면 경로가 몰라서 **일부러 늪으로 들어간다.**
    /// 둘을 갈라 두면 반드시 어긋나므로 원천을 하나로 묶는다.
    /// </summary>
    /// <summary>지형을 고르는 두 밑그림 (M26-1차 W2). 습도 = 물·늪·숲, 고도 = 절벽.</summary>
    public enum TerrainNoiseField
    {
        Wetness = 0,
        Relief  = 1,
    }

    [CreateAssetMenu(menuName = "AI Village/M0/Terrain", fileName = "Terrain")]
    public sealed class TerrainTypeSO : ScriptableObject
    {
        [Tooltip("화면·로그 표기용 (예: 늪)")]
        public string DisplayName;

        [Header("어디에 깔리는가 (코드가 아니라 여기가 정한다)")]
        [Tooltip("이 지형을 고르는 밑그림. 습도 = 물·늪·숲 / 고도 = 절벽.")]
        public TerrainNoiseField Field = TerrainNoiseField.Wetness;

        [Tooltip("그 밑그림 값이 이 수 **이상**이면 후보가 된다. 0 = 언제나 후보(= 폴백 지형).")]
        [Range(0f, 1f)] public float AppearsAbove = 0f;

        [Tooltip("후보가 여럿일 때 **높은 쪽이 이긴다**. 배포값: 물 40 > 절벽 30 > 늪 20 > 숲 10 > 평지 0. " +
                 "🔑 물이 절벽을 이기는 이유 — 호수 한가운데 절벽이 서면 화면에서 안 읽힌다.")]
        public int Priority;

        [Tooltip("통행 가능한가. false = 물·절벽 — **주민과 위협 둘 다** 못 지난다 (ADR-T-3). " +
                 "문처럼 개체별로 갈리는 규칙이 아니다.")]
        public bool Walkable = true;

        [Tooltip("이 칸에 **들어가는** 비용 배수 (1 = 평지, 3 = 늪). 경로가 이걸 보고 우회한다. " +
                 "⚠️ 1 미만이면 휴리스틱이 과대평가가 되어 최단 경로가 깨진다 — 그래서 하한이 1이다.")]
        [Min(1f)] public float EnterCost = 1f;

        [Tooltip("이 칸 위에서의 이동 속도 배수 (1 = 평지, 0.5 = 늪). EnterCost의 짝 — " +
                 "이게 없으면 느려지는 걸 화면에서 아무도 못 본다 (ADR-T-6).")]
        [Range(0.1f, 1f)] public float MoveSpeedMult = 1f;

        [Tooltip("바닥 색 (M26-1차 W5). 팩 타일은 2차 이후 — 1차는 색만.")]
        public Color GroundColor = new Color(0.39f, 0.71f, 0.31f, 1f);

        [Tooltip("이 지형의 자원 밀도 배수 — **2차에서 쓴다**. 1차에서는 아무도 안 읽는다 (중립).")]
        [Min(0f)] public float NodeDensityMult = 1f;

        private void OnValidate()
        {
            if (!Walkable && EnterCost > 1f)
                Debug.LogWarning($"[TerrainTypeSO] {name}: 통행 불가인데 EnterCost({EnterCost})가 설정돼 있습니다 " +
                                 "— 못 지나는 칸의 비용은 아무도 안 읽습니다 (혼동 방지).", this);
        }
    }
}
