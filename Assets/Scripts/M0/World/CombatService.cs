using System;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>타격 한 번의 결말 (M21-W3) — 호출자(러너·명령)가 다음 행동을 정하는 유일한 판독값.</summary>
    public enum HitOutcome
    {
        Missed   = 0, // 무효타 — 대상이 없거나 이미 도주·퇴장 중 (때릴 것이 없다)
        Damaged  = 1, // 피해만 — 아직 서 있다
        Repelled = 2, // 격퇴 — 도주선 아래로 내려가 물러났다 (이김의 한 형태, 죽이지 않아도 성립)
        Killed   = 3, // 사냥 — 체력 0. 드랍이 함께 일어난다
    }

    /// <summary>
    /// 전투 판정의 집 (M21-W3, ADR-M21-3) — 피해 계산·위협 체력 차감·도주선·무리 도주선·
    /// 사냥 판정·드랍이 전부 여기 모인다. 역할 경계는 M10-C의 확장이다:
    ///   ThreatService = 출몰 스케줄·수명 · ThreatAgent = 표현+이동 ·
    ///   VillagerAgent = 표현+자기 상태의 문(TakeDamage) · **CombatService = 판정**
    ///
    /// 🔴 에이전트=주민 전제 없음 (CLAUDE.md 후반 확장 규칙 ①, 명세 ⚠️ 오해 위험 ②):
    /// 타격자는 **ID 문자열 + 수치**로만 들어온다. `VillagerAgent` 타입을 시그니처에 받지 않는다 —
    /// 몬스터·타 종족이 서로를 때리게 될 때 같은 문으로 들어와야 한다.
    ///
    /// 🔴 위협의 죽음도 문 하나 (ADR-M21-8): 체력 차감은 `ThreatAgent.ApplyHit`,
    /// 도주 전환은 `BeginFlee`, 소멸은 `DespawnNow` — 개체는 문만 열고 **판정은 여기서** 한다.
    ///
    /// 왜 ThreatService에 얹지 않았나 (명세 ⚠️ 오해 위험 ①): 출몰 수명과 교전 판정은 다른 책무다.
    /// 주민 체력·아사는 위협과 무관한데 ThreatService가 그걸 들면 어색해진다.
    ///
    /// 중립 불변식: Threats가 비면 SimulationLoop이 이 서비스도 null로 둔다 (DisasterService 패턴).
    /// 세이브 대상 없음 — 진행 중 교전은 저장하지 않는다 (ADR-M0-10, 로드 후 재계획).
    /// </summary>
    public sealed class CombatService
    {
        private readonly WorldModel _world;
        private readonly Func<float> _gameTime;

        /// <summary>격퇴 성립 (위협, 타격자 ID, 게임일) — 연대기(W9)·완충(W7)·HUD 구독 지점.
        /// 여기서 직접 기록하지 않는 이유: 이 서비스는 판정만 한다 (ADR-M13-4 읽기 전용 축 정신).</summary>
        public event Action<ThreatSO, string, float> OnRepelled;

        /// <summary>사냥 성립 (위협, 타격자 ID, 드랍 수량, 게임일) — 같은 구독 지점.</summary>
        public event Action<ThreatSO, string, int, float> OnHunted;

        public CombatService(WorldModel world, Func<float> gameTime)
        {
            _world = world;
            _gameTime = gameTime;
        }

        // ── 순수 판정 (게이트 M21-T8 — 인스턴스·씬 의존 0) ────────────────────

        /// <summary>체력 눈금의 부동소수 오차 흡수폭 (알고리즘 상수 — 밸런스 아님).
        /// 🔴 왜 필요한가 (2026-08-07 게이트가 잡았다): `60f * 0.4f`는 24가 아니라 **24.0000003**이다.
        /// 그래서 체력이 정확히 24일 때 `24 < 24.0000003`이 참이 되어 "문턱에서는 버틴다"는 규약이
        /// 조용히 깨진다. 가상의 사례가 아니다 — 체력 60에 피해 12면 60→48→36→**24**로 정확히 닿는다.
        /// 폭은 체력 1눈금의 1/1000이라 밸런스에는 보이지 않고, 이 배율대(60~150)의 float 오차
        /// (~3e-6)보다는 충분히 크다.</summary>
        private const float HP_EPSILON = 1e-3f;

        /// <summary>개체 도주선 (순수): 체력이 최대치의 이 비율 **미만**이면 물러난다.
        /// 비율 0 = 도주 없음(끝까지 싸운다), 1 이상 = 첫 피격에 도주. 최대치 0 방어 = false.
        /// ⚠️ 이하가 아니라 **미만**이다: 0.4에서 정확히 40%면 아직 버틴다 — 경계에서 한 대 더
        /// 맞아야 물러나는 쪽이 "몰아붙였다"는 감각을 만든다. 그 '정확히'를 float에서도 지키려고
        /// HP_EPSILON만큼 물러선 선과 비교한다 (주석 참조 — 이 보정 없이는 규약이 반대로 뒤집힌다).</summary>
        public static bool ShouldFlee(float hp, float maxHp, float fleeBelowPct)
            => maxHp > 0f && fleeBelowPct > 0f && hp < maxHp * fleeBelowPct - HP_EPSILON;

        /// <summary>무리 도주선 (순수): 처음 스폰한 마릿수 대비 **아직 싸우는 개체 비율**이
        /// 이 값 미만이면 무리 전체가 무너진다 (이중 도주선의 바깥쪽 — 개체는 자기 체력, 무리는 잔존율).
        /// 스폰 수 ≤ 0 이면 false (무리가 아니다). 잔존 0은 판정할 무리가 이미 없으므로 false —
        /// 전멸에 "무리 도주"를 겹쳐 부르지 않는다 (사건이 두 번 세어진다).
        /// ⚠️ 마릿수 확장(W6) 전까지 스폰은 항상 1이라 이 판정은 실질 무효다 — 순수 함수를 먼저
        /// 두는 것은 W6이 배선만 하면 되게 하려는 것이고, 게이트도 지금 걸어 둔다.</summary>
        public static bool ShouldRout(int aliveAndFighting, int spawned, float routBelowPct)
            => spawned > 0 && aliveAndFighting > 0
               && aliveAndFighting < spawned * routBelowPct;

        /// <summary>타격 간격 (순수, 실시간 초): 기본 간격에 직업·무기 배율을 **곱 결합**한다
        /// (M20 배율 문법 — 무장 사냥꾼 = 0.5 × 0.5 = 4배 빠름).
        /// 무기는 게이트가 아니라 배율이다 (ADR-M21-5) — 맨손도 싸울 수 있고, 다만 느리다.
        /// 배율 ≤ 0 은 무시하고 1로 본다 (에셋 사고 방어 — 0이면 간격 0 = 매 프레임 타격).
        /// 결과는 최소 0.01초 클램프 — 어떤 배율 조합에서도 프레임 타격이 되지 않게.</summary>
        public static float HitInterval(float baseSec, float jobMult, bool hasWeapon, float weaponMult)
        {
            float sec = Mathf.Max(0f, baseSec);
            sec *= jobMult > 0f ? jobMult : 1f;
            if (hasWeapon) sec *= weaponMult > 0f ? weaponMult : 1f;
            return Mathf.Max(0.01f, sec);
        }

        // ── 타격 (유일한 진입) ────────────────────────────────────────────────

        /// <summary>
        /// 주민→위협 타격 (M21-W3). 순서가 곧 규칙이다:
        ///   ① 때릴 수 있는가(대상 존재·아직 살아 있음) → ②체력 차감 → ③사망 → ④도주선.
        /// 사망을 도주선보다 **먼저** 본다: 도주선 아래로 내려가면서 동시에 0이 된 타격은
        /// "물러났다"가 아니라 "죽었다"여야 한다 (드랍이 걸린 쪽이 이긴다).
        ///
        /// 🔴 **도주 중인 개체도 때릴 수 있다** (2026-08-07 자가 재검토에서 뒤집은 결정).
        /// 처음엔 "등 돌린 것을 때려 잡는 길을 막는다"며 도주 중을 Missed로 뒀는데, 배포 수치로
        /// 검산하니 **사냥이 영원히 도달 불가**였다: 늑대 60 · 주민 타격 10 · 도주선 24 →
        /// 60→50→40→30→20에서 도주로 빠지고 그 뒤 모든 타격이 막힌다. 그러면 `MeatDrop`도
        /// `Job_Hunter`(W8)도 ADR-M21-8("HP 0 = 사냥 + 드랍")도 전부 죽은 코드가 된다.
        /// 게다가 **그게 사냥의 정의다** — 도망가는 짐승을 끝까지 쫓아가 잡는 것. 도주선은
        /// 여전히 값을 한다: 도주한 개체는 더 이상 마을을 치지 않으므로 **격퇴만으로도 이김**이고
        /// (ADR-M21-1), 쫓아가 잡는 것은 추가 보상(고기)을 위한 **선택**이 된다.
        ///
        /// 격퇴 사건은 **전이 순간 1회만** 발신한다 (`!IsFleeing` 확인) — 도주 중 계속 때리면
        /// 매 타격마다 "물리쳤습니다"가 뜬다 (DoD ① "각 판정 지점에서 1회씩만").
        /// </summary>
        public HitOutcome VillagerHit(string attackerId, float damage, ThreatAgent target)
        {
            if (target == null || target.So == null) return HitOutcome.Missed;
            if (damage <= 0f) return HitOutcome.Missed;
            if (target.Hp <= 0f) return HitOutcome.Missed; // 이미 죽은 것 — 소멸 지연 프레임 방어

            ThreatSO so = target.So;
            float day = _gameTime != null ? _gameTime() : 0f;
            float remain = target.ApplyHit(damage); // 차감의 유일한 문 (ADR-M21-8)

            Debug.Log($"[Combat] 타격 — {attackerId} → {so.DisplayName} " +
                      $"{damage:0.#} 피해 (남은 체력 {remain:0}/{so.MaxHp:0})");

            if (remain <= 0f)
            {
                int drop = Mathf.Max(0, so.MeatDrop);
                if (drop > 0) _world?.AddStock(SlotId.RawFoodStock, drop); // 전역 스톡 (ADR-M21-8 전이 통로)
                Debug.Log($"[Combat] 사냥 — {attackerId}이(가) {so.DisplayName}을(를) 잡았다" +
                          (drop > 0 ? $" (고기 {drop} → 창고)" : ""));
                OnHunted?.Invoke(so, attackerId, drop, day);
                target.DespawnNow();
                return HitOutcome.Killed;
            }

            // 전이 순간 1회만 — 이미 도주 중이면 계속 때려도 격퇴 사건을 다시 부르지 않는다
            if (!target.IsFleeing && ShouldFlee(remain, so.MaxHp, so.FleeBelowHpPct))
            {
                Debug.Log($"[Combat] 격퇴 — {so.DisplayName}이(가) 물러난다 " +
                          $"(체력 {remain:0}/{so.MaxHp:0} < 도주선 {so.FleeBelowHpPct:P0})");
                OnRepelled?.Invoke(so, attackerId, day);
                target.BeginFlee(); // 도주 전환의 문 — 퇴장 경로는 ThreatService가 부여한다
                return HitOutcome.Repelled;
            }

            return HitOutcome.Damaged;
        }
    }
}
