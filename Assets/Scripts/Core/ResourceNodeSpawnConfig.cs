using System;
using UnityEngine;

namespace AIVillage.Core
{
    /// <summary>
    /// 자원 타입 1개에 대한 스폰 파라미터 묶음.
    /// ResourceNodeSpawnConfig 배열 원소로 사용한다.
    /// </summary>
    [Serializable]
    public class ResourceTypeSpawnData
    {
        [Tooltip("스폰할 자원 타입")]
        public ResourceType resourceType = ResourceType.Wood;

        // ── 수량 ─────────────────────────────────────────────────────────────────
        [Header("수량")]
        [Tooltip("맵에 배치할 총 노드 수")]
        [Min(0)] public int nodeCount = 10;
        [Tooltip("노드 1개당 최대 자원량")]
        [Min(1f)] public float maxAmount = 50f;

        [Tooltip("게임 1일당 재생량. M1-B에서 코드 상수 이관 — 기존 값: Wood 5 / Stone 3 / Iron 1.5 / " +
                 "Copper 0.5 / Silver 0.2. RawFood 3은 2026-07-14 기획 승인 (舊 '재생 없음'에서 변경).")]
        [Min(0f)] public float regenPerDay = 0f;

        // ── 배치 거리 제한 (기지 기준) ────────────────────────────────────────────
        [Header("기지 거리 제한")]
        [Tooltip("기지로부터 최소 맨해튼 거리. 이 값보다 가까이는 배치되지 않는다.")]
        [Min(0)] public int minDistanceFromBase = 10;
        [Tooltip("기지로부터 최대 맨해튼 거리. 0이면 맵 경계까지 허용.")]
        [Min(0)] public int maxDistanceFromBase = 0;

        // ── 클러스터 설정 ─────────────────────────────────────────────────────────
        [Header("클러스터 설정")]
        [Tooltip("클러스터 중심점 개수. 같은 자원끼리 덩어리로 모이게 된다.")]
        [Min(1)] public int clusterCount = 3;
        [Tooltip("클러스터 당 최소 노드 수")]
        [Min(1)] public int minNodesPerCluster = 2;
        [Tooltip("클러스터 당 최대 노드 수")]
        [Min(1)] public int maxNodesPerCluster = 4;
        [Tooltip("클러스터 중심에서 노드를 배치할 최대 반경 (타일 맨해튼 거리)")]
        [Min(1)] public int clusterSpreadRadius = 3;
        [Tooltip("클러스터 중심 간 최소 맨해튼 거리. 클러스터가 너무 붙지 않도록 보장.")]
        [Min(1)] public int minClusterSpacing = 15;

        // ── 시각화 ────────────────────────────────────────────────────────────────
        [Header("시각화")]
        [Tooltip("노드 그림 (2026-08-09). 채우면 색 원 대신 이 그림이 서고 색 보간은 꺼진다 " +
                 "(그림을 색으로 물들이면 아트가 죽는다). 비우면 舊 색 원 — 중립.")]
        public Sprite nodeSprite;

        [Tooltip("거의 고갈됐을 때 그림 (그루터기·부스러기). 비우면 nodeSprite를 흐리게만 한다. " +
                 "**잔량이 그림으로 읽히는 지점** — 어느 나무를 베러 갈지가 화면에서 정해진다.")]
        public Sprite depletedSprite;

