using UnityEngine;
using MukJump.Core;

namespace MukJump.Items
{
    /// 먹물방울 50m 점프와 같은 프레임에 재생되는 수묵 연출 진입점.
    /// 직렬화 에셋과 오디오를 소유하고 합성 연출은 게임 전체 공유 풀에 요청한다.
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
        [SerializeField] Sprite[] dropletFrames;
        [SerializeField] AudioClip immediateClip;
        [SerializeField] AudioClip whooshClip;

        [Header("연출 조절")]
        [SerializeField, Min(0.1f)] float effectScale = 1f;
        [SerializeField, Range(8, 36)] int sprayCount = 24;
        [SerializeField, Range(6, 28)] int residualDropCount = 18;
        [SerializeField, Min(0.5f)] float maximumStrokeLength = 15f;

        InkDropJumpVfxPool poolService;
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

        void Start()
        {
            // 로비 화면이 보이는 동안 현재 품질의 동시 합성 상한까지 준비한다.
            poolService = GetOrCreatePool();
            poolService?.PrewarmForCurrentTier();
        }

        public void Play()
        {
            if (!isActiveAndEnabled) return;

            float height = playerRenderer != null ? playerRenderer.bounds.size.y : 1f;
            height = Mathf.Max(0.25f, height) * effectScale;
            Vector3 ground = playerCollider != null
                ? new Vector3(playerCollider.bounds.center.x, playerCollider.bounds.min.y,
                    transform.position.z)
                : transform.position - Vector3.up * height * 0.5f;

            poolService = GetOrCreatePool();
            poolService.Play(this, transform, playerRenderer, ground, height,
                maximumStrokeLength);

            if (immediateClip == null) return;
            if (VfxAudioManager.Instance != null)
            {
                VfxAudioManager.Instance.PlayOneShot(immediateClip);
                VfxAudioManager.Instance.PlayOneShot(whooshClip, 0.48f);
            }
            else
                audioSource.PlayOneShot(
                    immediateClip,
                    LobbySettingsProfile.SfxVolume);
        }

        void OnDisable()
        {
            poolService?.ReleaseOwner(this);
            poolService = null;
        }

        InkDropJumpVfxPool GetOrCreatePool()
        {
            return InkDropJumpVfxPool.GetOrCreate(
                new InkDropJumpVfxInstance.AssetSet(
                    inkDrop, groundBlob, inkSplash, shockRing, verticalBrush, brushFibers,
                    softFlash, inkStreak, dropletFrames),
                sprayCount,
                residualDropCount);
        }

        void OnValidate()
        {
            effectScale = Mathf.Max(0.1f, effectScale);
            sprayCount = Mathf.Clamp(sprayCount, 8, 36);
            residualDropCount = Mathf.Clamp(residualDropCount, 6, 28);
            maximumStrokeLength = Mathf.Max(0.5f, maximumStrokeLength);
        }
    }
}
