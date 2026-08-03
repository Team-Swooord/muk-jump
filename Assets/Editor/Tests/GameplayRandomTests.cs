using NUnit.Framework;
using UnityEngine;
using MukJump.Core;

namespace MukJump.EditorTests
{
    public sealed class GameplayRandomTests
    {
        const int TestSeed = 20260726;

        [TearDown]
        public void TearDown()
        {
            GameplayRandom.ResetSession(0x4D554B);
        }

        [Test]
        public void ResetSession_같은Seed는_모든기능에서_같은수열을만든다()
        {
            foreach (GameplayRandomStream stream in
                     System.Enum.GetValues(typeof(GameplayRandomStream)))
            {
                GameplayRandom.ResetSession(TestSeed);
                var first = DrawSequence(stream, 24);

                GameplayRandom.ResetSession(TestSeed);
                var second = DrawSequence(stream, 24);

                CollectionAssert.AreEqual(first, second, $"{stream} 스트림이 재현되지 않습니다.");
            }
        }

        [Test]
        public void FeatureStreams_다른기능의호출횟수에영향받지않는다()
        {
            GameplayRandom.ResetSession(TestSeed);
            var expected = DrawSequence(GameplayRandomStream.Obstacles, 16);

            GameplayRandom.ResetSession(TestSeed);
            for (int i = 0; i < 200; i++)
            {
                GameplayRandom.Value(GameplayRandomStream.Items);
                GameplayRandom.Value(GameplayRandomStream.Weather);
            }
            var actual = DrawSequence(GameplayRandomStream.Obstacles, 16);

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void CosmeticUnityRandom_게임규칙수열을바꾸지않는다()
        {
            Random.State previousState = Random.state;
            try
            {
                GameplayRandom.ResetSession(TestSeed);
                var expected = DrawSequence(GameplayRandomStream.FallingRocks, 16);

                GameplayRandom.ResetSession(TestSeed);
                Random.InitState(99173);
                for (int i = 0; i < 500; i++)
                    _ = Random.value;
                var actual = DrawSequence(GameplayRandomStream.FallingRocks, 16);

                CollectionAssert.AreEqual(expected, actual);
            }
            finally
            {
                Random.state = previousState;
            }
        }

        [Test]
        public void ResetSession_같은Seed여도_세대번호는증가한다()
        {
            GameplayRandom.ResetSession(TestSeed);
            int firstVersion = GameplayRandom.SessionVersion;
            GameplayRandom.ResetSession(TestSeed);

            Assert.That(GameplayRandom.SessionSeed, Is.EqualTo(TestSeed));
            Assert.That(GameplayRandom.SessionVersion, Is.EqualTo(firstVersion + 1));
        }

        [Test]
        public void Range_정수와실수가요청한경계를벗어나지않는다()
        {
            GameplayRandom.ResetSession(TestSeed);
            foreach (GameplayRandomStream stream in
                     System.Enum.GetValues(typeof(GameplayRandomStream)))
            {
                for (int i = 0; i < 2000; i++)
                {
                    int integer = GameplayRandom.Range(stream, -7, 13);
                    float real = GameplayRandom.Range(stream, -2.5f, 4.75f);
                    Assert.That(integer, Is.GreaterThanOrEqualTo(-7).And.LessThan(13));
                    Assert.That(real, Is.GreaterThanOrEqualTo(-2.5f).And.LessThan(4.75f));
                }
            }
        }

        static int[] DrawSequence(GameplayRandomStream stream, int count)
        {
            var result = new int[count];
            for (int i = 0; i < count; i++)
                result[i] = GameplayRandom.Range(stream, int.MinValue, int.MaxValue);
            return result;
        }
    }
}
