using System;

namespace AIVillage.M0
{
    /// <summary>
    /// 침입 특성 축 (M24-1차 W5) — **종족이 어떻게 구는가**의 유일한 문법.
    ///
    /// 🔑 `TraitValue{축,값}`(성격) · `ActionAnim{Kind,3방}`(행동 그림)과 같은 규약이다:
    /// **append-only · 미등록 = 중립 폴백**. 특성을 하나도 안 준 종족은 배선 전과 똑같이
    /// 굴고, 그것이 이 축의 중립 불변식이다 (게이트 M24-T4가 감시).
    ///
    /// 🔴 `if (종족 == 오크)` 로 쓰는 순간 반려다 (`ADR-M24-4`). 종족 차이는 **데이터**이고,
    /// 특성은 새 실행 계열이 아니라 **기존 판정 지점의 입력**이다 — 진입점 선택·타깃 선택·
    /// 도주 판정·함정 발동·예고 시각. 새 러너를 만들고 싶어지면 그 자리가 설계 오류다.
    ///
    /// ⚠️ 번호 재사용 금지. 지우지 말고 뒤에 append (에셋이 정수로 직렬화한다).
    /// </summary>
    public enum InvaderTraitId
    {
        None = 0,

        /// <summary>창고 스톡을 노린다. 답 = 창고 잠금. **2차 배선** (답이 아직 없어 1차 에셋에 넣으면 게이트 red).</summary>
        TargetStorage = 1,

        /// <summary>망루 사거리를 피해 진입한다. 답 = 망루 재배치. **W7 배선**.</summary>
        ApproachFlank = 2,

        /// <summary>여러 진입점에서 동시에 들어온다. 답 = 전 방향 방어. **W7 배선**.</summary>
        ApproachMultiPoint = 3,

        /// <summary>목표를 이루면 즉시 이탈한다 (치고 빠지기). 답 = 추격·망루 요격.</summary>
        ExitOnGoal = 4,

        /// <summary>도주선이 없다 — 전멸까지 싸운다. 답 = 함정·다수 무장.</summary>
        NoFlee = 5,

        /// <summary>예고가 짧아진다 (야습). 답 = 상시 방어선.</summary>
        ShortWarning = 6,

        /// <summary>방어 시설을 먼저 노린다. 답 = 망루 분산·이중화.</summary>
        TargetDefense = 7,

        /// <summary>지휘관이 죽으면 무리가 무너진다. 답 = 표적 우선순위. **미배선 (예약)**.</summary>
        CommanderRout = 8,

        /// <summary>함정을 학습해 피한다 — 이 종족이 이전에 밟아 본 적이 있으면 안 밟는다.
        /// 답 = 함정 재배치.</summary>
        TrapLearning = 9,
    }

    /// <summary>특성 한 칸 — "이 종족은 유효압력 N 부터 이 수를 쓴다".</summary>
    [Serializable]
    public struct InvaderTrait
    {
        public InvaderTraitId Id;

        /// <summary>이 특성이 열리는 **종족 유효압력** (전역압력 아님 — 많이 만난 종족만 먼저 배운다).
        /// 🔒 공정성 규칙(게이트 M24-T4): 이 값은 **그 특성의 답이 마련 가능해지는 시점보다 뒤**여야
        /// 한다. 답이 없는데 수가 나오면 플레이어에게 대응 수단이 없는 난이도가 된다.</summary>
        public int UnlockPressure;
    }
}
