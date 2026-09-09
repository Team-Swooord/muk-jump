using UnityEngine;
using MukJump.AI;
using MukJump.Core;
using MukJump.Player;

namespace MukJump.Items
{
    /// 먹 방어막을 캐릭터 주변의 살아 움직이는 먹 원으로 표현한다.
    [RequireComponent(typeof(PlayerController))]
    public class ItemEffectView : MonoBehaviour, IRuntimeCloneLifecycle
    {
        const float VitalityHitDuration = 0.24f;
        const float VitalityFlashDuration = 0.06f;

        [SerializeField] int ringSegments = 48;
        [SerializeField] float ringRadius = 0.78f;
        [SerializeField] float wobble = 0.055f;
        [SerializeField] Sprite effectDroplet;
        [SerializeField] AudioClip shieldAnticipationClip;
        [SerializeField] AudioClip shieldImpactClip;
        [SerializeField] AudioClip shieldTailClip;

        PlayerController player;
        LineRenderer outerRing;
        LineRenderer innerRing;
        LineRenderer shieldPulse;
        SpriteRenderer[] shieldMotes;
        SpriteRenderer[] shieldShards;
        Vector3[] shieldShardVelocity;
        Vector3[] shieldShardPosition;
        SpriteRenderer playerRenderer;
        SpriteRenderer vitalityHitPuff;
        bool shieldWasVisible;
        bool shieldShattering;
        uint visualSeed = 0x198DA73F;
        float shieldPulseTime;
        float shieldShardTime;
        float vitalityHitTime;
        readonly System.Collections.Generic.List<Transform> cloneDetachedVisuals = new();

        void Awake()
        {
            player = GetComponent<PlayerController>();
            playerRenderer = GetComponent<SpriteRenderer>();
            RemoveLegacyGoldenVisuals();
            // 런타임에 효과 자식이 만들어진 플레이어를 복제해도 새 분신에는 기존
            // 렌더러가 함께 보이지 않도록 참조만 복구해 숨긴다. 새 오브젝트는 만들지 않는다.
            BindExistingVisuals();
        }

        void OnEnable()
        {
            if (player == null) player = GetComponent<PlayerController>();
            if (playerRenderer == null) playerRenderer = GetComponent<SpriteRenderer>();
            RemoveLegacyGoldenVisuals();
            BindExistingVisuals();
            if (player != null)
            {
                player.ShieldConsumed -= OnShieldConsumed;
                player.ShieldConsumed += OnShieldConsumed;
            }
        }

        void OnDisable()
        {
            if (player != null) player.ShieldConsumed -= OnShieldConsumed;
            if (vitalityHitPuff != null) vitalityHitPuff.enabled = false;
            vitalityHitTime = 0f;
            shieldWasVisible = false;
            shieldShattering = false;
            shieldPulseTime = shieldShardTime = 0f;
            if (outerRing != null) outerRing.enabled = false;
            if (innerRing != null) innerRing.enabled = false;
            if (shieldPulse != null) shieldPulse.enabled = false;
            HideMotes(shieldMotes);
            HideMotes(shieldShards);
        }

        void Update()
        {
            bool visible = player != null && player.HasShield && !player.IsDead &&
                           GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
            if (visible)
                EnsureShieldVisuals();

            if (outerRing != null) outerRing.enabled = visible;
            if (innerRing != null) innerRing.enabled = visible;
            if (visible)
            {
                bool reduced = LobbySettingsProfile.ReducedMotionEnabled;
                UpdateRing(outerRing, ringRadius, reduced ? 0f : Time.time * 2.2f);
                UpdateRing(innerRing, ringRadius * 0.88f, reduced ? 0f : -Time.time * 1.7f);
            }

            if (visible && !shieldWasVisible)
            {
                shieldPulseTime = 0.42f;
                shieldShattering = false;
                VfxAudioManager.Instance?.PlayOneShot(shieldAnticipationClip);
            }
            shieldWasVisible = visible;
            UpdateShieldPulse();
            UpdateShieldMotes(visible);
            UpdateShieldShards();
            UpdateVitalityHit();
        }

