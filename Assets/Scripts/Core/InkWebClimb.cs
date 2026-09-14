using MukJump.AI;
using MukJump.Player;
using UnityEngine;

namespace MukJump.Core
{
    /// 실제 화면 벽을 확인한 뒤 물리 속도로 끌어올린다. 장식 변형이나 위치 순간이동은 하지 않는다.
    [DisallowMultipleComponent, DefaultExecutionOrder(120)]
    public sealed class InkWebClimb : MonoBehaviour
    {
        public const float Height = 18f, Speed = 18f, CastSeconds = .14f;
        readonly RaycastHit2D[] hits = new RaycastHit2D[16];
        [SerializeField, HideInInspector] LineRenderer outline, thread;
        PlayerController player;
        ScreenSideWall wall;
        Vector2 anchor, destination;
        int launchVersion;
        float elapsed, travelSeconds;
        bool pulling;
        public bool IsClimbing => player != null;
        public Vector2 Destination => destination;
        public float Elapsed => elapsed;

        void OnEnable()
        {
            outline = WarmLine("SkinWebOutline", .075f, InkPalette.Ink, 8);
            thread = WarmLine("SkinWebThread", .032f, InkPalette.Paper, 9);
            Stop(false);
        }
        LineRenderer WarmLine(string name, float width, Color color, int order)
        {
            var child = transform.Find(name);
            if (child == null) { child = new GameObject(name).transform; child.SetParent(transform, false); }
            var line = child.GetComponent<LineRenderer>();
            if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
            line.useWorldSpace = true; line.positionCount = 2;
            line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
            line.numCapVertices = 2; line.sortingOrder = order; line.enabled = false;
            return line;
        }
        public bool TryStart(PlayerController candidate)
        {
            if (IsClimbing || candidate == null || candidate.IsDead || candidate.IsRuntimeClone ||
                !candidate.isActiveAndEnabled || candidate.IsInkDropBoosted || candidate.IsWallClinging ||
                candidate.Body == null || !candidate.Body.simulated || !CanTick() ||
                RunGrowthController.Instance?.SkinSnapshot != InkSkinId.SpiderMask) return false;
            Vector2 origin = candidate.Body.position;
            float side = origin.x < 0 ? -1 : 1;
            if (!FindWall(origin, side, out var hit) && !FindWall(origin, -side, out hit)) return false;
            wall = hit.collider.GetComponent<ScreenSideWall>();
            float clearance = (candidate.PrimaryCollider != null ? candidate.PrimaryCollider.bounds.extents.x : .4f) + .16f;
            float rise = Mathf.Min(Height, hit.collider.bounds.max.y - origin.y - 1);
            if (rise < 4) { wall = null; return false; }
            anchor = new Vector2(hit.point.x, origin.y + rise);
            destination = anchor + Vector2.right * (wall.IsLeft ? clearance : -clearance);
            player = candidate; elapsed = 0; pulling = false;
            travelSeconds = Vector2.Distance(origin, destination) / Speed;
            Present();
            return true;
        }
        bool FindWall(Vector2 origin, float side, out RaycastHit2D hit)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            int count = Physics2D.Raycast(origin, Vector2.right * side, filter, hits, 30);
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null && hits[i].collider.GetComponent<ScreenSideWall>() != null)
                { hit = hits[i]; return true; }
            hit = default; return false;
        }
        static bool CanTick() => GameManager.Instance != null && GameManager.Instance.IsGameplayTicking &&
            MobileApplicationLifecycle.IsApplicationActive && MukJumpAccountRuntime.Instance?.BlocksGameplayForAccountSync != true;
        void FixedUpdate() => Advance(Time.fixedDeltaTime);
        void Advance(float seconds)
        {
            if (!IsClimbing) return;
            if (player.IsDead || !player.isActiveAndEnabled || !player.Body.simulated || wall == null ||
                !wall.isActiveAndEnabled || GameManager.Instance == null || GameManager.Instance.State != GameState.Playing ||
                RunGrowthController.Instance?.SkinSnapshot != InkSkinId.SpiderMask ||
                !pulling && player.IsInkDropBoosted ||
                pulling && player.SpecialLaunchVersion != launchVersion) { Stop(false); return; }
            if (!CanTick()) { Show(false); return; }
            if (!float.IsFinite(seconds) || seconds <= 0) return;
            elapsed += seconds;
            if (elapsed >= CastSeconds)
            {
                if (!pulling)
                {
                    // 같은 보호 배관을 사용하되 아이템 수집/비행 연쇄 업적은 증가시키지 않는다.
                    player.LaunchInkDrop(Height, false);
                    launchVersion = player.SpecialLaunchVersion; pulling = true;
                }
                Vector2 delta = destination - player.Body.position;
                if (delta.magnitude < .25f || elapsed >= CastSeconds + travelSeconds + .15f)
                { Stop(true); return; }
                player.Body.linearVelocity = Vector2.ClampMagnitude(delta / Mathf.Max(.04f, travelSeconds - (elapsed - CastSeconds)), Speed * 1.15f);
            }
            Present();
        }
        void Present()
        {
            Vector3 start = player.transform.position;
            Vector3 end = Vector2.Lerp(start, anchor, Mathf.Clamp01(elapsed / CastSeconds));
            outline.SetPosition(0, start); outline.SetPosition(1, end);
            thread.SetPosition(0, start); thread.SetPosition(1, end); Show(true);
        }
        void Show(bool visible) { if (outline != null) outline.enabled = visible; if (thread != null) thread.enabled = visible; }
        void Stop(bool release)
        {
            if (release && player != null && !player.IsDead && player.Body.simulated)
                player.Body.linearVelocity = new Vector2(wall != null && wall.IsLeft ? 3 : -3, 3);
            player = null; wall = null; pulling = false; elapsed = 0; Show(false);
        }
        void OnDisable() => Stop(false);
    }
}
