using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 직업 찾아가기 계열 (M17-R3) — 특정 직업의 주민 곁으로 걸어간다.
    ///
    /// **왜 필요한가**: M16 이후 집은 "50동을 모아 목수에게 부탁한다"로 바뀌었는데, 저축은
    /// goal(능동)이고 부탁은 우연한 마주침(수동)이라 경제의 반쪽만 능동이었다. 100×100 맵에서
    /// 반경 12의 우연은 잘 오지 않아, 돈을 다 모으고도 아무것도 하지 않는 주민이 남았다
    /// (Play 관측 2026-08-01: "고집쟁이가 50동을 넘겼는데 집을 원하는 상황이 발생하지 않았다").
    /// 이 액션이 마지막 한 걸음을 플래너 안으로 들여온다.
    ///
    /// 세계 상태를 바꾸지 않는 **표현·이동 전용** 액션이다 (여가·방문과 동일 성질).
    /// 부탁 성립·장면·지불은 전부 RequestService가 한다 — 여기는 거리만 좁힌다.
    /// 그래서 DirectActionPool 전용이며 ActionCatalog에 등록하지 않는다
    /// (Stroll·RestByCampfire 전례 — 플래너를 안 타는 액션은 인덱스 신원이 필요 없다).
    ///
    /// ⚠️ 대상은 **직업**으로 지정한다 (참조 매핑, ADR-M0-1) — 액션 이름 분기 금지 규약과
    /// 같은 이유로 "목수"라는 문자열이 코드에 등장하지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/SeekJob", fileName = "SeekJobAction")]
    public sealed class SeekJobActionSO : ActionSO
    {
        [Tooltip("찾아갈 직업 (참조 매핑). 비면 실행 실패 — 대상 없는 이동은 의미가 없다.")]
        public JobSO TargetJob;

        [Tooltip("도착 인정 반경 (타일, 맨해튼). 이 안이면 걷지 않고 즉시 도착.\n" +
                 "부탁 성립 반경(RequestSO.RadiusTiles)보다 **작아야** 도착이 곧 부탁 기회가 된다.")]
        public int ArriveRadiusTiles = 3;

        [Tooltip("탐색 반경 (타일, 맨해튼). 이 밖의 대상은 없는 것으로 친다 — 맵 끝까지 걸어가는 " +
                 "행군을 막는다. 0 이하 = 무제한.")]
        public int SearchRadiusTiles = 40;

        public override IActionRunner CreateRunner(VillagerAgent agent) => new SeekJobRunner(this);
    }
}
