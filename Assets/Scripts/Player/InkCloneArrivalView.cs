using System.Collections;
using UnityEngine;
using MukJump.Core;

namespace MukJump.Player
{
    /// 먹분신 생성 순간에 눈·다리 없는 먹 몸통이 먼저 맺힌 뒤 완성 캐릭터가
    /// 짧게 튀어나오는 연출. 캐릭터마다 보조 렌더러 하나를 만들어 계속 재사용한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(PlayerController))]
    public sealed class InkCloneArrivalView : MonoBehaviour, IRuntimeCloneLifecycle
    {
        const string VisualName = "InkCloneArrivalVisual";

        [SerializeField, Min(0.04f)] float bodyOnlyDuration = 0.12f;
        [SerializeField, Min(0.06f)] float characterPopDuration = 0.18f;
        [SerializeField, Range(0.2f, 1f)] float bodyStartScale = 0.46f;
        [SerializeField, Range(0.5f, 1.2f)] float bodyEndScale = 0.88f;
        [SerializeField, Range(1f, 1.3f)] float popOvershoot = 1.12f;

        SpriteRenderer playerRenderer;
        SpriteRenderer arrivalRenderer;
        PlayerController player;
        Coroutine arrivalRoutine;
        bool hasRendererState;
        bool playerRendererWasEnabled;
        bool clonePreparationActive;
        bool preparedPlayerEnabled;
        bool preparedArrivalEnabled;

        void Awake()
        {
            EnsureVisuals();
            if (arrivalRenderer != null)
                arrivalRenderer.enabled = false;
        }

        void OnDisable()
        {
            CancelArrival();
        }

        /// 실제 플레이에서는 몸통 단계와 완성 캐릭터 팝을 재생한다.
        /// EditMode 검증에서는 코루틴을 시작하지 않고 정상 렌더 상태만 유지한다.
        public void Play()
        {
            EnsureVisuals();
            if (playerRenderer == null || arrivalRenderer == null)
                return;

            CancelArrival();
            if (!Application.isPlaying)
            {
                playerRenderer.enabled = true;
                arrivalRenderer.enabled = false;
                return;
            }

            arrivalRoutine = StartCoroutine(AnimateArrival());
        }

        IEnumerator AnimateArrival()
        {
            hasRendererState = true;
            playerRendererWasEnabled = playerRenderer.enabled;
            playerRenderer.enabled = false;
            ConfigureArrivalRenderer();

            Sprite blob = InkUiTextureFactory.CreateBlobSprite();
            float fullWidth = playerRenderer.sprite != null
                ? Mathf.Max(0.01f, playerRenderer.sprite.bounds.size.x)
                : 1f;
            float blobWidth = blob != null
                ? Mathf.Max(0.01f, blob.bounds.size.x)
                : 1f;
            float blobToCharacterScale = fullWidth / blobWidth;

            arrivalRenderer.sprite = blob;
            arrivalRenderer.flipX = false;
            arrivalRenderer.flipY = false;
            arrivalRenderer.color = InkPalette.Ink;
            arrivalRenderer.enabled = true;

            float phaseStartedAt = Time.time;
            while (Time.time - phaseStartedAt < SafeBodyOnlyDuration)
            {
                if (player == null || player.IsDead)
                    break;

                float elapsed = Time.time - phaseStartedAt;
                float t = Mathf.Clamp01(elapsed / SafeBodyOnlyDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float scale = Mathf.Lerp(bodyStartScale, bodyEndScale, eased) *
                              blobToCharacterScale;
                arrivalRenderer.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            if (player != null && !player.IsDead)
            {
                phaseStartedAt = Time.time;
                while (Time.time - phaseStartedAt < SafeCharacterPopDuration)
                {
                    if (player == null || player.IsDead)
                        break;

                    float elapsed = Time.time - phaseStartedAt;
                    float t = Mathf.Clamp01(elapsed / SafeCharacterPopDuration);
                    SyncCharacterFrame();
                    float scale = t < 0.58f
                        ? Mathf.Lerp(
                            0.72f,
                            popOvershoot,
                            1f - Mathf.Pow(1f - t / 0.58f, 3f))
                        : Mathf.Lerp(
                            popOvershoot,
                            1f,
                            Mathf.SmoothStep(0f, 1f, (t - 0.58f) / 0.42f));
                    arrivalRenderer.transform.localScale = Vector3.one * scale;
                    Color color = playerRenderer.color;
                    color.a *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 4.5f));
                    arrivalRenderer.color = color;
                    yield return null;
                }
            }

            RestoreRendererState();
            arrivalRoutine = null;
        }

