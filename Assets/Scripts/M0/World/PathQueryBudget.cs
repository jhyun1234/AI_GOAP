namespace AIVillage.M0
{
    /// <summary>
    /// 프레임당 위협 리패스 상한 (M28-W1 — ADR_경로탐색_확장경계 트리거 B-2, 스파이크 제거).
    ///
    /// 상한 초과 = 실패가 아니라 **연기**다: 추격은 지난 경로로 한 프레임 더 걷고 다음 프레임
    /// 재시도한다. 맵이 커질수록 쿼리 1건의 최악 비용이 면적 비례로 늘므로(300² = 100²의 9배),
    /// 연속 소스(추격 재경로·배회 재선정)가 한 프레임에 몰리는 것만 막으면 히치가 사라진다.
    ///
    /// ⚠️ 주민 경로는 여기를 지나지 않는다 (ADR-M28-4 — 주민 계획 실행은 게임 코어라 조이지
    /// 않는다). 웨이브 스폰 버스트도 1차 범위 밖 (드문 이벤트 — 명세 §8에서 재평가).
    /// </summary>
    public sealed class PathQueryBudget
    {
        /// <summary>알고리즘 상수 — 60fps × 4 = 초당 240쿼리 상한. 밸런스 수치가 아니라
        /// 히치 방지 장치라 코드에 둔다 (ADR-M0-2 판정: 플레이어가 패치로 바꿀 값이 아님).</summary>
        public const int PER_FRAME = 4;

        private int _frame = -1;
        private int _used;

        /// <summary>이 프레임에 쿼리 1건을 쓸 수 있으면 true (소비 포함). 순수 판정 —
        /// frameCount를 밖에서 받아 EditMode 게이트가 프레임 흐름 없이 검산한다 (M28_T1).</summary>
        public bool TryConsume(int frameCount)
        {
            if (frameCount != _frame) { _frame = frameCount; _used = 0; }
            return ++_used <= PER_FRAME;
        }
    }
}
