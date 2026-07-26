using NUnit.Framework;
using UnityEngine;
using MukJump.Core;

namespace MukJump.EditorTests
{
    public sealed class WindWeatherControllerTests
    {
        [Test]
        public void CalculateVelocity_약한_바람은_수평_속도만_서서히_민다()
        {
            var result = WindWeatherController.CalculateVelocity(
                new Vector2(-1f, 3.2f),
                1f,
                0.8f,
                2.2f,
                false,
                0.7f,
                0.55f,
                0f,
                0.5f);

            Assert.That(result.x, Is.EqualTo(-0.6f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(3.2f).Within(0.0001f));
        }

        [Test]
        public void CalculateVelocity_바람보다_빠른_같은방향_점프를_감속하지_않는다()
        {
            var result = WindWeatherController.CalculateVelocity(
                new Vector2(5f, 1f),
                1f,
                0.8f,
                2.2f,
                false,
                0.7f,
                0.55f,
                0f,
                1f);

            Assert.That(result, Is.EqualTo(new Vector2(5f, 1f)));
        }

        [Test]
        public void CalculateVelocity_상승기류는_낙하를_멈추고_천천히_띄운다()
        {
            const float gravityScale = 2.2f;
            const float deltaTime = 0.02f;
            float gravityAcceleration = Mathf.Abs(Physics2D.gravity.y * gravityScale);
            var result = WindWeatherController.CalculateVelocity(
                new Vector2(0f, -8f),
                0f,
                0.8f,
                2.2f,
                true,
                0.7f,
                0.55f,
                gravityAcceleration,
                deltaTime);

            float velocityAfterGravity =
                result.y + Physics2D.gravity.y * gravityScale * deltaTime;
            Assert.That(velocityAfterGravity, Is.GreaterThanOrEqualTo(0f),
                "다음 물리 단계에서 중력이 적용된 뒤에도 아래로 떨어지면 안 됩니다.");
            Assert.That(velocityAfterGravity, Is.EqualTo(0.014f).Within(0.0001f));
            Assert.That(velocityAfterGravity, Is.LessThanOrEqualTo(0.55f));
        }

        [Test]
        public void CalculateVelocity_이미_빠른_상승은_상승기류가_덮어쓰지_않는다()
        {
            var result = WindWeatherController.CalculateVelocity(
                new Vector2(0f, 6f),
                -1f,
                0f,
                2.2f,
                true,
                0.7f,
                0.55f,
                Mathf.Abs(Physics2D.gravity.y * 2.2f),
                0.1f);

            Assert.That(result.y, Is.EqualTo(6f).Within(0.0001f));
        }

        [TestCase(HeightZoneController.Zone.QuietMountain)]
        [TestCase(HeightZoneController.Zone.WindPass)]
        [TestCase(HeightZoneController.Zone.InkRain)]
        [TestCase(HeightZoneController.Zone.RockGorge)]
        public void GetZoneStrengthMultiplier_모든_맵에_바람이_존재한다(
            HeightZoneController.Zone zone)
        {
            Assert.That(
                WindWeatherController.GetZoneStrengthMultiplier(zone),
                Is.GreaterThan(0f));
        }
    }
}
