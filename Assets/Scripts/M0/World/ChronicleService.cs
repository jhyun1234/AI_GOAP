using System.Collections.Generic;

namespace AIVillage.M0
{
    /// <summary>퇴장 사유 — **append-only enum** (ADR-M13-2, SlotId 규약 복제).
    /// 새 사유는 반드시 뒤에만 추가한다: 기존 값의 정수가 바뀌면 저장된 연대기가 다른 이야기가 된다.</summary>
    public enum ExitCause
    {
        Alive      = 0, // 아직 살아 있다 (기본값 — 0 = "미기록"과 같은 뜻이 되게 둔다)
        Injury     = 1, // 부상 방치 사망 (VillagerAgent.Die)
        Starvation = 2, // 아사 (VillagerAgent.StarveToDeath)
        Unknown    = 3, // 사망 경로를 안 거치고 소멸 (씬 종료·강제 파괴 — 보루, ADR-M13-3)
    }

    /// <summary>사건 종류 (M13-C2 예약 — C1은 선언만, append 지점·표시는 C2).
    /// **append-only enum** (ADR-M13-2) — 값 재배열·중간 삽입 금지, 게이트 M13-T4가 감시 예정.</summary>
    public enum EventId
    {
        Injured      = 0, // 부상        ← VillagerAgent.TakeInjury
        Healed       = 1, // 치료 완료   ← VillagerAgent.HealInjury (개입의 보상)
        GotHome      = 2, // 집 획득     ← OwnershipService.OnAssigned (구독)
        OrderTaken   = 3, // 명령 수락   ← VillagerAgent.TryGiveOrder
        OrderRefused = 4, // 명령 거부   ← 같은 곳 (거부 3종은 한 칸 — 종류는 Value에)
        Built        = 5, // 완공        ← ConstructionService.OnCompleted (구독)
        Traded       = 6, // 식량 구입   ← RequestService.AskTrade (M16-W5. Value = 지불액 동, OtherId = 판매자)
        HomePaid     = 7, // 집값 지불   ← [M19 휴면 — 기록 지점 없음. 옛 판 표시 호환용으로 잔류
                          //               (append-only). 화폐 철거로 지불 사건 자체가 소멸]
        FoodShared   = 8, // 식량 나눔   ← RequestService.AskShare (M19-W3. Value = 개수,
                          //               OtherId = 나눠준 주민). "굶다가 얻어먹고 살았다"의 계승
    }

    /// <summary>사건 1건 (M13-C2) — 값 타입 취급: 서비스만 만들고 밖에서 고치지 않는다.</summary>
    public struct ChronicleEvent
    {
        public EventId Kind;
        public float   Day;       // 게임일
        public string  SubjectId; // 주체 (= 레코드 주인. 사건만 따로 순회할 때 필요)
        public string  OtherId;   // 대상 (없으면 null — 예: 완공)
        public int     Value;     // 부가값 (없으면 0 — 예: 거부 사유 코드)

        // 명령 거부 사유 코드 (OrderRefused의 Value) — OrderResult enum 정수에 결합하지 않는다
        // (그 enum은 저장 규약이 아니라 재배열될 수 있다). 여기 상수가 기록·표시의 단일 출처.
        public const int REFUSE_HUNGRY  = 1;
        public const int REFUSE_TIRED   = 2;
        public const int REFUSE_INJURED = 3;
    }

    /// <summary>주민 한 명의 생애 기록 (M13-C1) — 죽어도 남는다. 서비스가 들고 있고
    /// GameObject와 무관하다. 성격·직업은 SO 참조가 아니라 **문자열** (BehaviorProfiler 선례 —
    /// 에셋 참조 수명과 무관하게, 세이브 GUID 문제 회피).</summary>
    public sealed class VillagerRecord
    {
        // ── C1 ──────────────────────────────────────────────────────────
        public string    Id;              // AgentId — 사후에도 유일한 키 (_usedNames가 재사용 방지)
        public string    ShortName;       // 표시명 ("A")
        public string    PersonalityName; // "없음" = 중립 (ComposeSelected 규약)
        public string    JobName;         // "무직" = 무직
        public float     BornDay;         // 등장 게임일 (RegisterAgent 시점)
        public float     LeftDay = -1f;   // 퇴장 게임일. **-1 = 생존 중** (0이면 Day 0 사망과 구별 불가)
        public ExitCause Cause = ExitCause.Alive;

        // ── C2 예약 (그릇만 — C1·C2는 이 클래스에 필드를 추가하지 않는다, ADR-M13-2) ──
        /// <summary>이 주민에게 일어난 일. C2는 여기에 **Add만** 하고 위 필드를 건드리지 않는다.</summary>
        public readonly List<ChronicleEvent> Events = new List<ChronicleEvent>(8);

        // ── C3 예약 (선언만 — 퇴장 시점의 관계 스냅샷. C1·C2 단계에선 null) ──
        public string BuddyIdAtExit;
        public string GrudgeIdAtExit;
    }

