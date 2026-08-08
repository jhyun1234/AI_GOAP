using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 방어 계획의 집 (M22-W3, Docs/M22_방어건설_실행명세서.md) — W5에서 시설 내구도까지 얹는다
    /// (ADR-M22-3: 내구도 상태의 단일 소유자).
    /// W3 범위: 플레이어가 확정한 방어 구역(ZoneService.OnZoneEstablished 구독)의 둘레를
    /// 결정적으로 계산해 "지을 자리"(울타리 N + 문 1)를 만들고, 완공마다 계획에서 차감한다.
    /// 시공은 W4의 건설 goal 몫 — 이 서비스는 자리만 안다 (구역 지정 ≠ 즉시 건설, 합의 2).
    /// 세이브 대상 (ADR-M0-10: 계획 잔여·문 자리 — 내구도는 W5에서 함께 선언).
    /// </summary>
    public sealed class DefenseService
    {
        private readonly List<Vector2Int> _plannedFences = new List<Vector2Int>();
        private Vector2Int? _plannedGate;
        // 시설 내구도 (M22-W5, ADR-M22-3) — 타일 키 상태 (HomeStorageService·FarmService 선례).
        // 파괴(0 도달)된 항목은 NotifyRemoved가 지운다 — "파괴 = 손상"이 아니다 (소멸 + 계획 복귀).
        private readonly Dictionary<(SlotId slot, Vector2Int tile), (float cur, float max)> _durability
            = new Dictionary<(SlotId, Vector2Int), (float, float)>();

        /// <summary>내구도 변화 알림 (slot, tile, 현재, 최대) — 시각(W7)·HUD 구독. 표현 전용.</summary>
        public event Action<SlotId, Vector2Int, float, float> OnDurabilityChanged;

        /// <summary>계획이 수립됐는가 (구역 확정 후 true — 전량 완공돼도 유지. 재수립 없음, ADR-M22-4).</summary>
        public bool HasPlan { get; private set; }

        /// <summary>미건설 울타리 자리 (읽기 전용 — W4 BuildRunner가 시공자 최근접을 고른다).</summary>
        public IReadOnlyList<Vector2Int> PlannedFenceTiles => _plannedFences;

        /// <summary>미건설 문 자리. null = 없음(미계획 또는 완공됨).</summary>
        public Vector2Int? PlannedGateTile => _plannedGate;

        /// <summary>미건설 잔여 총수 — DefensePlannedCount 슬롯의 유일한 원천 (Goal_BuildDefense 트리거).</summary>
        public int PlannedCount => _plannedFences.Count + (_plannedGate.HasValue ? 1 : 0);

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
                foreach (KeyValuePair<(SlotId, Vector2Int), (float cur, float max)> e in _durability)
                    if (e.Value.cur < e.Value.max) n++;
                return n;
            }
        }

        /// <summary>내구도 조회 (표현·게이트용). 항목 없으면 false — 방어 시설이 아니다.</summary>
        public bool TryGetDurability(SlotId slot, Vector2Int tile, out float cur, out float max)
        {
            if (_durability.TryGetValue((slot, tile), out (float c, float m) v))
            {
                cur = v.c; max = v.m;
                return true;
            }
            cur = max = 0f;
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
            foreach (KeyValuePair<(SlotId slot, Vector2Int tile), (float, float)> e in _durability)
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
            if (amount <= 0f || !_durability.TryGetValue(key, out (float cur, float max) v))
                return float.MaxValue;
            v.cur = Mathf.Max(0f, v.cur - amount);
            _durability[key] = v;
            OnDurabilityChanged?.Invoke(slot, tile, v.cur, v.max);
            return v.cur;
        }

        /// <summary>수리의 유일한 문 (ADR-M22-3 쓰기 문 2 — 호출은 RepairRunner, W6). 전량 복원
        /// (한 걸음, ADR-M0-12). Wood 차감은 러너 몫 — 스톡의 문은 WorldModel이다 (ADR-M0-3).</summary>
        public bool Repair(SlotId slot, Vector2Int tile)
        {
            var key = (slot, tile);
            if (!_durability.TryGetValue(key, out (float cur, float max) v) || v.cur >= v.max)
                return false;
            v.cur = v.max;
            _durability[key] = v;
            OnDurabilityChanged?.Invoke(slot, tile, v.cur, v.max);
            return true;
        }

        /// <summary>가장 많이 손상된 시설 (W6 수리 대상 선정) — 동률은 좌표순 (결정적).</summary>
        public bool TryGetMostDamaged(out SlotId slot, out Vector2Int tile)
        {
            slot = default;
            tile = default;
            float worst = float.MaxValue; // 남은 내구도가 가장 적은 것
            bool found = false;
            foreach (KeyValuePair<(SlotId slot, Vector2Int tile), (float cur, float max)> e in _durability)
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
            if (!HasPlan) return;
            if (slot == SlotId.GateCount)
            {
                if (!_plannedGate.HasValue) _plannedGate = tile;
            }
            else if (slot == SlotId.FenceCount && !_plannedFences.Contains(tile))
            {
                _plannedFences.Add(tile);
            }
        }

        /// <summary>확정된 사각 영역 (표현·세이브용). HasPlan일 때만 유효.</summary>
        public Vector2Int PlanMin { get; private set; }
        public Vector2Int PlanMax { get; private set; }

        /// <summary>계획 확정 알림 (min, max) — 테두리 뷰 구독 (표현 전용).</summary>
        public event Action<Vector2Int, Vector2Int> OnPlanEstablished;

        /// <summary>
        /// 사각형(min~max)의 경계 타일 — 결정적 순서 (아랫변 좌→우, 오른변 아래→위,
        /// 윗변 우→좌, 왼변 위→아래). 같은 입력이면 언제나 같은 목록 (순수, 게이트 대상).
        /// 가로 W × 세로 H 사각의 타일 수 = 2(W+H) − 4 (W,H ≥ 2).
        /// </summary>
        public static List<Vector2Int> PerimeterTilesRect(Vector2Int min, Vector2Int max)
        {
            int minX = Mathf.Min(min.x, max.x), maxX = Mathf.Max(min.x, max.x);
            int minY = Mathf.Min(min.y, max.y), maxY = Mathf.Max(min.y, max.y);
            var tiles = new List<Vector2Int>(2 * (maxX - minX + maxY - minY) + 4);
            if (minX == maxX && minY == maxY) { tiles.Add(new Vector2Int(minX, minY)); return tiles; }
            for (int x = minX; x <= maxX; x++) tiles.Add(new Vector2Int(x, minY));          // 아랫변
            for (int y = minY + 1; y <= maxY; y++) tiles.Add(new Vector2Int(maxX, y));      // 오른변
            if (maxY > minY)
                for (int x = maxX - 1; x >= minX; x--) tiles.Add(new Vector2Int(x, maxY)); // 윗변
            if (maxX > minX)
                for (int y = maxY - 1; y >= minY + 1; y--) tiles.Add(new Vector2Int(minX, y)); // 왼변
            return tiles;
        }

        /// <summary>정사각 편의 오버로드 (기존 게이트·호출부 호환) — anchor ± radius.</summary>
        public static List<Vector2Int> PerimeterTiles(Vector2Int anchor, int radius)
        {
            if (radius <= 0) return new List<Vector2Int> { anchor };
            return PerimeterTilesRect(new Vector2Int(anchor.x - radius, anchor.y - radius),
                                      new Vector2Int(anchor.x + radius, anchor.y + radius));
        }

        /// <summary>설치에 필요한 나무 (순수 — 지정 프리뷰의 초록/빨강 판정 원천, ADR-M22-4 개정).
        /// 둘레 = 울타리 (N−1)칸 + 문 1칸. 비용 인자는 에셋에서 파생해 넘긴다 (이중 기입 금지).</summary>
        public static int RequiredWood(int perimeterTileCount, int fenceWoodCost, int gateWoodCost)
        {
            if (perimeterTileCount <= 0) return 0;
            return Mathf.Max(0, perimeterTileCount - 1) * Mathf.Max(0, fenceWoodCost)
                 + Mathf.Max(0, gateWoodCost);
        }

        /// <summary>문 자리 = 기지 최근접(맨해튼) 둘레 타일 (순수, 결정적 — 동률이면 목록 앞 우선).
        /// 주민들이 아침마다 기지 쪽 문으로 줄지어 나가는 동선의 근거 (§7 재미 검증).</summary>
        public static Vector2Int PickGateTile(IReadOnlyList<Vector2Int> perimeter, Vector2Int baseTile)
        {
            Vector2Int best = perimeter[0];
            int bestDist = int.MaxValue;
            foreach (Vector2Int t in perimeter)
            {
                int d = Mathf.Abs(t.x - baseTile.x) + Mathf.Abs(t.y - baseTile.y);
                if (d < bestDist) { bestDist = d; best = t; }
            }
            return best;
        }

        /// <summary>
        /// 구역 확정 → 계획 수립 (1회 — 이미 있으면 무시, ADR-M22-4). tileBuildable 필터(맵 밖·
        /// 기존 건물·노드·통행 불가 제외)는 조립 배선이 주입한다 — 서비스는 맵을 모른다.
        /// 문은 필터를 통과한 둘레에서 고른다 (막힌 자리에 문을 계획하면 영원히 못 짓는다).
        /// </summary>
        public void EstablishPlanRect(Vector2Int min, Vector2Int max, Vector2Int baseTile,
                                      Func<int, int, bool> tileBuildable)
        {
            if (HasPlan) return;
            List<Vector2Int> perimeter = PerimeterTilesRect(min, max);
            var buildable = new List<Vector2Int>(perimeter.Count);
            foreach (Vector2Int t in perimeter)
                if (tileBuildable == null || tileBuildable(t.x, t.y))
                    buildable.Add(t);
            if (buildable.Count == 0)
            {
                Debug.LogWarning($"[Defense] 방어 구역 ({min.x},{min.y})~({max.x},{max.y}) — " +
                                 "지을 수 있는 둘레 타일이 0개라 계획 없음.");
                return;
            }

            Vector2Int gate = PickGateTile(buildable, baseTile);
            _plannedGate = gate;
            _plannedFences.Clear();
            foreach (Vector2Int t in buildable)
                if (t != gate) _plannedFences.Add(t);
            PlanMin = Vector2Int.Min(min, max);
            PlanMax = Vector2Int.Max(min, max);
            HasPlan = true;
            Debug.Log($"[Defense] 방어 계획 수립 — 울타리 {_plannedFences.Count} + 문 1 " +
                      $"(제외 {perimeter.Count - buildable.Count}) ({PlanMin.x},{PlanMin.y})~({PlanMax.x},{PlanMax.y})");
            OnPlanEstablished?.Invoke(PlanMin, PlanMax);
        }

        /// <summary>정사각 편의 오버로드 (기존 게이트 호환) — anchor ± radius.</summary>
        public void EstablishPlan(Vector2Int anchor, int radius, Vector2Int baseTile,
                                  Func<int, int, bool> tileBuildable)
            => EstablishPlanRect(new Vector2Int(anchor.x - radius, anchor.y - radius),
                                 new Vector2Int(anchor.x + radius, anchor.y + radius),
                                 baseTile, tileBuildable);

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
            if (b.CountSlot == SlotId.GateCount)
            {
                if (!_plannedGate.HasValue) return false;
                Vector2Int g = _plannedGate.Value;
                if (occupied != null && occupied(g.x, g.y)) return false;
                tile = g;
                return true;
            }
            int bestDist = int.MaxValue;
            bool found = false;
            foreach (Vector2Int t in _plannedFences)
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
                _durability[(b.CountSlot, tile)] = (b.MaxDurability, b.MaxDurability);
                OnDurabilityChanged?.Invoke(b.CountSlot, tile, b.MaxDurability, b.MaxDurability);
            }
            if (!b.PlaceOnDefensePlan) return;
            if (_plannedGate.HasValue && _plannedGate.Value == tile) _plannedGate = null;
            else _plannedFences.Remove(tile);
        }
    }
}
