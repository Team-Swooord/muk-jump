using UnityEngine;
using MukJump.AI;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Player;

namespace MukJump.Items
{
    /// 모든 먹분신이 공유하는 황금 붓 표현 한 묶음.
    /// 효과가 실제 발동한 뒤에만 선 4개와 모트 20개를 만들고 생존자 수와 무관하게 재사용한다.
    [DisallowMultipleComponent]
    public sealed class GoldenBrushEffectView : MonoBehaviour
    {
        const int StrokeCount = 3;
        const int MoteCount = 20;

        StrokeCapture strokeCapture;
        Sprite effectDroplet;
        AudioClip activationClip;
        Transform visualRoot;
        LineRenderer[] strokes;
        LineRenderer aura;
        SpriteRenderer[] motes;
        int ringSegments = 48;
        float ringRadius = 0.78f;
        float wobble = 0.055f;
        bool wasVisible;

        /// 아이템 기능에서만 호출한다. Core의 GameManager는 이 표현 타입을 알지 않는다.
        public static GoldenBrushEffectView Request(StrokeCapture capture, Sprite droplet,
            AudioClip clip, int segments, float radius, float noise)
        {
            var manager = GameManager.Instance;
            if (manager == null || capture == null) return null;

            var service = manager.GetComponent<GoldenBrushEffectView>();
            if (service == null)
                service = manager.gameObject.AddComponent<GoldenBrushEffectView>();
            service.Configure(capture, droplet, clip, segments, radius, noise);
            service.enabled = true;
            return service;
        }

        void OnEnable()
        {
            BindExistingVisuals();
        }

        void OnDisable()
        {
            HideVisuals();
            wasVisible = false;
        }

        void Update()
        {
            var manager = GameManager.Instance;
            PlayerController target = manager != null ? manager.HighestLivingPlayer : null;
            bool visible = manager != null && manager.State == GameState.Playing &&
                           strokeCapture != null && strokeCapture.HasUnlimitedInk &&
                           target != null && !target.IsDead;
            if (!visible)
            {
                HideVisuals();
                wasVisible = false;
                enabled = false;
                return;
            }

            EnsureVisuals();
            visualRoot.position = target.transform.position;
            visualRoot.rotation = target.transform.rotation;
            visualRoot.localScale = target.transform.lossyScale;

            if (!wasVisible)
                VfxAudioManager.Instance?.PlayOneShot(activationClip);
            wasVisible = true;
            AnimateVisuals();
        }

        internal void Configure(StrokeCapture capture, Sprite droplet, AudioClip clip,
            int segments, float radius, float noise)
        {
            strokeCapture = capture;
            if (droplet != null) effectDroplet = droplet;
            if (clip != null) activationClip = clip;
            ringSegments = Mathf.Max(8, segments);
            ringRadius = Mathf.Max(0.1f, radius);
            wobble = Mathf.Max(0f, noise);
            EnsureVisuals();
            for (int i = 0; i < motes.Length; i++)
                motes[i].sprite = effectDroplet;
            aura.positionCount = ringSegments;
        }

        void EnsureVisuals()
        {
            if (visualRoot == null)
            {
                var existing = transform.Find("GoldenBrushSharedVisual");
                if (existing != null)
                    visualRoot = existing;
                else
                {
                    var root = new GameObject("GoldenBrushSharedVisual");
                    root.transform.SetParent(transform, false);
                    visualRoot = root.transform;
                }
            }

            if (strokes == null || strokes.Length != StrokeCount)
                strokes = new LineRenderer[StrokeCount];
            for (int i = 0; i < strokes.Length; i++)
                if (strokes[i] == null)
                    strokes[i] = CreateGoldenStroke(i);

            if (aura == null)
                aura = CreateAura();

            if (motes == null || motes.Length != MoteCount)
                motes = new SpriteRenderer[MoteCount];
            for (int i = 0; i < motes.Length; i++)
                if (motes[i] == null)
                    motes[i] = CreateMote(i);
        }

        void BindExistingVisuals()
        {
            visualRoot = transform.Find("GoldenBrushSharedVisual");
            if (visualRoot == null) return;

            strokes = new LineRenderer[StrokeCount];
            for (int i = 0; i < strokes.Length; i++)
            {
                var child = visualRoot.Find($"GoldenBrushStroke{i + 1}");
                if (child != null) strokes[i] = child.GetComponent<LineRenderer>();
            }

            var auraChild = visualRoot.Find("GoldenBrushAura");
            aura = auraChild != null ? auraChild.GetComponent<LineRenderer>() : null;
            motes = new SpriteRenderer[MoteCount];
            for (int i = 0; i < motes.Length; i++)
            {
                var child = visualRoot.Find($"GoldenMote{i + 1:00}");
                if (child != null) motes[i] = child.GetComponent<SpriteRenderer>();
            }
            HideVisuals();
        }

