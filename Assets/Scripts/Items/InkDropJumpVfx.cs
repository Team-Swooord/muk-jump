using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MukJump.Core;

namespace MukJump.Items
{
    /// 먹물방울 50m 점프와 같은 프레임에 재생되는 수묵 연출.
    /// 점프 물리는 건드리지 않고 제공된 스프라이트를 직접 애니메이션한다.
    [RequireComponent(typeof(AudioSource), typeof(SpriteRenderer))]
    public class InkDropJumpVfx : MonoBehaviour
    {
        [Header("먹물방울 VFX 에셋")]
        [SerializeField] Sprite inkDrop;
        [SerializeField] Sprite groundBlob;
        [SerializeField] Sprite inkSplash;
        [SerializeField] Sprite shockRing;
        [SerializeField] Sprite verticalBrush;
        [SerializeField] Sprite brushFibers;
        [SerializeField] Sprite softFlash;
        [SerializeField] Sprite inkStreak;
        [SerializeField] AudioClip immediateClip;

        [Header("연출 조절")]
        [SerializeField, Min(0.1f)] float effectScale = 1f;
        [SerializeField, Range(4, 24)] int sprayCount = 14;
        [SerializeField, Min(0.5f)] float maximumStrokeLength = 15f;

        static readonly Color Ink = new(0.09f, 0.086f, 0.071f, 1f);
        static readonly Color Paper = new(0.933f, 0.894f, 0.804f, 1f);

        readonly List<GameObject> activeRoots = new();
        SpriteRenderer playerRenderer;
        Collider2D playerCollider;
        AudioSource audioSource;

        void Awake()
        {
            playerRenderer = GetComponent<SpriteRenderer>();
            playerCollider = GetComponent<Collider2D>();
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
        }

        public void Play()
        {
            if (!isActiveAndEnabled) return;

            float height = playerRenderer != null ? playerRenderer.bounds.size.y : 1f;
            height = Mathf.Max(0.25f, height) * effectScale;
            Vector3 ground = playerCollider != null
                ? new Vector3(playerCollider.bounds.center.x, playerCollider.bounds.min.y, transform.position.z)
                : transform.position - Vector3.up * height * 0.5f;

            var root = new GameObject("VFX_InkDropJump_Pickup");
            root.transform.position = ground;
            activeRoots.Add(root);
            StartCoroutine(PlaySequence(root, ground, height));

            if (immediateClip != null)
            {
                if (VfxAudioManager.Instance != null)
                    VfxAudioManager.Instance.PlayOneShot(immediateClip);
                else
                    audioSource.PlayOneShot(immediateClip);
            }
        }

