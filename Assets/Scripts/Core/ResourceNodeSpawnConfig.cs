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
        [Tooltip("자원이 가득 찼을 때 노드 마커 색상")]
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
