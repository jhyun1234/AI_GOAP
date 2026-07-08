using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M18: VillagerFSM.StartPathTo가 PathResult.Unreachable 수신 시
    /// Brain 좌표 스냅으로 이동 실패를 GOAP에 성공으로 위장하지 않는지 정적 검증한다.
    /// 방향 ② 이동 실패 first-class 승격 — 결함 C 부활 감시 게이트.
    ///
    /// 계약 (ADR-M2 · 예정 ADR-9):
    ///   - Unreachable 브랜치는 좌표 스냅 금지 → OnMoveUnreachable 헬퍼로 위임한다.
    ///   - 헬퍼는 NearDiscoveredResource=false 강제 후 RequestReplanning 요청 로그를 남긴다.
    /// </summary>
    public class M18_StartPathToBranching
    {
        private static string ReadFsmSource()
        {
            var path = Path.Combine(Application.dataPath, "Scripts/AI/VillagerFSM.cs");
            Assert.IsTrue(File.Exists(path),
                $"VillagerFSM.cs를 찾지 못했습니다: {path}");
            return File.ReadAllText(path);
        }

        // 주석(//)으로 시작하는 라인은 오탐 방지를 위해 제외.
        private static bool ContainsNonComment(string src, string pattern)
        {
            var rx = new Regex(@"^(?!\s*//).*" + pattern, RegexOptions.Multiline);
            return rx.IsMatch(src);
        }

        [Test]
        public void StartPathTo_does_not_snap_TileX_on_unreachable()
        {
            var src = ReadFsmSource();
            Assert.IsFalse(
                ContainsNonComment(src, @"Brain\.TileX\s*=\s*targetX"),
                "결함 C 재발: VillagerFSM.cs에 'Brain.TileX = targetX' 좌표 스냅이 부활했습니다. " +
                "PathResult.Unreachable 수신 시 반드시 OnMoveUnreachable → RequestReplanning으로 위임하세요.");
        }

        [Test]
        public void StartPathTo_does_not_snap_TileY_on_unreachable()
        {
            var src = ReadFsmSource();
            Assert.IsFalse(
                ContainsNonComment(src, @"Brain\.TileY\s*=\s*targetY"),
                "결함 C 재발: VillagerFSM.cs에 'Brain.TileY = targetY' 좌표 스냅이 부활했습니다.");
        }

        [Test]
        public void Unreachable_branch_logs_replanning_request()
        {
            var src = ReadFsmSource();
            Assert.IsTrue(
                src.Contains("Unreachable 수신, Replanning 요청"),
                "M2에서 도입한 로그 문구 'Unreachable 수신, Replanning 요청'이 사라졌습니다. " +
                "이 문구는 이동 실패 first-class 계약의 관측 계기입니다.");
        }

        [Test]
        public void OnMoveUnreachable_helper_exists()
        {
            var src = ReadFsmSource();
            Assert.IsTrue(
                Regex.IsMatch(src, @"\bOnMoveUnreachable\s*\("),
                "OnMoveUnreachable 헬퍼가 사라졌습니다. StartPathTo의 Unreachable 브랜치는 이 헬퍼로 위임해야 합니다.");
        }
    }
}
