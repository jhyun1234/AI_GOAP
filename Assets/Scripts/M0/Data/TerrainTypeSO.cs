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

        [Tooltip("통행 가능한가. false = 물 — **주민과 위협 둘 다** 못 지난다 (ADR-T-3). " +
                 "문처럼 개체별로 갈리는 규칙이 아니다.\n" +
                 "🔑 절벽은 여기가 아니라 `HeightLevel`이 막는다 (M31): 물은 어디서나 못 지나고, " +
                 "절벽은 **남쪽 가장자리만** 못 지난다.")]
        public bool Walkable = true;

        [Tooltip("이 지형의 높이 (0 = 저지대, 1 = 고지대). 고지대는 **위를 걸을 수 있고**, " +
                 "남쪽 가장자리 두 줄만 벽이 된다 (M31 · TerrainService.IsWallRow).\n" +
                 "🔑 높이를 따로 안 두고 지형이 갖는 이유 = ADR-M31-1: 높이 지도를 따로 두면 " +
                 "밴드 문턱을 돌릴 때마다 지형과 어긋나 벽이 허공에 선다. 두 지도는 한 함수에서 나온다.")]
        [Min(0)] public int HeightLevel;

        [Tooltip("이 칸에 **들어가는** 비용 배수 (1 = 평지, 3 = 늪). 경로가 이걸 보고 우회한다. " +
                 "⚠️ 1 미만이면 휴리스틱이 과대평가가 되어 최단 경로가 깨진다 — 그래서 하한이 1이다.")]
        [Min(1f)] public float EnterCost = 1f;

        [Tooltip("이 칸 위에서의 이동 속도 배수 (1 = 평지, 0.5 = 늪). EnterCost의 짝 — " +
                 "이게 없으면 느려지는 걸 화면에서 아무도 못 본다 (ADR-T-6).")]
        [Range(0.1f, 1f)] public float MoveSpeedMult = 1f;

        [Tooltip("바닥 색 (M26-1차 W5). 팩 타일은 2차 이후 — 1차는 색만.")]
        public Color GroundColor = new Color(0.39f, 0.71f, 0.31f, 1f);

        [Tooltip("이 지형의 자원 밀도 배수 — **2차에서 쓴다**. 1차에서는 아무도 안 읽는다 (중립).\n" +
                 "🔴 이 배수는 **조건 없는 자원에만** 걸린다 (지형 조건이 붙은 광석은 이 검사를 건너뛴다) — " +
                 "암반 1.3은 광석이 아니라 **나무**를 불렸고, 나무 144그루 중 68이 바위 위에 섰다 " +
                 "(2026-08-12 Play 지적).")]
        [Min(0f)] public float NodeDensityMult = 1f;

        [Header("경계 그림 (M29-4차 — 계단 경계를 없앤다)")]
        [Tooltip("이 지형이 **낮은 이웃 위로 흘러넘치는** 가장자리 조각 4장 — 순서: 위·오른쪽·아래·왼쪽.\n" +
                 "🔑 조각은 **낮은 쪽 타일에** 그려진다: 위쪽 이웃이 이 지형이면 그 타일에 '위' 조각을 얹어 " +
                 "잔디 술이 아래로 늘어진다. 그래서 색면이 계단이 아니라 유기적 경계가 된다.\n" +
                 "누가 누구 위로 흐르는지는 `Priority`가 정한다 (지형 밴드와 같은 자).\n" +
                 "비우면 이 지형은 경계를 안 그린다 = 舊 계단 (중립).")]
        public Sprite[] EdgeTiles;

        [Tooltip("경계를 **자기 타일에** 그리는가. 기본 false = 낮은 이웃 위로 흘러넘친다 (잔디).\n" +
                 "true = 물가: 물은 뭍으로 넘치지 않는다 — 물 타일 자신의 가장자리에 둔치를 그린다. " +
                 "(2026-08-12 실측: 물 조각을 뭍에 얹었더니 호수가 잔디 위로 번졌다.)")]
        public bool EdgeOnSelf;

        [Tooltip("가장자리 조각에 곱할 색. 🔴 **`GroundColor`와 짝이다** — 둘 다 원본 타일 색에 " +
                 "같은 배율을 먹인 값이어야 술과 바닥이 같은 초록이 된다. 한쪽만 고치면 경계에 띠가 생긴다.")]
        public Color EdgeTint = Color.white;

        [Header("벽면 (M31-W2 — 절벽을 벽으로 세운다)")]
        [Tooltip("고지대 남쪽 벽면 조각 **6장** — 순서: 윗줄 좌·중·우, 아랫줄 좌·중·우.\n" +
                 "🔑 조각은 **자기 칸에** 그려진다: 아래 칸에 그리면 그 칸은 걸을 수 있는 땅이라 " +
                 "벽이 공중에 뜬다 (기단 잡석만 아래 칸이다).\n" +
                 "🔑 좌·중·우를 갈라야 벽 끝이 맺힌다 — 가운데 조각만 반복하면 절벽이 상자가 된다.\n" +
                 "비우면 이 지형은 벽을 안 그린다 = 舊 화면 (통행 규칙은 `HeightLevel`이 이미 정했다).")]
        public Sprite[] WallSprites;

        [Tooltip("벽 **바로 아래** 저지대 칸에 흩는 기단 잡석 3장 (좌·중·우). 굴러떨어진 돌이다.\n" +
                 "🔑 순수 장식 — 통행엔 아무 영향이 없다 (그 칸은 걸어 다니는 땅이다).")]
        public Sprite[] BaseSprites;

        [Header("이 땅에 서 있는 것 (M29-3차)")]
        [Tooltip("바닥에 흩뿌릴 장식 그림들 (풀·바위·버섯·종유석…). 비우면 장식 없음 = 舊 화면(중립).\n" +
                 "🔑 **색만 다른 타일은 지역이 아니다** (사용자 Play 지적 2026-08-12): 정글인지 늪인지 " +
                 "광산인지는 바닥색이 아니라 **그 땅에 무엇이 서 있는가**로 읽힌다.\n" +
                 "⚠️ 나무·광석 그림은 여기 넣지 말 것 — 캘 수 없는 가짜 자원이 된다 (장식은 판정이 없다).")]
        public Sprite[] DecorSprites;

        [Tooltip("타일당 장식이 설 확률 (0 = 없음, 0.5 = 두 칸에 하나). 정글은 빽빽하게, 평지는 성기게.")]
        [Range(0f, 1f)] public float DecorPerTile;

        [Tooltip("장식 크기 (월드 단위, 1 = 한 칸).")]
        [Min(0.1f)] public float DecorSize = 0.8f;

        private void OnValidate()
        {
            if (!Walkable && EnterCost > 1f)
                Debug.LogWarning($"[TerrainTypeSO] {name}: 통행 불가인데 EnterCost({EnterCost})가 설정돼 있습니다 " +
                                 "— 못 지나는 칸의 비용은 아무도 안 읽습니다 (혼동 방지).", this);

            // M31 ⚠️② — 에셋이 "못 지나는 땅"이라 말하는데 서비스는 위를 걷게 하면 두 값이 서로 거짓말이 된다.
            if (HeightLevel > 0 && !Walkable)
                Debug.LogWarning($"[TerrainTypeSO] {name}: 고지대(HeightLevel {HeightLevel})인데 Walkable 0 " +
                                 "— 고지대는 **위를 걷는 땅**이고 막는 것은 남쪽 벽 줄뿐입니다 (ADR-M31-2). " +
                                 "Walkable 을 켜세요.", this);
        }
    }
}
