using UnityEngine;
using MukJump.Core;

namespace MukJump.Obstacles
{
    /// 장애물 본체의 명암·알파는 보존하면서 붉은 한지색으로 치환한다.
    /// 구형 씬에 남은 받침과 외곽선은 재생성하지 않고 비활성화한다.
    public sealed class ObstacleVisibilityView : MonoBehaviour
    {
        const string PaperRedShaderName = "MukJump/ObstaclePaperRed";
        const string PaperRedShaderResourcePath = "MukJump/Shaders/ObstaclePaperRed";

        static Material sharedPaperRedMaterial;

        SpriteRenderer bodyRenderer;
        Material defaultSpriteMaterial;
        bool hasCapturedDefaultMaterial;
        SpriteRenderer paperHalo;
        LineRenderer dangerRing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReleaseRuntimeAssets()
        {
            DestroyRuntimeObject(sharedPaperRedMaterial);
            sharedPaperRedMaterial = null;
        }

        public void Configure(bool preserveInkOutlines = false)
        {
            if (bodyRenderer == null) bodyRenderer = GetComponent<SpriteRenderer>();
            DisableLegacyDecorations();
            if (bodyRenderer == null) return;

            if (!hasCapturedDefaultMaterial)
            {
                defaultSpriteMaterial = bodyRenderer.sharedMaterial;
                hasCapturedDefaultMaterial = true;
            }

            Color paperRed = InkPalette.ObstaclePaperRed;
            paperRed.a = bodyRenderer.color.a;
            bodyRenderer.color = paperRed;

            // 용은 밝은 종이색 몸과 검은 먹선을 함께 쓰므로 단순 색 곱으로
            // 검은 외곽선을 보존한다. 어두운 먹가시는 명암 치환 셰이더가 필요하다.
            if (preserveInkOutlines)
            {
                bodyRenderer.sharedMaterial = defaultSpriteMaterial;
                return;
            }

            var material = SharedPaperRedMaterial;
            if (material != null)
                bodyRenderer.sharedMaterial = material;
        }

        public void DisableLegacyDecorations()
        {
            var existing = transform.Find("PaperHalo");
            paperHalo = existing != null ? existing.GetComponent<SpriteRenderer>() : null;
            if (paperHalo != null) paperHalo.enabled = false;
            existing = transform.Find("DangerRing");
            dangerRing = existing != null ? existing.GetComponent<LineRenderer>() : null;
            if (dangerRing != null) dangerRing.enabled = false;
        }

        static Material SharedPaperRedMaterial
        {
            get
            {
                if (sharedPaperRedMaterial != null) return sharedPaperRedMaterial;
                var shader = Resources.Load<Shader>(PaperRedShaderResourcePath);
                if (shader == null) shader = Shader.Find(PaperRedShaderName);
                if (shader == null) return null;
                sharedPaperRedMaterial = new Material(shader)
                {
                    name = "MukJump_ObstaclePaperRed_Shared",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                return sharedPaperRedMaterial;
            }
        }

        static void DestroyRuntimeObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }
    }
}