        [Tooltip("종류 변형 (2026-08-10). 채우면 노드마다 **타일 좌표 해시로 하나를 골라** 선다 — " +
                 "같은 자리는 항상 같은 나무다(결정적). 비우면 위 nodeSprite 하나 (중립).\n" +
                 "🔑 왜 필요한가: 숲 전체가 같은 나무 한 종이면 지형이 벽지처럼 읽힌다 " +
                 "(사용자 Play 지적). 자원 종류는 그대로고 **생김새만** 갈린다 — 판정은 안 건드린다.\n" +
                 "🔮 **이 축은 나중에 갈린다** (사용자 방향 2026-08-10): ①지형별 — 추운 곳은 어두운 " +
                 "나무 ②계절별 — 봄 밝은 초록·가을 노랑·겨울은 수가 줄거나 어둡게. 그때 이 배열은 " +
                 "'좌표 해시로 하나'가 아니라 **지형·계절이 고르는 표**가 된다. 지금 구조에서 바뀌는 " +
                 "곳은 고르는 함수(ResourceNodeSpawner.PickVariantIndex) 하나뿐이도록 남겨 둔 것이다 — " +
                 "뷰·러너·판정은 어느 그림인지 모른다.")]
        public Sprite[] nodeSpriteVariants;

        [Tooltip("변형별 고갈 그림 — nodeSpriteVariants 와 **같은 순서**. 개수가 모자라면 " +
                 "그 변형은 위 depletedSprite 를 쓴다 (자작나무를 베었는데 참나무 그루터기가 " +
                 "남지 않게 짝을 맞춰 둘 것).")]
        public Sprite[] depletedSpriteVariants;

        [Tooltip("채집 중 흩날리는 그림 (벌목 나뭇잎). 비우면 효과 없음 — 돌·광석은 비워 둔다.")]
        public Sprite harvestParticle;

        [Tooltip("변형별 흩날림 그림 — nodeSpriteVariants 와 같은 순서. 참나무엔 참나무 잎, " +
                 "가문비나무엔 침엽이 떨어지게. 모자라면 위 harvestParticle 로 폴백.")]
        public Sprite[] harvestParticleVariants;

        [Tooltip("고갈 그림으로 바뀌는 잔량 비율 (0~1). 기본 0.25 = 1/4 남으면 그루터기.")]
        [Range(0f, 1f)] public float depletedBelowRatio = 0.25f;

        [Tooltip("자원이 가득 찼을 때 노드 마커 색상 (그림을 안 쓸 때만)")]
        public Color nodeColor = Color.green;
        [Tooltip("자원이 완전히 고갈됐을 때 노드 마커 색상")]
        public Color depletedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        [Tooltip("씬에서 노드 마커의 크기 (월드 단위, 1 = 타일 1칸 크기)")]
        [Min(0.1f)] public float nodeSize = 0.6f;
    }

    /// <summary>
    /// 자원 노드 스폰 전체 설정을 담는 ScriptableObject.
    /// Project 창 우클릭 → Create → AI Village → ResourceNodeSpawnConfig 로 생성 후
    /// ResourceNodeSpawner Inspector의 _config 슬롯에 드래그한다.
    ///
    /// 하드코딩 없음 — 모든 수치는 이 에셋에서만 관리한다.
    /// 맵 크기 변경 시 각 resourceType의 거리 수치만 재조정하면 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "AI Village/ResourceNodeSpawnConfig", fileName = "ResourceNodeSpawnConfig")]
    public class ResourceNodeSpawnConfig : ScriptableObject
    {
        // ── 전역 스폰 설정 ─────────────────────────────────────────────────────────
        [Header("전역 스폰 설정")]
        [Tooltip("0 = 매 실행 랜덤 시드. 양수 = 고정 시드 (동일한 맵 배치 재현 가능).")]
        public int randomSeed = 0;
        [Tooltip("어떤 두 노드 사이의 최소 맨해튼 거리. 노드 겹침 및 밀집을 방지한다.")]
        [Min(1)] public int nodeMinSpacing = 2;
        [Tooltip("유효 위치를 찾지 못할 때 포기하기 전 최대 재시도 횟수. 값이 작으면 배치 실패가 늘어난다.")]
        [Min(10)] public int maxPlacementAttempts = 50;

        // ── 자원별 스폰 설정 ───────────────────────────────────────────────────────
        [Header("자원별 스폰 설정")]
        [Tooltip("자원 타입마다 하나씩 추가한다. 순서는 무관하다.")]
        public ResourceTypeSpawnData[] resourceTypes;
    }
}