        IEnumerator PlaySequence(GameObject root, Vector3 ground, float height)
        {
            var flash = CreateSprite(root.transform, "Impact_CreamFlash", softFlash, Paper, 8);
            var blob = CreateSprite(root.transform, "Impact_GroundBlob", groundBlob, Ink, 5);
            var splash = CreateSprite(root.transform, "Impact_InkSplash", inkSplash, Ink, 6);
            var inkRing = CreateSprite(root.transform, "ShockRing_Ink", shockRing, Ink, 5);
            var paperRing = CreateSprite(root.transform, "ShockRing_Paper", shockRing,
                new Color(Paper.r, Paper.g, Paper.b, 0.4f), 4);
            var brush = CreateSprite(root.transform, "Vertical_Brush", verticalBrush, Ink, 3);
            var fibers = CreateSprite(root.transform, "Vertical_Fibers", brushFibers,
                new Color(Paper.r, Paper.g, Paper.b, 0.24f), 4);

            SetScale(flash, height * 0.15f, height * 0.15f);
            SetScale(blob, height, height * 0.42f);
            SetScale(splash, height * 1.05f, height * 0.48f);
            SetScale(inkRing, height * 0.2f, height * 0.06f);
            SetScale(paperRing, height * 0.18f, height * 0.05f);
            splash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f));
            inkRing.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-7f, 7f));

            for (int i = 0; i < sprayCount; i++)
                StartCoroutine(AnimateSpray(CreateSprite(root.transform, $"InkSpray_{i:00}", inkStreak,
                    Ink, 6), height, i));

            const float duration = 3.55f;
            float elapsed = 0f;
            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                float impactT = Mathf.Clamp01(elapsed / 0.6f);
                float quickT = Mathf.Clamp01(elapsed / 0.28f);

                SetScale(flash, height * Mathf.Lerp(0.15f, 1.55f, EaseOut(impactT)),
                    height * Mathf.Lerp(0.15f, 1.55f, EaseOut(impactT)));
                SetAlpha(flash, 1f - Mathf.Clamp01(elapsed / 0.16f));
                SetScale(blob, height * Mathf.Lerp(0.2f, 1f, EaseOut(quickT)), height * 0.42f);
                SetAlpha(blob, 1f - quickT);
                SetScale(splash, height * Mathf.Lerp(0.25f, 1.15f, EaseOut(quickT)), height * 0.48f);
                SetAlpha(splash, 1f - Mathf.Clamp01(elapsed / 0.26f));
                SetScale(inkRing, height * Mathf.Lerp(0.2f, 2.45f, EaseOut(impactT)), height * 0.27f);
                SetScale(paperRing, height * Mathf.Lerp(0.18f, 2.15f, EaseOut(impactT)), height * 0.24f);
                SetAlpha(inkRing, 1f - impactT);
                SetAlpha(paperRing, 0.4f * (1f - impactT));

                UpdateVerticalStroke(brush, fibers, ground, height, elapsed);
                yield return null;
            }

            activeRoots.Remove(root);
            if (root != null) Destroy(root);
        }

        IEnumerator AnimateSpray(SpriteRenderer sprite, float height, int index)
        {
            float duration = Random.Range(0.55f, 1.25f);
            float speed = Random.Range(1.8f, 5.5f) * height;
            float angle = Random.Range(-28f, 28f) * Mathf.Deg2Rad;
            Vector3 velocity = new(Mathf.Sin(angle) * speed * 0.35f, Mathf.Cos(angle) * speed, 0f);
            float size = Random.Range(0.025f, 0.075f) * height;
            sprite.transform.localPosition = new Vector3((index % 3 - 1) * 0.04f * height, 0f, 0f);
            SetScale(sprite, size, size * Random.Range(2.5f, 5f));

            float elapsed = 0f;
            while (elapsed < duration && sprite != null)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                velocity *= Mathf.Exp(-3.9f * dt);
                velocity += Vector3.down * (1.4f * height * dt);
                sprite.transform.position += velocity * dt;
                SetAlpha(sprite, 1f - Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
        }

        void UpdateVerticalStroke(SpriteRenderer brush, SpriteRenderer fibers, Vector3 ground,
            float height, float elapsed)
        {
            if (brush == null || fibers == null) return;
            float targetY = Mathf.Max(ground.y + 0.05f, transform.position.y - height * 0.45f);
            float length = Mathf.Min(maximumStrokeLength, targetY - ground.y);
            float grow = EaseOut(Mathf.Clamp01(elapsed / 0.43f));
            float shownLength = Mathf.Max(0.01f, length * grow);
            float alpha = elapsed < 1.45f ? 1f : 1f - Mathf.Clamp01((elapsed - 1.45f) / 2.1f);
            Vector3 center = new(ground.x, ground.y + shownLength * 0.5f, ground.z);

            brush.transform.position = center;
            fibers.transform.position = center;
            SetScale(brush, height * 0.28f, shownLength);
            SetScale(fibers, height * 0.28f, shownLength);
            SetAlpha(brush, alpha);
            SetAlpha(fibers, alpha * 0.24f);
        }

        static SpriteRenderer CreateSprite(Transform parent, string name, Sprite sprite, Color color,
            int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        static void SetScale(SpriteRenderer renderer, float worldWidth, float worldHeight)
        {
            if (renderer == null || renderer.sprite == null) return;
            Vector2 size = renderer.sprite.bounds.size;
            if (size.x <= 0f || size.y <= 0f) return;
            renderer.transform.localScale = new Vector3(worldWidth / size.x, worldHeight / size.y, 1f);
        }

        static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            Color color = renderer.color;
            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;
        }

        static float EaseOut(float value) => 1f - (1f - value) * (1f - value);

        void OnDisable()
        {
            StopAllCoroutines();
            for (int i = activeRoots.Count - 1; i >= 0; i--)
                if (activeRoots[i] != null) Destroy(activeRoots[i]);
            activeRoots.Clear();
        }

        void OnValidate()
        {
            effectScale = Mathf.Max(0.1f, effectScale);
            sprayCount = Mathf.Clamp(sprayCount, 4, 24);
            maximumStrokeLength = Mathf.Max(0.5f, maximumStrokeLength);
        }
    }
}
