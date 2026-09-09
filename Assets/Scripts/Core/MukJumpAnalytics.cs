using System;
using System.Collections.Generic;
using UnityEngine;

namespace MukJump.Core
{
    public enum AnalyticsScreen { Lobby, Playing, GameOver, Growth, Settings, Tutorial, Language, Account, Leaderboard, Privacy }
    public enum AnalyticsSetting { Music, Sound, Vibration, Language }
    public enum AnalyticsAdStage { LoadRequested, Loaded, LoadFailed, ShowRequested, Opened, Clicked, Closed, Failed, RewardEarned }
    public enum AnalyticsAccountAction { GuestLogin, AppleLink, Login, Logout, Delete }
    public enum AnalyticsDeathCause { Other, Fall, Obstacle }
    public enum AnalyticsOutcome { Requested, Success, Failed, Cancelled }

    /// 문자열 입력을 받지 않는 이벤트 계약. 계정 ID·원문 오류·좌표는 전달하지 않는다.
    public sealed class MukJumpAnalyticsEvent
    {
        public string Name { get; }
        public IReadOnlyDictionary<string, object> Parameters { get; }
        internal MukJumpAnalyticsEvent(string name, Dictionary<string, object> parameters)
        {
            Name = name;
            Parameters = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(parameters);
        }
    }

    /// 분석 장애는 게임 규칙에 영향을 주지 않는다. SDK 없이도 동작하며 고빈도 입력은 판별로 집계한다.
    public static class MukJumpAnalytics
    {
        const int QueueLimit = 128;
        static readonly object Gate = new();
        static readonly Queue<MukJumpAnalyticsEvent> Pending = new();
        static Action<MukJumpAnalyticsEvent> sink;
        static bool enabled;
        static AnalyticsScreen? screen;
        static string runKey;
        static int strokes, falls, hits, deaths, revives, peakSwarm, milestone;
        static double inkLength;
        static AnalyticsDeathCause deathCause;
        static readonly int[] Items = new int[4];
        static readonly int[] Milestones = { 50, 100, 250, 500, 750, 1000, 1500, 2000, 3000, 5000, 10000 };
        static int tutorialMask;
        static bool tutorialActive;

