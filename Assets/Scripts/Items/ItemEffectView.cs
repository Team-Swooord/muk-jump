using UnityEngine;
using MukJump.AI;
using MukJump.Core;
using MukJump.Player;
using MukJump.Drawing;

namespace MukJump.Items
{
    /// 먹 방어막을 캐릭터 주변의 살아 움직이는 먹 원으로 표현한다.
    [RequireComponent(typeof(PlayerController))]
    public class ItemEffectView : MonoBehaviour
    {
        [SerializeField] int ringSegments = 48;
        [SerializeField] float ringRadius = 0.78f;
        [SerializeField] float wobble = 0.055f;
        [SerializeField] Sprite effectDroplet;
        [SerializeField] AudioClip goldenBrushFullClip;
        [SerializeField] AudioClip shieldAnticipationClip;
        [SerializeField] AudioClip shieldImpactClip;
        [SerializeField] AudioClip shieldTailClip;

        PlayerController player;
        LineRenderer outerRing;
        LineRenderer innerRing;
        LineRenderer shieldPulse;
        LineRenderer[] goldenStrokes;
        LineRenderer goldenAura;
        SpriteRenderer[] goldenMotes;
        SpriteRenderer[] shieldMotes;
        SpriteRenderer[] shieldShards;
        Vector3[] shieldShardVelocity;
        StrokeCapture strokeCapture;
        bool shieldWasVisible;
        float shieldPulseTime;
        bool goldenWasVisible;
        float shieldShardTime;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            outerRing = CreateRing("InkShieldOuter", 7, 0.105f);
            innerRing = CreateRing("InkShieldInner", 6, 0.052f);
            shieldPulse = CreateRing("InkShieldPulse", 8, 0.085f);
            goldenStrokes = new LineRenderer[3];
            for (int i = 0; i < goldenStrokes.Length; i++)
                goldenStrokes[i] = CreateGoldenStroke(i);
            goldenAura = CreateRing("GoldenBrushAura", 9, 0.045f);
            goldenMotes = CreateMotes("GoldenMote", 20, InkPalette.Gold, 9);
            shieldMotes = CreateMotes("ShieldMote", 11, InkPalette.Ink, 8);
            shieldShards = CreateMotes("ShieldShard", 18, InkPalette.Ink, 10);
            shieldShardVelocity = new Vector3[shieldShards.Length];
        }

        void Start()
        {
            strokeCapture = FindFirstObjectByType<StrokeCapture>();
            player.ShieldConsumed += OnShieldConsumed;
        }

        void OnDestroy()
        {
            if (player != null) player.ShieldConsumed -= OnShieldConsumed;
        }

        void Update()
        {
            bool visible = player != null && player.HasShield && !player.IsDead &&
                           GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
            outerRing.enabled = visible;
            innerRing.enabled = visible;
            if (visible)
            {
                UpdateRing(outerRing, ringRadius, Time.time * 2.2f);
                UpdateRing(innerRing, ringRadius * 0.88f, -Time.time * 1.7f);
            }

            if (visible && !shieldWasVisible)
            {
                shieldPulseTime = 0.42f;
                VfxAudioManager.Instance?.PlayOneShot(shieldAnticipationClip);
            }
            shieldWasVisible = visible;
            UpdateShieldPulse();
            UpdateGoldenBrush();
            UpdateShieldMotes(visible);
            UpdateShieldShards();

        }

        void UpdateShieldPulse()
        {
            if (shieldPulseTime <= 0f)
            {
                shieldPulse.enabled = false;
                return;
            }

            shieldPulseTime -= Time.deltaTime;
            float progress = 1f - Mathf.Clamp01(shieldPulseTime / 0.42f);
            shieldPulse.enabled = true;
            UpdateRing(shieldPulse, Mathf.Lerp(ringRadius * 0.7f, ringRadius * 1.45f, progress),
                Time.time * 3f);
            Color color = InkPalette.Ink;
            color.a = (1f - progress) * 0.75f;
            shieldPulse.startColor = shieldPulse.endColor = color;
        }

