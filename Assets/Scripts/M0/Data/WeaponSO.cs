using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 무기 한 종 (M32-W3). 수치의 집은 에셋이다 (ADR-M0-2).
    ///
    /// 🔑 무기는 **게이트가 아니라 배율**이다 (ADR-M21-5 · ADR-M32-4 계승) — 맨손도 싸운다.
    /// 무기가 없으면(`MyWeapon == null`) 교전 수치는 전부 `FightActionSO`의 기본값이고,
    /// 그것이 곧 舊 동작이다 (중립 불변식 — 무기 축이 꺼진 판은 오늘과 완전히 같다).
    ///
    /// 🔴 팩에 있는 것만 만든다 (ADR-M32-8): 검·활·도끼. 창·방패는 그림이 없다.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Weapon", fileName = "Weapon")]
    public sealed class WeaponSO : ScriptableObject
    {
        public string DisplayName;

        [Tooltip("타격 간격 배율 — CombatService.HitInterval 의 weaponMult 로 들어간다. " +
                 "작을수록 빠르다. 0 이하는 HitInterval 이 1로 본다 (에셋 사고 방어).")]
        [Min(0f)] public float HitIntervalMult = 1f;

        [Tooltip("타격 1회 피해. 舊 FightActionSO.HitDamage 자리를 무기가 있을 때만 대신한다.")]
        [Min(0f)] public float Damage = 10f;

        [Tooltip("사거리(타일, 맨해튼). 근접 1~2, 활은 그보다 크다 — **사거리가 병과를 가르는 " +
                 "유일한 구조 인자**다. 나머지 둘(간격·피해)은 같은 축 위의 숫자일 뿐이다.")]
        [Min(1)] public int RangeTiles = 2;

        [Tooltip("교전 몸짓 — 러너가 액션 기본 몸짓 대신 이걸 쓴다. 스프라이트 미배선 종류는 " +
                 "몸짓이 안 나올 뿐 판정은 그대로다 (표현과 판정의 분리, ADR-M13-4 정신).")]
        public AnimKind Anim = AnimKind.Attack;

        /// <summary>초당 피해 (순수 — 게이트 M32-T4의 자). 병과가 실제로 갈리는지는
        /// 간격·피해를 따로 보면 알 수 없다: 느리고 센 도끼와 빠르고 약한 검이 같은 값일 수 있다.</summary>
        public static float DamagePerSec(float baseSec, float jobMult, float hitIntervalMult, float damage)
            => damage / CombatService.HitInterval(baseSec, jobMult, hasWeapon: true, hitIntervalMult);

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(DisplayName))
                Debug.LogWarning($"[WeaponSO] {name}: DisplayName 이 비었다 — 화면·로그에 이름이 안 나온다");
        }
    }
}