        public static bool IsCollecting { get { lock (Gate) return enabled; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            lock (Gate)
            {
                enabled = false;
                sink = null;
                Pending.Clear();
                ClearJourney();
            }
        }

        public static void SetCollectionEnabled(bool value)
        {
            lock (Gate)
            {
                if (enabled == value) return;
                enabled = value;
                // 철회 이전 데이터를 재동의 후 보내지 않으며 진행 중 판을 소급 생성하지 않는다.
                Pending.Clear();
                ClearJourney();
            }
        }

        static void ClearJourney()
        {
            screen = null;
            runKey = null;
            tutorialActive = false;
            tutorialMask = 0;
            strokes = falls = hits = deaths = revives = peakSwarm = milestone = 0;
            inkLength = 0;
            deathCause = AnalyticsDeathCause.Other;
            Array.Clear(Items, 0, Items.Length);
        }

        internal static void AttachSink(Action<MukJumpAnalyticsEvent> next)
        {
            lock (Gate)
            {
                sink = next;
                if (sink == null || !enabled) return;
                while (Pending.Count > 0) Deliver(Pending.Dequeue());
            }
        }

        static void Deliver(MukJumpAnalyticsEvent value)
        {
            try { sink?.Invoke(value); }
            catch { /* 분석 예외·원문에는 식별 정보가 있을 수 있어 출력하지 않는다. */ }
        }

        static Dictionary<string, object> Fields() => new() { ["schema_version"] = 1L };
        static string Token<T>(T value) where T : Enum => value.ToString().ToLowerInvariant();

        static void Emit(string name, Dictionary<string, object> fields)
        {
            lock (Gate)
            {
                if (!enabled) return;
                var value = new MukJumpAnalyticsEvent(name, fields);
                if (sink != null) Deliver(value);
                else
                {
                    if (Pending.Count == QueueLimit) Pending.Dequeue();
                    Pending.Enqueue(value);
                }
            }
        }

        public static void Screen(AnalyticsScreen value)
        {
            if (!IsCollecting || screen == value) return;
            screen = value;
            var p = Fields();
            p["screen_name"] = Token(value);
            p["screen_class"] = "muk_jump";
            Emit("screen_view", p);
        }

        // runKey는 메모리 중복 방지용이며 이벤트·사용자 속성으로 절대 전송하지 않는다.
        public static void BeginRun(string key, bool eligible, int growthLevel)
        {
            if (!IsCollecting || !eligible || string.IsNullOrEmpty(key) || runKey == key) return;
            runKey = key;
            strokes = falls = hits = deaths = revives = milestone = 0;
            peakSwarm = 1;
            inkLength = 0;
            deathCause = AnalyticsDeathCause.Other;
            Array.Clear(Items, 0, Items.Length);
            var p = Fields();
            p["level_name"] = "endless";
            p["growth_level"] = (long)Mathf.Clamp(growthLevel, 0, 32);
            Emit("level_start", p);
            Screen(AnalyticsScreen.Playing);
        }

        static bool HasRun => IsCollecting && runKey != null;
        public static void ExcludeDebugRun() { runKey = null; }
        public static void Stroke(float length)
        {
            if (!HasRun || float.IsNaN(length) || float.IsInfinity(length) || length <= 0) return;
            strokes = Math.Min(strokes + 1, 1000000);
            inkLength = Math.Min(inkLength + length, 10000000);
            if (strokes == 1) Emit("first_stroke", Fields());
        }

        public static void Item(MukJump.Items.ItemType type)
        {
            if (!HasRun || (int)type < 0 || (int)type >= Items.Length) return;
            Items[(int)type] = Math.Min(Items[(int)type] + 1, 1000000);
            // 반복 획득은 합계, 종류별 첫 획득은 아이템 이해도 퍼널로 남긴다.
            if (Items[(int)type] != 1) return;
            var p = Fields(); p["item_name"] = Token(type);
            Emit("item_first_pickup", p);
        }

        public static void Damage(bool fall)
        {
            if (!HasRun) return;
            if (fall) falls = Math.Min(falls + 1, 1000000);
            else hits = Math.Min(hits + 1, 1000000);
        }
        public static void PlayerDied(AnalyticsDeathCause cause = AnalyticsDeathCause.Other)
        {
            if (!HasRun) return;
            deaths = Math.Min(deaths + 1, 1000000);
            deathCause = cause;
        }

        public static void Progress(int height, int swarm)
        {
            if (!HasRun) return;
            peakSwarm = Math.Max(peakSwarm, Mathf.Clamp(swarm, 0, 24));
            while (milestone < Milestones.Length && height >= Milestones[milestone])
            {
                var p = Fields(); p["height_m"] = (long)Milestones[milestone++];
                Emit("height_milestone", p);
            }
        }

        static Dictionary<string, object> RunSummary(int height, float seconds)
        {
            var p = Fields();
            p["level_name"] = "endless";
            p["score"] = (long)Math.Max(0, height);
            p["duration_seconds"] = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0d : Math.Max(0d, seconds);
            p["stroke_count"] = (long)strokes;
            p["ink_length_m"] = inkLength;
            p["fall_damage_count"] = (long)falls;
            p["obstacle_damage_count"] = (long)hits;
            p["death_count"] = (long)deaths;
            p["death_cause"] = Token(deathCause);
            p["revive_count"] = (long)revives;
            p["peak_swarm"] = (long)peakSwarm;
            p["inkdrop_count"] = (long)Items[0];
            p["goldenbrush_count"] = (long)Items[1];
            p["shield_count"] = (long)Items[2];
            p["clone_count"] = (long)Items[3];
            return p;
        }

        public static void GameOver(int height, float seconds)
        {
            if (!HasRun) return;
            Emit("run_death", RunSummary(height, seconds));
            Screen(AnalyticsScreen.GameOver);
        }

        public static void EndRun(string key, int height, float seconds, bool abandoned, bool newBest)
        {
            if (!HasRun || runKey != key) return;
            var p = RunSummary(height, seconds);
            p["success"] = 0L; // 끝없는 모드는 클리어 성공을 만들지 않는다.
            p["end_reason"] = abandoned ? "abandon" : "death";
            Emit("level_end", p);
            if (!abandoned)
            {
                var score = Fields(); score["score"] = (long)Math.Max(0, height);
                score["level_name"] = "endless";
                score["new_best"] = newBest ? 1L : 0L;
                Emit("post_score", score);
            }
            runKey = null;
        }

        public static void Pause(bool paused)
        {
            if (!HasRun) return;
            Emit(paused ? "game_pause" : "game_resume", Fields());
        }
        public static void Revive()
        {
            if (!HasRun) return;
            revives++;
            Emit("revive", Fields());
            Screen(AnalyticsScreen.Playing);
        }

        public static void TutorialBegin()
        {
            if (!IsCollecting || tutorialActive) return;
            tutorialActive = true; tutorialMask = 0;
            Emit("tutorial_begin", Fields());
        }
        public static void TutorialStep(int step, bool review = false)
        {
            if (!IsCollecting || step < 0 || step >= 16) return;
            if (!review)
            {
                if (!tutorialActive || (tutorialMask & (1 << step)) != 0) return;
                tutorialMask |= 1 << step;
            }
            var p = Fields(); p["step"] = (long)step + 1;
            Emit(review ? "tutorial_review" : "tutorial_step", p);
        }
        public static void TutorialEnd(bool skipped, bool saved)
        {
            if (!tutorialActive) return;
            tutorialActive = false;
            var p = Fields(); p["progress_saved"] = saved ? 1L : 0L;
            Emit(skipped ? "tutorial_skip" : "tutorial_complete", p);
        }
        public static void TutorialInterrupted()
        {
            if (!tutorialActive) return;
            tutorialActive = false;
            Emit("tutorial_exit", Fields());
        }

        public static void EarnCurrency(int amount, int balance, bool refund = false)
        {
            if (amount <= 0) return;
            var p = Fields(); p["virtual_currency_name"] = "inklight";
            p["value"] = (long)amount; p["balance"] = (long)Math.Max(0, balance);
            p["source"] = refund ? "growth_refund" : "distance";
            Emit("earn_virtual_currency", p);
            if (refund) Emit("growth_reset", p);
        }
        public static void Upgrade(PermanentGrowthType type, int level, int cost, int balance)
        {
            var p = Fields(); p["virtual_currency_name"] = "inklight";
            p["item_name"] = Token(type); p["value"] = (long)Math.Max(0, cost);
            p["balance"] = (long)Math.Max(0, balance);
            Emit("spend_virtual_currency", p);
            var up = Fields(); up["character"] = Token(type); up["level"] = (long)Mathf.Clamp(level, 1, 8);
            Emit("level_up", up);
        }
        public static void Setting(AnalyticsSetting setting, int value)
        {
            var p = Fields(); p["setting"] = Token(setting); p["setting_value"] = (long)Mathf.Clamp(value, 0, 100);
            Emit("setting_change", p);
        }
        public static void Ad(AnalyticsAdStage stage, bool banner = false)
        {
            var p = Fields(); p["ad_format"] = banner ? "banner" : "rewarded";
            p["placement"] = banner ? "top_banner" : "game_over_revive";
            p["stage"] = Token(stage);
            Emit("ad_flow", p);
        }
        public static void Account(AnalyticsAccountAction action, AnalyticsOutcome outcome)
        {
            var p = Fields(); p["action"] = Token(action); p["result"] = Token(outcome);
            Emit("account_action", p);
        }
        public static void AccountState(MukJumpAccountPhase phase)
        {
            var p = Fields(); p["phase"] = Token(phase);
            Emit("account_state", p);
        }
        public static void Night(bool night)
        {
            var p = Fields(); p["theme"] = night ? "night" : "day";
            Emit("easter_egg", p);
        }

#if UNITY_EDITOR
        public static void UseSinkForTests(Action<MukJumpAnalyticsEvent> recorder, bool consent = true)
        {
            ResetStatics();
            SetCollectionEnabled(consent);
            AttachSink(recorder);
        }
        public static void ResetForTests() => ResetStatics();
        public static int PendingCountForTests { get { lock (Gate) return Pending.Count; } }
#endif
    }
}
