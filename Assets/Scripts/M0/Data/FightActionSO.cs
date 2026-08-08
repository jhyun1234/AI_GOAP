using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 교전 계열 액션 (M21-W4). 최근접 위협에게 다가가 타격 간격마다 때린다 —
    /// **판정은 하지 않는다**: 피해·도주선·사냥은 CombatService의 몫이고 러너는 부를 뿐이다
    /// (명세 ⚠️ 오해 위험 ③ — "FightRunner가 위협 HP를 직접 깎는다"는 금지).
    ///
    /// 수치가 여기 있는 이유 (ADR-M0-1 액션 단일 응집): 타격 피해·간격·사거리는 이 액션의
    /// **실행 파라미터**다. AgentConfig에 두면 "때리는 일"의 정의가 두 에셋에 흩어진다.
    /// ⚠️ BaseCost는 5 이상 유지 — 카탈로그 최소 비용이 잡 휴리스틱의 눈금이다 (게이트 M10-T2).
    /// 다만 이 액션은 Goal_Fight의 DirectActionPool로 실행되어 **플래너를 거치지 않으므로**
    /// 비용은 실질적으로 쓰이지 않는다 (카탈로그 눈금 규약만 지킨다).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/Fight", fileName = "FightAction")]
    public sealed class FightActionSO : ActionSO
    {
        [Tooltip("타격 사거리 (타일, 맨해튼). 이 안에 들어와야 때린다. 제안 1 — 맨손 근접이므로 " +
                 "위협의 타격 반경(3~4)보다 **좁아야** 한다: 늑대가 먼저 닿고 주민이 파고들어야 " +
                 "'맞으면서 때린다'가 성립한다.")]
        public int StrikeRangeTiles = 1;

        [Tooltip("기본 타격 간격 (실시간 초). §4 제안치 4초 — 늑대 60 체력을 맨손 10 피해로 " +
                 "6대(24초)에 몰아낸다. 직업·무기 배율이 여기에 곱해진다 (CombatService.HitInterval).")]
        public float BaseHitSec = 4f;

        [Tooltip("한 대의 피해량. §4 제안치 10 — 늑대(60) 기준 4대에 도주선(24), 6대에 사냥. " +
                 "⚠️ 위협의 MaxHp·FleeBelowHpPct와 짝이다 (M17 수치의 짝) — 한쪽만 바꾸면 " +
                 "'맨손 솔로는 아프다'는 검산이 조용히 무너진다.")]
        public float HitDamage = 10f;

        [Tooltip("무기 보유 시 타격 간격 배율 (M21-W5). §4 제안치 0.5 = 2배 빠름. 직업 배율(W8)과 " +
                 "곱 결합 (M20 배율 문법 — 무장 사냥꾼 = 4배). ⚠️ 무기는 게이트가 아니라 배율이다 " +
                 "(ADR-M21-5) — 맨손도 싸울 수 있고, 이 값은 '빨라지는 정도'만 정한다.")]
        public float WeaponHitSecMult = 0.5f;

        [Tooltip("매복 대기 상한 (실시간 초, 2026-08-08 개정 — 쫓지 않고 매복한다). 사거리 밖 대상을 " +
                 "기다리는 최대 시간. 재타격 주기(0.25일 = 25초)보다 길어야 늑대가 한 번은 돌아온다. " +
                 "초과 시 플랜 실패(쿨다운) — 교전(105) > 배고픔(100)이라 상한 없는 대기는 매복 아사가 " +
                 "된다. 제안 30.")]
        public float AmbushGiveUpSec = 30f;

        public override IActionRunner CreateRunner(VillagerAgent agent) => new FightRunner(this);

        protected override void OnValidate()
        {
            base.OnValidate(); // ADR-M18-1 전제 문법 검사 — private으로 가리면 이 액션만 감사를 빠져나간다
            if (BaseHitSec <= 0f)
                Debug.LogWarning($"[FightActionSO] {name}: BaseHitSec({BaseHitSec}) ≤ 0 — 매 프레임 타격이 됩니다.", this);
            if (HitDamage <= 0f)
                Debug.LogWarning($"[FightActionSO] {name}: HitDamage({HitDamage}) ≤ 0 — 때려도 아무 일이 없습니다.", this);
            if (StrikeRangeTiles < 0)
                Debug.LogWarning($"[FightActionSO] {name}: StrikeRangeTiles({StrikeRangeTiles})는 음수일 수 없습니다.", this);
            if (WeaponHitSecMult <= 0f || WeaponHitSecMult >= 1f)
                Debug.LogWarning($"[FightActionSO] {name}: WeaponHitSecMult({WeaponHitSecMult})는 0~1 사이여야 " +
                                 "합니다 — 1 이상이면 무기가 오히려 느리거나 무의미합니다 (ADR-M21-5).", this);
            if (AmbushGiveUpSec <= 0f)
                Debug.LogWarning($"[FightActionSO] {name}: AmbushGiveUpSec({AmbushGiveUpSec}) ≤ 0 — 매복 없이 " +
                                 "즉시 실패해 舊 헛걸음 순환으로 돌아갑니다.", this);
        }
    }
}
