using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 이동 속도 표현: 개체 편차 + 출발 가속 + 도착 감속.
    /// 논리(타일 예약·도착 판정)와 무관 — 속도 크기만 다룬다 (표현/논리 분리).
    /// 개체 편차는 AgentId 해시 기반 결정적 값 — 재실행해도 같은 주민은 같은 속도.
    /// </summary>
    public sealed class MoveMotion
    {
        private readonly AgentConfigSO _cfg;
        private readonly float _speedMult;
        private float _accel; // 0→1 출발 가속 계수

        public MoveMotion(AgentConfigSO cfg, string agentId)
        {
            _cfg = cfg;
            // FNV-1a 결정적 편차 — GetHashCode()%1000은 꼬리 1글자 차이 이름에서 붕괴 (2026-07-17 수정)
            float t = StableHash.Spread(agentId, "speed"); // [-1, 1]
            _speedMult = 1f + t * cfg.SpeedVariancePct;
        }

        /// <summary>새 경로 시작 시 호출 — 가속을 0에서 다시 시작.</summary>
        public void ResetPath() => _accel = 0f;

        /// <summary>이번 프레임의 이동 속도 (타일/초). nearDestination이면 감속 배율 적용.</summary>
        public float Tick(float dt, bool nearDestination)
        {
            _accel = _cfg.AccelSec <= 0f ? 1f : Mathf.MoveTowards(_accel, 1f, dt / _cfg.AccelSec);
            float factor = Mathf.Lerp(0.3f, 1f, _accel);
            if (nearDestination) factor = Mathf.Min(factor, _cfg.DecelFactor);
            return _cfg.BaseMoveSpeed * _speedMult * factor;
        }
    }
}