        void EnsureVisuals()
        {
            playerRenderer ??= GetComponent<SpriteRenderer>();
            player ??= GetComponent<PlayerController>();

            if (arrivalRenderer == null)
            {
                Transform existing = transform.Find(VisualName);
                if (existing != null)
                    arrivalRenderer = existing.GetComponent<SpriteRenderer>();
            }

            if (arrivalRenderer == null)
            {
                var visual = new GameObject(VisualName);
                visual.layer = gameObject.layer;
                visual.transform.SetParent(transform, false);
                arrivalRenderer = visual.AddComponent<SpriteRenderer>();
            }

            arrivalRenderer.gameObject.layer = gameObject.layer;
            arrivalRenderer.transform.localPosition = Vector3.zero;
            arrivalRenderer.transform.localRotation = Quaternion.identity;
        }

        void ConfigureArrivalRenderer()
        {
            arrivalRenderer.sharedMaterial = playerRenderer.sharedMaterial;
            arrivalRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            arrivalRenderer.sortingOrder = playerRenderer.sortingOrder + 1;
            arrivalRenderer.maskInteraction = playerRenderer.maskInteraction;
        }

        void SyncCharacterFrame()
        {
            arrivalRenderer.sprite = playerRenderer.sprite;
            arrivalRenderer.flipX = playerRenderer.flipX;
            arrivalRenderer.flipY = playerRenderer.flipY;
            ConfigureArrivalRenderer();
        }

        void CancelArrival()
        {
            if (arrivalRoutine != null)
            {
                StopCoroutine(arrivalRoutine);
                arrivalRoutine = null;
            }
            RestoreRendererState();
        }

        void RestoreRendererState()
        {
            if (arrivalRenderer != null)
            {
                arrivalRenderer.enabled = false;
                arrivalRenderer.transform.localScale = Vector3.one;
            }
            if (hasRendererState && playerRenderer != null)
            {
                // 사망 시퀀스는 본체 프레임을 먹 자국과 함께 보여 주므로, 생성 연출
                // 도중 죽었더라도 과거 숨김 상태가 사망 렌더를 다시 끄면 안 된다.
                playerRenderer.enabled =
                    (player != null && player.IsDead) || playerRendererWasEnabled;
            }
            hasRendererState = false;
        }

        /// 생성 연출 도중인 분신을 다시 복제해도 숨겨진 본체·활성 보조 렌더러 상태가
        /// 새 분신에 복사되지 않게 동기 Instantiate 동안만 정상 모습으로 바꾼다.
        public void PrepareForRuntimeClone()
        {
            EnsureVisuals();
            if (clonePreparationActive) return;
            clonePreparationActive = true;
            preparedPlayerEnabled = playerRenderer != null && playerRenderer.enabled;
            preparedArrivalEnabled = arrivalRenderer != null && arrivalRenderer.enabled;
            if (playerRenderer != null) playerRenderer.enabled = true;
            if (arrivalRenderer != null) arrivalRenderer.enabled = false;
        }

        public void RestoreAfterRuntimeClone()
        {
            if (!clonePreparationActive) return;
            if (playerRenderer != null) playerRenderer.enabled = preparedPlayerEnabled;
            if (arrivalRenderer != null) arrivalRenderer.enabled = preparedArrivalEnabled;
            clonePreparationActive = false;
        }

        void OnValidate()
        {
            bodyOnlyDuration = SafeBodyOnlyDuration;
            characterPopDuration = SafeCharacterPopDuration;
            bodyStartScale = Mathf.Clamp(bodyStartScale, 0.2f, 1f);
            bodyEndScale = Mathf.Clamp(bodyEndScale, 0.5f, 1.2f);
            popOvershoot = Mathf.Clamp(popOvershoot, 1f, 1.3f);
        }

        float SafeBodyOnlyDuration =>
            float.IsNaN(bodyOnlyDuration) || float.IsInfinity(bodyOnlyDuration)
                ? 0.12f
                : Mathf.Max(0.04f, bodyOnlyDuration);

        float SafeCharacterPopDuration =>
            float.IsNaN(characterPopDuration) || float.IsInfinity(characterPopDuration)
                ? 0.18f
                : Mathf.Max(0.06f, characterPopDuration);
    }
}
