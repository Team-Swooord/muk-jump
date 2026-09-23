using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MukJump.Core
{
    /// 토스의 자체 지표만 연결한다. 네이티브 Firebase 동의·설정은 바꾸지 않는다.
    public static class AppsInTossAnalyticsRuntime
    {
        public const int RequestTimeoutMs = 5000;
        static EventQueue queue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!ShouldCollect(true, Debug.isDebugBuild)) return;
            queue = new EventQueue(SendAsync);
            MukJumpAnalytics.SetCollectionEnabled(true);
            MukJumpAnalytics.AttachSink(queue.Enqueue);
            Application.quitting -= Shutdown;
            Application.quitting += Shutdown;
#endif
        }

        public static bool ShouldCollect(bool webPlayer, bool development) => webPlayer && !development;

        static void Shutdown()
        {
            MukJumpAnalytics.AttachSink(null);
            MukJumpAnalytics.SetCollectionEnabled(false);
            queue?.Stop();
            queue = null;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        static async Task SendAsync(Event value)
        {
            if (value.IsScreen)
                await AppsInToss.AIT.AnalyticsScreen(value.Parameters, RequestTimeoutMs);
            else
                await AppsInToss.AIT.AnalyticsImpression(value.Parameters, RequestTimeoutMs);
        }
#endif

        public sealed class Event
        {
            public bool IsScreen { get; }
            public IReadOnlyDictionary<string, object> Parameters { get; }
            internal Event(bool screen, Dictionary<string, object> parameters)
            {
                IsScreen = screen;
                Parameters = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(parameters);
            }
        }

        // 전체 이벤트를 그대로 복사하지 않는다. 이름·화면 토큰·숫자를 허용 목록으로 제한한다.
        public static Event Map(MukJumpAnalyticsEvent source)
        {
            if (source == null) return null;
            var p = new Dictionary<string, object> { ["schema_version"] = 1L };
            bool screen = false;
            switch (source.Name)
            {
                case "screen_view":
                    if (!source.Parameters.TryGetValue("screen_name", out var value) || !(value is string name) ||
                        !Enum.TryParse(name, true, out AnalyticsScreen parsed) || !Enum.IsDefined(typeof(AnalyticsScreen), parsed) ||
                        parsed.ToString().ToLowerInvariant() != name) return null;
                    p["log_name"] = "mukjump_screen_view";
                    p["screen_name"] = name;
                    screen = true;
                    break;
                case "level_start":
                    p["log_name"] = "mukjump_run_started";
                    CopyNumber(source, p, "growth_level", 32);
                    break;
                case "first_stroke":
                    p["log_name"] = "mukjump_first_stroke";
                    break;
                case "post_score":
                    // GameManager의 저장 완료 뒤 EndRun에서 한 번만 발생한다.
                    // 중도 이탈·부활 대기·복원 기록을 완료 전환으로 세지 않는다.
                    p["log_name"] = "mukjump_run_completed";
                    CopyNumber(source, p, "score", int.MaxValue);
                    CopyNumber(source, p, "new_best", 1);
                    break;
                case "tutorial_complete":
                    if (!source.Parameters.TryGetValue("progress_saved", out var saved) || !Equals(saved, 1L)) return null;
                    p["log_name"] = "mukjump_tutorial_completed";
                    break;
                case "level_up":
                    p["log_name"] = "mukjump_growth_completed";
                    CopyNumber(source, p, "level", 8);
                    break;
                default:
                    return null;
            }
            return new Event(screen, p);
        }

        static void CopyNumber(MukJumpAnalyticsEvent source, Dictionary<string, object> target, string key, long max)
        {
            if (source.Parameters.TryGetValue(key, out var value) && value is long number && number >= 0 && number <= max)
                target[key] = number;
        }

        /// 메모리 한정·순차 전송. 느린 분석이 입력·저장·광고를 기다리게 하지 않는다.
        public sealed class EventQueue
        {
            public const int Capacity = 32;
            readonly Queue<Event> pending = new();
            readonly Func<Event, Task> send;
            bool draining, stopped;
            public int PendingCount => pending.Count;

            public EventQueue(Func<Event, Task> sender) => send = sender ?? throw new ArgumentNullException(nameof(sender));

            public void Enqueue(MukJumpAnalyticsEvent source)
            {
                if (stopped) return;
                var value = Map(source);
                if (value == null) return;
                if (pending.Count == Capacity) pending.Dequeue();
                pending.Enqueue(value);
                if (!draining) _ = DrainAsync();
            }

            async Task DrainAsync()
            {
                draining = true;
                try
                {
                    while (!stopped && pending.Count > 0)
                    {
                        var value = pending.Dequeue();
                        try { await send(value); }
                        catch
                        {
                            // 타임아웃은 실제 접수 여부가 불명확하다. 중복 전환을 피하려고
                            // 자동 재전송하지 않으며, 원문 오류·사용자 식별자는 출력하지 않는다.
                        }
                    }
                }
                finally { draining = false; }
            }

            public void Stop()
            {
                stopped = true;
                pending.Clear();
            }
        }
    }
}
