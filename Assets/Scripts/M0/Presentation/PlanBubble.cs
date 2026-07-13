using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 주민 머리 위 플랜 말풍선 — GOAP의 계획을 플레이어에게 노출하는 M0 재미 장치.
    /// WorldSpace TextMeshPro(3D 텍스트). 舊 UI 교훈 계승:
    ///   - sizeDelta 명시 (버그C7 오버플로우 방어)
    ///   - 이모지 금지 (NeoDunggeunmo 한국어 폰트 U+1F000 미지원)
    ///   - 코루틴 없음 — 상태 전환 지점에서 즉시 갱신
    /// 문구는 코드에 없다 — 전부 ActionSO.DisplayName (ADR-M0-1).
    /// </summary>
    public sealed class PlanBubble
    {
        private readonly TextMeshPro _text;
        private readonly string _currentColorHex;

        public PlanBubble(Transform parent, TMP_FontAsset font, AgentConfigSO cfg)
        {
            var go = new GameObject("PlanBubble");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = new Vector3(0f, cfg.BubbleOffsetY, 0f);

            _text = go.AddComponent<TextMeshPro>();
            if (font != null) _text.font = font;
            _text.fontSize = cfg.BubbleFontSize;
            _text.alignment = TextAlignmentOptions.Bottom;
            _text.rectTransform.sizeDelta = new Vector2(cfg.BubbleWidth, 2f); // 버그C7: (0,0) 금지
            _text.textWrappingMode = TextWrappingModes.Normal;
            _text.color = Color.white;
            _text.outlineWidth = 0.2f;
            _text.outlineColor = new Color32(0, 0, 0, 220);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 20; // 주민(10)·건물(5) 위

            _currentColorHex = ColorUtility.ToHtmlStringRGB(cfg.BubbleCurrentColor);
            Clear();
        }

        /// <summary>임시 문구 표시 (거부 대사 등). 다음 ShowPlan/Clear가 덮어쓴다.</summary>
        public void ShowText(string text) => _text.text = text;

        /// <summary>플랜 표시 — 현재 액션은 강조색, 완료분은 생략, 남은 액션은 → 로 연결. prefix는 명령 표시용.</summary>
        public void ShowPlan(IReadOnlyList<ActionSO> plan, int currentIndex, string prefix = null)
        {
            if (plan == null || currentIndex >= plan.Count)
            {
                Clear();
                return;
            }

            var sb = new System.Text.StringBuilder(64);
            if (!string.IsNullOrEmpty(prefix)) sb.Append(prefix);
            for (int i = currentIndex; i < plan.Count; i++)
            {
                if (i > currentIndex) sb.Append(" → ");
                if (i == currentIndex)
                    sb.Append("<color=#").Append(_currentColorHex).Append('>')
                      .Append(plan[i].DisplayName).Append("</color>");
                else
                    sb.Append(plan[i].DisplayName);
            }
            _text.text = sb.ToString();
        }

        public void Clear() => _text.text = string.Empty;
    }
}