    /// <summary>
    /// 주민 생애 기록 (M13-C1, 새 선반 ② — M13에서 유일하게 새로 짓는 부분).
    ///
    /// 왜 신설인가: 개인의 생애는 지금까지 어디에도 없었다 — BehaviorProfiler는 성격 **집단**
    /// 통계(읽기 전용·세이브 아님)이고, 이쪽은 **개인 단위·세이브 대상**이라 등급이 다르다.
    /// 왜 VillagerAgent가 아닌가: Destroy(gameObject)와 함께 사라져 목적("죽은 뒤에도 남는 것")
    /// 자체가 무너진다.
    ///
    /// 쓰기 지점 (ADR-M13-3): 퇴장 기록은 **사유를 아는 곳**(Die·StarveToDeath)에서만.
    /// UnregisterAgent는 CloseIfOpen 보루만 — 그 메서드는 ReleaseBy와 한 몸이라 C3의 관계
    /// 스냅샷 시점과 얽히면 안 된다 (명세 §5.4 — 증분이 성립하는 유일한 배치).
    ///
    /// 에이전트=주민 전제 없음 — ID 문자열로만 결합 (후반 확장 인지 규칙 ①).
    /// 읽기 전용 출력 축 (ADR-M13-4) — 시뮬레이션 상태를 쓰지 않는다.
    /// 세이브 대상 (ADR-M0-10): _records 사전이 저장 상태의 전부다.
    /// </summary>
    public sealed class ChronicleService
    {
        private readonly Dictionary<string, VillagerRecord> _records =
            new Dictionary<string, VillagerRecord>(16);

        /// <summary>등장 기록 — 호출처는 RegisterAgent뿐. 재등록은 무시 (멱등 — 첫 기록이 진실).</summary>
        public void RecordBirth(string id, string shortName, string personality, string job, float day)
        {
            if (string.IsNullOrEmpty(id) || _records.ContainsKey(id)) return;
            _records[id] = new VillagerRecord
            {
                Id = id,
                ShortName = shortName,
                PersonalityName = personality,
                JobName = job,
                BornDay = day,
            };
        }

        /// <summary>퇴장 기록 — 호출처는 Die()·StarveToDeath() **둘뿐** (ADR-M13-3).
        /// 이미 마감된 레코드는 무시 (멱등 — 두 경로가 겹칠 수 없지만 보루). 미등장자도 무시.</summary>
        public void RecordExit(string id, float day, ExitCause cause)
        {
            if (!_records.TryGetValue(id, out VillagerRecord r) || r.Cause != ExitCause.Alive) return;
            r.LeftDay = day;
            r.Cause = cause;
        }

        /// <summary>미마감 마감 보루 (ADR-M13-3) — 호출처는 UnregisterAgent뿐.
        /// Cause == Alive일 때만 Unknown 처리 — 사망 경로를 거친 레코드의 사유를 덮으면
        /// 이야기가 사라진다 (탐지: 게이트 M13-T3).</summary>
        public void CloseIfOpen(string id, float day)
        {
            if (!_records.TryGetValue(id, out VillagerRecord r) || r.Cause != ExitCause.Alive) return;
            r.LeftDay = day;
            r.Cause = ExitCause.Unknown;
        }

        /// <summary>사건 기록 (M13-C2) — 레코드의 Events에 **Add만** 한다 (ADR-M13-2:
        /// C1 필드를 읽지도 고치지도 않는다). 미등장 주체는 무시 (구독 배선이 주민 아닌
        /// ID를 넘겨도 안전 — 방어).</summary>
        public void RecordEvent(string subjectId, EventId kind, float day,
                                string otherId = null, int value = 0)
        {
            if (string.IsNullOrEmpty(subjectId)
                || !_records.TryGetValue(subjectId, out VillagerRecord r)) return;
            r.Events.Add(new ChronicleEvent
            {
                Kind = kind, Day = day, SubjectId = subjectId, OtherId = otherId, Value = value,
            });
        }

        /// <summary>레코드 조회 (M13-C2 — 정보줄 표시용 읽기 창구). 미등장 = false.</summary>
        public bool TryGetRecord(string id, out VillagerRecord record)
            => _records.TryGetValue(id, out record);

        /// <summary>퇴장 관계 스냅샷 (M13-C3) — 호출처는 Die/StarveToDeath 둘뿐 (ADR-M13-3:
        /// UnregisterAgent의 ReleaseBy가 관계를 지우기 **전**이어야 한다 — 이 배치가 증분이
        /// 성립한 이유, 명세 §5.4). 멱등 — 첫 스냅샷 보존. null = 단짝/원한 없음 (중립).</summary>
        public void SnapshotRelations(string id, string buddyId, string grudgeId)
        {
            if (!_records.TryGetValue(id, out VillagerRecord r)) return;
            if (r.BuddyIdAtExit == null) r.BuddyIdAtExit = buddyId;
            if (r.GrudgeIdAtExit == null) r.GrudgeIdAtExit = grudgeId;
        }

        /// <summary>회고용 명부 — **BornDay 오름차순, 동률은 Id 사전순** (결정적 —
        /// PickNearestIndex 동률 규칙과 같은 규약: 같은 판을 다시 봐도 같은 순서).</summary>
        public IReadOnlyList<VillagerRecord> RosterByBirth()
        {
            var list = new List<VillagerRecord>(_records.Values);
            list.Sort((a, b) => a.BornDay != b.BornDay
                ? a.BornDay.CompareTo(b.BornDay)
                : string.CompareOrdinal(a.Id, b.Id));
            return list;
        }
    }
}
