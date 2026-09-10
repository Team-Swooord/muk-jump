using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 한글·영문 먹글씨 원화를 교체한다. 숨은 로비와 첫 로딩 프레임도 즉시 반영한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public sealed class InkLocalizedGameLogo : MonoBehaviour
    {
        public const string EnglishResourcePath = "MukJump/UI/Common/muk_logo_en";
        public const string EnglishAssetPath = "Assets/Resources/" + EnglishResourcePath + ".png";

        // MUK JUMP와 길게 뻗은 J 윗획의 실제 먹획 폭·중심을 한글판과 맞춘다.
        // 기존 로비 RectTransform과 한글 원화의 배치는 바꾸지 않는다.
        const float EnglishUvScale = 1351f / 1044f;
        public static readonly Rect EnglishUvRect = new Rect(
            (768.5f - 805f * EnglishUvScale) / 1536f,
            (500.5f - 529.5f * EnglishUvScale) / 1024f,
            EnglishUvScale, EnglishUvScale);

        [SerializeField] Texture koreanTexture;
        [SerializeField] Rect koreanUvRect = new Rect(0f, 0f, 1f, 1f);
        RawImage target;
        static Texture2D englishTexture;
        static readonly HashSet<InkLocalizedGameLogo> bindings = new();

        public static void Bind(RawImage image, Texture korean = null)
        {
            if (image == null) return;
            var binding = image.GetComponent<InkLocalizedGameLogo>() ??
                image.gameObject.AddComponent<InkLocalizedGameLogo>();
            binding.target = image;
            if (korean != null) binding.koreanTexture = korean;
            binding.Refresh();
        }

        void Awake() => Refresh();
        void OnEnable() => Refresh();
        // 숨은 로비도 언어 변경 대상이므로 파괴될 때만 등록을 해제한다.
        void OnDestroy() => bindings.Remove(this);

        public void Refresh()
        {
            if (target == null) target = GetComponent<RawImage>();
            if (target == null) return;
            bindings.Add(this);
            if (koreanTexture == null)
            {
                koreanTexture = target.texture;
                koreanUvRect = target.uvRect;
            }
#if UNITY_EDITOR
            // 원화 재임포트 뒤 살아 있는 이전 참조 대신 현재 리소스를 다시 받는다.
            englishTexture = Resources.Load<Texture2D>(EnglishResourcePath);
#else
            if (englishTexture == null)
                englishTexture = Resources.Load<Texture2D>(EnglishResourcePath);
#endif
            bool english = GameLocalization.Language != GameLanguage.Korean && englishTexture != null;
            target.texture = english ? englishTexture : koreanTexture;
            target.uvRect = english ? EnglishUvRect : koreanUvRect;
        }

        public static void RefreshAll()
        {
            // 재로드된 씬은 검색으로 복구하고 이미 바인딩한 비활성/격리 로비는
            // 등록 목록으로 갱신한다. 씬 검색에서 누락돼도 첫 노출을 기다리지 않는다.
            foreach (var binding in FindObjectsByType<InkLocalizedGameLogo>(FindObjectsInactive.Include))
                bindings.Add(binding);
            bindings.RemoveWhere(binding => binding == null);
            foreach (var binding in new List<InkLocalizedGameLogo>(bindings))
                binding.Refresh();
        }
    }
}