        LineRenderer CreateGoldenStroke(int index)
        {
            var go = GetOrCreateChild($"GoldenBrushStroke{index + 1}");
            var line = go.GetComponent<LineRenderer>();
            if (line == null) line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 12;
            line.startWidth = 0.055f - index * 0.01f;
            line.endWidth = 0.015f;
            line.numCapVertices = 3;
            line.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
            line.sortingOrder = 8 - index;
            line.enabled = false;
            return line;
        }

        LineRenderer CreateAura()
        {
            var go = GetOrCreateChild("GoldenBrushAura");
            var line = go.GetComponent<LineRenderer>();
            if (line == null) line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = ringSegments;
            line.startWidth = line.endWidth = 0.045f;
            line.numCapVertices = 3;
            line.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
            line.sortingOrder = 9;
            line.enabled = false;
            return line;
        }

        SpriteRenderer CreateMote(int index)
        {
            var go = GetOrCreateChild($"GoldenMote{index + 1:00}");
            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = effectDroplet;
            renderer.color = InkPalette.Gold;
            renderer.sortingOrder = 9;
            renderer.enabled = false;
            return renderer;
        }

        GameObject GetOrCreateChild(string childName)
        {
            var child = visualRoot.Find(childName);
            if (child != null) return child.gameObject;
            var go = new GameObject(childName);
            go.transform.SetParent(visualRoot, false);
            return go;
        }

        void AnimateVisuals()
        {
            for (int i = 0; i < strokes.Length; i++)
            {
                var line = strokes[i];
                line.enabled = true;
                float phase = Time.time * (2.4f + i * 0.35f) + i * 2.1f;
                for (int point = 0; point < line.positionCount; point++)
                {
                    float t = point / (float)(line.positionCount - 1);
                    float x = Mathf.Lerp(-0.62f, 0.62f, t);
                    float y = -0.62f + i * 0.08f +
                              Mathf.Sin(t * Mathf.PI * 2f + phase) * 0.07f;
                    line.SetPosition(point, new Vector3(x, y, 0f));
                }
                Color gold = InkPalette.Gold;
                gold.a = 0.42f + 0.28f * (0.5f + 0.5f * Mathf.Sin(phase));
                line.startColor = line.endColor = gold;
            }

            aura.enabled = true;
            float pulse = 0.52f + Mathf.Sin(Time.time * 4.5f) * 0.06f;
            UpdateRing(aura, pulse, -Time.time * 2.8f);
            Color auraColor = InkPalette.Gold;
            auraColor.a = 0.55f + Mathf.Sin(Time.time * 4.5f) * 0.18f;
            aura.startColor = aura.endColor = auraColor;

            for (int i = 0; i < motes.Length; i++)
            {
                var mote = motes[i];
                mote.enabled = true;
                float angle = i * Mathf.PI * 2f / motes.Length +
                              Time.time * (0.45f + i % 3 * 0.12f);
                float radius = 0.55f + (i % 5) * 0.055f +
                               Mathf.Sin(Time.time * 2f + i) * 0.04f;
                mote.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius,
                    -0.2f + Mathf.Sin(angle) * radius * 0.6f, 0f);
                float scale = 0.035f + (i % 4) * 0.012f;
                mote.transform.localScale = Vector3.one * scale;
                Color color = InkPalette.Gold;
                color.a = 0.35f +
                          0.4f * (0.5f + 0.5f * Mathf.Sin(Time.time * 3f + i));
                mote.color = color;
            }
        }

        void UpdateRing(LineRenderer ring, float radius, float phase)
        {
            if (ring.positionCount != ringSegments)
                ring.positionCount = ringSegments;
            for (int i = 0; i < ringSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / ringSegments;
                float noise = Mathf.Sin(angle * 5f + phase) * wobble +
                              Mathf.Sin(angle * 9f - phase * 0.7f) * wobble * 0.4f;
                float value = radius + noise;
                ring.SetPosition(i,
                    new Vector3(Mathf.Cos(angle) * value, Mathf.Sin(angle) * value, 0f));
            }
        }

        void HideVisuals()
        {
            if (strokes != null)
                for (int i = 0; i < strokes.Length; i++)
                    if (strokes[i] != null) strokes[i].enabled = false;
            if (aura != null) aura.enabled = false;
            if (motes != null)
                for (int i = 0; i < motes.Length; i++)
                    if (motes[i] != null) motes[i].enabled = false;
        }
    }
}
