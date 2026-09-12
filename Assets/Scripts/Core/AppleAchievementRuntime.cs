#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace MukJump.Core
{
    /// 업적만 Game Center에 보낸다. 인증 취소·통신 실패는 게임과 뒤끝 저장을 막지 않는다.
    public sealed class AppleAchievementRuntime : MonoBehaviour
    {
        [Serializable] sealed class Reply
        { public string kind, player; public int request; public bool success; public AppleAchievementProgress[] items; }
        [Serializable] sealed class Batch { public AppleAchievementProgress[] items; }
        [DllImport("__Internal")] static extern void MJ_AchInit(string receiver);
        [DllImport("__Internal")] static extern void MJ_AchTick([MarshalAs(UnmanagedType.I1)] bool allowUi);
        [DllImport("__Internal")] static extern void MJ_AchLoad(string player, int request);
        [DllImport("__Internal")] static extern void MJ_AchReport(string player, int request, string json);
        readonly AppleAchievementSyncState sync = new();
        string player = "";
        bool initialized;
        float nextTick, retryAt, deadline, delay = 3;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            var root = new GameObject("MukJumpAppleAchievements");
            DontDestroyOnLoad(root); root.AddComponent<AppleAchievementRuntime>();
        }
        bool SafeAuthenticationUi()
        {
            var game = GameManager.Instance;
            var nav = LobbyScreenNavigator.Instance;
            if (game == null || game.State != GameState.Lobby || game.IsPaused || game.IsTransitioning ||
                nav == null || nav.IsTransitioning || nav.CurrentSection != LobbyScreenNavigator.LobbySection.Lobby ||
                LobbySettingsProfile.NeedsGameplayTutorial || FirstRunTutorialController.Instance?.IsActive == true ||
                StartupBrandSplash.IsBlockingInput || !Application.isFocused ||
                MukJumpAccountRuntime.Instance?.BlocksGameplayForAccountSync == true ||
                BackgroundMusicController.Instance?.IsFullScreenAdActive == true ||
                game.GetComponent<LobbyOptionsView>()?.IsOpen == true) return false;
            var ads = FindFirstObjectByType<GoogleMobileAdsRuntime>();
            return ads == null || !ads.IsPrivacyFlowBusy;
        }
        void Update()
        {
            if (!MobileApplicationLifecycle.IsApplicationActive || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + .5f;
            bool safe = SafeAuthenticationUi();
            if (!initialized)
            {
                if (!safe) return;
                initialized = true; MJ_AchInit(gameObject.name);
            }
            MJ_AchTick(safe);
            sync.Bind(player, PermanentGrowthProfile.SkinAccountToken);
            if (sync.Pending != 0 && Time.unscaledTime >= deadline) { sync.Cancel(); Backoff(); }
            if (string.IsNullOrEmpty(player) || sync.Pending != 0 || Time.unscaledTime < retryAt ||
                MukJumpAccountRuntime.Instance?.BlocksGameplayForAccountSync == true || PermanentGrowthProfile.RequiresRecovery) return;
            // 전체 저장 JSON은 저빈도 로비 동기화에서만 읽는다. 게임 중 알림은 로컬 저장으로 즉시 확정한다.
            if (GameManager.Instance == null || GameManager.Instance.State == GameState.Playing) return;
            deadline = Time.unscaledTime + 30;
            if (!sync.Loaded) MJ_AchLoad(player, sync.Begin());
            else
            {
                var items = sync.Changes(PermanentGrowthProfile.AchievementSyncSnapshot(), PermanentGrowthProfile.AchievementDistance);
                retryAt = Time.unscaledTime + 3;
                if (items.Length > 0) MJ_AchReport(player, sync.Begin(items), JsonUtility.ToJson(new Batch { items = items }));
            }
        }
        [UnityEngine.Scripting.Preserve]
        public void OnAchievementReply(string json)
        {
            Reply reply;
            try { reply = JsonUtility.FromJson<Reply>(json); } catch (Exception) { return; }
            if (reply == null) return;
            if (reply.kind == "auth")
            { player = reply.success ? reply.player : ""; sync.Bind(player, PermanentGrowthProfile.SkinAccountToken); return; }
            sync.Bind(player, PermanentGrowthProfile.SkinAccountToken);
            if (reply.request != sync.Pending) return;
            if (sync.Accept(reply.request, reply.player, reply.success, reply.items)) { delay = 3; retryAt = 0; }
            else Backoff();
        }
        void Backoff() { retryAt = Time.unscaledTime + delay; delay = Mathf.Min(120, delay * 2); }
    }
}
#endif
