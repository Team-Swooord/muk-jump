using System.Collections.Generic;
using UnityEngine;
namespace MukJump.Core
{
    /// 조작을 가로채지 않는 수묵 업적 알림. 고정 UI 하나를 큐로 재사용한다.
    public sealed class InkAchievementToast : MonoBehaviour
    {
        readonly Queue<int> queue = new();
        readonly HashSet<int> announced = new();
        public const float Duration = 3f;
        CanvasGroup group;
        RectTransform safe, panel;
        UnityEngine.UI.Text label;
        UnityEngine.UI.Image costumeIcon;
        object owner;
        float time;
        bool showing;
        bool lobbyNotice;
        AudioSource noticeSource;
        AudioClip noticeSound;
#if UNITY_EDITOR
        public int SoundPlayCountForTests { get; private set; }
#endif
        public void Enqueue(int id) => EnqueueForState(id, false);
        public void EnqueueLobby(int id) => EnqueueForState(id, true);
        void EnqueueForState(int id, bool inLobby)
        {
            if (id <= 0 || id > InkSkinCatalog.Count + 1) return;
            if (!ReferenceEquals(owner, PermanentGrowthProfile.SkinAccountToken) || lobbyNotice != inLobby) Clear();
            owner = PermanentGrowthProfile.SkinAccountToken;
            lobbyNotice = inLobby;
            if (announced.Add(id)) queue.Enqueue(id);
        }
        void Build()
        {
            if (group != null) return;
            var root = new GameObject("AchievementToastCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 4000;
            MobileUiLayout.ConfigurePortraitScaler(root.GetComponent<UnityEngine.UI.CanvasScaler>());
            group = root.GetComponent<CanvasGroup>(); group.blocksRaycasts = group.interactable = false;
            safe = Rect("SafeArea", root.transform, Vector2.zero, Vector2.zero);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
            panel = Rect("InkBanner", safe, new Vector2(-24, -210), new Vector2(580, 136));
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            var brush = Rect("Brush", panel, Vector2.zero, new Vector2(610, 144));
            // 게임의 흰 한지 버튼 면을 그대로 재사용한다. 배경만 비치고 글자는 또렷하게 유지한다.
            var paper = brush.gameObject.AddComponent<UnityEngine.UI.Image>();
            paper.raycastTarget = false;
            InkUiStyle.ConfigureHanjiSurface(paper);
            Color paperColor = paper.color; paperColor.a = .9f; paper.color = paperColor;
            var stamp = Rect("Seal", panel, new Vector2(-246, 0), new Vector2(50, 50));
            InkHudSurface.Ensure(stamp, true);
            Label(stamp, "해금", 25, Vector2.zero, new Vector2(48, 48));
            var heading = Label(panel, "업적 달성", 26, new Vector2(-24, 45), new Vector2(350, 28));
            heading.alignment = TextAnchor.MiddleLeft;
            heading.color = Color.Lerp(InkPalette.Ink, InkPalette.Paper, .24f);
            heading.fontStyle = FontStyle.Normal;
            label = Label(panel, "", 36, new Vector2(-24, -15), new Vector2(350, 84));
            label.alignment = TextAnchor.MiddleLeft;
            label.color = InkPalette.Ink;
            // 착용한 먹이 아니라 이번에 얻은 코스튬 원화만 오른쪽에 표시한다.
            costumeIcon = Rect("CostumeIcon", panel, new Vector2(226, 0), new Vector2(104, 104))
                .gameObject.AddComponent<UnityEngine.UI.Image>();
            costumeIcon.preserveAspect = true; costumeIcon.raycastTarget = false;
            costumeIcon.enabled = false;
            // 재생 중 조작음과 경쟁하지 않는 짧은 전용 알림. 캔버스 수명 동안 하나만 재사용한다.
            noticeSource = root.AddComponent<AudioSource>();
            noticeSource.playOnAwake = noticeSource.loop = false; noticeSource.spatialBlend = 0;
            float[] samples = InkAchievementNoticeSound.BuildSamples();
            noticeSound = AudioClip.Create("Ink_Achievement_TwoNotes", samples.Length, 1,
                InkAchievementNoticeSound.SampleRate, false);
            noticeSound.SetData(samples, 0); noticeSource.clip = noticeSound;
            foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
            group.alpha = 0;
        }
        void Update()
        {
            var game = GameManager.Instance;
            if (!ReferenceEquals(owner, PermanentGrowthProfile.SkinAccountToken) || game == null ||
                game.State != (lobbyNotice ? GameState.Lobby : GameState.Playing))
            { Clear(); return; }
            bool canAnimate = lobbyNotice
                ? MobileApplicationLifecycle.IsApplicationActive && !game.IsPaused && !game.IsTransitioning &&
                  !StartupBrandSplash.IsBlockingInput && (LobbyScreenNavigator.Instance == null ||
                  !LobbyScreenNavigator.Instance.IsTransitioning &&
                  LobbyScreenNavigator.Instance.CurrentSection == LobbyScreenNavigator.LobbySection.Lobby)
                : MobileApplicationLifecycle.IsApplicationActive && game.IsGameplayTicking;
            if (!canAnimate) { Suspend(); return; }
            if (noticeSource != null) noticeSource.volume = InkAchievementNoticeSound.Volume * LobbySettingsProfile.SfxVolume;
            if (!showing && queue.Count == 0) return;
            Build();
            MobileUiLayout.ApplySafeArea(safe, MobileUiLayout.CurrentSafeArea, Screen.width, Screen.height);
            panel.localScale = Vector3.one * (lobbyNotice
                ? Mathf.Min(1, safe.rect.width * .56f / 610f)
                : Mathf.Min(1, safe.rect.width / 650f));
            if (!showing)
            {
                int id = queue.Dequeue(); showing = true; time = 0;
                ShowReward(id);
                if (Application.isPlaying && LobbySettingsProfile.SfxVolume > 0)
                {
                    noticeSource.volume = InkAchievementNoticeSound.Volume * LobbySettingsProfile.SfxVolume;
                    noticeSource.Play();
#if UNITY_EDITOR
                    SoundPlayCountForTests++;
#endif
                }
            }
            time += Mathf.Min(Time.unscaledDeltaTime, .05f);
            float fade = Mathf.SmoothStep(0, 1, Mathf.Min(Mathf.Clamp01(time / .22f), Mathf.Clamp01((Duration - time) / .28f)));
            group.alpha = fade;
            panel.anchoredPosition = new Vector2(-24 + (LobbySettingsProfile.ReducedMotionEnabled ? 0 : 22 * (1 - fade)),
                lobbyNotice ? CalculateLobbyTop() : CalculateTop(MobileUiLayout.CurrentSafeArea, Screen.width, Screen.height));
            if (time >= Duration) { showing = false; group.alpha = 0; }
        }
        void Suspend() { if (group != null) group.alpha = 0; if (noticeSource != null) noticeSource.Stop(); }
        void ShowReward(int id)
        {
            InkLocalizedText.SetSource(label, InkAchievementCatalog.Name(id));
            // 벽타기·연속 획득처럼 코스튬 보상이 없는 알림에는 이전 그림을 남기지 않는다.
            costumeIcon.sprite = id > 0 && id < InkSkinCatalog.Count ? InkSkinCatalog.Sprite((InkSkinId)id) : null;
            costumeIcon.enabled = costumeIcon.sprite != null;
        }
        void Clear()
        {
            queue.Clear(); announced.Clear(); showing = false; Suspend();
            if (costumeIcon != null) { costumeIcon.sprite = null; costumeIcon.enabled = false; }
        }
        void OnDisable() => Clear();
        void OnApplicationPause(bool paused) { if (paused) Suspend(); }
        void OnApplicationFocus(bool focused) { if (!focused) Suspend(); }
        void OnDestroy()
        {
            Suspend();
            if (noticeSource != null) noticeSource.clip = null;
            if (noticeSound == null) return;
            if (Application.isPlaying) Destroy(noticeSound); else DestroyImmediate(noticeSound);
        }
        // 로비에는 플레이 HUD가 없다. 달 왼쪽·기록 아래와 겹치지 않도록 배너 바로 아래 오른쪽에 둔다.
        public static float CalculateLobbyTop() => -LobbyAdLayout.CalculateCanvasInset(MobileUiLayout.ReferenceHeight,
            Mathf.Max(LobbyAdLayout.TopInsetFraction, LobbyAdLayout.MeasuredTopInsetFraction)) - 24f;
        public static float CalculateTop(Rect area, int width, int height)
        {
            float hudBottom = Mathf.Min(GameplayHudView.CalculateVisibleHudRect(area, width, height).yMin,
                GameplayHudView.CalculatePauseTouchRect(area, width, height).yMin);
            return hudBottom + MobileUiLayout.GetLogicalTopInset(area, width, height) - 20f;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = Vector2.one * .5f;
            r.anchoredPosition = pos; r.sizeDelta = size; return r;
        }
        static UnityEngine.UI.Text Label(Transform parent, string source, int size, Vector2 pos, Vector2 bounds)
        {
            var t = Rect("Label", parent, pos, bounds).gameObject.AddComponent<UnityEngine.UI.Text>();
            InkLocalizedText.SetSource(t, source); InkUiStyle.ApplyReadableText(t, size, TextAnchor.MiddleCenter, true, true);
            // 작은 알림에는 두꺼운 외곽선을 더하지 않는다.
            t.GetComponent<UnityEngine.UI.Outline>().enabled = false;
            t.color = InkPalette.Paper; t.raycastTarget = false; return t;
        }
    }
}
