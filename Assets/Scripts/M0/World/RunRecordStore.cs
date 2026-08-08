using System;
using System.IO;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 역대 최고 기록 (M14-W4 — 넓히기 ② 새 선반: 첫 파일 지속 통로).
    /// 파일 = persistentDataPath/run_records.json. 스키마는 **append-only + Version** (ADR-M14-3) —
    /// M15 연대기 아카이브·장기 백로그(세계 지속)가 같은 선반을 읽어야 하므로 필드 제거·의미 변경 금지.
    /// 쓰기 지점은 겨울 전환·전멸 래치 **2곳뿐** (명세 ⚠️W4-③ — 상태 쓰기 단일 지점의 정신).
    /// 파일 실패는 게임을 못 막는다 (⚠️W4-④ — 경고 로그 후 진행).
    /// </summary>
    public static class RunRecordStore
    {
        [Serializable]
        public sealed class RunRecord
        {
            public int Version = 1;  // append-only 스키마 (ADR-M14-3) — 필드 추가는 버전 불변
            public int BestWinters;  // 넘긴 겨울(봉쇄 계절) 수 — 비교 키 1순위
            public int BestDay;      // 그 판의 생존일 — 비교 키 2순위
            public int BestPeakPop;  // 그 판의 최대 동시 인구 — 표기용 (비교 키 아님)
            public int BestRepels;   // 역대 최다 격퇴 (M21-W9) — **판과 독립인 최댓값** (비교 키
                                     // 아님). 진 판의 최다 격퇴도 기록으로 남는다 — "격퇴하며
                                     // 버티는 기록 경주"(ADR-M21-1)의 두 번째 자. 옛 파일은
                                     // 필드 부재 → 0 (JsonUtility 역직렬화 호환, DoD ③)
        }

        private static string FilePath => Path.Combine(Application.persistentDataPath, "run_records.json");

        /// <summary>기록 읽기 — 파일 없음·손상이면 0 기록 (예외는 경고 로그로 삼키되 침묵 금지).</summary>
        public static RunRecord Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var rec = JsonUtility.FromJson<RunRecord>(File.ReadAllText(FilePath));
                    if (rec != null) return rec;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunRecord] 읽기 실패 — 0 기록으로 진행 (기록은 게임을 못 막는다): {e.Message}");
            }
            return new RunRecord();
        }

        /// <summary>비교부 (순수 — 게이트 M14-T3): 사전순 (겨울 우선, 동률이면 생존일). 동률 = 갱신 아님.</summary>
        public static bool IsBetter(int winters, int day, RunRecord best)
            => winters > best.BestWinters
            || (winters == best.BestWinters && day > best.BestDay);

        /// <summary>더 좋으면 저장하고 true. 호출 지점 = 겨울 전환·전멸 래치 2곳 (⚠️W4-③ — 매 틱 금지).
        /// repels(M21-W9)는 판 기록과 **독립으로** 최댓값 갱신 — 오래 못 버텼지만 많이 물리친 판의
        /// 격퇴 기록이 사라지면 안 된다. 반환값은 여전히 "판 기록 갱신 여부"다 (신기록! 표시의 짝).</summary>
        public static bool SaveIfBetter(int winters, int day, int peakPop, int repels = 0)
        {
            RunRecord best = Load();
            bool betterRun = IsBetter(winters, day, best);
            bool betterRepels = repels > best.BestRepels;
            if (!betterRun && !betterRepels) return false;

            if (betterRun)
            {
                best.BestWinters = winters;
                best.BestDay = day;
                best.BestPeakPop = peakPop;
            }
            if (betterRepels) best.BestRepels = repels;
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(best, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunRecord] 쓰기 실패 — 이번 갱신은 휘발 (기록은 게임을 못 막는다): {e.Message}");
            }
            return betterRun;
        }
    }
}
