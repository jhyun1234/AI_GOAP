using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 스프라이트 자식 뷰 장착의 **유일한 지점** (2026-08-10). 주민과 위협이 같은 규칙으로
    /// 화면에 선다 — 여기가 하나가 아니면 반드시 갈린다.
    ///
    /// 🔴 계기: 위협(ThreatAgent)이 `AgentSpriteSetSO.Scale`을 아예 안 읽고 배율 1로 그렸다.
    /// 팩 셀 크기가 종족마다 달라서(32/48/64px) 고블린 1.33배·오크 2배·타락천사 2.7배로 나왔다.
    ///
    /// 🔴 **피벗 정정 (2026-08-10 실측)** — 처음엔 "팩 피벗이 좌하단"이라 적었고 주민 쪽 舊 코드도
    /// 그 전제로 `localPosition = -Scale` 을 걸고 있었다. 둘 다 틀렸다: meta 의 `pivot: {0,0}` 은
    /// `alignment: 0`(Center) 이라 **무시되는 값**이고, 런타임 피벗은 전 시트가 **중앙**이다
    /// (rect 32×32 → pivotPx (16,16), 48/64px 도 동일). 그래서 舊 주민 오프셋은 보정이 아니라
    /// **반 칸짜리 어긋남**이었고(그림자는 0,-0.12 에 있었으니 몸과 따로 놀았다), 이 함수로
    /// 갈아끼우면서 사라졌다. 위협은 원래 오프셋이 없어 위치 변화가 없다.
    ///
    /// 🔑 교훈은 그대로다 — 수치를 코드가 아니라 **그림에게 묻는다**: rect·pivot·PPU 로 실제
    /// 렌더 크기를 재고 중심을 맞춘다. 피벗 규약이 무엇이든(중앙이든 좌하단이든) 이 함수는
    /// 옳은 답을 낸다. 상수를 박았다면 이번 정정에서 또 틀렸을 것이다.
    /// </summary>
    public static class SpriteViewRig
    {
        /// <summary>모든 서 있는 것의 **접지선** (타일 중심 기준, 아래가 음수).
        /// 값의 출처는 주민이다: 실측한 현재 주민 그림 아래끝이 −0.42 이고 그 화면이 승인된
        /// 상태라, 그 선을 공용 기준으로 삼으면 주민은 안 움직이고 나머지가 주민에게 맞춰 선다.
        /// 🔑 하나의 상수인 것이 핵심이다 — 땅에 닿는 높이가 종족마다 다를 이유가 없다.
        /// 🔑 맞추는 대상이 **구운 그림자의 아래끝**이라 정확하다 (팩 시트가 그림자를 품고 있다,
        /// 실측 2026-08-10) — 그림쟁이가 이미 찍어 둔 접지선을 쓰는 셈이다.</summary>
        public const float FootBaselineTiles = -0.42f;

        /// <summary>그림자 폭 ÷ 캐릭터 키. 주민 배포값(폭 0.62 · 키 0.94)에서 나온 비다 —
        /// 이 비를 쓰면 주민 그림자는 그대로이면서 다른 종족이 제 몸에 맞는 그림자를 갖는다.</summary>
        private const float ShadowWidthPerCharHeight = 0.66f;

        /// <summary>자식 "View"를 만들어 스프라이트 렌더러를 돌려준다.
        /// 최종 배율 = 세트의 규격 정규화(Scale) × 연출 배율(sizeMult).</summary>
        /// <param name="sizeMult">연출 배율 — 1이면 규격 크기 그대로 (주민과 같은 키).
        /// 위압감처럼 **의도한** 크기 차이는 여기로 들어온다 (에셋 한 칸, 코드 아님).</param>
        public static SpriteRenderer Attach(Transform parent, AgentSpriteSetSO set, float sizeMult = 1f)
        {
            float scale = ScaleOf(set, sizeMult);
            Sprite frame = set.AnyFrame();

            var view = new GameObject("View");
            view.transform.SetParent(parent, worldPositionStays: false);
            view.transform.localScale = Vector3.one * scale;
            view.transform.localPosition = CenterOffset(frame, scale)
                                         + new Vector3(0f, FootLift(set, frame, scale), 0f);
            return view.AddComponent<SpriteRenderer>();
        }

        /// <summary>발밑 그림자를 붙여 돌려준다 (없으면 null). 깊이 갱신은 호출자 몫 —
        /// 움직이는 것은 매 프레임 다시 재야 한다.</summary>
        public static SpriteRenderer AttachShadow(Transform parent, AgentSpriteSetSO set, float sizeMult = 1f)
        {
            if (set == null || set.ShadowWidthTiles <= 0f || set.ShadowAlpha <= 0f) return null;
            float scale = ScaleOf(set, sizeMult);
            float width = ShadowWidth(set, set.AnyFrame(), scale);
            if (width <= 0f) return null;

            var go = new GameObject("Shadow");
            go.transform.SetParent(parent, worldPositionStays: false);
            // 그림자는 **발밑 선**에 놓인다 — 몸이 어디 그려지든 서 있는 자리는 하나다.
            go.transform.localPosition = new Vector3(0f, FootBaselineTiles + set.ShadowOffsetTiles, 0f);
            go.transform.localScale = new Vector3(width, width * set.ShadowFlatten, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = M0Sprites.Circle;
            sr.color = new Color(0f, 0f, 0f, set.ShadowAlpha);
            return sr;
        }

        private static float ScaleOf(AgentSpriteSetSO set, float sizeMult)
            => Mathf.Max(0.01f, set.Scale) * Mathf.Max(0.01f, sizeMult);

        /// <summary>중앙 정렬에서 얼마나 더 들어올려야 **그림의 발끝**이 기준선에 오는가.
        /// 미측정(ArtBottomNorm &lt; 0)이면 0 = 중앙 정렬 그대로 (중립 불변식).</summary>
        public static float FootLift(AgentSpriteSetSO set, Sprite frame, float scale)
        {
            if (set == null || set.ArtBottomNorm < 0f || frame == null) return 0f;
            float boxUnits = frame.rect.height / Mathf.Max(0.01f, frame.pixelsPerUnit) * scale;
            float artBottomFromBox = set.ArtBottomNorm * boxUnits; // 셀 아래끝~그림 아래끝
            return FootBaselineTiles + boxUnits * 0.5f - artBottomFromBox;
        }

        /// <summary>이 세트의 그림자 폭 — 캐릭터 키에서 파생한다 (셀이 아니라).
        /// 미측정이면 에셋 값을 그대로 쓴다 (중립).</summary>
        public static float ShadowWidth(AgentSpriteSetSO set, Sprite frame, float scale)
        {
            if (set == null) return 0f;
            if (set.ArtHeightNorm < 0f || frame == null) return set.ShadowWidthTiles;
            float boxUnits = frame.rect.height / Mathf.Max(0.01f, frame.pixelsPerUnit) * scale;
            return set.ArtHeightNorm * boxUnits * ShadowWidthPerCharHeight;
        }

        /// <summary>스프라이트 중심을 부모 원점(=타일)에 맞추는 자식 오프셋 (부모 좌표계).
        /// 피벗이 중앙이면 0 (중립 — 보정 없는 시트에 억지로 손대지 않는다).</summary>
        public static Vector3 CenterOffset(Sprite s, float scale)
        {
            if (s == null || s.rect.width <= 0f || s.rect.height <= 0f) return Vector3.zero;
            float ppu = Mathf.Max(0.01f, s.pixelsPerUnit);
            // 피벗에서 본 스프라이트 중심 (유닛) → 그만큼 반대로 밀면 중심이 원점에 온다.
            float cx = (s.rect.width * 0.5f - s.pivot.x) / ppu * scale;
            float cy = (s.rect.height * 0.5f - s.pivot.y) / ppu * scale;
            return new Vector3(-cx, -cy, 0f);
        }
    }
}
