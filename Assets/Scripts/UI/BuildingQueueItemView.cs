using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AIVillage.Core;

namespace AIVillage.UI
{
    [DisallowMultipleComponent]
    public sealed class BuildingQueueItemView : MonoBehaviour
    {
        [Tooltip("건물 이름과 상태를 표시하는 TMP_Text.")]
        [SerializeField] private TMP_Text _label;

        [Tooltip("건설 진행률(0~100)을 표시하는 Slider.")]
        [SerializeField] private Slider _progressSlider;

        public void Bind(BuildingQueueEntry entry)
        {
            if (entry == null) return;

            if (_label != null)
            {
                string status;
                switch (entry.Status)
                {
                    case BuildingStatus.Pending:
                        status = "[대기]";
                        break;
                    case BuildingStatus.InProgress:
                        status = $"[건설중] ({entry.AssignedVillagerIds?.Count ?? 0}명)";
                        break;
                    case BuildingStatus.Completed:
                        status = "[완료]";
                        break;
                    default:
                        status = $"[{entry.Status}]";
                        break;
                }

                _label.SetText($"{status} {entry.BuildingId}");
            }

            if (_progressSlider != null)
            {
                _progressSlider.minValue = 0f;
                _progressSlider.maxValue = 100f;
                _progressSlider.value    = entry.ProgressPercent;
            }
        }
    }
}
