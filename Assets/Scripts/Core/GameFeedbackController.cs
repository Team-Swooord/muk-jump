using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MukJump.AI;
using MukJump.Items;

namespace MukJump.Core
{
    /// 점프·착지·드로잉·아이템 등 순간 피드백을 한곳에서 관리한다.
    /// 외부 음원 없이 짧은 효과음을 런타임에 합성해 API 키나 추가 에셋 없이 동작한다.
    public class GameFeedbackController : MonoBehaviour
    {
        public static GameFeedbackController Instance { get; private set; }

        AudioClip jumpClip;
        AudioClip landingClip;
        AudioClip drawClip;
        AudioClip invalidClip;
        AudioClip itemClip;
        AudioClip milestoneClip;
        AudioClip brushLoopClip;
        AudioClip brushTransitionClip;
        AudioClip wallHitClip;
        AudioClip deathSqueakClip;
        AudioClip gameOverClip;
        AudioSource brushSource;
        AudioSource accentSource;
        Coroutine gameOverSoundRoutine;
        Sprite dotSprite;
        Canvas overlayCanvas;
        Text bannerText;
        Coroutine bannerRoutine;

        void OnEnable()
        {
            Instance = this;
            EnsureInitialized();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void Awake()
        {
            EnsureInitialized();
        }

        void EnsureInitialized()
        {
            if (jumpClip != null && brushSource != null && accentSource != null) return;

            jumpClip = CreateTone("JumpBrush", 0.16f, 240f, 520f, 0.18f, 0.04f);
            landingClip = CreateTone("LandingInk", 0.13f, 150f, 82f, 0.24f, 0.18f);
            drawClip = CreateTone("DrawSet", 0.1f, 390f, 320f, 0.12f, 0.08f);
            invalidClip = CreateTone("InvalidStroke", 0.12f, 170f, 125f, 0.16f, 0.2f);
            itemClip = CreateTone("ItemPickup", 0.22f, 420f, 760f, 0.16f, 0.03f);
            milestoneClip = CreateTone("MilestoneSeal", 0.34f, 220f, 440f, 0.2f, 0.08f);
            brushLoopClip = LoadSfx("SFX_Brush_Community") ??
                            LoadSfx("SFX_Brush_Draw_Loop") ??
                            CreateBrushNoise("BrushDrawing", 0.42f, 0.16f);
            brushTransitionClip = LoadSfx("SFX_Brush_Community") ??
                                  LoadSfx("SFX_Brush_Transition") ??
                                  CreateBrushNoise("BrushTransition", 1.15f, 0.3f, true);
            wallHitClip = LoadSfx("SFX_Wall_Hit") ??
                          CreateTone("WallHit", 0.11f, 120f, 72f, 0.28f, 0.32f);
            deathSqueakClip = LoadSfx("SFX_Character_Death_Slime") ??
                              LoadSfx("SFX_Character_Death") ??
                              CreateTone("DeathSqueak", 0.32f, 1080f, 185f, 0.68f, 0.025f);
            gameOverClip = LoadSfx("SFX_Game_Over_Ink_Spill") ??
                           LoadSfx("SFX_Game_Over") ??
                           CreateTone("GameOver", 0.58f, 310f, 92f, 0.42f, 0.08f);
            CreateDedicatedAudioSources();
            if (dotSprite == null) dotSprite = CreateDotSprite();
            if (bannerText == null)
            {
                var existingBanner = transform.Find("FeedbackOverlay/ZoneBanner");
                if (existingBanner != null)
                {
                    bannerText = existingBanner.GetComponent<Text>();
                    overlayCanvas = existingBanner.GetComponentInParent<Canvas>();
                }
                else
                    CreateOverlay();
            }
        }

        public void StartBrushDrawing()
        {
            EnsureInitialized();
            if (brushSource == null || brushLoopClip == null || brushSource.isPlaying) return;
            brushSource.volume = 0.28f;
            brushSource.pitch = Random.Range(0.94f, 1.04f);
            if (brushSource.timeSamples > 0)
                brushSource.UnPause();
            else
                brushSource.Play();
        }

        public void PlayBrushMovement(float movement)
        {
            EnsureInitialized();
            if (brushSource == null || brushLoopClip == null) return;
            brushSource.volume = Mathf.Lerp(0.24f, 0.4f, Mathf.Clamp01(movement / 0.5f));
            brushSource.pitch = Mathf.Lerp(0.9f, 1.12f, Mathf.Clamp01(movement / 0.5f));
        }

        public void StopBrushDrawing()
        {
            if (brushSource != null && brushSource.isPlaying)
                brushSource.Pause();
        }

        public void PlayBrushTransition()
        {
            EnsureInitialized();
            StopBrushDrawing();
            VfxAudioManager.Instance?.PlayOneShot(brushTransitionClip, 0.78f);
        }

        public void PlayWallHit()
        {
            EnsureInitialized();
            VfxAudioManager.Instance?.PlayOneShot(wallHitClip, 0.78f);
        }

        public void PlayGameOver()
        {
            EnsureInitialized();
            if (gameOverSoundRoutine != null) StopCoroutine(gameOverSoundRoutine);
            gameOverSoundRoutine = StartCoroutine(PlayGameOverAfterDeath());
        }

        public void PlayJump(Vector3 position)
        {
            EnsureInitialized();
            VfxAudioManager.Instance?.PlayOneShot(jumpClip, 0.72f);
            StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.18f, 0.78f, 0.24f, 0.07f, 0.2f));
            StartCoroutine(AnimateBrushStreak(position + Vector3.down * 0.25f));
        }

