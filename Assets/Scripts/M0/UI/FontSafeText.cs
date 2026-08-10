using System.Text;
using TMPro;

namespace AIVillage.M0
{
    /// <summary>
    /// 화면 문구의 글자 안전망 (2026-08-10) — **폰트에 없는 글자를 있는 글자로 바꾼다.**
    ///
    /// 왜 필요한가: 배포 폰트(`NeoDunggeunmoPro-Regular SDF`)는 **정적 아틀라스**
    /// (`m_AtlasPopulationMode: 0`)이고 폴백도 없다(`m_FallbackFontAssetTable: []`).
    /// 없는 글자는 □ 로 그려지고 TMP 가 매 측정마다 경고를 뱉는다 — `SeasonHud.Reflow` 가
    /// 달력이 바뀔 때마다 전 블록을 다시 재므로, 문구 하나가 잘못되면 **콘솔이 계속 찬다**
    /// (2026-08-10 관측: `⚔` 하나로 습격 내내 경고 반복).
    ///
    /// 🔑 왜 문자열을 하나씩 안 고치고 여기서 막는가 — **화면 문구의 출처가 코드만이 아니다.**
    /// 종족 대사·표시명·직업 이름은 **에셋에 적혀 있고**, 거기에 `·` 하나만 들어가도 같은 일이
    /// 난다. 코드 리터럴을 전수로 고쳐도 그 경로는 못 막는다. 그래서 **대입 지점 한 곳**
    /// (`SetSafe`)에 안전망을 둔다 (`ADR-M0-3` 쓰기 단일 지점의 표현판).
    ///
    /// ⚠️ 판정·로그는 원문 그대로다. 여기서 바꾸는 것은 **화면에 그려지는 순간의 글자뿐**이고,
    /// 콘솔 로그는 어떤 글꼴도 안 타므로 원래 기호를 유지하는 편이 읽기 좋다.
    /// </summary>
    public static class FontSafeText
    {
        /// <summary>
        /// 치환표 — 왼쪽은 **배포 폰트에 없는** 글자, 오른쪽은 **있는** 글자다.
        /// 게이트 `M23_T6_FontSafeTable_MatchesShippedFont` 가 그 두 조건을 실제 폰트 에셋과
        /// 대조하므로, 폰트를 갈아 끼우면 표가 먼저 red 로 알려 준다.
        ///
        /// 표에 없는 글자는 **일부러 그냥 통과시킨다** — 조용히 뭉개지 않고 TMP 경고가 뜨게
        /// 두어야 "새 글자가 들어왔다"는 신호가 남는다 (경고를 지우는 것이 목적이 아니라
        /// 화면이 깨지지 않는 것이 목적이다).
        /// </summary>
        public static readonly (char From, string To)[] Table =
        {
            ('·', "•"),      // 가운뎃점 — 정보줄 구분자 (가장 많이 쓴다)
            ('×', "x"),      // 곱셈표 — "×3" 반복 표기
            ('→', "▸"),      // 화살표 — 계획 사슬 "행동A → 행동B"
            ('←', "◂"),
            ('↔', "▸"),
            ('↑', "^"),
            ('↓', "v"),
            ('⚔', "▲"),      // 습격 프롬프트 머리표 (2026-08-10 경고의 발단)
            ('⚠', "!"),
            ('️', ""),  // 이모지 변형 선택자 — 앞 글자에 붙어 오는 보이지 않는 글자
            ('±', "+-"),
            ('−', "-"),      // 유니코드 마이너스 (ASCII '-' 아님)
            ('≤', "<="),
            ('≥', ">="),
            ('≈', "~"),
            ('※', "*"),
            ('⌊', ""),
            ('⌋', ""),
            ('★', "◆"),
            ('☆', "◇"),
        };

        /// <summary>치환 (순수). 바꿀 글자가 없으면 **원본 참조를 그대로 돌려준다** —
        /// 매 틱 불리는 자리라 평시 할당 0 (달력 캐시·조기 반환과 같은 배려).</summary>
        public static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            int first = -1;
            for (int i = 0; i < s.Length && first < 0; i++)
                if (IndexOf(s[i]) >= 0) first = i;
            if (first < 0) return s;

            var sb = new StringBuilder(s.Length + 4);
            sb.Append(s, 0, first);
            for (int i = first; i < s.Length; i++)
            {
                int at = IndexOf(s[i]);
                if (at < 0) sb.Append(s[i]);
                else sb.Append(Table[at].To);
            }
            return sb.ToString();
        }

        private static int IndexOf(char c)
        {
            for (int i = 0; i < Table.Length; i++)
                if (Table[i].From == c) return i;
            return -1;
        }

        /// <summary>화면 대입의 유일한 문 — `t.text = s` 대신 이것을 쓴다.
        /// 게이트가 `.text =` 직접 대입을 금지해 우회를 막는다.</summary>
        public static void SetSafe(this TMP_Text t, string s)
        {
            if (t != null) t.text = Sanitize(s) ?? "";
        }
    }
}
