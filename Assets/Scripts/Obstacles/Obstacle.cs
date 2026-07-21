using UnityEngine;
using MukJump.Core;
using MukJump.Player;

namespace MukJump.Obstacles
{
    public enum ObstacleMotion
    {
        Static,
        Horizontal,
        Vertical,
    }

    /// 닿으면 플레이어를 사망시키는 원형 먹 가시 장애물.
    /// 이동형도 Transform만 움직이며 트리거이므로 발판 접지 판정에는 관여하지 않는다.
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class Obstacle : MonoBehaviour
    {
        ObstacleMotion motion;
        Vector3 origin;
        float amplitude;
        float speed;
        float phase;

        public void Configure(ObstacleMotion newMotion, float newAmplitude, float newSpeed, float newPhase)
        {
            motion = newMotion;
            origin = transform.position;
            amplitude = newAmplitude;
            speed = newSpeed;
            phase = newPhase;
        }

        void Update()
        {
            if (motion == ObstacleMotion.Static || GameManager.Instance == null ||
                GameManager.Instance.State != GameState.Playing) return;

            float offset = Mathf.Sin(Time.time * speed + phase) * amplitude;
            transform.position = motion == ObstacleMotion.Horizontal
                ? origin + Vector3.right * offset
                : origin + Vector3.up * offset;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
                player.TakeHit();
        }
    }
}