        public void PlayLanding(Vector3 position, float impactSpeed)
        {
            EnsureInitialized();
            float strength = Mathf.InverseLerp(2f, 14f, impactSpeed);
            VfxAudioManager.Instance?.PlayOneShot(landingClip, Mathf.Lerp(0.45f, 0.9f, strength));
            StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.12f,
                Mathf.Lerp(0.55f, 1.05f, strength), 0.28f, 0.09f, 0.12f, 0.35f));
            SpawnDroplets(position, 5 + Mathf.RoundToInt(strength * 4f), InkPalette.Ink);
        }

        public void PlayStrokeResolved(Vector3 position, bool valid)
        {
            EnsureInitialized();
            if (valid)
            {
                VfxAudioManager.Instance?.PlayOneShot(drawClip, 0.55f);
                StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.08f, 0.48f, 0.2f, 0.05f, 0.2f));
            }
            else
            {
                VfxAudioManager.Instance?.PlayOneShot(invalidClip, 0.65f);
                StartCoroutine(AnimateInvalidSeal(position));
            }
        }

        public void PlayItemPickup(Vector3 position, ItemType type)
        {
            EnsureInitialized();
            Color color = type == ItemType.GoldenBrush ? InkPalette.Gold : InkPalette.Ink;
            VfxAudioManager.Instance?.PlayOneShot(itemClip, 0.72f);
            StartCoroutine(AnimateRing(position, color, 0.2f, 1.15f, 0.38f, 0.08f, 0.15f));
            SpawnDroplets(position, 9, color);
        }

        public void PlayDeath(Vector3 position)
        {
            EnsureInitialized();
            StopBrushDrawing();
            PlayAccent(deathSqueakClip, 1f);
            StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.1f, 1.35f,
                0.42f, 0.12f, 0.75f));
            SpawnDroplets(position, 14, InkPalette.Ink);
        }

        public void ShowZone(string title, string subtitle)
        {
            EnsureInitialized();
            VfxAudioManager.Instance?.PlayOneShot(milestoneClip, 0.72f);
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(AnimateBanner(title, subtitle));
        }

        IEnumerator AnimateRing(Vector3 position, Color color, float startRadius, float endRadius,
            float duration, float width, float startAlpha, float yScale = 1f)
        {
            var go = new GameObject("FeedbackRing");
            go.transform.position = position;
            go.transform.localScale = new Vector3(1f, yScale, 1f);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 32;
            line.material = FallbackInkStyle.SharedInkMaterial;
            line.sortingOrder = 12;
            line.startWidth = line.endWidth = width;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float radius = Mathf.Lerp(startRadius, endRadius, 1f - Mathf.Pow(1f - t, 3f));
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / line.positionCount;
                    float wobble = 1f + Mathf.Sin(angle * 5f + t * 8f) * 0.035f;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) *
                        radius * wobble);
                }
                color.a = Mathf.Lerp(startAlpha, 0f, t);
                line.startColor = line.endColor = color;
                yield return null;
            }
            Destroy(go);
        }

        IEnumerator AnimateBrushStreak(Vector3 position)
        {
            var go = new GameObject("JumpBrushStreak");
            go.transform.position = position;
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 4;
            line.material = FallbackInkStyle.SharedInkMaterial;
            line.sortingOrder = 11;
            line.startWidth = 0.16f;
            line.endWidth = 0.025f;
            line.SetPosition(0, new Vector3(-0.12f, -0.35f));
            line.SetPosition(1, new Vector3(0.05f, -0.08f));
            line.SetPosition(2, new Vector3(-0.04f, 0.24f));
            line.SetPosition(3, new Vector3(0.08f, 0.55f));
            float elapsed = 0f;
            while (elapsed < 0.24f)
            {
                elapsed += Time.deltaTime;
                Color color = InkPalette.Ink;
                color.a = 1f - elapsed / 0.24f;
                line.startColor = line.endColor = color;
                yield return null;
            }
            Destroy(go);
        }

        IEnumerator AnimateInvalidSeal(Vector3 position)
        {
            var root = new GameObject("InvalidStrokeSeal");
            root.transform.position = position;
            for (int i = 0; i < 2; i++)
            {
                var line = new GameObject($"Slash_{i}").AddComponent<LineRenderer>();
                line.transform.SetParent(root.transform, false);
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.material = FallbackInkStyle.SharedInkMaterial;
                line.sortingOrder = 13;
                line.startWidth = line.endWidth = 0.1f;
                float sign = i == 0 ? 1f : -1f;
                line.SetPosition(0, new Vector3(-0.28f, -0.28f * sign));
                line.SetPosition(1, new Vector3(0.28f, 0.28f * sign));
                line.startColor = line.endColor = InkPalette.Red;
            }
            float elapsed = 0f;
            while (elapsed < 0.38f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.38f);
                root.transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, t);
                yield return null;
            }
            Destroy(root);
        }

        void SpawnDroplets(Vector3 position, int count, Color color)
        {
            for (int i = 0; i < count; i++)
                StartCoroutine(AnimateDroplet(position, color, i, count));
        }

        IEnumerator AnimateDroplet(Vector3 position, Color color, int index, int count)
        {
            var go = new GameObject("FeedbackDroplet");
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = dotSprite;
            renderer.sortingOrder = 12;
            renderer.color = color;
            float angle = Mathf.Lerp(20f, 160f, (index + 0.5f) / count) * Mathf.Deg2Rad;
            float speed = Random.Range(1.1f, 2.5f);
            Vector3 velocity = new(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 0f);
            float scale = Random.Range(0.035f, 0.085f);
            go.transform.localScale = Vector3.one * scale;
            float elapsed = 0f;
            while (elapsed < 0.45f)
            {
                elapsed += Time.deltaTime;
                velocity += Vector3.down * (4.5f * Time.deltaTime);
                go.transform.position += velocity * Time.deltaTime;
                color.a = 1f - elapsed / 0.45f;
                renderer.color = color;
                yield return null;
            }
            Destroy(go);
        }

        void CreateOverlay()
        {
            var canvasObject = new GameObject("FeedbackOverlay", typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            overlayCanvas = canvasObject.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 140;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            var textObject = new GameObject("ZoneBanner", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(canvasObject.transform, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.78f);
            rect.sizeDelta = new Vector2(840f, 150f);
            bannerText = textObject.GetComponent<Text>();
            bannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bannerText.fontSize = 42;
            bannerText.fontStyle = FontStyle.Bold;
            bannerText.alignment = TextAnchor.MiddleCenter;
            bannerText.color = Color.clear;
        }

        IEnumerator AnimateBanner(string title, string subtitle)
        {
            bannerText.text = $"{title}\n<size=25>{subtitle}</size>";
            float duration = 2.2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Min(Mathf.InverseLerp(0f, 0.16f, t),
                    1f - Mathf.InverseLerp(0.72f, 1f, t));
                Color color = InkPalette.Ink;
                color.a = alpha;
                bannerText.color = color;
                yield return null;
            }
            bannerText.color = Color.clear;
            bannerRoutine = null;
        }

        static AudioClip CreateTone(string name, float duration, float startFrequency,
            float endFrequency, float volume, float noiseAmount)
        {
            const int sampleRate = 44100;
            int count = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += frequency / sampleRate * Mathf.PI * 2f;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) *
                                 Mathf.Pow(1f - t, 0.7f);
                float tonal = Mathf.Sin(phase) * (1f - noiseAmount);
                float noise = Random.Range(-1f, 1f) * noiseAmount;
                samples[i] = (tonal + noise) * envelope * volume;
            }
            var clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static AudioClip LoadSfx(string fileName)
        {
            return Resources.Load<AudioClip>($"MukJump/Audio/SFX/{fileName}");
        }

        void CreateDedicatedAudioSources()
        {
            var brushTransform = transform.Find("BrushDrawingAudio");
            var sourceObject = brushTransform != null
                ? brushTransform.gameObject
                : new GameObject("BrushDrawingAudio");
            if (brushTransform == null) sourceObject.transform.SetParent(transform, false);
            brushSource = sourceObject.GetComponent<AudioSource>();
            if (brushSource == null) brushSource = sourceObject.AddComponent<AudioSource>();
            brushSource.playOnAwake = false;
            brushSource.loop = true;
            brushSource.spatialBlend = 0f;
            brushSource.clip = brushLoopClip;

            var accentTransform = transform.Find("PriorityAccentAudio");
            var accentObject = accentTransform != null
                ? accentTransform.gameObject
                : new GameObject("PriorityAccentAudio");
            if (accentTransform == null) accentObject.transform.SetParent(transform, false);
            accentSource = accentObject.GetComponent<AudioSource>();
            if (accentSource == null) accentSource = accentObject.AddComponent<AudioSource>();
            accentSource.playOnAwake = false;
            accentSource.loop = false;
            accentSource.spatialBlend = 0f;
            accentSource.priority = 32;
        }

        void PlayAccent(AudioClip clip, float volume)
        {
            if (accentSource == null || clip == null) return;
            accentSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        IEnumerator PlayGameOverAfterDeath()
        {
            // 마지막 캐릭터의 짧은 "찍" 사망음이 먼저 온전히 들린 뒤 종료음을 붙인다.
            float deathDuration = deathSqueakClip != null ? deathSqueakClip.length : 0.24f;
            yield return new WaitForSecondsRealtime(deathDuration + 0.04f);
            PlayAccent(gameOverClip, 0.74f);
            gameOverSoundRoutine = null;
        }

        static AudioClip CreateBrushNoise(string name, float duration, float volume,
            bool fadeOut = false)
        {
            const int sampleRate = 44100;
            int count = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[count];
            float filtered = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float grain = Random.Range(-1f, 1f);
                filtered = Mathf.Lerp(filtered, grain, 0.18f);
                float bristle = Mathf.Sin(t * Mathf.PI * 2f * 23f) * 0.12f;
                float envelope = fadeOut
                    ? Mathf.Sin(Mathf.PI * t) * Mathf.Pow(1f - t, 0.28f)
                    : 0.72f + Mathf.Sin(t * Mathf.PI * 2f * 3f) * 0.18f;
                samples[i] = (filtered * 0.88f + bristle) * envelope * volume;
            }
            var clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static Sprite CreateDotSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeInkDot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                byte alpha = (byte)(Mathf.Clamp01(1f - Mathf.InverseLerp(0.72f, 1f, distance)) * 255);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), size);
        }
    }
}
