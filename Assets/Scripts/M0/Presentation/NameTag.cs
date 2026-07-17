using TMPro;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 주민 머리 아래 상시 이름표 "A · 농부" (M7-A). 표현 전용 — PlanBubble과 별개 오브젝트
    /// (말풍선은 뜨고 지지만 이름표는 상시라 Clear에 휩쓸리면 안 된다).
    /// 직업만 표기, 성격은 선택 정보줄에서만 — 성격 이름이 직업처럼 들리는 혼동 방지 (ADR-M7-5).
    /// WorldSpace TMP — PlanBubble의 舊 UI 교훈 계승 (sizeDelta 명시, 이모지 금지).
    /// </summary>
    public sealed class NameTag
    {
        public NameTag(Transform parent, TMP_FontAsset font, AgentConfigSO cfg, string label)
        {
            var go = new GameObject("NameTag");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = new Vector3(0f, cfg.NameTagOffsetY, 0f);

            var text = go.AddComponent<TextMeshPro>();
            if (font != null) text.font = font;
            text.fontSize = cfg.NameTagFontSize;
            text.alignment = TextAlignmentOptions.Top;
            text.rectTransform.sizeDelta = new Vector2(cfg.BubbleWidth, 1f); // 버그C7: (0,0) 금지
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = Color.white;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 220);
            text.text = label;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 20; // 주민(10)·건물(5) 위 — 말풍선과 동일 층
        }
    }
}
