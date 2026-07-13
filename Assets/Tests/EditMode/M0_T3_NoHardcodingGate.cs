using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M0-T3 하드코딩 자동 게이트 (명세 W7, 성공 기준 S2/S3/S6) —
    /// 사람이 아니라 게이트가 규칙을 지킨다. M0 소스 전체를 스캔해 어긴 파일:라인을 보고한다.
    /// </summary>
    public class M0_T3_NoHardcodingGate
    {
        private static string M0Root => Path.Combine(Application.dataPath, "Scripts/M0");

        private static IEnumerable<string> M0Sources()
            => Directory.GetFiles(M0Root, "*.cs", SearchOption.AllDirectories);

        private static List<string> FindViolations(Regex pattern, System.Func<string, bool> fileFilter = null)
        {
            var hits = new List<string>();
            foreach (string file in M0Sources())
            {
                string norm = file.Replace('\\', '/');
                if (fileFilter != null && !fileFilter(norm)) continue;

                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (pattern.IsMatch(lines[i]))
                        hits.Add($"{norm}:{i + 1}: {lines[i].Trim()}");
                }
            }
            return hits;
        }

        /// <summary>S2: 액션 이름 문자열 분기 금지 — 舊 3,476줄 god class가 만들어진 경로.</summary>
        [Test]
        public void M0_T3_CaseStringSwitch_Zero()
        {
            List<string> hits = FindViolations(new Regex(@"case\s+"""));
            Assert.IsEmpty(hits, "문자열 case 분기 발견 (ADR-M0-1 위반):\n" + string.Join("\n", hits));
        }

        /// <summary>S3: 舊 수치 상수 참조 금지 — 수치는 SO 에셋에만 (ADR-M0-2).</summary>
        [Test]
        public void M0_T3_OldConstantRefs_Zero()
        {
            List<string> hits = FindViolations(new Regex(@"YIELD_|BuildingCosts\."));
            Assert.IsEmpty(hits, "舊 수치 상수 참조 발견 (ADR-M0-2 위반):\n" + string.Join("\n", hits));
        }

        /// <summary>효과 정의는 데이터 계층 전용 — 실행 계층에서 SlotEffect를 만들면 수치 이원화의 시작.</summary>
        [Test]
        public void M0_T3_SlotEffectCreation_OnlyInDataLayer()
        {
            List<string> hits = FindViolations(
                new Regex(@"new\s+SlotEffect"),
                file => !file.Contains("/M0/Data/"));
            Assert.IsEmpty(hits, "데이터 계층 밖 SlotEffect 생성 발견:\n" + string.Join("\n", hits));
        }

        /// <summary>S6: 완공 플래그 단일 쓰기 — 정의(WorldModel) 외 호출부는 ConstructionService 하나여야 한다.</summary>
        [Test]
        public void M0_T3_BuiltFlag_SingleWritePath()
        {
            List<string> hits = FindViolations(
                new Regex(@"SetBuiltFlag\s*\("),
                file => !file.EndsWith("/World/WorldModel.cs"));
            Assert.AreEqual(1, hits.Count,
                "SetBuiltFlag 호출부는 ConstructionService 1건이어야 합니다:\n" + string.Join("\n", hits));
            StringAssert.Contains("ConstructionService.cs", hits[0]);
        }

        /// <summary>말풍선/로그용 한국어 대사 하드코딩 방지 — 문구는 SO DisplayName에만.</summary>
        [Test]
        public void M0_T3_TransformSnap_Zero()
        {
            // 舊 결함 C 부활 감시 (커밋 전 체크 ⑦의 자동화): 논리 타일 강제 대입 금지
            List<string> hits = FindViolations(new Regex(@"TileX\s*=\s*target|TileY\s*=\s*target"));
            Assert.IsEmpty(hits, "이동 실패 좌표 스냅 의심 코드 (ADR-8/9 위반):\n" + string.Join("\n", hits));
        }
    }
}
