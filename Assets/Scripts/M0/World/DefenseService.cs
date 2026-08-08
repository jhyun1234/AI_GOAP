using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 방어 계획의 집 (M22-W3R2, Docs/M22_방어건설_실행명세서.md) — W5부터 시설 내구도까지 얹는다
    /// (ADR-M22-3: 내구도 상태의 단일 소유자).
    /// 계획 모델 (ADR-M22-4 재개정, 사용자 UX 확정 2026-08-08): "사각형 1회"가 아니라 **줄 누적**이다 —
    /// 플레이어가 드래그한 직선(울타리 줄)과 우클릭(문 칸)이 계획에 쌓이고, 완공마다 차감된다.
    /// 시공은 W4의 건설 goal 몫 — 이 서비스는 자리만 안다 (지정 ≠ 즉시 건설, 합의 2).
    /// 세이브 대상 (ADR-M0-10: 계획 잔여 울타리·문 + 내구도).
    /// </summary>
    public sealed class DefenseService
    {
        private readonly List<Vector2Int> _plannedFences = new List<Vector2Int>();
        private readonly List<Vector2Int> _plannedGates = new List<Vector2Int>();
        // 시설 내구도 (M22-W5, ADR-M22-3) — 타일 키 상태 (HomeStorageService·FarmService 선례).
        // 파괴(0 도달)된 항목은 NotifyRemoved가 지운다 — "파괴 = 손상"이 아니다 (소멸 + 계획 복귀).
        private readonly Dictionary<(SlotId slot, Vector2Int tile), (float cur, float max, int repairCost)> _durability
            = new Dictionary<(SlotId, Vector2Int), (float, float, int)>();

        /// <summary>내구도 변화 알림 (slot, tile, 현재, 최대) — 시각(W7)·HUD 구독. 표현 전용.
        /// ⚠️ 소멸(파괴·제거) 시에는 (0, 0)으로 한 번 더 나간다 — 손상 오버레이가 유령으로 남지 않게.</summary>
        public event Action<SlotId, Vector2Int, float, float> OnDurabilityChanged;

        /// <summary>수리 완료 알림 (slot, tile, 수리자 표시명 — null 가능) — HUD 정보줄 구독 (W7).
        /// 등록(완공)의 OnDurabilityChanged와 분리 — 완공마다 "수리되었습니다"가 뜨면 거짓말이다.</summary>
        public event Action<SlotId, Vector2Int, string> OnRepaired;

        /// <summary>계획 변경 알림 (추가·차감·복귀) — 계획 마커 뷰 구독 (표현 전용).</summary>
        public event Action OnPlanChanged;

        /// <summary>미건설 울타리 자리 (읽기 전용 — W4 BuildRunner가 시공자 최근접을 고른다).</summary>
        public IReadOnlyList<Vector2Int> PlannedFenceTiles => _plannedFences;

        /// <summary>미건설 문 자리들 (읽기 전용). 문은 여러 개일 수 있다 — 출입구도 플레이어가 정한다.</summary>
        public IReadOnlyList<Vector2Int> PlannedGateTiles => _plannedGates;

        /// <summary>미건설 잔여 총수 — DefensePlannedCount 슬롯의 유일한 원천 (Goal_BuildDefense 트리거).</summary>
        public int PlannedCount => _plannedFences.Count + _plannedGates.Count;

        /// <summary>미건설 문 수 — GatePlannedCount 슬롯의 유일한 원천 (BuildGate 액션 전제:
        /// 문 계획이 있어야만 문 액션이 후보가 된다. GateCount==0 전제는 문이 여러 개가 되며 폐기 —
        /// 그대로 두면 두 번째 문이 영영 안 서고 계획이 바닥나지 않아 goal이 공회전한다).</summary>
        public int GatePlannedCount => _plannedGates.Count;

        /// <summary>서 있는 방어 시설이 하나라도 있는가 — 공성 전환(ADR-M22-2)의 전제 판독점.</summary>
        public bool HasStructures => _durability.Count > 0;

        /// <summary>이 자리에 방어 시설이 서 있는가 (공성 중 다른 개체가 이미 부쉈는지 판독).</summary>
        public bool HasStructureAt(SlotId slot, Vector2Int tile) => _durability.ContainsKey((slot, tile));

        /// <summary>손상(0 < 내구도 < 최대) 시설 수 — DefenseDamagedCount 슬롯의 유일한 원천
        /// (Goal_RepairDefense 트리거, W6). 파괴된 시설은 여기 없다 — 재건은 건설 goal 몫.</summary>
        public int DamagedCount
        {
            get
            {
                int n = 0;
                foreach (KeyValuePair<(SlotId, Vector2Int), (float cur, float max, int repairCost)> e in _durability)
                    if (e.Value.cur < e.Value.max) n++;
                return n;
            }
        }

        /// <summary>내구도 조회 (표현·게이트용). 항목 없으면 false — 방어 시설이 아니다.</summary>
        public bool TryGetDurability(SlotId slot, Vector2Int tile, out float cur, out float max)
        {
            if (_durability.TryGetValue((slot, tile), out (float c, float m, int rc) v))
            {
                cur = v.c; max = v.m;
                return true;
            }
            cur = max = 0f;
            return false;
        }

        // ── 문 잠금 (M22-2차 W1, ADR-M22-9) ─────────────────────────────────

        /// <summary>모든 문이 잠겨 있는가 (전역 1비트 — 개별 문 잠금은 관측 후 등록부 확장).
        /// 잠금은 배열 상태이지 건물 속성이 아니다 — Gate.asset은 무변 (ADR-M22-9).
        /// 세이브 대상 (ADR-M0-10).</summary>
        public bool GatesLocked { get; private set; }

        /// <summary>잠금 상태 변화 알림 — 자물쇠 마커 뷰 구독 (표현 전용).</summary>
        public event Action<bool> OnGateLockChanged;

        /// <summary>완공(서 있는) 문 타일들 — 내구도 등록부에서 파생 (전용 등록부 신설 금지:
        /// NotifyBuilt/NotifyRemoved가 이미 문의 생사를 안다. 전제 확인 ③).</summary>
        public IEnumerable<Vector2Int> BuiltGateTiles
        {
            get
            {
                foreach (KeyValuePair<(SlotId slot, Vector2Int tile), (float, float, int)> e in _durability)
                    if (e.Key.slot == SlotId.GateCount) yield return e.Key.tile;
            }
        }

        /// <summary>서 있는 문이 하나라도 있는가 — L 키의 "잠글 문이 없습니다" no-op 판정.
        /// ⚠️ 잠금 해제는 문 0개여도 허용해야 한다 (잠금 중 전부 파괴되면 열 수 없는 상태가 된다).</summary>
        public bool HasBuiltGates
        {
            get
            {
                foreach (KeyValuePair<(SlotId slot, Vector2Int tile), (float, float, int)> e in _durability)
                    if (e.Key.slot == SlotId.GateCount) return true;
                return false;
            }
        }

        /// <summary>완공 울타리 → 문 전환 가능 판정 (M22-2차 W2, ADR-M22-10 — 철거 **전** 사전
        /// 검사, 부분 성공 금지). 전환 대상 = 서 있는 울타리뿐 — 계획 칸은 기존 우클릭 전환
        /// 문법(TryAddGatePlan)이, 문·빈 칸은 대상이 아니다. 문 계획 중복은 불변식상 없지만
        /// 방어적으로 함께 검사한다 (철거 후 TryAddGatePlan이 실패하면 원자성이 깨진다).</summary>
        public bool CanConvertToGateAt(Vector2Int tile)
            => _durability.ContainsKey((SlotId.FenceCount, tile)) && !_plannedGates.Contains(tile);

        /// <summary>잠금 토글 — 쓰기 문 하나 (ADR-M0-3). 통행 배열 갱신·선별 재계획은
        /// 배선(M0SimulationLoop.ToggleGateLock)이 이어서 한다. 새 상태를 돌려준다.</summary>
        public bool ToggleGateLock()
        {
            GatesLocked = !GatesLocked;
            Debug.Log($"[Defense] 문 잠금 {(GatesLocked ? "설정" : "해제")}");
            OnGateLockChanged?.Invoke(GatesLocked);
            return GatesLocked;
        }

        /// <summary>수리 비용 조회 (M22-W6) — 완공 시 에셋(BuildingSO.RepairCost)에서 받아 둔 값.
        /// 러너의 Wood 선검사 원천 (비용의 단일 출처는 에셋, ADR-M0-2).</summary>
        public bool TryGetRepairCost(SlotId slot, Vector2Int tile, out int cost)
        {
            if (_durability.TryGetValue((slot, tile), out (float c, float m, int rc) v))
            {
                cost = v.rc;
                return true;
            }
            cost = 0;
            return false;
        }

        /// <summary>from 최근접(맨해튼) 시설 — 공성 타깃 선정 (ADR-M22-2). 동률은 좌표순 (결정적,
        /// ADR-M10-1 — 같은 판이면 같은 울타리를 두드린다).</summary>
        public bool TryGetNearestStructure(Vector2Int from, out SlotId slot, out Vector2Int tile)
        {
            slot = default;
            tile = default;
            int best = int.MaxValue;
            bool found = false;
            foreach (KeyValuePair<(SlotId slot, Vector2Int tile), (float, float, int)> e in _durability)
            {
                Vector2Int t = e.Key.tile;
                int d = Mathf.Abs(t.x - from.x) + Mathf.Abs(t.y - from.y);
                if (d > best) continue;
                if (d == best && found && (t.x > tile.x || (t.x == tile.x && t.y >= tile.y))) continue;
                best = d;
                slot = e.Key.slot;
                tile = t;
                found = true;
            }
            return found;
        }

        /// <summary>내구도 차감의 유일한 문 (ADR-M22-3 쓰기 문 1 — 호출은 ThreatService 공성).
        /// 남은 내구도를 돌려준다. 0 이하 = 파괴 신호 — **제거는 여기서 하지 않는다**:
        /// 호출자가 ConstructionService.RemoveCountableAt을 지나야 한다 (ADR-M0-3, 재해 밭 파괴 동형).
        /// 추적 항목이 아니면 float.MaxValue (아무 일 없음 — 방어 시설이 아니다).</summary>
        public float ApplyDamage(SlotId slot, Vector2Int tile, float amount)
        {
            var key = (slot, tile);
            if (amount <= 0f || !_durability.TryGetValue(key, out (float cur, float max, int repairCost) v))
                return float.MaxValue;
            v.cur = Mathf.Max(0f, v.cur - amount);
            _durability[key] = v;
            OnDurabilityChanged?.Invoke(slot, tile, v.cur, v.max);
            return v.cur;
        }

        /// <summary>수리의 유일한 문 (ADR-M22-3 쓰기 문 2 — 호출은 RepairRunner, W6). 전량 복원
        /// (한 걸음, ADR-M0-12). Wood 차감은 러너 몫 — 스톡의 문은 WorldModel이다 (ADR-M0-3).</summary>
        public bool Repair(SlotId slot, Vector2Int tile, string actorName = null)
        {
            var key = (slot, tile);
            if (!_durability.TryGetValue(key, out (float cur, float max, int repairCost) v) || v.cur >= v.max)
                return false;
            v.cur = v.max;
            _durability[key] = v;
            OnDurabilityChanged?.Invoke(slot, tile, v.cur, v.max);
            OnRepaired?.Invoke(slot, tile, actorName);
            return true;
        }

        /// <summary>가장 많이 손상된 시설 (W6 수리 대상 선정) — 동률은 좌표순 (결정적).</summary>
        public bool TryGetMostDamaged(out SlotId slot, out Vector2Int tile)
        {
            slot = default;
            tile = default;
            float worst = float.MaxValue; // 남은 내구도가 가장 적은 것
            bool found = false;
            foreach (KeyValuePair<(SlotId slot, Vector2Int tile), (float cur, float max, int repairCost)> e in _durability)
            {
                if (e.Value.cur >= e.Value.max) continue;
                float remain = e.Value.cur / e.Value.max;
                if (remain > worst) continue;
                Vector2Int t = e.Key.tile;
                if (Mathf.Approximately(remain, worst) && found
                    && (t.x > tile.x || (t.x == tile.x && t.y >= tile.y))) continue;
                worst = remain;
                slot = e.Key.slot;
                tile = t;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// 제거 통지 (M22-W5, ADR-M22-6) — ConstructionService.OnRemoved 구독 배선이 호출한다.
        /// 내구도 항목을 지우고, 그 자리를 계획으로 되돌린다 — 부서진 자리는 다시 "지을 자리"다
        /// (재건은 W4 건설 goal이 같은 문법으로 잇는다).
        /// </summary>
        public void NotifyRemoved(SlotId slot, int x, int y)
        {
            var tile = new Vector2Int(x, y);
            if (!_durability.Remove((slot, tile))) return; // 방어 시설이 아니면 무관 (밭 소실 등)
            OnDurabilityChanged?.Invoke(slot, tile, 0f, 0f); // 소멸 신호 — 손상 오버레이 정리 (W7)
            if (slot == SlotId.GateCount)
            {
                if (!_plannedGates.Contains(tile)) _plannedGates.Add(tile);
            }
            else if (slot == SlotId.FenceCount && !_plannedFences.Contains(tile))
            {
                _plannedFences.Add(tile);
            }
            OnPlanChanged?.Invoke();
        }

        // ── 계획 입력 (M22-W3R2 — 줄 누적, ADR-M22-4 재개정) ─────────────────

        /// <summary>드래그 끝점을 우세축 직선으로 스냅 (순수 — 대각 줄은 대각 이동에 새서 벽이 아니다).
        /// |Δx| ≥ |Δy|면 수평(끝점 y = 시작 y), 아니면 수직.</summary>
        public static Vector2Int SnapLineEnd(Vector2Int start, Vector2Int end)
            => Mathf.Abs(end.x - start.x) >= Mathf.Abs(end.y - start.y)
                ? new Vector2Int(end.x, start.y)
                : new Vector2Int(start.x, end.y);

        /// <summary>축 정렬 직선의 타일 목록 (순수, 결정적 — 시작→끝 순서). 끝점은 스냅돼 있어야 한다.</summary>
        public static List<Vector2Int> LineTiles(Vector2Int start, Vector2Int snappedEnd)
        {
            var tiles = new List<Vector2Int>();
            int dx = Math.Sign(snappedEnd.x - start.x), dy = Math.Sign(snappedEnd.y - start.y);
            var cur = start;
            tiles.Add(cur);
            while (cur != snappedEnd)
            {
                cur = new Vector2Int(cur.x + dx, cur.y + dy);
                tiles.Add(cur);
            }
            return tiles;
        }

        /// <summary>울타리 줄 계획 추가 — buildable 필터(맵 밖·기존 건물·노드·통행 불가)는 배선이
        /// 주입한다. 이미 계획됐거나(울타리·문) 시설이 선 칸은 건너뛴다 (줄이 기존 줄을 겹쳐 그어도
        /// 이중 계획이 안 된다 — "줄끼리 연결"의 실체). 추가된 칸 수를 돌려준다.</summary>
        public int AddFencePlan(IReadOnlyList<Vector2Int> tiles, Func<int, int, bool> buildable)
        {
            int added = 0;
            foreach (Vector2Int t in tiles)
            {
                if (buildable != null && !buildable(t.x, t.y)) continue;
                if (_plannedFences.Contains(t) || _plannedGates.Contains(t)) continue;
                if (_durability.ContainsKey((SlotId.FenceCount, t))
                    || _durability.ContainsKey((SlotId.GateCount, t))) continue;
                _plannedFences.Add(t);
                added++;
            }
            if (added > 0)
            {
                Debug.Log($"[Defense] 울타리 줄 계획 +{added}칸 (잔여 {PlannedCount})");
                OnPlanChanged?.Invoke();
            }
            return added;
        }

        /// <summary>문 계획 추가 (우클릭 1칸 — 출입구도 플레이어가 정한다, 합의 2). 같은 칸의 울타리
        /// 계획은 문으로 전환된다. 이미 시설이 선 칸·이미 문 계획인 칸은 거부 (완공 울타리의 문 전환은
        /// 철거 축이 없어 2차+).</summary>
        public bool TryAddGatePlan(Vector2Int tile, Func<int, int, bool> buildable)
        {
            if (_plannedGates.Contains(tile)) return false;
            if (_durability.ContainsKey((SlotId.FenceCount, tile))
                || _durability.ContainsKey((SlotId.GateCount, tile))) return false;
            bool convertedFromFence = _plannedFences.Remove(tile); // 줄 위 우클릭 = 울타리 → 문 전환
            if (!convertedFromFence && buildable != null && !buildable(tile.x, tile.y)) return false;
            _plannedGates.Add(tile);
            Debug.Log($"[Defense] 문 계획 @ ({tile.x},{tile.y}){(convertedFromFence ? " (울타리 전환)" : "")}");
            OnPlanChanged?.Invoke();
            return true;
        }

        /// <summary>계획 취소 (M22-W8 철거 — 플레이어 전용). 미건설 계획 칸을 지운다 — 자재를
        /// 안 썼으니 무료. 있었으면 true.</summary>
        public bool RemovePlanAt(Vector2Int tile)
        {
            bool removed = _plannedFences.Remove(tile) | _plannedGates.Remove(tile);
            if (removed) OnPlanChanged?.Invoke();
            return removed;
        }

        /// <summary>
        /// 철거 준비 (M22-W8 — 플레이어 전용): 내구도 항목만 지운다. **계획 복귀 없음** — 이것이
        /// 위협의 파괴와 철거의 갈림길이다: 파괴는 NotifyRemoved가 그 자리를 계획으로 되돌려
        /// 재건되지만, 철거는 여기서 항목을 먼저 지워 뒤따르는 NotifyRemoved(제거 문
        /// RemoveCountableAt → OnRemoved 경유)가 조기 반환하게 만든다 — 주민이 도로 짓지 않는다.
        /// ⚠️ 호출자는 반드시 직후 RemoveCountableAt을 호출할 것 (제거의 유일한 문, ADR-M0-3).
        /// </summary>
        public bool Demolish(SlotId slot, Vector2Int tile)
        {
            if (!_durability.Remove((slot, tile))) return false;
            OnDurabilityChanged?.Invoke(slot, tile, 0f, 0f); // 소멸 신호 — 손상 오버레이 정리
            return true;
        }

        /// <summary>from 곁(체비쇼프 ≤ range)의 계획·시설 타일 — 드래그 시작점 달라붙기(줄 연결)용.
        /// 최근접 우선, 동률은 좌표순 (결정적).</summary>
        public bool TryGetNearestPlanOrStructureTile(Vector2Int from, int range, out Vector2Int tile)
        {
            Vector2Int bestTile = default;
            int best = int.MaxValue;
            bool found = false;
            void Consider(Vector2Int t)
            {
                int d = Mathf.Max(Mathf.Abs(t.x - from.x), Mathf.Abs(t.y - from.y));
                if (d > range || d > best) return;
                if (d == best && found && (t.x > bestTile.x || (t.x == bestTile.x && t.y >= bestTile.y))) return;
                best = d;
                bestTile = t;
                found = true;
            }
            foreach (Vector2Int t in _plannedFences) Consider(t);
            foreach (Vector2Int t in _plannedGates) Consider(t);
            foreach (KeyValuePair<(SlotId slot, Vector2Int tile), (float, float, int)> e in _durability)
                Consider(e.Key.tile);
            tile = bestTile;
            return found;
        }

        /// <summary>
        /// 다음 시공 자리 (M22-W4) — 문 에셋(GateCount)은 문 자리, 울타리는 from 최근접(맨해튼)
        /// 계획 타일. occupied 필터(노드·기존 건물·타 주민 예약)는 BuildRunner가 주입 — 계획
        /// 수립 후 상태가 변했을 수 있어 시공 시점에 다시 거른다. 문/울타리 구분이 여기 있는
        /// 이유: 계획 의미의 단일 소유자가 이 서비스다 (러너에 슬롯 분기를 흩뿌리지 않는다).
        /// </summary>
        public bool TryGetNextBuildTile(BuildingSO b, Vector2Int from,
                                        Func<int, int, bool> occupied, out Vector2Int tile)
        {
            tile = default;
            if (b == null || !b.PlaceOnDefensePlan) return false;
            List<Vector2Int> pool = b.CountSlot == SlotId.GateCount ? _plannedGates : _plannedFences;
            int bestDist = int.MaxValue;
            bool found = false;
            foreach (Vector2Int t in pool)
            {
                if (occupied != null && occupied(t.x, t.y)) continue;
                int d = Mathf.Abs(t.x - from.x) + Mathf.Abs(t.y - from.y);
                if (d < bestDist) { bestDist = d; tile = t; found = true; }
            }
            return found;
        }

        /// <summary>
        /// 완공 통지 — 방어 계획 건물(PlaceOnDefensePlan)만 해당 타일을 계획에서 차감한다.
        /// ConstructionService.OnCompleted 구독 배선이 호출 (완공 자체는 Complete()만이 한다, ADR-M0-3).
        /// </summary>
        public void NotifyBuilt(BuildingSO b, int x, int y)
        {
            if (b == null) return;
            var tile = new Vector2Int(x, y);
            // 내구도 등록 (M22-W5) — 최대치의 단일 출처는 에셋 (BuildingSO.MaxDurability)
            if (b.MaxDurability > 0f && b.IsCountable)
            {
                _durability[(b.CountSlot, tile)] = (b.MaxDurability, b.MaxDurability, Mathf.Max(0, b.RepairCost));
                OnDurabilityChanged?.Invoke(b.CountSlot, tile, b.MaxDurability, b.MaxDurability);
            }
            if (!b.PlaceOnDefensePlan) return;
            if (b.CountSlot == SlotId.GateCount) _plannedGates.Remove(tile);
            else _plannedFences.Remove(tile);
            OnPlanChanged?.Invoke();
        }
    }
}