        void UpdateShieldPulse()
        {
            if (shieldPulse == null) return;

            if (shieldPulseTime <= 0f)
            {
                shieldPulse.enabled = false;
                return;
            }

            shieldPulseTime = Mathf.Max(0f, shieldPulseTime - Mathf.Min(Time.deltaTime, 0.05f));
            float progress = 1f - Mathf.Clamp01(shieldPulseTime / 0.42f);
            shieldPulse.enabled = true;
            bool reduced = LobbySettingsProfile.ReducedMotionEnabled;
            UpdateRing(shieldPulse, reduced ? ringRadius :
                ringRadius * (shieldShattering
                    ? Mathf.Lerp(1f, 1.65f, 1f - (1f - progress) * (1f - progress))
                    : Mathf.Lerp(1.7f, 1f, 1f - Mathf.Pow(1f - progress, 3f))),
                reduced ? 0f : Time.time * 3f);
            Color color = InkPalette.Ink;
            color.a = InkVfxMotion.TailAlpha(progress, 0.08f) * 0.75f;
            shieldPulse.startWidth = shieldPulse.endWidth =
                0.085f * Mathf.Lerp(1f, 0.16f, progress * progress);
            shieldPulse.startColor = shieldPulse.endColor = color;
        }

        /// 체력을 잃었을 때 캐시된 실루엣 하나를 앞면 종이빛 플래시와 뒤쪽 붉은
        /// 먹 번짐으로 연속 재사용한다. 루트 스케일과 콜라이더는 바꾸지 않는다.
        public void PlayVitalityHit()
        {
            if (player == null || player.IsDead) return;
            EnsureVitalityHitVisual();
            if (vitalityHitPuff == null) return;

            vitalityHitTime = VitalityHitDuration;
            vitalityHitPuff.enabled = true;
            vitalityHitPuff.transform.localPosition = Vector3.zero;
            vitalityHitPuff.transform.localRotation = Quaternion.identity;
            vitalityHitPuff.transform.localScale = Vector3.one;
        }

        void UpdateVitalityHit()
        {
            if (vitalityHitPuff == null || vitalityHitTime <= 0f)
            {
                if (vitalityHitPuff != null) vitalityHitPuff.enabled = false;
                return;
            }

            if (playerRenderer == null)
                playerRenderer = GetComponent<SpriteRenderer>();
            if (playerRenderer == null || playerRenderer.sprite == null)
            {
                vitalityHitTime = 0f;
                vitalityHitPuff.enabled = false;
                return;
            }

            vitalityHitTime = Mathf.Max(
                0f,
                vitalityHitTime - Mathf.Min(Time.unscaledDeltaTime, 0.05f));
            float progress = 1f - vitalityHitTime / VitalityHitDuration;
            vitalityHitPuff.sprite = playerRenderer.sprite;
            vitalityHitPuff.flipX = playerRenderer.flipX;
            vitalityHitPuff.flipY = playerRenderer.flipY;
            vitalityHitPuff.sharedMaterial = playerRenderer.sharedMaterial;
            vitalityHitPuff.sortingLayerID = playerRenderer.sortingLayerID;
            float flashRatio = VitalityFlashDuration / VitalityHitDuration;
            if (progress <= flashRatio)
            {
                float flashProgress = Mathf.Clamp01(progress / flashRatio);
                vitalityHitPuff.sortingOrder = playerRenderer.sortingOrder + 2;
                vitalityHitPuff.transform.localScale = new Vector3(
                    Mathf.Lerp(1.2f, 0.96f, flashProgress),
                    Mathf.Lerp(0.86f, 1.12f, flashProgress),
                    1f);
                Color flash = Color.Lerp(
                    InkPalette.Paper,
                    InkPalette.Red,
                    flashProgress * 0.34f);
                flash.a = Mathf.Lerp(0.96f, 0.82f, flashProgress);
                vitalityHitPuff.color = flash;
            }
            else
            {
                float tailProgress = Mathf.Clamp01(
                    (progress - flashRatio) / (1f - flashRatio));
                float eased = 1f - Mathf.Pow(1f - tailProgress, 3f);
                vitalityHitPuff.sortingOrder = playerRenderer.sortingOrder - 1;
                vitalityHitPuff.transform.localScale = new Vector3(
                    Mathf.Lerp(1.02f, 1.35f, eased),
                    Mathf.Lerp(1.08f, 1.22f, eased),
                    1f);
                Color spread = Color.Lerp(
                    InkPalette.Red,
                    InkPalette.Ink,
                    tailProgress * 0.55f);
                spread.a = Mathf.Lerp(0.68f, 0f, tailProgress);
                vitalityHitPuff.color = spread;
            }
            if (vitalityHitTime <= 0f)
                vitalityHitPuff.enabled = false;
            if (LobbySettingsProfile.ReducedMotionEnabled)
            {
                vitalityHitPuff.transform.localScale = Vector3.one;
                vitalityHitPuff.sortingOrder = playerRenderer.sortingOrder + 1;
                Color quietHit = InkPalette.Red;
                quietHit.a = 0.55f * (1f - progress);
                vitalityHitPuff.color = quietHit;
            }
        }

