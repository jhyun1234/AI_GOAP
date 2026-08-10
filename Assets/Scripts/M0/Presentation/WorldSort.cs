using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 깊이 정렬의 단일 출처 (2026-08-09) — **월드 Y가 앞뒤를 정한다**.
    ///
    /// 왜 생겼나: 舊 방식은 층 번호가 고정이었다(건물 5 · 주민 10 · 위협 11). 임시 색 원 시절엔
    /// 아무 문제가 없었지만, 실그림이 들어와 집이 3타일짜리 덩어리가 되자 **주민이 집 뒤에 서
    /// 있어도 항상 집 위에 그려졌다** — 앞뒤가 화면에서 사라졌다 (사용자 Play 지적).
    /// 위에서 내려다보는 2D에서 앞뒤는 층이 아니라 **아래쪽에 있는 것이 앞**이다.
    ///
    /// 규약:
    ///   · <see cref="Order"/>(y, bias) = round(−y × <see cref="YScale"/>) + bias.
    ///     y가 작을수록(아래) 큰 값 = 나중에 그려짐 = 앞.
    ///   · bias는 **같은 y에서의 위아래**만 정한다. 0~15만 쓴다 —
    ///     16 이상이면 한 타일을 건너뛰어 y 판정을 이긴다 (그 순간 이 규약이 무너진다).
    ///   · 화면 붙박이(이름표·말풍선)는 <see cref="Overlay"/>, 바닥 데칼(구역 테두리·미리보기)은
    ///     <see cref="Decal"/> — 둘은 y와 무관하다.
    ///
    /// 맵 100타일(±50) × 16 = ±800이라 short 범위(±32767) 안에서 넉넉하다.
    /// </summary>
    public static class WorldSort
    {
        /// <summary>타일당 정렬 눈금. bias 상한(15)과 짝 — 이 값을 바꾸면 bias 표도 같이 본다.</summary>
        public const int YScale = 16;

        // ── bias 표 (같은 칸에서 누가 위인가) ──
        public const int Node    = 0; // 자원 노드·바닥 물건
        public const int Ghost   = 1; // 계획 고스트 (아직 없는 것 — 실물 아래)
        public const int Plot    = 2; // 밭 흙
        public const int Crop    = 3; // 밭 작물 (흙 위)
        public const int Trap    = 4; // 함정 (밟히는 것 — 서 있는 것 아래)
        public const int Build   = 5; // 건물 몸체
        public const int Damage  = 6; // 손상 오버레이 (제 건물 위)
        public const int Lock    = 7; // 문 자물쇠
        public const int Select  = 8; // 선택 링 (발밑)
        public const int Agent   = 9; // 주민·방랑자
        public const int Threat  = 10; // 위협 (같은 칸이면 짐승이 위 — 물리는 게 보여야 한다)
        public const int Particle = 11; // 흩날리는 것 (벌목 나뭇잎) — 같은 칸의 모든 것 앞에서 떨어진다

        /// <summary>지면·안개 판(MapQuad) — 맨 아래. ⚠️ 이 값이 없으면 y가 양수인 물건이
        /// 전부 지면 뒤로 숨는다 (Y 정렬은 음수 order를 만드는데 舊 MapQuad는 0이었다 —
        /// 2026-08-09 실측에서 잡은 회귀).</summary>
        public const int Ground = -3000;

        /// <summary>바닥 데칼 — 모든 월드 물건 아래 (구역 테두리·마우스 칸·줄 미리보기).</summary>
        public const int Decal = -2000;

        /// <summary>화면 붙박이 — 모든 월드 물건 위 (이름표·말풍선). 가려지면 못 읽는다.</summary>
        public const int Overlay = 2000;

        public static int Order(float worldY, int bias)
            => Mathf.Clamp(Mathf.RoundToInt(-worldY * YScale), -30000, 30000) + bias;
    }
}
