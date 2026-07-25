using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 건설 러너: 제자리 실행 (舊 BC4 — 건설 지점으로의 장거리 이동 스킵 계승).
    /// 단, 현재 타일에 자원 노드/기존 건물이 있으면 인접 빈 타일로 한 발짝 옮겨 짓는다
    /// (M2-C 리뷰 ① 발견 — 밭이 노드를 덮는 문제. BC4의 본질은 유지).
    /// 세계 반영은 ConstructionService.Complete()가 전담하므로 AppliesOwnEffects = true —
    /// 에이전트의 EffectApplier 일괄 적용을 건너뛴다 (이중 차감 방지, ADR-M0-3).
    /// </summary>
    public sealed class BuildRunner : ActionRunnerBase
    {
        private const int SEARCH_RADIUS = 3; // 알고리즘 상수 — 인접 빈 타일 링 탐색 한계

        private readonly BuildActionSO _so;
        private Vector2Int _buildTile; // 완공 위치 — 차단 건물은 서는 타일(MoveTarget)과 분리 (ADR-M3-3)

        public override bool AppliesOwnEffects => true;

        public BuildRunner(BuildActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            if (_so.Building == null)
            {
                FailReason = $"{_so.name}: BuildingSO 미연결";
                return false;
            }

            // 주민이 서 있거나 이동 중인 타일(타인 예약)도 제외 — 통행 차단 건물이 주민 위에
            // 완공되면 JPS 출발 불가로 영구 고립 (2026-07-16 방치 관측: 경로없음 반복의 근본 원인 후보)
            bool Occupied(int x, int y)
                => agent.Discovery.HasNodeAt(x, y) || agent.Construction.HasBuildingAt(x, y)
                || AIVillage.AI.TileReservationRegistry.IsReservedByOther(new Vector2Int(x, y), agent.AgentId);

            // 맵 경계 — MapBounds 단일 출처 (M3-F)
            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);

            // 집 곁 배치 (M11-E) — 개인 소유물(밭)은 제 집 둘레에 모인다. 집이 없으면 실패:
            // goal 트리거(MyHasHome==1)가 이미 막지만, 배치 규칙 자체도 집을 전제한다.
            if (_so.Building.PlaceNearOwnedHome)
            {
                if (!agent.TryGetHomeTile(out Vector2Int home))
                {
                    FailReason = $"{_so.Building.DisplayName}: 내 집이 없어 곁에 지을 수 없음";
                    return false;
                }
                int r = agent.WorldConfig != null ? agent.WorldConfig.FarmNearHomeRadius : 2;
                if (!TryFindFreeTileNear(Occupied, home.x, home.y, minX, maxX, minY, maxY, r, out _buildTile))
                {
                    FailReason = $"{_so.Building.DisplayName}: 집 주변(반경 {r}) 만원 — 빈 자리 없음";
                    return false;
                }
                MoveTarget = _buildTile; // 밭은 비차단이라 그 자리에 서서 짓는다
                return true;
            }

            // 택지 (M11-F, ADR-M11-5) — MinSpacingTiles > 0 건물(집)은 구역 대신 택지가 결정자다.
            // 앵커는 마을 기지 고정, 밀집은 기존 완공과의 최소 간격이 막는다.
            if (_so.Building.IsCountable && _so.Building.MinSpacingTiles > 0)
                return PrepareHomesite(agent, Occupied, minX, maxX, minY, maxY);

            // 구역 (M9-A, ADR-M9-1) — 수량형 건물 배치의 유일한 결정자. 군집 휴리스틱은 삭제됐다.
            // 확정 구역이 있으면 그 반경 안에만 짓는다 (만원이면 실패 = 보이는 소프트 상한).
            // 미확정(첫 완공 전)·ZoneRadius 0이면 hasZone=false → 기존 제자리 경로 그대로.
            Vector2Int zoneAnchor = default;
            int zoneRadius = 0;
            bool hasZone = _so.Building.IsCountable && _so.Building.ZoneRadius > 0
                && agent.Zones.TryGetZone(_so.Building.CountSlot, out zoneAnchor, out zoneRadius);

            if (!TryPickBuildTile(Occupied, hasZone, zoneAnchor, zoneRadius,
                    new Vector2Int(agent.TileX, agent.TileY), minX, maxX, minY, maxY,
                    out _buildTile, out bool needMove))
            {
                FailReason = hasZone
                    ? $"{_so.Building.DisplayName}: 농경지 구역(반경 {zoneRadius}) 만원 — 빈 타일 없음"
                    : $"{_so.Building.DisplayName}: 주변 {SEARCH_RADIUS}칸 내 건설 가능한 빈 타일 없음";
                return false;
            }

            if (_so.Building.BlocksMovement)
            {
                // 차단 건물은 자기가 만든 벽 위에 설 수 없다 — 건설 타일 인접 빈 칸에서 짓는다 (ADR-M3-3)
                if (!TryPickStandTile(Occupied, _buildTile, minX, maxX, minY, maxY, out Vector2Int stand))
                {
                    FailReason = $"{_so.Building.DisplayName}: 건설 타일 곁에 설 자리 없음";
                    return false;
                }
                MoveTarget = stand;
            }
            else if (needMove)
            {
                MoveTarget = _buildTile;
            }
            return true;
        }

        /// <summary>
        /// 택지 경로 (M11-F) — 집처럼 최소 간격이 있는 건물의 Prepare 분기.
        /// 취향(HomePreferredDist)은 **의뢰인**(집주인이 될 사람) 것이다 — 목수가 자기 취향으로
        /// 지으면 "목수 취향의 남의 집"이 된다 (⚠️①). 부탁이 아니면(자기 집) 본인 취향.
        /// </summary>
        private bool PrepareHomesite(VillagerAgent agent, System.Func<int, int, bool> occupied,
            int minX, int maxX, int minY, int maxY)
        {
            VillagerAgent owner = agent;
            if (agent.Requests != null && agent.Requests.TryGetRequester(agent.AgentId, out VillagerAgent requester)
                && requester != null)
                owner = requester;

            // 맵-비례 (M11-K): 반경·선호 거리 모두 맵 크기에 비례. 성격 값은 비율(0~1)이라
            // 실효 반경을 곱해 절대 거리로 바꾼다. 맵이 커지면 마을이 자동으로 넓게 퍼진다.
            int radius = agent.WorldConfig.EffectiveVillageRadius();
            float preferredFrac = HomePicker.PreferredDist(owner.Personality, owner.Job,
                agent.WorldConfig != null ? agent.WorldConfig.TraitRules : null);
            int preferred = agent.WorldConfig.PreferredHomeDist(preferredFrac);
            int seed = HomePicker.StableSeed(owner.AgentId); // 집주인 신원 → 대역 내 결정적 자리
            var anchor = new Vector2Int(agent.WorldConfig.BaseTileX, agent.WorldConfig.BaseTileY);

            if (!HomePicker.PickHomesite(occupied, agent.Construction.BuiltTilesOf(_so.Building.CountSlot),
                    anchor, radius, _so.Building.MinSpacingTiles, preferred, seed,
                    minX, maxX, minY, maxY, out _buildTile))
            {
                FailReason = $"{_so.Building.DisplayName}: 마을(반경 {radius}) 만원 — " +
                             $"간격 {_so.Building.MinSpacingTiles} 이상 빈 택지 없음";
                return false;
            }

            if (_so.Building.BlocksMovement)
            {
                // 차단 건물은 자기가 만든 벽 위에 설 수 없다 (ADR-M3-3) — 구역 경로와 동일 규약
                if (!TryPickStandTile(occupied, _buildTile, minX, maxX, minY, maxY, out Vector2Int stand))
                {
                    FailReason = $"{_so.Building.DisplayName}: 택지 곁에 설 자리 없음";
                    return false;
                }
                MoveTarget = stand;
            }
            else
            {
                MoveTarget = _buildTile;
            }
            return true;
        }

        /// <summary>차단 건물 건설자가 설 타일 — 건설 타일 자신을 제외한 인접 빈 칸 (순수 함수, 게이트 대상).</summary>
        public static bool TryPickStandTile(System.Func<int, int, bool> occupied, Vector2Int buildTile,
            int minX, int maxX, int minY, int maxY, out Vector2Int standTile)
        {
            bool Blocked(int x, int y) => (x == buildTile.x && y == buildTile.y) || occupied(x, y);
            return TryFindFreeTileNear(Blocked, buildTile.x, buildTile.y, minX, maxX, minY, maxY, SEARCH_RADIUS, out standTile);
        }

        /// <summary>
        /// 건설 타일 결정 (순수 함수 — EditMode 게이트 대상, ADR-M9-1):
        /// ① 구역 확정: 앵커 중심 반경 zoneRadius 링에서만 빈 타일 (만원이면 실패 — 구역이
        ///    소프트 상한 역할을 하므로 구역 밖 폴백 금지) →
        /// ② 구역 미확정(ZoneRadius 0 or 첫 완공 전): 현재 타일 비점유면 제자리 →
        /// ③ 현재 위치 곁 빈 타일 (그 완공이 앵커가 된다) → ④ 실패 (좌표 스냅 없이 재계획).
        /// </summary>
        public static bool TryPickBuildTile(System.Func<int, int, bool> occupied,
            bool hasZone, Vector2Int zoneAnchor, int zoneRadius, Vector2Int agentTile,
            int minX, int maxX, int minY, int maxY, out Vector2Int tile, out bool needMove)
        {
            if (hasZone)
            {
                // 구역 밖 건설 금지 = 소프트 상한 (M9-A ⚠️②: 만원을 곁 확장으로 우회하면 상한이 무너진다)
                if (TryFindFreeTileNear(occupied, zoneAnchor.x, zoneAnchor.y, minX, maxX, minY, maxY, zoneRadius, out tile))
                {
                    needMove = true;
                    return true;
                }
                needMove = false;
                return false;
            }
            if (!occupied(agentTile.x, agentTile.y))
            {
                tile = agentTile;
                needMove = false;
                return true;
            }
            if (TryFindFreeTileNear(occupied, agentTile.x, agentTile.y, minX, maxX, minY, maxY, SEARCH_RADIUS, out tile))
            {
                needMove = true;
                return true;
            }
            needMove = false;
            return false;
        }

        /// <summary>
        /// 중심 기준 링 순회(반경 1→maxRadius)로 첫 비점유 타일 탐색. 순수 함수 — EditMode 게이트 대상.
        /// maxRadius는 구역 반경(M9-A)이나 SEARCH_RADIUS(제자리 곁 탐색)를 받는다.
        /// </summary>
        public static bool TryFindFreeTileNear(System.Func<int, int, bool> occupied,
            int cx, int cy, int minX, int maxX, int minY, int maxY, int maxRadius, out Vector2Int tile)
        {
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue; // 링 테두리만
                        int x = cx + dx, y = cy + dy;
                        if (x < minX || x > maxX || y < minY || y > maxY) continue;
                        if (occupied(x, y)) continue;
                        tile = new Vector2Int(x, y);
                        return true;
                    }
                }
            }
            tile = default;
            return false;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
        {
            if (!DurationElapsed(dt)) return RunnerResult.Running;

            // 완공 순간 타일에 다른 주민이 있으면 실패 → 재계획 (매몰 방지의 두 번째 겹).
            // 대기(Running 유지)는 금지 — 건설자가 동상이 되어 통행 정체를 누적시키고
            // 상호 대기 데드락까지 가능 (2026-07-16 방치에서 실증). 실패는 first-class:
            // 자원은 Complete 시점에만 차감되므로 잃는 것 없이 다음 시도가 새 자리를 찾는다.
            if (_so.Building.BlocksMovement
                && AIVillage.AI.TileReservationRegistry.IsReservedByOther(_buildTile, agent.AgentId))
                return Fail($"{_so.Building.DisplayName} 건설 타일에 주민 체류 — 재계획");

            // 완공 위치 = Prepare에서 정한 건설 타일 (비차단은 도착 타일과 동일, 차단은 인접에서 시공)
            // builderId 전달 = 밭 소유 등록의 원천 (M11-E) — 완공 이벤트가 소유자를 실어 나른다
            if (!agent.Construction.Complete(_so.Building, _buildTile.x, _buildTile.y, agent.AgentId))
                return Fail($"{_so.Building.DisplayName} 완공 실패 (비용 부족 또는 중복)");

            // 자가 소유 건축 (M11-I 집 → M11-K 일반화) — OwnedBuilding 완공은 지은 본인이 소유한다
            // (집·모닥불). 부탁 완수 집은 RequestService.NotifyFulfilled가 의뢰인에게 배정하므로
            // 부탁 수행 중이면 건너뛴다(모닥불은 부탁 대상이 아니라 항상 자가). 배정 쓰기는 여전히
            // Assign 하나 (ADR-M8-3 불변). CountSlot을 소유 키로 넘긴다(집=HouseCount, 모닥불=CampfireCount).
            if (_so.Building.OwnedBuilding && _so.Building.IsCountable
                && agent.Requests != null && !agent.Requests.TryGetRequester(agent.AgentId, out _)
                && agent.Ownership != null)
                agent.Ownership.Assign(_buildTile, _so.Building.CountSlot, agent.AgentId, "자가 건축");

            return RunnerResult.Succeeded;
        }
    }
}