        void UpdateShieldMotes(bool visible)
        {
            if (shieldMotes == null) return;

            bool gathering = !shieldShattering && shieldPulseTime > 0.08f;
            int visibleCount = VfxQualityRuntime.Profile.ScaleDecorativeCount(
                gathering ? shieldMotes.Length : Mathf.Min(player.IsRuntimeClone ? 2 : 5, shieldMotes.Length),
                Mathf.Min(2, shieldMotes.Length));
            for (int i = 0; i < shieldMotes.Length; i++)
            {
                var mote = shieldMotes[i];
                if (mote == null) continue;
                bool showMote = visible && i < visibleCount &&
                    !LobbySettingsProfile.ReducedMotionEnabled;
                mote.enabled = showMote;
                if (!showMote) continue;
                // 품질 강등·획득 종료로 개수가 줄어도 살아남은 알갱이의 위치는 유지한다.
                float angle = i * 2.399963f +
                              Time.time * (0.6f + i % 2 * 0.14f);
                float radius = ringRadius + Mathf.Sin(Time.time * 1.8f + i) * 0.08f;
                float gather = Mathf.Clamp01(1f - shieldPulseTime / 0.42f);
                if (gathering)
                {
                    float eased = 1f - Mathf.Pow(1f - gather, 3f);
                    radius = ringRadius * Mathf.Lerp(1.85f, 1f, eased);
                    angle += (1f - eased) * 0.5f;
                }
                mote.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius * 0.9f, 0f);
                float size = gathering ? 0.13f + i % 3 * 0.025f : 0.09f;
                SetMoteSize(mote, size, size);
                Color color = InkPalette.Ink;
                color.a = gathering ? 0.8f : 0.42f;
                if (gathering && i >= VfxQualityRuntime.Profile.ScaleDecorativeCount(
                    Mathf.Min(player.IsRuntimeClone ? 2 : 5, shieldMotes.Length), 2))
                    color.a *= Mathf.InverseLerp(0.08f, 0.18f, shieldPulseTime);
                mote.color = color;
            }
        }

        void OnShieldConsumed()
        {
            EnsureShieldVisuals();
            shieldPulseTime = 0.42f;
            shieldShardTime = 0.7f;
            shieldShattering = true;
            int visibleShardCount = LobbySettingsProfile.ReducedMotionEnabled ? 0 :
                VfxQualityRuntime.Profile.ScaleDecorativeCount(
                shieldShards.Length,
                Mathf.Min(4, shieldShards.Length));
            for (int i = 0; i < shieldShards.Length; i++)
            {
                if (shieldShards[i] == null) continue;
                if (i >= visibleShardCount)
                {
                    shieldShards[i].enabled = false;
                    continue;
                }
                float angle = (i * 360f / Mathf.Max(1, visibleShardCount) + Range(-12f, 12f)) * Mathf.Deg2Rad;
                float speed = Range(1.5f, 3.6f);
                shieldShardVelocity[i] = new Vector3(Mathf.Cos(angle) * speed,
                    Mathf.Sin(angle) * speed + 0.65f, 0f);
                shieldShardPosition[i] = transform.TransformPoint(new Vector3(
                    Mathf.Cos(angle) * ringRadius, Mathf.Sin(angle) * ringRadius, 0f));
                shieldShards[i].transform.position = shieldShardPosition[i];
                SetMoteSize(shieldShards[i], Range(0.18f, 0.3f), Range(0.08f, 0.14f));
                shieldShards[i].transform.localRotation = Quaternion.Euler(0f, 0f,
                    angle * Mathf.Rad2Deg + 90f);
                shieldShards[i].color = InkPalette.Ink;
                shieldShards[i].enabled = true;
            }
            VfxAudioManager.Instance?.PlayOneShot(shieldImpactClip != null
                ? shieldImpactClip : shieldTailClip);
        }

        void UpdateShieldShards()
            => AdvanceShieldShards(Mathf.Min(Time.deltaTime, 0.05f));

