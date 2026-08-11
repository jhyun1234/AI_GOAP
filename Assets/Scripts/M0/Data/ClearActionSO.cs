using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 개간 액션 (M22-4차 W4, ADR-C-1) — 계획을 막고 선 자원 노드를 **세상에서 치운다**.
    ///
    /// 🔑 **채집이 아니다.** 채집은 "나무를 얻으려고" 하고 개간은 **"자리를 비우려고"** 한다.
    /// 목적이 다르므로 행동도 다르다 — 그래서 자원 종류를 **묻지 않는다** (나무든 돌이든
    /// 담을 막고 있으면 치운다). 회수는 덤이지 목적이 아니다.
    ///
    /// 🔴 **채집에 얹지 않은 진짜 이유는 플래너 계약이다** (명세 ADR-C-1): GOAP는 선언된
    /// 효과로 계획하는데 `ChopWood`는 `ClearPendingCount`를 안 건드린다고 선언돼 있어 그
    /// goal의 계획을 세울 수 없고, 건드린다고 적으면 **평범한 벌목까지 "개간했다"고 거짓말**하게
    /// 된다. (처음엔 "한 그루에 채집 50회라 느리다"를 근거로 적었는데 그건 **틀렸다** —
    /// 실측은 5회다. 산수는 에셋에서 읽어야 한다.)
    ///
    /// ⚠️ `Anim`은 **채집과 같은 값**을 넣는다 (나무=Chop·돌=Mine 중 하나) — 도끼질은 도끼질이다.
    /// 새 그림 0장. 자원별로 몸짓을 갈라야 한다면 그건 다음 차수의 일이다.
    /// </summary>
    [CreateAssetMenu(menuName = "AI Village/M0/Action/Clear", fileName = "ClearNode")]
    public sealed class ClearActionSO : ActionSO
    {
        public override ActionRunnerBase CreateRunner(VillagerAgent agent) => new ClearRunner(this);
    }
}
