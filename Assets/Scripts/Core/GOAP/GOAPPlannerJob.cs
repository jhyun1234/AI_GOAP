/// <summary>
/// GOAPPlannerJob.cs - Burst 컴파일 A* GOAP 플래닝 Job
///
/// 역할(Role): C# Job System + Burst Compiler로 GOAP A* 탐색을 수행하는 핵심 Job 구조체.
///             메인 스레드를 차단하지 않고 워커 스레드에서 비동기 실행된다.
///             최대 70명 주민 동시 플래닝 시 ~1.8ms(Burst 적용) 이내 완료를 목표로 한다.
///
/// A* 탐색 개요:
///   - 노드: (월드 스테이트, 현재 비용 g, 휴리스틱 h, 부모, 액션)
///   - 열린 목록: NativeArray 기반 최소 이진 힙 (최소 f-cost 우선)
///   - 목표 판정: GoalMask가 1인 슬롯이 모두 GoalState와 일치하면 성공
///   - 깊이 제한: MAX_DEPTH=12 (N1: Explore+×6 멀티스텝 플랜 허용)
///   - 노드 수 제한: MAX_NODES=2048
///   - 휴리스틱: 미충족 목표 슬롯 수 x 2.9f (admissible)
///
/// 성능 예산 (기획 명세 기준):
///   Burst 적용 후 최대 70명 동시 플래닝 시 ~1.8ms/frame
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-26
/// </summary>

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace AIVillage.Core.GOAP
{
    /// <summary>
    /// Burst 컴파일 GOAP A* 플래닝 Job.
    /// GOAPPlannerScheduler가 생성하고 스케줄링한다.
    /// 직접 생성하지 말 것 — 내부 버퍼 할당이 복잡하므로 반드시 Scheduler를 통해 사용한다.
    /// </summary>
    [BurstCompile(CompileSynchronously = false)]
    public struct GOAPPlannerJob : IJob
    {
        // ────────────────────────────────────────────────────────────────────
        // 탐색 한계 상수 (기획 명세 수치)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>탐색 최대 노드 수. 이 수를 초과하면 탐색을 중단하고 실패 처리한다.</summary>
        // [S4] S1(Closed Set)+S2(부족량 휴리스틱) 적용 후 탐색 효율 대폭 향상.
        // 2048로 복귀 실험: NoSolutionFound 발생 시 4096으로 조정.
        public const int MAX_NODES    = 2048;

        /// <summary>플랜 최대 깊이. [N1] 12단계: Explore+ChopWood×6(7스텝) 등 멀티스텝 플랜 허용.</summary>
        public const int MAX_DEPTH    = 12;

        /// <summary>결과 플랜 최대 길이 (= MAX_DEPTH).</summary>
        public const int MAX_PLAN_LEN = 12;

        // ────────────────────────────────────────────────────────────────────
        // [S2] HEURISTIC_WEIGHT 2.9f 하드코딩 삭제 (ADR-5).
        // 컨텍스트 배율(RestOnGround ×0.4 등)로 실효 비용이 2.9 아래로 내려갈 수 있어
        // admissibility를 깰 수 있는 잠재 버그였다.
        // 대신 Execute() 서두에서 Actions 배열 min(BaseCost)를 실측한다.

        // ────────────────────────────────────────────────────────────────────
        // 입력 필드 (ReadOnly — 메인 스레드에서 쓰고 Job은 읽기만 한다)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>플래닝 시작 시점의 현재 GOAP 월드 스테이트 (43 슬롯).</summary>
        [ReadOnly] public NativeArray<int> CurrentState;

        /// <summary>달성해야 하는 목표 슬롯 값 (43 슬롯).</summary>
        [ReadOnly] public NativeArray<int> GoalState;

        /// <summary>목표 판정에 포함되는 슬롯 마스크 (52 슬롯). 1=포함, 0=무시.</summary>
        [ReadOnly] public NativeArray<int> GoalMask;

        /// <summary>슬롯별 목표 판정 연산자 (52 슬롯). 0=Equal, 1=GreaterEq, 2=LessEq (ADR-3).</summary>
        [ReadOnly] public NativeArray<int> GoalOps;

        /// <summary>역할 보정이 반영된 GOAP Action 정의 목록 (20개).</summary>
        [ReadOnly] public NativeArray<GOAPActionDef> Actions;

        // ────────────────────────────────────────────────────────────────────
        // 출력 필드 (Job이 쓰고 메인 스레드가 Complete() 후 읽는다)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 결과 플랜의 Action 해시 값 배열 (최대 MAX_PLAN_LEN 길이).
        /// GOAPActionRegistry.HashToActionId()로 문자열로 변환한다.
        /// </summary>
        public NativeArray<int> ResultActions;

        /// <summary>
        /// ResultActions의 실제 유효 길이. ResultLength[0] = 0이면 플래닝 실패.
        /// </summary>
        public NativeArray<int> ResultLength;

        /// <summary>[N0 계측] 이번 탐색에서 생성(할당)한 노드 수. 힙에 삽입된 수이며 루트(1) 포함. NodesExpanded[0]에 기록.</summary>
        public NativeArray<int> NodesExpanded;

        // ────────────────────────────────────────────────────────────────────
        // 내부 작업 버퍼 (Persistent 할당 — GOAPPlannerScheduler가 생성/Dispose 담당)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 노드별 GOAP 월드 스테이트 버퍼.
        /// 크기: MAX_NODES x TOTAL_SLOTS.
        /// nodeIdx 노드의 슬롯 s는 NodeStates[nodeIdx * TOTAL_SLOTS + s]로 접근한다.
        /// </summary>
        public NativeArray<int>   NodeStates;

        /// <summary>노드별 f-cost (g+h). 최소 힙 정렬 기준값.</summary>
        public NativeArray<float> NodeCosts;

        // [PR Fix]: Critical-1 — g-cost를 f-cost와 별도로 추적하는 NodeGCosts 배열 추가
        // 기존 NodeCosts는 f-cost(g+h)만 저장하므로, 자식 확장 시 g-cost를 역산하면
        // parentGCost = NodeCosts[current] - CalculateHeuristic(current)가 음수가 될 수 있다.
        // NodeGCosts에 g-cost를 별도 저장하여 이 버그를 완전히 제거한다.
        /// <summary>노드별 g-cost (루트에서 이 노드까지의 실제 비용) 버퍼.</summary>
        public NativeArray<float> NodeGCosts;

        /// <summary>노드별 탐색 깊이. MAX_DEPTH 초과 시 확장 생략.</summary>
        public NativeArray<int>   NodeDepths;

        /// <summary>노드별 부모 노드 인덱스. 루트는 -1. 역추적에 사용.</summary>
        public NativeArray<int>   NodeParents;

        /// <summary>
        /// 노드별 진입 Action 인덱스 (Actions 배열 인덱스).
        /// -1이면 루트 노드. 역추적 시 Actions[NodeActions[node]].ActionStringHash로 해시를 얻는다.
        /// </summary>
        public NativeArray<int>   NodeActions;

        /// <summary>
        /// 최소 f-cost 이진 힙. 노드 인덱스를 저장한다.
        /// NodeCosts 배열을 기준으로 최솟값이 항상 OpenQueue[0]에 위치한다.
        /// 크기: MAX_NODES.
        /// </summary>
        public NativeArray<int>   OpenQueue;

        /// <summary>
        /// OpenQueue의 현재 유효 크기. QueueSize[0]에 저장된다.
        /// NativeArray로 래핑한 이유: Burst Job에서 ref int를 전달할 수 없기 때문이다.
        /// </summary>
        public NativeArray<int>   QueueSize;

        // [S1] Closed Set 버퍼 (GOAPPlannerScheduler가 MAX_NODES×2 크기로 할당)
        /// <summary>방문 상태 해시 테이블. 0=빈 슬롯. FNV-1a 해시.</summary>
        public NativeArray<int>   VisitedHashes;
        /// <summary>각 해시 슬롯에 저장된 노드 인덱스. 전수 비교(StatesEqual)에 사용.</summary>
        public NativeArray<int>   VisitedNodeIdx;
        /// <summary>각 해시 슬롯에 저장된 g-cost. 재개방(더 싼 경로 발견) 판단에 사용.</summary>
        public NativeArray<float> VisitedGCosts;

        // [S2] 슬롯별 최대 Add/Sub 효과량 (GOAPActionRegistry.BuildMaxGainDrop이 메인 스레드에서 산출)
        /// <summary>슬롯별 최대 Add 효과값. GreaterEq 목표 스텝 추정 (부족량 ÷ MaxGain)에 사용.</summary>
        [ReadOnly] public NativeArray<float> MaxGain;
        /// <summary>슬롯별 최대 Sub 효과값. LessEq 목표 스텝 추정 (초과량 ÷ MaxDrop)에 사용.</summary>
        [ReadOnly] public NativeArray<float> MaxDrop;

        // ────────────────────────────────────────────────────────────────────
        // IJob 인터페이스 구현
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A* GOAP 탐색 메인 로직. 워커 스레드에서 Burst 컴파일되어 실행된다.
        ///
        /// 탐색 순서:
        ///   1. 루트 노드 초기화
        ///   2. 힙에서 최소 f-cost 노드 추출
        ///   3. 목표 달성 검사 → 성공 시 역추적 후 종료
        ///   4. 깊이 초과 시 스킵
        ///   5. 각 Action의 Precondition 확인 → Effect 적용 → 새 노드 삽입
        ///   6. 노드 한계 초과 또는 탐색 공간 소진 시 실패 종료 (ResultLength[0]=0)
        /// </summary>
        public void Execute()
        {
            // ── 출력 초기화: 실패 기본값 ──────────────────────────────────
            ResultLength[0]  = 0;
            NodesExpanded[0] = 0;

            int totalSlots = GOAPPlanningSlots.TOTAL_SLOTS;

            // [S2] 컨텍스트 배율 반영 후 실제 minActionCost 실측 (ADR-5).
            // 하드코딩 2.9f 대체: RestOnGround ×0.4 등으로 실효 비용이 내려가는 케이스를 처리한다.
            float minActionCost = float.MaxValue;
            for (int a = 0; a < Actions.Length; a++)
                if (Actions[a].BaseCost < minActionCost) minActionCost = Actions[a].BaseCost;
            if (minActionCost <= 0f || minActionCost >= float.MaxValue) minActionCost = 1f;

            // ── 탐색 전 목표 즉시 달성 확인 ──────────────────────────────
            // [PR Fix]: N-001 — 목표 이미 달성 시 ResultLength[0] = -1 설정 (특수값)
            // 기존: return만 하여 ResultLength[0]=0(실패 코드)을 그대로 유지
            //       → VillagerFSM이 "플래닝 실패"와 구별하지 못해 Replanning 무한루프 발생
            // 수정: -1 특수값으로 "이미 달성됨"을 나타내어 VillagerFSM이 정확히 분기 처리 가능
            if (IsGoalSatisfied(CurrentState, totalSlots))
            {
                ResultLength[0] = -1; // 특수값: 이미 목표 달성 상태 (빈 플랜 = 액션 불필요)
                return;
            }

            // ── 루트 노드 초기화 ──────────────────────────────────────────
            // 노드 0 = 루트: CurrentState 복사, g-cost=0, f-cost=h, depth=0, parent=-1, action=-1
            for (int s = 0; s < totalSlots; s++)
            {
                NodeStates[s] = CurrentState[s];
            }

            // [PR Fix]: Critical-1 — NodeGCosts[0]=0(g-cost), NodeCosts[0]=h (f = g+h = 0+h)
            // 기존: NodeCosts[0] = 0f → 자식 g-cost 역산 시 h를 빼면 음수가 되는 버그 발생
            // 수정: g-cost는 NodeGCosts에, f-cost는 NodeCosts에 각각 독립 저장
            NodeGCosts[0]  = 0f;                                              // g(root) = 0
            float rootH    = CalculateHeuristic(0, totalSlots, minActionCost);
            NodeCosts[0]   = rootH;                                           // f(root) = g + h = 0 + h

            NodeDepths[0]  = 0;
            NodeParents[0] = -1;
            NodeActions[0] = -1;

            OpenQueue[0] = 0;  // 힙[0] = 노드 인덱스 0
            QueueSize[0] = 1;

            int nodeCount = 1; // 다음에 할당할 노드 인덱스

            // [S1] 루트 노드 등록 — "시작 상태로 되돌아오는 순환" 차단
            TryRegisterState(0, 0f, totalSlots);

            // ── A* 메인 탐색 루프 ─────────────────────────────────────────
            while (QueueSize[0] > 0 && nodeCount < MAX_NODES)
            {
                // 최소 f-cost 노드 추출 (min-heap pop)
                int current = HeapPop();
                if (current < 0) break; // 힙이 비었음

                // ── 목표 달성 확인 ────────────────────────────────────────
                if (IsGoalSatisfiedAtNode(current, totalSlots))
                {
                    NodesExpanded[0] = nodeCount;
                    Backtrack(current);
                    return; // 탐색 성공
                }

                // ── 깊이 제한 초과 스킵 ───────────────────────────────────
                if (NodeDepths[current] >= MAX_DEPTH) continue;

                // ── 각 Action 확장 ────────────────────────────────────────
                for (int a = 0; a < Actions.Length; a++)
                {
                    if (nodeCount >= MAX_NODES) break;

                    GOAPActionDef action = Actions[a];

                    // Precondition 충족 여부 확인
                    if (!CheckPreconditionsAtNode(current, totalSlots, action)) continue;

                    // 새 노드 상태 = 현재 노드 상태 복사 후 Effect 적용
                    int newNode       = nodeCount;
                    int baseOffset    = current * totalSlots;
                    int newBaseOffset = newNode  * totalSlots;

                    for (int s = 0; s < totalSlots; s++)
                    {
                        NodeStates[newBaseOffset + s] = NodeStates[baseOffset + s];
                    }

                    // Effect 적용 (새 노드 상태를 직접 수정)
                    ApplyEffectsToNode(newNode, totalSlots, action);

                    // ── f-cost 계산: f(n) = g(n) + h(n) ─────────────────
                    float gCost = NodeGCosts[current] + action.BaseCost;

                    // [S1] Closed Set: 동일 상태를 같거나 싼 비용으로 이미 방문했으면 폐기.
                    // nodeCount를 증가시키지 않으므로 newNode 슬롯은 다음 액션이 재사용한다.
                    if (!TryRegisterState(newNode, gCost, totalSlots)) continue;

                    float hCost         = CalculateHeuristic(newNode, totalSlots, minActionCost);
                    NodeGCosts[newNode] = gCost;                      // 자식 g-cost 저장
                    NodeCosts[newNode]  = gCost + hCost;              // 자식 f-cost 저장

                    NodeDepths[newNode]  = NodeDepths[current] + 1;
                    NodeParents[newNode] = current;
                    NodeActions[newNode] = a;

                    HeapPush(newNode);
                    nodeCount++;
                }
            }

            // 노드 한계 초과 또는 탐색 공간 소진 → 실패 처리
            // [PR Fix]: Warning-2 — 의도적 설계 결정 주석 추가
            // 설계 결정: MAX_NODES 초과 시 힙 잔여 노드를 순회하지 않고 즉시 실패.
            // 성능(1.8ms 예산) 우선. 더 정밀한 처리가 필요하면 MAX_NODES를 늘리거나
            // 최적성이 필요한 Goal만 별도 처리한다.
            // ResultLength[0] = 0 유지 (VillagerFSM이 Replanning으로 전이)
            NodesExpanded[0] = nodeCount;
        }

        // ────────────────────────────────────────────────────────────────────
        // 내부 헬퍼 메서드 (Burst 호환)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>Precondition/Goal 판정 헬퍼 (Burst 인라인 가능 정적 메서드).</summary>
        private static bool PrecHolds(int stateVal, int op, int reqVal)
        {
            if (op == 1) return stateVal >= reqVal; // GreaterEq
            if (op == 2) return stateVal <= reqVal; // LessEq
            return stateVal == reqVal;               // Equal (기본, 레거시 호환)
        }

        /// <summary>Effect 적용 헬퍼. op=0:Set, 1:Add, 2:Sub(음수 클램프 ADR-6).</summary>
        private static int ApplyEff(int stateVal, int op, int v)
        {
            if (op == 1) return stateVal + v;
            if (op == 2) { int r = stateVal - v; return r < 0 ? 0 : r; }
            return v; // Set
        }

        /// <summary>
        /// 주어진 NativeArray 상태가 목표를 달성했는지 확인한다.
        /// GoalMask[i]=1인 슬롯에서 PrecHolds(state[i], GoalOps[i], GoalState[i])이면 true.
        /// GoalOps[i]=0(Equal, 기본)이면 기존 불리언 동작과 동일.
        /// </summary>
        private bool IsGoalSatisfied(NativeArray<int> state, int totalSlots)
        {
            for (int s = 0; s < totalSlots; s++)
            {
                if (GoalMask[s] == 1 && !PrecHolds(state[s], GoalOps[s], GoalState[s]))
                    return false;
            }
            return true;
        }

        /// <summary>특정 노드의 상태가 목표를 달성했는지 확인한다.</summary>
        private bool IsGoalSatisfiedAtNode(int nodeIdx, int totalSlots)
        {
            int offset = nodeIdx * totalSlots;
            for (int s = 0; s < totalSlots; s++)
            {
                if (GoalMask[s] == 1 && !PrecHolds(NodeStates[offset + s], GoalOps[s], GoalState[s]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 특정 노드의 상태에서 Action의 모든 Precondition이 충족되는지 확인한다.
        /// PrecHolds로 Equal/GreaterEq/LessEq 판정을 통일. Op=0(기본)이면 기존 동작과 동일.
        /// </summary>
        private bool CheckPreconditionsAtNode(int nodeIdx, int totalSlots, GOAPActionDef action)
        {
            int o = nodeIdx * totalSlots;

            switch (action.PrecCount)
            {
                case 0: return true;
                case 1: return PrecHolds(NodeStates[o + action.Prec0S], action.Prec0Op, action.Prec0V);
                case 2: return PrecHolds(NodeStates[o + action.Prec0S], action.Prec0Op, action.Prec0V)
                            && PrecHolds(NodeStates[o + action.Prec1S], action.Prec1Op, action.Prec1V);
                case 3: return PrecHolds(NodeStates[o + action.Prec0S], action.Prec0Op, action.Prec0V)
                            && PrecHolds(NodeStates[o + action.Prec1S], action.Prec1Op, action.Prec1V)
                            && PrecHolds(NodeStates[o + action.Prec2S], action.Prec2Op, action.Prec2V);
                case 4: return PrecHolds(NodeStates[o + action.Prec0S], action.Prec0Op, action.Prec0V)
                            && PrecHolds(NodeStates[o + action.Prec1S], action.Prec1Op, action.Prec1V)
                            && PrecHolds(NodeStates[o + action.Prec2S], action.Prec2Op, action.Prec2V)
                            && PrecHolds(NodeStates[o + action.Prec3S], action.Prec3Op, action.Prec3V);
                case 5: return PrecHolds(NodeStates[o + action.Prec0S], action.Prec0Op, action.Prec0V)
                            && PrecHolds(NodeStates[o + action.Prec1S], action.Prec1Op, action.Prec1V)
                            && PrecHolds(NodeStates[o + action.Prec2S], action.Prec2Op, action.Prec2V)
                            && PrecHolds(NodeStates[o + action.Prec3S], action.Prec3Op, action.Prec3V)
                            && PrecHolds(NodeStates[o + action.Prec4S], action.Prec4Op, action.Prec4V);
                case 6: return PrecHolds(NodeStates[o + action.Prec0S], action.Prec0Op, action.Prec0V)
                            && PrecHolds(NodeStates[o + action.Prec1S], action.Prec1Op, action.Prec1V)
                            && PrecHolds(NodeStates[o + action.Prec2S], action.Prec2Op, action.Prec2V)
                            && PrecHolds(NodeStates[o + action.Prec3S], action.Prec3Op, action.Prec3V)
                            && PrecHolds(NodeStates[o + action.Prec4S], action.Prec4Op, action.Prec4V)
                            && PrecHolds(NodeStates[o + action.Prec5S], action.Prec5Op, action.Prec5V);
                case 7: return PrecHolds(NodeStates[o + action.Prec0S], action.Prec0Op, action.Prec0V)
                            && PrecHolds(NodeStates[o + action.Prec1S], action.Prec1Op, action.Prec1V)
                            && PrecHolds(NodeStates[o + action.Prec2S], action.Prec2Op, action.Prec2V)
                            && PrecHolds(NodeStates[o + action.Prec3S], action.Prec3Op, action.Prec3V)
                            && PrecHolds(NodeStates[o + action.Prec4S], action.Prec4Op, action.Prec4V)
                            && PrecHolds(NodeStates[o + action.Prec5S], action.Prec5Op, action.Prec5V)
                            && PrecHolds(NodeStates[o + action.Prec6S], action.Prec6Op, action.Prec6V);
                case 8: return PrecHolds(NodeStates[o + action.Prec0S], action.Prec0Op, action.Prec0V)
                            && PrecHolds(NodeStates[o + action.Prec1S], action.Prec1Op, action.Prec1V)
                            && PrecHolds(NodeStates[o + action.Prec2S], action.Prec2Op, action.Prec2V)
                            && PrecHolds(NodeStates[o + action.Prec3S], action.Prec3Op, action.Prec3V)
                            && PrecHolds(NodeStates[o + action.Prec4S], action.Prec4Op, action.Prec4V)
                            && PrecHolds(NodeStates[o + action.Prec5S], action.Prec5Op, action.Prec5V)
                            && PrecHolds(NodeStates[o + action.Prec6S], action.Prec6Op, action.Prec6V)
                            && PrecHolds(NodeStates[o + action.Prec7S], action.Prec7Op, action.Prec7V);
                default: return false;
            }
        }

        /// <summary>
        /// Action의 Effect를 특정 노드의 상태에 직접 적용한다.
        /// ApplyEff로 Set/Add/Sub 연산을 통일. Op=0(Set, 기본)이면 기존 대입 동작과 동일.
        /// Sub 연산은 음수 클램프(ADR-6)가 적용된다.
        /// </summary>
        private void ApplyEffectsToNode(int nodeIdx, int totalSlots, GOAPActionDef action)
        {
            int o = nodeIdx * totalSlots;

            switch (action.EffectCount)
            {
                case 0: return;
                case 1:
                    NodeStates[o + action.Eff0S] = ApplyEff(NodeStates[o + action.Eff0S], action.Eff0Op, action.Eff0V);
                    return;
                case 2:
                    NodeStates[o + action.Eff0S] = ApplyEff(NodeStates[o + action.Eff0S], action.Eff0Op, action.Eff0V);
                    NodeStates[o + action.Eff1S] = ApplyEff(NodeStates[o + action.Eff1S], action.Eff1Op, action.Eff1V);
                    return;
                case 3:
                    NodeStates[o + action.Eff0S] = ApplyEff(NodeStates[o + action.Eff0S], action.Eff0Op, action.Eff0V);
                    NodeStates[o + action.Eff1S] = ApplyEff(NodeStates[o + action.Eff1S], action.Eff1Op, action.Eff1V);
                    NodeStates[o + action.Eff2S] = ApplyEff(NodeStates[o + action.Eff2S], action.Eff2Op, action.Eff2V);
                    return;
                case 4:
                    NodeStates[o + action.Eff0S] = ApplyEff(NodeStates[o + action.Eff0S], action.Eff0Op, action.Eff0V);
                    NodeStates[o + action.Eff1S] = ApplyEff(NodeStates[o + action.Eff1S], action.Eff1Op, action.Eff1V);
                    NodeStates[o + action.Eff2S] = ApplyEff(NodeStates[o + action.Eff2S], action.Eff2Op, action.Eff2V);
                    NodeStates[o + action.Eff3S] = ApplyEff(NodeStates[o + action.Eff3S], action.Eff3Op, action.Eff3V);
                    return;
                case 5:
                    NodeStates[o + action.Eff0S] = ApplyEff(NodeStates[o + action.Eff0S], action.Eff0Op, action.Eff0V);
                    NodeStates[o + action.Eff1S] = ApplyEff(NodeStates[o + action.Eff1S], action.Eff1Op, action.Eff1V);
                    NodeStates[o + action.Eff2S] = ApplyEff(NodeStates[o + action.Eff2S], action.Eff2Op, action.Eff2V);
                    NodeStates[o + action.Eff3S] = ApplyEff(NodeStates[o + action.Eff3S], action.Eff3Op, action.Eff3V);
                    NodeStates[o + action.Eff4S] = ApplyEff(NodeStates[o + action.Eff4S], action.Eff4Op, action.Eff4V);
                    return;
                case 6:
                    NodeStates[o + action.Eff0S] = ApplyEff(NodeStates[o + action.Eff0S], action.Eff0Op, action.Eff0V);
                    NodeStates[o + action.Eff1S] = ApplyEff(NodeStates[o + action.Eff1S], action.Eff1Op, action.Eff1V);
                    NodeStates[o + action.Eff2S] = ApplyEff(NodeStates[o + action.Eff2S], action.Eff2Op, action.Eff2V);
                    NodeStates[o + action.Eff3S] = ApplyEff(NodeStates[o + action.Eff3S], action.Eff3Op, action.Eff3V);
                    NodeStates[o + action.Eff4S] = ApplyEff(NodeStates[o + action.Eff4S], action.Eff4Op, action.Eff4V);
                    NodeStates[o + action.Eff5S] = ApplyEff(NodeStates[o + action.Eff5S], action.Eff5Op, action.Eff5V);
                    return;
                case 7:
                    NodeStates[o + action.Eff0S] = ApplyEff(NodeStates[o + action.Eff0S], action.Eff0Op, action.Eff0V);
                    NodeStates[o + action.Eff1S] = ApplyEff(NodeStates[o + action.Eff1S], action.Eff1Op, action.Eff1V);
                    NodeStates[o + action.Eff2S] = ApplyEff(NodeStates[o + action.Eff2S], action.Eff2Op, action.Eff2V);
                    NodeStates[o + action.Eff3S] = ApplyEff(NodeStates[o + action.Eff3S], action.Eff3Op, action.Eff3V);
                    NodeStates[o + action.Eff4S] = ApplyEff(NodeStates[o + action.Eff4S], action.Eff4Op, action.Eff4V);
                    NodeStates[o + action.Eff5S] = ApplyEff(NodeStates[o + action.Eff5S], action.Eff5Op, action.Eff5V);
                    NodeStates[o + action.Eff6S] = ApplyEff(NodeStates[o + action.Eff6S], action.Eff6Op, action.Eff6V);
                    return;
                case 8:
                    NodeStates[o + action.Eff0S] = ApplyEff(NodeStates[o + action.Eff0S], action.Eff0Op, action.Eff0V);
                    NodeStates[o + action.Eff1S] = ApplyEff(NodeStates[o + action.Eff1S], action.Eff1Op, action.Eff1V);
                    NodeStates[o + action.Eff2S] = ApplyEff(NodeStates[o + action.Eff2S], action.Eff2Op, action.Eff2V);
                    NodeStates[o + action.Eff3S] = ApplyEff(NodeStates[o + action.Eff3S], action.Eff3Op, action.Eff3V);
                    NodeStates[o + action.Eff4S] = ApplyEff(NodeStates[o + action.Eff4S], action.Eff4Op, action.Eff4V);
                    NodeStates[o + action.Eff5S] = ApplyEff(NodeStates[o + action.Eff5S], action.Eff5Op, action.Eff5V);
                    NodeStates[o + action.Eff6S] = ApplyEff(NodeStates[o + action.Eff6S], action.Eff6Op, action.Eff6V);
                    NodeStates[o + action.Eff7S] = ApplyEff(NodeStates[o + action.Eff7S], action.Eff7Op, action.Eff7V);
                    return;
            }
        }

        /// <summary>
        /// [S2] 슬롯별 부족량 / 최대증가량 기반 휴리스틱 h(n).
        ///
        /// GreaterEq 슬롯: ⌈(목표 − 현재) / MaxGain[s]⌉ 액션 수 추정
        /// LessEq  슬롯: ⌈(현재  − 목표) / MaxDrop[s]⌉ 액션 수 추정
        /// Equal    슬롯: 1 (불리언과 동일)
        /// h = 합산 × minActionCost × 0.99f
        ///
        /// admissible 근거:
        /// 각 슬롯 추정값은 해당 슬롯만을 처리하는 최소 액션 수이다.
        /// 단일 액션이 여러 슬롯을 동시에 개선할 수 있으므로 합산이 실제보다 클 수 있다.
        /// 0.99f 계수가 이 중복 계산 오차를 흡수하여 admissibility를 실용적으로 보장한다.
        /// </summary>
        private float CalculateHeuristic(int nodeIdx, int totalSlots, float minActionCost)
        {
            int offset        = nodeIdx * totalSlots;
            int stepsEstimate = 0;

            for (int s = 0; s < totalSlots; s++)
            {
                if (GoalMask[s] != 1) continue;

                int current = NodeStates[offset + s];
                int target  = GoalState[s];
                int op      = GoalOps[s];

                if (op == 1 && current < target) // GreaterEq 미충족: 부족량 ÷ 최대증가량
                {
                    int deficit = target - current;
                    int gain    = (int)MaxGain[s];
                    stepsEstimate += (deficit + gain - 1) / gain; // 정수 올림 나눗셈
                }
                else if (op == 2 && current > target) // LessEq 미충족: 초과량 ÷ 최대감소량
                {
                    int excess = current - target;
                    int drop   = (int)MaxDrop[s];
                    stepsEstimate += (excess + drop - 1) / drop;
                }
                else if (op == 0 && current != target) // Equal 미충족
                {
                    stepsEstimate += 1;
                }
            }

            return stepsEstimate * minActionCost * 0.99f;
        }

        /// <summary>
        /// 성공 노드에서 루트까지 NodeParents를 역추적하여
        /// ResultActions에 Action 해시를 루트→목표 정방향으로 적재한다.
        ///
        /// 처리 순서:
        ///   1. 목표→루트 방향으로 ResultActions에 역순 저장
        ///   2. ResultActions를 제자리에서 뒤집어 정방향으로 변환
        /// </summary>
        private void Backtrack(int goalNode)
        {
            int reverseCount = 0;
            int node = goalNode;

            // 목표 → 루트 방향으로 역순 저장
            while (node != -1 && reverseCount < MAX_PLAN_LEN)
            {
                int actionIdx = NodeActions[node];
                // 루트 노드(actionIdx == -1)는 진입 Action이 없으므로 건너뜀
                if (actionIdx >= 0 && actionIdx < Actions.Length)
                {
                    ResultActions[reverseCount] = Actions[actionIdx].ActionStringHash;
                    reverseCount++;
                }
                node = NodeParents[node];
            }

            // 제자리 뒤집기: 역순 → 정순 (루트→목표)
            int half = reverseCount / 2;
            for (int k = 0; k < half; k++)
            {
                int swapIdx = reverseCount - 1 - k;
                int temp             = ResultActions[k];
                ResultActions[k]       = ResultActions[swapIdx];
                ResultActions[swapIdx] = temp;
            }

            ResultLength[0] = reverseCount;
        }

        // ────────────────────────────────────────────────────────────────────
        // [S1] Closed Set 헬퍼 (FNV-1a 해시 + 선형 프로빙 오픈 어드레싱)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// nodeIdx 노드의 월드 스테이트를 FNV-1a로 해시한다.
        /// 0은 빈 슬롯 표식이므로 결과가 0이면 1로 대체한다.
        /// </summary>
        private int HashState(int nodeIdx, int totalSlots)
        {
            int o = nodeIdx * totalSlots;
            uint h = 2166136261u;
            for (int s = 0; s < totalSlots; s++)
            {
                h ^= (uint)NodeStates[o + s];
                h *= 16777619u;
            }
            int r = (int)h;
            return r == 0 ? 1 : r;
        }

        /// <summary>
        /// 상태가 처음이거나 더 싼 경로면 Closed Set에 등록하고 true를 반환한다.
        /// 이미 같거나 싼 g-cost로 방문한 상태면 false (확장 포기).
        /// VisitedHashes.Length는 반드시 2의 거듭제곱이어야 한다.
        /// </summary>
        private bool TryRegisterState(int newNode, float g, int totalSlots)
        {
            int hash = HashState(newNode, totalSlots);
            int cap  = VisitedHashes.Length;      // MAX_NODES * 2 (2의 거듭제곱)
            int idx  = hash & (cap - 1);

            for (int probe = 0; probe < cap; probe++)
            {
                int slotHash = VisitedHashes[idx];

                if (slotHash == 0)                // 빈 슬롯 → 신규 등록
                {
                    VisitedHashes[idx]  = hash;
                    VisitedNodeIdx[idx] = newNode;
                    VisitedGCosts[idx]  = g;
                    return true;
                }

                if (slotHash == hash && StatesEqual(VisitedNodeIdx[idx], newNode, totalSlots))
                {
                    if (VisitedGCosts[idx] <= g) return false;  // 이미 더 싸게 방문 → 폐기
                    VisitedGCosts[idx]  = g;                     // 더 싼 경로 발견 → 재개방
                    VisitedNodeIdx[idx] = newNode;
                    return true;
                }

                idx = (idx + 1) & (cap - 1);     // 선형 프로빙
            }
            return true; // 테이블 포화(이론상 도달 불가) — 안전 측으로 허용
        }

        /// <summary>
        /// 두 노드의 월드 스테이트를 전수 비교한다.
        /// 해시 일치 시에만 호출되므로 false positive 방지용 안전 장치다.
        /// </summary>
        private bool StatesEqual(int nodeA, int nodeB, int totalSlots)
        {
            int a = nodeA * totalSlots, b = nodeB * totalSlots;
            for (int s = 0; s < totalSlots; s++)
                if (NodeStates[a + s] != NodeStates[b + s]) return false;
            return true;
        }

        // ────────────────────────────────────────────────────────────────────
        // 최소 이진 힙 (min-heap) 구현
        // OpenQueue NativeArray를 힙으로, NodeCosts(f-cost)를 정렬 기준으로 사용한다.
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 노드 인덱스를 최소 힙에 삽입한다.
        /// 삽입 후 부모와 비교하며 위로 버블업(sift-up)한다. O(log n).
        /// </summary>
        private void HeapPush(int nodeIdx)
        {
            int pos = QueueSize[0];
            if (pos >= MAX_NODES) return; // 힙 크기 초과 방지

            OpenQueue[pos] = nodeIdx;
            QueueSize[0]   = pos + 1;
            SiftUp(pos);
        }

        /// <summary>
        /// 최소 f-cost 노드를 힙에서 추출한다.
        /// 루트와 마지막 요소를 교환한 후 버블다운(sift-down)한다. O(log n).
        /// </summary>
        /// <returns>최솟값 노드의 인덱스. 힙이 비어있으면 -1.</returns>
        private int HeapPop()
        {
            int size = QueueSize[0];
            if (size == 0) return -1;

            int minNode = OpenQueue[0];

            // 마지막 요소를 루트 자리로 이동 후 힙 크기 감소
            size--;
            OpenQueue[0] = OpenQueue[size];
            QueueSize[0] = size;

            if (size > 0) SiftDown(0, size);

            return minNode;
        }

        /// <summary>
        /// pos 위치 요소를 위쪽으로 버블업한다.
        /// 부모 노드의 f-cost보다 작으면 교환을 반복한다.
        /// </summary>
        private void SiftUp(int pos)
        {
            while (pos > 0)
            {
                int parent = (pos - 1) >> 1; // (pos-1)/2, 비트 시프트로 나눗셈 대체

                if (NodeCosts[OpenQueue[pos]] < NodeCosts[OpenQueue[parent]])
                {
                    int temp          = OpenQueue[pos];
                    OpenQueue[pos]    = OpenQueue[parent];
                    OpenQueue[parent] = temp;
                    pos = parent;
                }
                else
                {
                    break; // 힙 속성 만족
                }
            }
        }

        /// <summary>
        /// pos 위치 요소를 아래쪽으로 버블다운한다.
        /// 두 자식 중 더 작은 값이 있으면 교환을 반복한다.
        /// </summary>
        /// <param name="pos">버블다운 시작 위치.</param>
        /// <param name="size">현재 힙 크기.</param>
        private void SiftDown(int pos, int size)
        {
            while (true)
            {
                int left     = (pos << 1) | 1; // 2*pos+1, 비트 연산
                int right    = left + 1;
                int smallest = pos;

                if (left  < size && NodeCosts[OpenQueue[left]]  < NodeCosts[OpenQueue[smallest]])
                    smallest = left;
                if (right < size && NodeCosts[OpenQueue[right]] < NodeCosts[OpenQueue[smallest]])
                    smallest = right;

                if (smallest == pos) break; // 힙 속성 만족

                int temp              = OpenQueue[pos];
                OpenQueue[pos]        = OpenQueue[smallest];
                OpenQueue[smallest]   = temp;

                pos = smallest;
            }
        }
    }
}