        void AdvanceShieldShards(float delta)
        {
            if (shieldShards == null || shieldShardVelocity == null) return;

            if (shieldShardTime <= 0f || LobbySettingsProfile.ReducedMotionEnabled)
            {
                shieldShardTime = 0f;
                HideMotes(shieldShards);
                return;
            }

            shieldShardTime = Mathf.Max(0f, shieldShardTime - delta);
            float progress = 1f - Mathf.Clamp01(shieldShardTime / 0.7f);
            int limit = VfxQualityRuntime.Profile.ScaleDecorativeCount(shieldShards.Length, Mathf.Min(4, shieldShards.Length));
            for (int i = 0; i < shieldShards.Length; i++)
            {
                if (shieldShards[i] == null) continue;
                if (i >= limit) shieldShards[i].enabled = false;
                if (!shieldShards[i].enabled) continue;
                // 부모 계층은 복제 수명 관리용으로 유지하되 파편 자체는 월드에 남긴다.
                InkVfxMotion.Integrate(ref shieldShardPosition[i], ref shieldShardVelocity[i],
                    Vector3.down * 2.2f, 2.8f, delta);
                shieldShards[i].transform.position = shieldShardPosition[i];
                shieldShards[i].transform.rotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(shieldShardVelocity[i].y, shieldShardVelocity[i].x) * Mathf.Rad2Deg);
                Color color = InkPalette.Ink;
                // 조각별 마지막 박자를 조금씩 달리해 고리 전체가 한 번에 꺼지지 않게 한다.
                color.a = InkVfxMotion.TailAlpha(Mathf.Clamp01(progress / (0.78f + i % 4 * 0.073f)), 0.12f);
                shieldShards[i].color = color;
            }
        }

        /// 방어막을 실제로 얻거나 소모한 순간에만 관련 렌더러를 준비한다.
        /// 기존 자식이 있으면 Create*가 재사용하므로 여러 번 호출해도 중복 생성되지 않는다.
        void EnsureShieldVisuals()
        {
            if (outerRing == null) outerRing = CreateRing("InkShieldOuter", 7, 0.105f);
            if (innerRing == null) innerRing = CreateRing("InkShieldInner", 6, 0.052f);
            if (shieldPulse == null) shieldPulse = CreateRing("InkShieldPulse", 8, 0.085f);
            // 먹떼의 모든 개체가 원본과 같은 29개 입자를 캐시하면 24마리에서
            // 696개 자식이 생긴다. 분신은 실루엣을 읽는 데 필요한 최소 밀도만 사용한다.
            int moteCount = player != null && player.IsRuntimeClone ? 4 : 11;
            int shardCount = player != null && player.IsRuntimeClone ? 6 : 18;
            if (NeedsRenderers(shieldMotes, moteCount))
                shieldMotes = CreateMotes("ShieldMote", moteCount, InkPalette.Ink, 8);
            if (NeedsRenderers(shieldShards, shardCount))
                shieldShards = CreateMotes("ShieldShard", shardCount, InkPalette.Ink, 10);
            if (shieldShardVelocity == null ||
                shieldShardVelocity.Length != shieldShards.Length)
                shieldShardVelocity = new Vector3[shieldShards.Length];
            if (shieldShardPosition == null || shieldShardPosition.Length != shieldShards.Length)
                shieldShardPosition = new Vector3[shieldShards.Length];
        }

        /// 이미 효과가 만들어진 원본을 복제한 경우 자식 렌더러 참조만 되찾는다.
        /// 일반 시작 시에는 찾을 자식이 없으므로 Hierarchy를 전혀 늘리지 않는다.
        void BindExistingVisuals()
        {
            outerRing = FindChildComponent<LineRenderer>("InkShieldOuter");
            innerRing = FindChildComponent<LineRenderer>("InkShieldInner");
            shieldPulse = FindChildComponent<LineRenderer>("InkShieldPulse");
            if (outerRing != null) outerRing.enabled = false;
            if (innerRing != null) innerRing.enabled = false;
            if (shieldPulse != null) shieldPulse.enabled = false;

            shieldMotes = FindExistingMotes("ShieldMote", 11);
            shieldShards = FindExistingMotes("ShieldShard", 18);
            if (shieldShards != null)
                shieldShardVelocity = new Vector3[shieldShards.Length];
            vitalityHitPuff =
                FindChildComponent<SpriteRenderer>("GrowthVitalityPuff");
            if (vitalityHitPuff != null)
                vitalityHitPuff.enabled = false;
        }

        /// 먹분신은 플레이어의 게임 상태만 복제해야 한다. 한 번 생성된 방어막 표현 캐시와
        /// hot reload 전에 남은 구형 황금 효과를 Instantiate 대상에서 잠시 제외한다.
        internal void DetachRuntimeVisualsForClone(System.Collections.Generic.List<Transform> buffer)
        {
            if (buffer == null) return;
            buffer.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (!IsRuntimeVisual(child.name)) continue;
                buffer.Add(child);
                child.SetParent(null, true);
            }
        }

        internal void RestoreRuntimeVisualsAfterClone(
            System.Collections.Generic.List<Transform> buffer)
        {
            if (buffer == null) return;
            for (int i = 0; i < buffer.Count; i++)
                if (buffer[i] != null)
                    buffer[i].SetParent(transform, true);
            buffer.Clear();
        }

        public void PrepareForRuntimeClone()
        {
            DetachRuntimeVisualsForClone(cloneDetachedVisuals);
        }

        public void RestoreAfterRuntimeClone()
        {
            RestoreRuntimeVisualsAfterClone(cloneDetachedVisuals);
        }

        static bool IsRuntimeVisual(string objectName)
        {
            return objectName.StartsWith("InkShield") ||
                   objectName.StartsWith("GoldenBrush") ||
                   objectName.StartsWith("GoldenMote") ||
                   objectName.StartsWith("ShieldMote") ||
                   objectName.StartsWith("ShieldShard") ||
                   objectName.StartsWith("GrowthVitalityPuff");
        }

        /// 공유 황금 효과로 전환하기 전 Play 세션의 자식이 hot reload 뒤 남아 있으면
        /// 즉시 숨기고 한 번만 정리해 플레이어별 구형 캐시가 누적되지 않게 한다.
        void RemoveLegacyGoldenVisuals()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (!child.name.StartsWith("GoldenBrush") &&
                    !child.name.StartsWith("GoldenMote"))
                    continue;
                child.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        SpriteRenderer[] FindExistingMotes(string prefix, int count)
        {
            SpriteRenderer[] existing = null;
            for (int i = 0; i < count; i++)
            {
                var renderer = FindChildComponent<SpriteRenderer>($"{prefix}{i + 1:00}");
                if (renderer == null) continue;
                existing ??= new SpriteRenderer[count];
                existing[i] = renderer;
                renderer.enabled = false;
            }
            return existing;
        }

        T FindChildComponent<T>(string objectName) where T : Component
        {
            var child = transform.Find(objectName);
            return child != null ? child.GetComponent<T>() : null;
        }

        static bool NeedsRenderers(SpriteRenderer[] renderers, int count)
        {
            if (renderers == null || renderers.Length != count) return true;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] == null)
                    return true;
            return false;
        }

        static void HideMotes(SpriteRenderer[] renderers)
        {
            if (renderers == null) return;
            foreach (var value in renderers)
                if (value != null) value.enabled = false;
        }

        void SetMoteSize(SpriteRenderer renderer, float width, float height)
        {
            Vector2 art = renderer.sprite != null ? (Vector2)renderer.sprite.bounds.size : Vector2.one;
            Vector3 parentScale = transform.lossyScale;
            renderer.transform.localScale = new Vector3(
                width / Mathf.Max(0.001f, art.x * Mathf.Abs(parentScale.x)),
                height / Mathf.Max(0.001f, art.y * Mathf.Abs(parentScale.y)), 1f);
        }

        float Range(float min, float max)
        {
            visualSeed ^= visualSeed << 13; visualSeed ^= visualSeed >> 17; visualSeed ^= visualSeed << 5;
            return Mathf.Lerp(min, max, (visualSeed & 0xFFFFFF) / 16777216f);
        }

        void EnsureVitalityHitVisual()
        {
            if (vitalityHitPuff != null) return;

            var child = transform.Find("GrowthVitalityPuff");
            var visualObject = child != null
                ? child.gameObject
                : new GameObject("GrowthVitalityPuff");
            if (child == null)
                visualObject.transform.SetParent(transform, false);
            vitalityHitPuff = visualObject.GetComponent<SpriteRenderer>();
            if (vitalityHitPuff == null)
                vitalityHitPuff = visualObject.AddComponent<SpriteRenderer>();
            vitalityHitPuff.enabled = false;
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
            ring.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
            var color = InkPalette.Ink;
            color.a = objectName.EndsWith("Outer") ? 0.72f : 0.32f;
            ring.startColor = ring.endColor = color;
            ring.sortingOrder = sortingOrder;
            ring.enabled = false;
            return ring;
        }

        void UpdateRing(LineRenderer ring, float radius, float phase)
        {
            if (ring == null) return;

            int visibleSegments = Mathf.Min(
                ringSegments,
                VfxQualityRuntime.Profile.PersistentRingSegments);
            if (ring.positionCount != visibleSegments)
                ring.positionCount = visibleSegments;
            for (int i = 0; i < visibleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / visibleSegments;
                float noise = Mathf.Sin(angle * 5f + phase) * wobble +
                              Mathf.Sin(angle * 9f - phase * 0.7f) * wobble * 0.4f;
                float r = radius + noise;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f));
            }
        }
    }
}
