using System;
using UnityEngine;

namespace MukJump.Core
{
    /// 로비에서 고른 수련 방향을 저장한다.
    /// 수치 영구 강화 대신 첫 성장 두루마리의 한 칸을 보장해 신규 계보를 찾기 어려워지는
    /// 카탈로그 풀 오염을 줄인다.
    public static class GrowthFocusProfile
    {
        const string PlayerPrefsKey = "MukJump.GrowthFocusId";

        static string selectedDefinitionId;
        static bool loaded;

        public static event Action Changed;

        public static string SelectedDefinitionId
        {
            get
            {
                EnsureLoaded();
                return selectedDefinitionId;
            }
        }

        public static bool HasSelection => !string.IsNullOrEmpty(SelectedDefinitionId);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            selectedDefinitionId = null;
            loaded = false;
            Changed = null;
        }

        public static bool TrySelect(string definitionId)
        {
            if (!RoguelikeGrowthCatalog.TryGet(definitionId, out var definition) ||
                definition.Status != ImplementationStatus.RuntimeReady ||
                !definition.RuntimeType.HasValue)
                return false;

            EnsureLoaded();
            if (string.Equals(selectedDefinitionId, definition.Id,
                    StringComparison.Ordinal))
                return true;

            selectedDefinitionId = definition.Id;
            PlayerPrefs.SetString(PlayerPrefsKey, selectedDefinitionId);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        public static void Clear()
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(selectedDefinitionId))
                return;

            selectedDefinitionId = string.Empty;
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool TryGetRuntimeUpgrade(out GrowthUpgradeType upgrade)
        {
            upgrade = default;
            string id = SelectedDefinitionId;
            return !string.IsNullOrEmpty(id) &&
                   RoguelikeGrowthCatalog.TryGetRuntimeType(id, out upgrade);
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            string saved = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (RoguelikeGrowthCatalog.TryGet(saved, out var definition) &&
                definition.Status == ImplementationStatus.RuntimeReady &&
                definition.RuntimeType.HasValue)
            {
                selectedDefinitionId = definition.Id;
                return;
            }

            selectedDefinitionId = string.Empty;
            if (!string.IsNullOrEmpty(saved))
                PlayerPrefs.DeleteKey(PlayerPrefsKey);
        }

#if UNITY_EDITOR
        /// 테스트가 로컬 사용자 설정을 읽거나 덮어쓰지 않고 메모리 선택만 구성하는 용도.
        public static bool SetForTests(string definitionId)
        {
            if (!RoguelikeGrowthCatalog.TryGet(definitionId, out var definition) ||
                definition.Status != ImplementationStatus.RuntimeReady ||
                !definition.RuntimeType.HasValue)
                return false;
            selectedDefinitionId = definition.Id;
            loaded = true;
            Changed?.Invoke();
            return true;
        }

        public static void ResetForTests(bool deleteSavedValue = false)
        {
            if (deleteSavedValue)
                PlayerPrefs.DeleteKey(PlayerPrefsKey);
            selectedDefinitionId = null;
            loaded = false;
            Changed = null;
        }
#endif
    }
}
