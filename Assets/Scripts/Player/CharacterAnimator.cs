using UnityEngine;

namespace MukJump.Player
{
    /// 물리 상태(수직 속도)에 따라 점프 8프레임을 전환한다.
    /// 중력 아래에서 수직 속도는 이륙 직후 최대치에서 정점(0)을 지나 하강 최대치까지
    /// 단조 감소하므로, 속도 구간만으로 launch→rise→apex→fall→dive가 자연스럽게 이어진다.
    /// Animator 에셋 없이 코드로 직접 구동.
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(PlayerController))]
    public class CharacterAnimator : MonoBehaviour
    {
        [Header("프레임 (muk_spritesheet: idle→crouch→launch→rise / apex→fall→dive→land)")]
        [SerializeField] Sprite idle;
        [SerializeField] Sprite crouch;
        [SerializeField] Sprite launch;
        [SerializeField] Sprite rise;
        [SerializeField] Sprite apex;
        [SerializeField] Sprite fall;
        [SerializeField] Sprite dive;
        [SerializeField] Sprite land;
        [Header("피격 단계 프레임 (동일한 8상태 순서)")]
        [Tooltip("체력 1회 소모 뒤의 idle→crouch→launch→rise / apex→fall→dive→land")]
        [SerializeField] Sprite[] damageStageOneFrames;
        [Tooltip("체력 2회 소모 뒤의 idle→crouch→launch→rise / apex→fall→dive→land")]
        [SerializeField] Sprite[] damageStageTwoFrames;
        [Tooltip("죽음 포즈들 (X 눈) — 죽음 연출 동안 순환 재생 (허우적거리는 느낌)")]
        [SerializeField] Sprite[] deadFrames;
        [SerializeField, Min(0f)] float deadFps = 12f;

        [Header("공중 상태 전환 속도 구간")]
        [Tooltip("수직 속도가 이보다 크면 도약(launch) 포즈")]
        [SerializeField] float highBand = 8f;
        [Tooltip("수직 속도 절대값이 이보다 작으면 정점(apex) 포즈")]
        [SerializeField] float apexBand = 2f;

        [Header("접지 상태 타이밍")]
        [Tooltip("점프 게이지가 이 비율을 넘으면 웅크림 포즈 (점프 예고)")]
        [SerializeField] float crouchChargeRatio = 0.85f;
        [Tooltip("착지 순간 몸이 눌리는 시간")]
        [SerializeField] float landDuration = 0.08f;

        SpriteRenderer sr;
        Rigidbody2D rb;
        PlayerController player;
        AutoJump jump;
        float landTimer;
        float deathTime;
        bool wasGrounded = true;

        const int FrameCount = 8;
        const int IdleFrame = 0;
        const int CrouchFrame = 1;
        const int LaunchFrame = 2;
        const int RiseFrame = 3;
        const int ApexFrame = 4;
        const int FallFrame = 5;
        const int DiveFrame = 6;
        const int LandFrame = 7;
        const string DamageStageOneResource =
            "MukJump/Player/muk_spritesheet_hit_01";
        const string DamageStageTwoResource =
            "MukJump/Player/muk_spritesheet_hit_02";
        static readonly string[] StateNames =
        {
            "idle", "crouch", "launch", "rise",
            "apex", "fall", "dive", "land",
        };
        static Sprite[] cachedDamageStageOneFrames;
        static Sprite[] cachedDamageStageTwoFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntimeFrameCache()
        {
            cachedDamageStageOneFrames = null;
            cachedDamageStageTwoFrames = null;
        }

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            rb = GetComponent<Rigidbody2D>();
            player = GetComponent<PlayerController>();
            jump = GetComponent<AutoJump>();
            damageStageOneFrames = LoadDamageFramesIfNeeded(
                damageStageOneFrames,
                DamageStageOneResource,
                idle,
                ref cachedDamageStageOneFrames);
            damageStageTwoFrames = LoadDamageFramesIfNeeded(
                damageStageTwoFrames,
                DamageStageTwoResource,
                idle,
                ref cachedDamageStageTwoFrames);
        }

        void LateUpdate()
        {
            if (idle == null) return; // 프레임 미할당 시 기본 스프라이트 유지

            if (player.IsDead)
            {
                deathTime += Time.deltaTime;
                if (deadFrames != null && deadFrames.Length > 0)
                {
                    // 한 번만 재생하고 마지막 포즈에서 멈춘다
                    float playbackFps = SafeDeadFps;
                    int frame = Mathf.Min(
                        Mathf.Max(0, (int)(deathTime * playbackFps)),
                        deadFrames.Length - 1);
                    sr.sprite = deadFrames[frame];
                }
                return;
            }
            deathTime = 0f;

            if (MukJump.Core.GameManager.Instance != null &&
                MukJump.Core.GameManager.Instance.State == MukJump.Core.GameState.Lobby)
            {
                SetStateSprite(IdleFrame);
                return;
            }

            if (!player.IsGrounded)
            {
                wasGrounded = false;
                SetStateSprite(FrameForVelocity(rb.linearVelocity.y));
                if (Mathf.Abs(rb.linearVelocity.x) > 0.25f)
                    sr.flipX = rb.linearVelocity.x < 0f;
                return;
            }

            if (!wasGrounded)
            {
                wasGrounded = true;
                landTimer = landDuration;
            }

            if (landTimer > 0f)
            {
                landTimer -= Time.deltaTime;
                SetStateSprite(LandFrame);
                return;
            }

            bool preparingJump = jump != null && jump.IsCharging && jump.ChargeRatio >= crouchChargeRatio;
            SetStateSprite(preparingJump ? CrouchFrame : IdleFrame);
        }

        int FrameForVelocity(float vy)
        {
            if (vy > highBand) return LaunchFrame;
            if (vy > apexBand) return RiseFrame;
            if (vy > -apexBand) return ApexFrame;
            if (vy > -highBand) return FallFrame;
            return DiveFrame;
        }

        void SetStateSprite(int frameIndex)
        {
            Sprite selected = ResolveStateSprite(frameIndex);
            if (selected != null)
                sr.sprite = selected;
        }

        Sprite ResolveStateSprite(int frameIndex)
        {
            Sprite[] damageFrames = player != null
                ? player.DamageStage switch
                {
                    1 => damageStageOneFrames,
                    2 => damageStageTwoFrames,
                    _ => null,
                }
                : null;
            if (HasValidFrames(damageFrames) &&
                frameIndex >= 0 && frameIndex < damageFrames.Length &&
                damageFrames[frameIndex] != null)
                return damageFrames[frameIndex];
            return BaseFrame(frameIndex);
        }

        Sprite BaseFrame(int frameIndex)
        {
            return frameIndex switch
            {
                IdleFrame => idle,
                CrouchFrame => crouch,
                LaunchFrame => launch,
                RiseFrame => rise,
                ApexFrame => apex,
                FallFrame => fall,
                DiveFrame => dive,
                LandFrame => land,
                _ => idle,
            };
        }

        static Sprite[] LoadDamageFramesIfNeeded(
            Sprite[] serializedFrames,
            string resourcePath,
            Sprite sizeReference,
            ref Sprite[] cachedFrames)
        {
            if (HasValidFrames(serializedFrames))
                return serializedFrames;
            if (HasValidFrames(cachedFrames))
                return cachedFrames;

            var loaded = Resources.LoadAll<Sprite>(resourcePath);
            var ordered = OrderFramesByState(loaded);
            if (HasValidFrames(ordered))
            {
                cachedFrames = ordered;
                return cachedFrames;
            }

            // 씬 재생성 전에 새 PNG가 아직 Multiple Sprite로 임포트되지 않았어도
            // 4×2 원본 Texture2D를 같은 상태 순서로 한 번만 잘라 즉시 사용한다.
            var texture = Resources.Load<Texture2D>(resourcePath);
            var sliced = SliceRuntimeSheet(texture, sizeReference);
            if (HasValidFrames(sliced))
            {
                cachedFrames = sliced;
                return cachedFrames;
            }
            return serializedFrames;
        }

        static Sprite[] OrderFramesByState(Sprite[] loaded)
        {
            if (!HasValidFrames(loaded)) return null;
            var ordered = new Sprite[FrameCount];

            // 빌더가 붙이는 semantic 이름을 최우선으로 사용한다.
            for (int state = 0; state < StateNames.Length; state++)
            {
                string suffix = "_" + StateNames[state];
                for (int i = 0; i < loaded.Length; i++)
                {
                    string frameName = loaded[i].name.ToLowerInvariant();
                    if (frameName == StateNames[state] ||
                        frameName.EndsWith(suffix, System.StringComparison.Ordinal))
                    {
                        ordered[state] = loaded[i];
                        break;
                    }
                }
            }
            if (HasValidFrames(ordered))
                return ordered;

            // 구형 숫자 이름이어도 4×2 시트상의 위치는 변하지 않는다.
            System.Array.Clear(ordered, 0, ordered.Length);
            Texture2D texture = loaded[0].texture;
            if (texture == null) return null;
            float frameWidth = texture.width / 4f;
            float frameHeight = texture.height / 2f;
            for (int i = 0; i < loaded.Length; i++)
            {
                Rect rect = loaded[i].rect;
                int column = Mathf.RoundToInt(rect.x / frameWidth);
                int rowFromTop = Mathf.RoundToInt(
                    (texture.height - rect.yMax) / frameHeight);
                int state = rowFromTop * 4 + column;
                if (state < 0 || state >= FrameCount || ordered[state] != null)
                    return null;
                ordered[state] = loaded[i];
            }
            return HasValidFrames(ordered) ? ordered : null;
        }

        static Sprite[] SliceRuntimeSheet(
            Texture2D texture, Sprite sizeReference)
        {
            if (texture == null ||
                texture.width < 4 || texture.height < 2 ||
                texture.width % 4 != 0 || texture.height % 2 != 0)
                return null;

            int frameWidth = texture.width / 4;
            int frameHeight = texture.height / 2;
            float pixelsPerUnit = 900f;
            if (sizeReference != null &&
                sizeReference.pixelsPerUnit > 0f &&
                sizeReference.rect.width > 0f)
            {
                float referenceWorldWidth =
                    sizeReference.rect.width / sizeReference.pixelsPerUnit;
                pixelsPerUnit = frameWidth /
                                Mathf.Max(0.01f, referenceWorldWidth);
            }

            var frames = new Sprite[FrameCount];
            for (int state = 0; state < FrameCount; state++)
            {
                int column = state % 4;
                int rowFromTop = state / 4;
                var rect = new Rect(
                    column * frameWidth,
                    texture.height - (rowFromTop + 1) * frameHeight,
                    frameWidth,
                    frameHeight);
                frames[state] = Sprite.Create(
                    texture, rect, Vector2.one * 0.5f, pixelsPerUnit);
                frames[state].name =
                    $"{texture.name}_{StateNames[state]}_runtime";
            }
            return frames;
        }

        static bool HasValidFrames(Sprite[] frames)
        {
            if (frames == null || frames.Length != FrameCount) return false;
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] == null)
                    return false;
            return true;
        }

        void OnValidate()
        {
            deadFps = SafeDeadFps;
        }

        float SafeDeadFps =>
            float.IsNaN(deadFps) || float.IsInfinity(deadFps)
                ? 12f
                : Mathf.Max(0f, deadFps);
    }
}
