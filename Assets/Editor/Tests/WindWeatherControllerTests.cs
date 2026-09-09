using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using MukJump.Core;

namespace MukJump.EditorTests
{
    public sealed class WindWeatherControllerTests
    {
        [Test]
        public void UpdraftAndGaleHaveSeparateGravityAndRecovery()
        {
            var go = new GameObject("WeatherPhaseTest");
            try
            {
                var weather = go.AddComponent<WindWeatherController>();
                var setPhase = typeof(WindWeatherController).GetMethod("SetPhase",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                setPhase.Invoke(weather, new object[] { WindWeatherPhase.Updraft });
                Assert.That(weather.IsUpdraftActive, Is.True);
                Assert.That(weather.GravitySuppression, Is.Zero);
                Assert.That(weather.RisingGravityMultiplier, Is.EqualTo(.65f));
                setPhase.Invoke(weather, new object[] { WindWeatherPhase.Downdraft });
                Assert.That(weather.RisingGravityMultiplier, Is.EqualTo(1.35f));
                Assert.That(weather.GravitySuppression, Is.Zero);
                setPhase.Invoke(weather, new object[] { WindWeatherPhase.Gale });
                Assert.That(weather.IsUpdraftActive, Is.False);
                Assert.That(weather.IsGaleActive, Is.True);
                Assert.That(weather.GravitySuppression, Is.EqualTo(1f));
                setPhase.Invoke(weather, new object[] { WindWeatherPhase.Recovery });
                Assert.That(weather.GravitySuppression, Is.EqualTo(1f));
                setPhase.Invoke(weather, new object[] { WindWeatherPhase.Updraft });
                setPhase.Invoke(weather, new object[] { WindWeatherPhase.Recovery });
                Assert.That(weather.GravitySuppression, Is.Zero);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void UpdraftDriftMovesInBothAxesAndChangesDirection()
        {
            Vector2 velocity = Vector2.zero;
            float minX = 0, maxX = 0, minY = 0, maxY = 0;
            for (int frame = 0; frame < 275; frame++)
            {
                velocity = WindWeatherController.CalculateDriftVelocity(velocity, frame * .02f,
                    0f, new Vector2(.5f, .5f), .02f);
                minX = Mathf.Min(minX, velocity.x); maxX = Mathf.Max(maxX, velocity.x);
                minY = Mathf.Min(minY, velocity.y); maxY = Mathf.Max(maxY, velocity.y);
                Assert.That(velocity.magnitude, Is.LessThan(8f));
            }
            Assert.That(minX, Is.LessThan(-3f)); Assert.That(maxX, Is.GreaterThan(3f));
            Assert.That(minY, Is.LessThan(-2f)); Assert.That(maxY, Is.GreaterThan(2f));
        }

        [Test]
        public void DriftSteersAwayFromEdgesAndDoesNotAdvanceWhenPaused()
        {
            Vector2 velocity = new(1f, -1f);
            Assert.That(WindWeatherController.CalculateDriftVelocity(velocity, 2f, 1f, Vector2.zero, 0f),
                Is.EqualTo(velocity));
            for (int step = 0; step < 40; step++)
            {
                float phase = step * .2f;
                Vector2 left = WindWeatherController.CalculateDriftVelocity(Vector2.zero, 2f, phase,
                    new Vector2(.02f, .1f), .02f);
                Vector2 right = WindWeatherController.CalculateDriftVelocity(Vector2.zero, 2f, phase,
                    new Vector2(.98f, .95f), .02f);
                Assert.That(left.x, Is.GreaterThan(0)); Assert.That(left.y, Is.GreaterThan(0));
                Assert.That(right.x, Is.LessThan(0)); Assert.That(right.y, Is.LessThan(0));
            }
        }

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

        [TestCase(WindWeatherPhase.Breeze)]
        [TestCase(WindWeatherPhase.Warning)]
        [TestCase(WindWeatherPhase.Updraft)]
        [TestCase(WindWeatherPhase.Recovery)]
        public void 광고부활은_같은_날씨_세션과_예약을_보존한다(
            WindWeatherPhase phase)
        {
            var host = new GameObject("WindReviveStateTests");
            host.SetActive(false);
            var weather = host.AddComponent<WindWeatherController>();
            try
            {
                SetField(weather, "sessionActive", true);
                SetField(weather, "phaseElapsed", 0.73f);
                SetField(weather, "directionHoldRemaining", 17f);
                SetProperty(weather, "Phase", phase);
                SetProperty(weather, "DirectionSign", -1);
                SetProperty(weather, "DirectionBlend", -0.65f);
                SetProperty(weather, "NextUpdraftHeight", 612);
                SetProperty(weather, "Strength01", 0.9f);

                InvokeStateChanged(
                    weather,
                    GameState.Playing,
                    GameState.GameOver);
                Assert.That(weather.Strength01, Is.Zero);
                AssertWeatherStatePreserved(weather, phase);

                InvokeStateChanged(
                    weather,
                    GameState.GameOver,
                    GameState.Playing);
                AssertWeatherStatePreserved(weather, phase);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void 로비에서_시작할_때만_첫_풍맥_구간을_새로_예약한다()
        {
            var host = new GameObject("WindNewRunStateTests");
            host.SetActive(false);
            var weather = host.AddComponent<WindWeatherController>();
            try
            {
                SetField(weather, "sessionActive", true);
                SetProperty(weather, "Phase", WindWeatherPhase.Updraft);
                SetProperty(weather, "NextUpdraftHeight", 999);

                InvokeStateChanged(
                    weather,
                    GameState.Playing,
                    GameState.Lobby);
                Assert.That(GetField<bool>(weather, "sessionActive"), Is.False);
                Assert.That(weather.NextUpdraftHeight, Is.Zero);

                InvokeStateChanged(
                    weather,
                    GameState.Lobby,
                    GameState.Playing);
                Assert.That(GetField<bool>(weather, "sessionActive"), Is.True);
                Assert.That(weather.Phase, Is.EqualTo(WindWeatherPhase.Breeze));
                Assert.That(weather.NextUpdraftHeight, Is.InRange(180, 260));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        static void AssertWeatherStatePreserved(
            WindWeatherController weather,
            WindWeatherPhase phase)
        {
            Assert.That(GetField<bool>(weather, "sessionActive"), Is.True);
            Assert.That(weather.Phase, Is.EqualTo(phase));
            Assert.That(weather.NextUpdraftHeight, Is.EqualTo(612));
            Assert.That(weather.DirectionSign, Is.EqualTo(-1));
            Assert.That(weather.DirectionBlend, Is.EqualTo(-0.65f));
            Assert.That(GetField<float>(weather, "phaseElapsed"),
                Is.EqualTo(0.73f));
            Assert.That(GetField<float>(weather, "directionHoldRemaining"),
                Is.EqualTo(17f));
        }

        static void InvokeStateChanged(
            WindWeatherController target,
            GameState previous,
            GameState next)
        {
            MethodInfo method = typeof(WindWeatherController).GetMethod(
                "HandleStateChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, new object[] { previous, next });
        }

        static void SetField<T>(object target, string name, T value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        static T GetField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(target);
        }

        static void SetProperty<T>(object target, string name, T value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, name);
            property.SetValue(target, value);
        }
    }

    public sealed class ScoreManagerRecordTests
    {
        [TestCase(0, 0, false)]
        [TestCase(10, 10, false)]
        [TestCase(11, 10, true)]
        [TestCase(1, 0, true)]
        public void BeatsRecord_이전_기록을_실제로_넘을_때만_신기록이다(
            int height, int previousBest, bool expected)
        {
            Assert.That(
                ScoreManager.BeatsRecord(height, previousBest),
                Is.EqualTo(expected));
        }
    }
}
