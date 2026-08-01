using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 판별 연대기 아카이브 (M15-W1 — 새 선반 ②: 판이 끝나도 마을의 이야기가 남는다).
    /// 파일 = persistentDataPath/chronicle_archive.json — run_records.json(역대 최고 1건)과
    /// **별개 파일**이다 (ADR-M15-1: 검증된 신기록 통로는 한 글자도 안 바꾼다).
    /// 스키마 = append-only + Version (ADR-M14-3 계승) — 필드 제거·의미 변경 금지, 확장은 append만.
    /// 쓰기 지점 = 겨울 전환·전멸 래치 2곳뿐 (ADR-M15-2 — 매 틱·매 사건 쓰기 금지).
    /// 파일 실패는 게임을 못 막는다 (ADR-M15-4 — RunRecordStore 원칙 계승).
    /// </summary>
    public static class ChronicleArchive
    {
        /// <summary>명부 한 줄 — VillagerRecord의 직렬화 판. 성격·직업은 문자열 (에셋 수명 무관,
        /// VillagerRecord 선례). 사건은 요약 문자열만 (ADR-M15-3 — 원본 타임라인은 판과 함께 소멸).</summary>
        [Serializable]
        public sealed class VillagerEntry
        {
            public string ShortName;
            public string Personality;
            public string Job;
            public int    BornDay;
            public int    LeftDay;      // -1 = 스냅샷 시점 생존 (VillagerRecord 센티넬 유지)
            public int    Cause;        // ExitCause 정수 — append-only enum이라 안전 (ADR-M13-2)
            public string BuddyShort;   // 퇴장 시점 단짝 표시명. "" = 없음
            public string GrudgeShort;
            public string LifeEvents;   // ComposeLifeEvents 출력 ("D1 집 마련 · D5~34 명령 거부(피로) ×30")
        }

        /// <summary>판 하나의 기록 — 요약 + 명부.</summary>
        [Serializable]
        public sealed class RunEntry
        {
            public int    Version = 1;
            public int    RunNumber;    // 1부터 (목록 표시용) — Apply가 부여, 조립자는 안 채운다
            public string EndedAt;      // 마지막 갱신 벽시계 "2026-07-31 21:04" — 게임 밖 이력
            public int    Winters;
            public int    LastDay;
            public int    PeakPop;
            public int    Settles;
            public bool   Ended;        // true = 전멸 마감 / false = 겨울 갱신까지 (중도 종료 판)
            public int    PeakPricePct; // 판 중 최고 물가 % (M16-W6 append — ADR-M14-3: 필드 append는
                                        // Version 인상 없음. 0 = 화폐 이전 기록·물가 무변동 판)
            public int    TaxTotal;     // 판 전체 세수 누적(동) — M17-W6 append
            public int    MintTotal;    // 판 전체 **발행** 누적(동) — M17-W6 append.
                                        // ⚠️ WorldModel.IssuedTotal만 담는다. MintedTotal에는
                                        // 임금·원천징수가 섞여 있어 "찍어서 버텼다"를 왜곡한다.
                                        // 이 둘의 비율이 곧 서사다: 세수로 굴린 판인가, 찍어서 버틴 판인가.
            public List<VillagerEntry> Roster = new List<VillagerEntry>();
        }

        /// <summary>파일 루트 — JsonUtility는 List 루트 직렬화 불가라 wrapper가 필수.</summary>
        [Serializable]
        public sealed class ArchiveFile
        {
            public int Version = 1;
            public List<RunEntry> Runs = new List<RunEntry>();
        }

        private static string FilePath => Path.Combine(Application.persistentDataPath, "chronicle_archive.json");

        /// <summary>순수부 (게이트 M15-T2) — runIndex &lt; 0 이면 append, 아니면 그 자리 갱신.
        /// 반환 = 실제 인덱스. 범위 밖 인덱스는 append로 강등 (방어 — 기록 유실보다 중복이 낫다).
        /// RunNumber는 여기서 부여한다 (append = Runs.Count+1, 갱신 = 기존 번호 유지) —
        /// 조립자(SnapshotCurrentRun)는 파일 내용을 모르고, 번호를 아는 곳은 store뿐이다.</summary>
        public static int Apply(ArchiveFile file, int runIndex, RunEntry entry)
        {
            if (runIndex >= 0 && runIndex < file.Runs.Count)
            {
                entry.RunNumber = file.Runs[runIndex].RunNumber; // 갱신 — 번호는 처음 받은 그대로
                file.Runs[runIndex] = entry;
                return runIndex;
            }
            entry.RunNumber = file.Runs.Count + 1;
            file.Runs.Add(entry);
            return file.Runs.Count - 1;
        }

        /// <summary>파싱부 (게이트 M15-T3 — 파일 IO 없이 손상 처리를 검증하는 심).
        /// null·빈 문자열·손상 JSON = null 반환 (예외는 경고 로그로 삼키되 침묵 금지).</summary>
        public static ArchiveFile Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var file = JsonUtility.FromJson<ArchiveFile>(json);
                if (file != null && file.Runs != null) return file;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chronicle] 아카이브 파싱 실패 — 빈 연대기로 진행 (기록은 게임을 못 막는다): {e.Message}");
            }
            return null;
        }

        /// <summary>아카이브 읽기 — 없음·손상이면 빈 아카이브.</summary>
        public static ArchiveFile Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    ArchiveFile file = Parse(File.ReadAllText(FilePath));
                    if (file != null) return file;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chronicle] 아카이브 읽기 실패 — 빈 연대기로 진행 (기록은 게임을 못 막는다): {e.Message}");
            }
            return new ArchiveFile();
        }

        /// <summary>Load→Apply→Save 한 번에. 반환 = 실제 인덱스 (호출자가 다음 갱신을 위해 보관).
        /// 호출 지점 = 겨울 전환·전멸 래치 2곳 (ADR-M15-2). 판 수십 건 JSON 전체 rewrite는
        /// 의도된 단순성이다 — 증분 IO 최적화 금지 (명세 ⚠️W2).</summary>
        public static int SaveRun(int runIndex, RunEntry entry)
        {
            ArchiveFile file = Load();
            int actual = Apply(file, runIndex, entry);
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(file, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chronicle] 아카이브 쓰기 실패 — 이번 갱신은 휘발 (기록은 게임을 못 막는다): {e.Message}");
            }
            return actual;
        }
    }
}
