#if UNITY_EDITOR
using NUnit.Framework;
using MukJump.Drawing;
using MukJump.EditorTools;

namespace MukJump.EditorTests
{
    public sealed class RecordingScenarioTests
    {
        [Test]
        public void SubmissionTimelineFitsFiftySecondRecorderWindow()
        {
            Assert.That(RecordingScenarioDirector.StageCount, Is.EqualTo(9));
            Assert.That(
                RecordingScenarioDirector.ScheduledDuration,
                Is.InRange(42f, RecordingScenarioDirector.TargetDurationSeconds));
            for (int i = 0; i < RecordingScenarioDirector.StageCount; i++)
            {
                Assert.That(
                    RecordingScenarioDirector.GetStageLabel(i),
                    Is.Not.Empty);
                Assert.That(
                    RecordingScenarioDirector.GetStageDuration(i),
                    Is.GreaterThan(0f));
            }
        }

        [Test]
        public void StrokeCaptureExposesRecorderOnlyRealStrokePath()
        {
            Assert.That(
                typeof(StrokeCapture).GetMethod("BeginRecordingStroke"),
                Is.Not.Null);
            Assert.That(
                typeof(StrokeCapture).GetMethod("AppendRecordingStroke"),
                Is.Not.Null);
            Assert.That(
                typeof(StrokeCapture).GetMethod("EndRecordingStroke"),
                Is.Not.Null);
            Assert.That(
                StrokeCapture.ShouldProcessLivePointer(recorderOwnsStroke: true),
                Is.False,
                "촬영 획은 포인터 미입력으로 첫 프레임에 종료되면 안 됩니다.");
            Assert.That(
                StrokeCapture.ShouldProcessLivePointer(recorderOwnsStroke: false),
                Is.True);
        }
    }
}
#endif
