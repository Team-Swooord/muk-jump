using UnityEngine;
using MukJump.AI;
using MukJump.Core;
using MukJump.Player;

namespace MukJump.Items
{
    /// 먹 방어막을 캐릭터 주변의 살아 움직이는 먹 원으로 표현한다.
    [RequireComponent(typeof(PlayerController))]
    public class ItemEffectView : MonoBehaviour
    {
        [SerializeField] int ringSegments = 48;
        [SerializeField] float ringRadius = 0.78f;
        [SerializeField] float wobble = 0.055f;

        PlayerController player;
        LineRenderer outerRing;
        LineRenderer innerRing;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            outerRing = CreateRing("InkShieldOuter", 7, 0.065f);
            innerRing = CreateRing("InkShieldInner", 6, 0.025f);
        }

        void Update()
        {
            bool visible = player != null && player.HasShield && !player.IsDead &&
                           GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
            outerRing.enabled = visible;
            innerRing.enabled = visible;
            if (visible)
            {
                UpdateRing(outerRing, ringRadius, Time.time * 2.2f);
                UpdateRing(innerRing, ringRadius * 0.88f, -Time.time * 1.7f);
            }

        }

        LineRenderer CreateRing(string objectName, int sortingOrder, float width)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = ringSegments;
            ring.startWidth = ring.endWidth = width;
            ring.numCapVertices = 3;
            ring.material = FallbackInkStyle.SharedInkMaterial;
            var color = InkPalette.Ink;
            color.a = objectName.EndsWith("Outer") ? 0.72f : 0.32f;
            ring.startColor = ring.endColor = color;
            ring.sortingOrder = sortingOrder;
            ring.enabled = false;
            return ring;
        }

        void UpdateRing(LineRenderer ring, float radius, float phase)
        {
            for (int i = 0; i < ringSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / ringSegments;
                float noise = Mathf.Sin(angle * 5f + phase) * wobble +
                              Mathf.Sin(angle * 9f - phase * 0.7f) * wobble * 0.4f;
                float r = radius + noise;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f));
            }
        }
    }
}
