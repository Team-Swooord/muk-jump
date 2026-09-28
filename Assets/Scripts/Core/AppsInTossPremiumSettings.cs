using UnityEngine;

namespace MukJump.Core
{
    [CreateAssetMenu(fileName = "AppsInTossPremiumSettings", menuName = "MukJump/Apps in Toss Premium Settings")]
    public sealed class AppsInTossPremiumSettings : ScriptableObject
    {
        public const string ResourcePath = "MukJump/Settings/AppsInTossPremiumSettings";
        [SerializeField] string removeAdsSku;
        public string RemoveAdsSku => removeAdsSku?.Trim() ?? string.Empty;
#if UNITY_EDITOR
        public void Configure(string sku) => removeAdsSku = sku?.Trim() ?? string.Empty;
#endif
    }
}
