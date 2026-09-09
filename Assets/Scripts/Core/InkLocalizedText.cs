using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// Text의 원문을 보존해 동적 수치·재사용 팝업·언어 왕복 전환을 같은 경로로 처리한다.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class InkLocalizedText : MonoBehaviour
    {
        static readonly HashSet<InkLocalizedText> bindings = new();
        [SerializeField, HideInInspector] string source;
        [SerializeField, HideInInspector] string rendered;
        [SerializeField, HideInInspector] bool excluded;
        [SerializeField, HideInInspector] string englishOverride;
        Text target;
        bool applying;
        bool pending;
        GameLanguage renderedLanguage;
        static bool canvasHooked;

        public string SourceText => source;

        public static void Bind(Text text)
        {
            if (text == null) return;
            var binding = text.GetComponent<InkLocalizedText>() ?? text.gameObject.AddComponent<InkLocalizedText>();
            binding.Connect();
        }

        public static void BindTree(Transform root)
        {
            if (root == null) return;
            foreach (Text text in root.GetComponentsInChildren<Text>(true)) Bind(text);
        }

        /// 비활성 Text는 uGUI dirty 알림을 보내지 않으므로 원문 변경을 직접 전달한다.
        /// 페이지를 켜거나 다음 프레임을 기다리지 않고 표시 언어를 확정한다.
        public static void SetSource(Text text, string value)
        {
            if (text == null) return;
            var binding = text.GetComponent<InkLocalizedText>() ?? text.gameObject.AddComponent<InkLocalizedText>();
            if (binding.excluded) { text.text = value; return; }
            if (binding.target == null) binding.Connect();
            if (binding.source == value && text.text == binding.rendered &&
                binding.renderedLanguage == GameLocalization.Language && !binding.pending) return;
            binding.source = value ?? string.Empty;
            binding.Render();
        }

        public static void OverrideEnglish(Text text, string value)
        {
            Bind(text);
            var binding = text.GetComponent<InkLocalizedText>();
            binding.englishOverride = value;
            binding.Refresh();
        }

        /// 닉네임·문의 코드 등 사용자가 소유한 값은 번역하지 않는다.
        public static void Exclude(Text text)
        {
            if (text == null) return;
            var binding = text.GetComponent<InkLocalizedText>();
            if (binding == null)
            {
                string original = text.text;
                binding = text.gameObject.AddComponent<InkLocalizedText>();
                binding.excluded = true;
                text.text = original;
            }
            binding.excluded = true;
            binding.Disconnect();
        }

        void Awake() => Connect();
        void OnEnable() => Connect();
        // 숨긴 로비·재사용 팝업도 언어 변경 대상이다. 수명이 끝날 때만 등록을 해제한다.
        void OnDisable() { }
        void OnDestroy() => Disconnect();

        void Connect()
        {
            if (excluded) return;
            target = GetComponent<Text>();
            if (target == null) return;
            target.UnregisterDirtyVerticesCallback(OnTextDirty);
            target.RegisterDirtyVerticesCallback(OnTextDirty);
            bindings.Add(this);
            if (!canvasHooked)
            {
                Canvas.preWillRenderCanvases += BeforeCanvasRender;
                canvasHooked = true;
            }
            Apply(true);
        }

        void Disconnect()
        {
            if (target != null) target.UnregisterDirtyVerticesCallback(OnTextDirty);
            bindings.Remove(this);
            if (bindings.Count == 0 && canvasHooked)
            {
                Canvas.preWillRenderCanvases -= BeforeCanvasRender;
                canvasHooked = false;
            }
        }

        void OnTextDirty() => Apply(false);
        public void Refresh() => Apply(true);

        void Apply(bool force)
        {
            if (excluded || applying || target == null) return;
            if (CanvasUpdateRegistry.IsRebuildingGraphics()) { pending = true; return; }
            bool changed = target.text != rendered;
            if (!force && !changed && renderedLanguage == GameLocalization.Language) return;
            if (changed || source == null) source = target.text;
            Render();
        }

        void Render()
        {
            if (CanvasUpdateRegistry.IsRebuildingGraphics()) { pending = true; return; }
            applying = true;
            try
            {
                renderedLanguage = GameLocalization.Language;
                rendered = GameLocalization.IsEnglish && !string.IsNullOrEmpty(englishOverride)
                    ? englishOverride : GameLocalization.Translate(source);
                target.text = rendered;
                pending = false;
            }
            finally { applying = false; }
        }

        public static void RefreshAll()
        {
            // 재활성화/도메인 재로드 순서에 관계없이 저장된 씬의 숨은 라벨까지 포함한다.
            // 언어 선택 때만 검색하고 프레임마다 씬 전체를 검색하지 않는다.
            foreach (var binding in FindObjectsByType<InkLocalizedText>(
                         FindObjectsInactive.Include))
                binding.Connect();
            bindings.RemoveWhere(binding => binding == null);
            foreach (var binding in new List<InkLocalizedText>(bindings)) binding.Refresh();
        }

        static void BeforeCanvasRender()
        {
            // LateUpdate보다 늦게 다시 켜진 UI도 첫 렌더 전에 번역한다.
            bindings.RemoveWhere(binding => binding == null);
            foreach (var binding in bindings)
                if (binding != null) binding.Apply(binding.pending);
        }
    }
}
