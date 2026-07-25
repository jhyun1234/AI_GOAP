using System;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 성향 축 (M12-A). 성격은 이 축들의 벡터로 표현되고, 행동에는 4작용형식
    /// (①우선순위 ②비용 ③문턱 ④대상)으로만 작용한다 (ADR-M12-1).
    ///
    /// ⚠️ **뒤에만 append** — 기존 인덱스 불변 (에셋 호환, ADR-M12-3 · SlotId 규약 복제).
    /// 벡터는 고정 길이 배열이 아니라 {TraitId, Value} 쌍 배열이라, 축을 새로 추가해도
    /// 기존 에셋을 한 개도 고치지 않고 행동이 100% 불변이다 (미등록 = 0 = 중립).
    /// </summary>
    public enum TraitId
    {
        Diligence   = 0, // 근면: 태만 ↔ 부지런 (노동 goal·노동 비용)
        Foresight   = 1, // 대비: 즉흥 ↔ 미리준비 (비축·조리·저축 goal)
        Sociability = 2, // 사교: 고립 ↔ 어울림 (간호·부탁·구걸·택지 근접)
        Wanderlust  = 3, // 모험: 정주 ↔ 방랑 (탐험·야외채집·택지 거리)
        Willfulness = 4, // 자존: 순응 ↔ 고집 (거부·보상 태도·공용 goal 회피)
        Caution     = 5, // 겁: 담대 ↔ 조심 (위협 감지 반경·도망)
    }

    /// <summary>
    /// 성격 벡터의 한 축 (M12-A). 미등록 축은 0 = 중립 — 이 규약이 ADR-M12-3의 근거다.
    /// </summary>
    [Serializable]
    public struct TraitValue
    {
        public TraitId Trait;

        [Range(-100, 100)]
        [Tooltip("이 성격의 축 값. 0 = 중립 (= 미등록과 동일). ±100이 양끝")]
        public int Value;
    }

    /// <summary>
    /// 소비처가 각 축을 얼마나 읽는지 (M12-A). goal·계열·문턱마다 다른 것은 이 가중치뿐이고,
    /// 유도식은 형식마다 하나다 (ADR-M12-1).
    /// </summary>
    [Serializable]
    public struct TraitWeight
    {
        public TraitId Trait;

        [Range(-1f, 1f)]
        [Tooltip("+면 축이 높을수록 강해지고, -면 축이 높을수록 약해진다")]
        public float Weight;
    }

    /// <summary>
    /// ③문턱 형식의 소비처 설정 (M12-A). 가중합(정규화 편향)에 이 소비처의 단위를 곱해
    /// 환산한다 — 포만%·타일·친밀도처럼 단위가 제각각이라 환산만 소비처가 맡는다.
    /// Weights가 비면 편향 0 = base 그대로 (중립 불변식).
    /// </summary>
    [Serializable]
    public struct TraitBias
    {
        [Tooltip("이 문턱을 움직이는 축들. 비면 성향 무관 (중립)")]
        public TraitWeight[] Weights;

        [Tooltip("편향(-1~+1)을 이 소비처의 단위로 환산하는 폭. 현행 값의 폭을 그대로 쓴다 " +
                 "(예: 거부 오프셋 ±15, 감지 반경 배율 ±0.4)")]
        public float Sensitivity;
    }

    /// <summary>
    /// 축별 혼잣말 풀 한 칸 (M12-F). 축 하나의 한쪽 끝(높음/낮음)에 대응한다 —
    /// "부지런한 사람의 혼잣말"과 "게으른 사람의 혼잣말"은 다른 풀이다.
    /// </summary>
    [Serializable]
    public struct TraitMoodPool
    {
        public TraitId Trait;

        [Tooltip("true = 이 축이 **높은** 주민의 대사, false = 낮은 주민의 대사")]
        public bool ForHighValue;

        public string[] Lines;
    }

    /// <summary>
    /// 성향 조건 (M12-A). 부탁 성립처럼 **결과 상태(스톡)만 읽던 판정 경로**에 기질을 들이는
    /// 창구다 — 성격 페널티가 통째로 우회되던 구조(2026-07-24 관측)의 차단점.
    /// ⚠️ GoalSO.TriggerConditions에는 쓰지 않는다 (ADR-M12-2).
    /// </summary>
    [Serializable]
    public struct TraitCondition
    {
        public TraitId Trait;

        [Range(-100, 100)]
        [Tooltip("이 값 이상이어야 성립. 음수면 '이하'가 아니라 여전히 '이상'이다 (단조 규약)")]
        public int MinValue;
    }
}
