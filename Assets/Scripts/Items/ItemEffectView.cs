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
        ParticleSystem inkDropTrail;
        uint observedInkDropLaunchVersion;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            outerRing = CreateRing("InkShieldOuter", 7, 0.065f);
            innerRing = CreateRing("InkShieldInner", 6, 0.025f);
            inkDropTrail = CreateInkDropTrail();
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

            UpdateInkDropTrail();
        }

        void UpdateInkDropTrail()
        {
            bool active = player != null && player.IsInkDropBoosted && !player.IsDead &&
                          GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
            if (active)
            {
                if (!inkDropTrail.isPlaying)
                    inkDropTrail.Play();

                if (observedInkDropLaunchVersion != player.InkDropLaunchVersion)
                {
                    observedInkDropLaunchVersion = player.InkDropLaunchVersion;
                    inkDropTrail.Emit(18);
                }
            }
            else if (inkDropTrail.isPlaying)
            {
                inkDropTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        ParticleSystem CreateInkDropTrail()
        {
            var go = new GameObject("InkDropRiseTrail");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -0.38f, 0f);

            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.18f, 0.27f, 0.31f, 0.8f),
                new Color(0.42f, 0.62f, 0.72f, 0.45f));
            main.maxParticles = 100;

            var emission = particles.emission;
            emission.rateOverTime = 26f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.32f;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            velocity.y = new ParticleSystem.MinMaxCurve(-2.8f, -1.5f);

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.25f, 1f),
                    new Keyframe(1f, 0f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = FallbackInkStyle.SharedInkMaterial;
            renderer.sortingOrder = 4;
            return particles;
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
