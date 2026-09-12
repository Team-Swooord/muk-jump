using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MukJump.Core
{
    /// 정산 데이터가 아닌 일회성 화면 이동만 전달한다. 계정 변경 시 공개 예약을 버린다.
    public sealed class SkinUnlockSceneController : MonoBehaviour
    {
        public const string SceneName = "SkinUnlock";
        static ulong pending;
        static object owner;
        static bool preview;
        bool leaving;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPending() { pending = 0; owner = null; preview = false; }
        public static void Prepare(ulong mask, object account) { pending = mask; owner = account; preview = false; }
        public static void PreparePreview(ulong mask, object account)
        {
            ResetPending();
            if (!SkinDebugSession.Available || !ReferenceEquals(account, PermanentGrowthProfile.SkinAccountToken)) return;
            pending = mask; owner = account; preview = true;
        }
        IEnumerator Start()
        {
            var transition = GetComponent<BrushTransitionView>();
            // 붓 가림이 걷힌 뒤부터 의상 공개를 시작한다.
            yield return null;
            while (transition.IsPlaying) yield return null;
            ulong mask = pending; object account = owner; bool testing = preview; ResetPending();
            var view = GetComponent<InkSkinUnlockView>();
            if (!(testing ? view.ShowPreview(mask, account, ReturnToLobby, ReturnToLobby) :
                view.Show(mask, account, ReturnToLobby, ReturnToLobby)))
                ReturnToLobby();
        }
        void ReturnToLobby()
        {
            if (leaving) return;
            leaving = true;
            PointerInput.SuppressUntilRelease();
            if (!GetComponent<BrushTransitionView>().TryPlay(() =>
            {
                BrushTransitionView.RequestRevealAfterSceneLoad();
                SceneManager.LoadScene("Main");
            }, () => leaving = false)) leaving = false;
        }
        void Update()
        {
            if (!leaving && !GetComponent<InkSkinUnlockView>().IsOpen && pending == 0 &&
                !GetComponent<BrushTransitionView>().IsPlaying && MobileApplicationLifecycle.IsApplicationActive)
                ReturnToLobby();
        }
    }
}
