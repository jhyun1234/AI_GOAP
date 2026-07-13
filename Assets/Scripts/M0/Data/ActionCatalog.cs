using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// M0 액션 전체 목록. 배열 인덱스 = 플래너 액션 인덱스 (ADR-M0-6).
    /// 플랜 결과 인덱스가 이 배열로 직결되므로 이름 해시 역매핑(舊 names[] 3종 세트)이 필요 없다.
    /// 액션 추가 = 에셋 생성 + 이 배열에 등록. 코드 수정 없음.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/ActionCatalog", fileName = "ActionCatalog")]
    public sealed class ActionCatalog : ScriptableObject
    {
        public ActionSO[] Actions;

        private void OnValidate()
        {
            if (Actions == null) return;
            for (int i = 0; i < Actions.Length; i++)
            {
                if (Actions[i] == null)
                {
                    Debug.LogError($"[ActionCatalog] {i}번 슬롯이 비어 있습니다.", this);
                    continue;
                }
                for (int j = i + 1; j < Actions.Length; j++)
                {
                    if (Actions[i] == Actions[j])
                        Debug.LogError($"[ActionCatalog] '{Actions[i].name}'이(가) {i}, {j}에 중복 등록됐습니다.", this);
                }
            }
        }
    }
}