        void UpdateGoldenBrush()
        {
            bool visible = strokeCapture != null && strokeCapture.HasUnlimitedInk && !player.IsDead &&
                           GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
            if (visible && !goldenWasVisible)
                VfxAudioManager.Instance?.PlayOneShot(goldenBrushFullClip);
            goldenWasVisible = visible;
            for (int i = 0; i < goldenStrokes.Length; i++)
            {
                var line = goldenStrokes[i];
                line.enabled = visible;
                if (!visible) continue;

                float phase = Time.time * (2.4f + i * 0.35f) + i * 2.1f;
                for (int point = 0; point < line.positionCount; point++)
                {
                    float t = point / (float)(line.positionCount - 1);
                    float x = Mathf.Lerp(-0.62f, 0.62f, t);
                    float y = -0.62f + i * 0.08f + Mathf.Sin(t * Mathf.PI * 2f + phase) * 0.07f;
                    line.SetPosition(point, new Vector3(x, y, 0f));
                }
                Color gold = InkPalette.Gold;
                gold.a = 0.42f + 0.28f * (0.5f + 0.5f * Mathf.Sin(phase));
                line.startColor = line.endColor = gold;
            }

            goldenAura.enabled = visible;
            if (visible)
            {
                float pulse = 0.52f + Mathf.Sin(Time.time * 4.5f) * 0.06f;
                UpdateRing(goldenAura, pulse, -Time.time * 2.8f);
                Color auraColor = InkPalette.Gold;
                auraColor.a = 0.55f + Mathf.Sin(Time.time * 4.5f) * 0.18f;
                goldenAura.startColor = goldenAura.endColor = auraColor;
            }
            for (int i = 0; i < goldenMotes.Length; i++)
            {
                var mote = goldenMotes[i];
                mote.enabled = visible;
                if (!visible) continue;
                float angle = i * Mathf.PI * 2f / goldenMotes.Length + Time.time * (0.45f + i % 3 * 0.12f);
                float radius = 0.55f + (i % 5) * 0.055f + Mathf.Sin(Time.time * 2f + i) * 0.04f;
                mote.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius,
                    -0.2f + Mathf.Sin(angle) * radius * 0.6f, 0f);
                float scale = 0.035f + (i % 4) * 0.012f;
                mote.transform.localScale = Vector3.one * scale;
                Color color = InkPalette.Gold;
                color.a = 0.35f + 0.4f * (0.5f + 0.5f * Mathf.Sin(Time.time * 3f + i));
                mote.color = color;
            }
        }

        void UpdateShieldMotes(bool visible)
        {
            for (int i = 0; i < shieldMotes.Length; i++)
            {
                var mote = shieldMotes[i];
                mote.enabled = visible;
                if (!visible) continue;
                float angle = i * Mathf.PI * 2f / shieldMotes.Length + Time.time * (0.6f + i % 2 * 0.14f);
                float radius = ringRadius + Mathf.Sin(Time.time * 1.8f + i) * 0.08f;
                mote.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius * 0.9f, 0f);
                mote.transform.localScale = Vector3.one * (0.055f + i % 3 * 0.018f);
            }
        }

        void OnShieldConsumed()
        {
            shieldPulseTime = 0.42f;
            shieldShardTime = 0.7f;
            for (int i = 0; i < shieldShards.Length; i++)
            {
                float angle = Random.Range(-25f, 205f) * Mathf.Deg2Rad;
                float speed = Random.Range(1.8f, 4.8f);
                shieldShardVelocity[i] = new Vector3(Mathf.Cos(angle) * speed,
                    Mathf.Sin(angle) * speed, 0f);
                shieldShards[i].transform.localPosition = Vector3.zero;
                shieldShards[i].transform.localScale = new Vector3(
                    Random.Range(0.05f, 0.11f), Random.Range(0.025f, 0.055f), 1f);
                shieldShards[i].enabled = true;
            }
            VfxAudioManager.Instance?.PlayOneShot(shieldImpactClip);
            VfxAudioManager.Instance?.PlayOneShot(shieldTailClip, 0.72f);
        }

        void UpdateShieldShards()
        {
            if (shieldShardTime <= 0f)
            {
                for (int i = 0; i < shieldShards.Length; i++) shieldShards[i].enabled = false;
                return;
            }

            shieldShardTime -= Time.deltaTime;
            float alpha = Mathf.Clamp01(shieldShardTime / 0.7f);
            for (int i = 0; i < shieldShards.Length; i++)
            {
                shieldShardVelocity[i] += Vector3.down * (2.2f * Time.deltaTime);
                shieldShardVelocity[i] *= Mathf.Exp(-2.8f * Time.deltaTime);
                shieldShards[i].transform.localPosition += shieldShardVelocity[i] * Time.deltaTime;
                Color color = InkPalette.Ink;
                color.a = alpha;
                shieldShards[i].color = color;
            }
        }

        SpriteRenderer[] CreateMotes(string prefix, int count, Color color, int sortingOrder)
        {
            var motes = new SpriteRenderer[count];
            for (int i = 0; i < count; i++)
            {
                string objectName = $"{prefix}{i + 1:00}";
                var child = transform.Find(objectName);
                var go = child != null ? child.gameObject : new GameObject(objectName);
                if (child == null) go.transform.SetParent(transform, false);
                var renderer = go.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = effectDroplet;
                renderer.color = color;
                renderer.sortingOrder = sortingOrder;
                renderer.enabled = false;
                motes[i] = renderer;
            }
            return motes;
        }

        LineRenderer CreateRing(string objectName, int sortingOrder, float width)
        {
            var child = transform.Find(objectName);
            var go = child != null ? child.gameObject : new GameObject(objectName);
            if (child == null) go.transform.SetParent(transform, false);
            var ring = go.GetComponent<LineRenderer>();
            if (ring == null) ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = ringSegments;
            ring.startWidth = ring.endWidth = width;
            ring.numCapVertices = 3;
            ring.material = FallbackInkStyle.SharedInkMaterial;
            var color = InkPalette.Ink;
            color.a = objectName.EndsWith("Outer") ? 0.72f : 0.32f;
            ring.startColor = ring.endColor = color;
            ring.sortingOrder = sortingOrder;
            ring.enabled = false;
            return ring;
        }

        LineRenderer CreateGoldenStroke(int index)
        {
            string objectName = $"GoldenBrushStroke{index + 1}";
            var child = transform.Find(objectName);
            var go = child != null ? child.gameObject : new GameObject(objectName);
            if (child == null) go.transform.SetParent(transform, false);
            var line = go.GetComponent<LineRenderer>();
            if (line == null) line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 12;
            line.startWidth = 0.055f - index * 0.01f;
            line.endWidth = 0.015f;
            line.numCapVertices = 3;
            line.material = FallbackInkStyle.SharedInkMaterial;
            line.sortingOrder = 8 - index;
            line.enabled = false;
            return line;
        }

        void UpdateRing(LineRenderer ring, float radius, float phase)
        {
            for (int i = 0; i < ringSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / ringSegments;
                float noise = Mathf.Sin(angle * 5f + phase) * wobble +
                              Mathf.Sin(angle * 9f - phase * 0.7f) * wobble * 0.4f;
                float r = radius + noise;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f));
            }
        }
    }
}
