using System;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    public sealed partial class GameOverPopupView
    {
        const float GrowthBarWidth = 600f;
        const float GrowthPulseDuration = .34f;
        RectTransform growthRoot;
        Image growthFill;
        Image growthTip;
        Text growthCaption;
        Text growthAddedText;
        Text growthProgressText;
        Text growthEarnedText;
        GameOverResult growthResult;
        long growthBefore;
        long growthAdded;
        long growthShownAdded;
        int growthStartRewardCount;
        long growthLastCrossing;
        int growthPulseCount;
        float growthElapsed;
        float growthDuration;
        float growthPulse;
        float growthFullHold;
        bool growthAnimating;
        bool growthBound;

        void BuildGrowthProgress(RectTransform parent, Sprite brush)
        {
            growthRoot = CreateRect("GrowthProgress", parent, new Vector2(0f, -124f), new Vector2(600f, 108f));
            growthCaption = CreateText("Caption", growthRoot, "먹빛 누적", 36,
                new Vector2(-128f, 34f), new Vector2(344f, 40f), InkPalette.TextDark,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            growthAddedText = CreateText("Added", growthRoot, "+0m", 40,
                new Vector2(190f, 34f), new Vector2(220f, 40f), InkPalette.TextDark,
                FontStyle.Bold, TextAnchor.MiddleRight);
            CreateImage("Track", growthRoot, brush, Vector2.zero, new Vector2(GrowthBarWidth, 18f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, .17f));
            growthFill = CreateImage("Fill", growthRoot, brush, Vector2.zero,
                new Vector2(GrowthBarWidth, 18f), InkPalette.Ink);
            growthFill.type = Image.Type.Filled;
            growthFill.fillMethod = Image.FillMethod.Horizontal;
            growthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            // 한 개의 작은 붓 끝만 이동한다. 모든 품질 등급에서 같은 고정 UI를 재사용한다.
            growthTip = CreateImage("InkTip", growthRoot, InkUiTextureFactory.CreateBlobSprite(),
                Vector2.zero, new Vector2(20f, 26f), InkPalette.Gold);
            growthProgressText = CreateText("Progress", growthRoot, string.Empty, 34,
                new Vector2(-162f, -34f), new Vector2(276f, 36f), InkPalette.TextDark,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            growthEarnedText = CreateText("Earned", growthRoot, string.Empty, 36,
                new Vector2(146f, -34f), new Vector2(308f, 36f), InkPalette.Red,
                FontStyle.Bold, TextAnchor.MiddleRight);
            growthRoot.gameObject.SetActive(false);
        }

        void BindGrowthProgress(GameOverResult result, bool restart)
        {
            if (growthRoot == null) return;
            bool visible = result.RewardsAllowed && result.GrowthRewardSaved &&
                result.PersistenceState == GameOverPersistenceState.Complete &&
                (result.NextGrowthRewardDistanceMeters > 0 || result.GrowthDistanceJourneyComplete);
            bool sameDistance = growthBound && growthRoot.gameObject.activeSelf &&
                growthResult.CumulativeGrowthDistanceMeters == result.CumulativeGrowthDistanceMeters &&
                growthResult.PreviousGrowthRewardDistanceMeters == result.PreviousGrowthRewardDistanceMeters &&
                growthResult.GrowthDistanceRewardOffsetMeters == result.GrowthDistanceRewardOffsetMeters &&
                growthResult.GrowthDistanceJourneyComplete == result.GrowthDistanceJourneyComplete;
            growthRoot.gameObject.SetActive(visible);
            growthResult = result;
            growthBound = true;
            InkLocalizedText.SetSource(growthCaption, result.IsGrowthPreview ? "먹빛 누적 (예상)" : "먹빛 누적");
            if (!visible)
            {
                growthAnimating = false;
                return;
            }
            // 저장 재시도·부활 광고 준비 알림은 이미 재생 중인 숫자와 게이지를 되감지 않는다.
            if (!restart && sameDistance)
            {
                PaintGrowthProgress();
                return;
            }
            long total = Math.Max(0L, result.CumulativeGrowthDistanceMeters);
            growthBefore = result.GrowthDistanceBeforeMeters < 0 ? total :
                Math.Clamp(result.GrowthDistanceBeforeMeters, 0L, total);
            growthAdded = total - growthBefore;
            growthStartRewardCount = RunRewardCalculator.GetRewardCountForDistance(GrowthEffectiveDistance(growthBefore));
            growthShownAdded = 0;
            growthElapsed = growthPulse = growthFullHold = 0f;
            growthLastCrossing = growthPulseCount = 0;
            growthDuration = 1.1f + .22f * Math.Min(3L,
                RunRewardCalculator.GetRewardCountForDistance(GrowthEffectiveDistance(total)) - growthStartRewardCount);
            growthAnimating = growthAdded > 0 && !LobbySettingsProfile.ReducedMotionEnabled;
            if (!growthAnimating) FinishGrowthProgress();
            else PaintGrowthProgress();
        }

        void AdvanceGrowthProgress(float deltaTime)
        {
            if (!growthAnimating || growthRoot == null || !growthRoot.gameObject.activeSelf) return;
            if (LobbySettingsProfile.ReducedMotionEnabled)
            {
                FinishGrowthProgress();
                return;
            }
            float dt = float.IsFinite(deltaTime) ? Mathf.Clamp(deltaTime, 0f, .05f) : 0f;
            growthPulse = Mathf.Max(0f, growthPulse - dt);
            if (growthFullHold > 0f)
                growthFullHold = Mathf.Max(0f, growthFullHold - dt);
            else
            {
                growthElapsed = Mathf.Min(growthDuration, growthElapsed + dt);
                float t = EaseOutCubic(growthElapsed / growthDuration);
                growthShownAdded = t >= 1f ? growthAdded :
                    Math.Min(growthAdded, (long)Math.Round(growthAdded * (double)t));
                long crossing = RunRewardCalculator.GetRewardCountForDistance(
                    GrowthEffectiveDistance(growthBefore + growthShownAdded)) - growthStartRewardCount;
                if (crossing > growthLastCrossing)
                {
                    growthLastCrossing = crossing;
                    // 큰 기록도 수십 번 기다리지 않는다. 문턱 강조는 최대 세 번으로 합친다.
                    if (growthPulseCount < 3)
                    {
                        growthPulseCount++;
                        growthPulse = GrowthPulseDuration;
                        growthFullHold = .10f;
                    }
                }
            }
            if (growthElapsed >= growthDuration && growthPulse <= 0f && growthFullHold <= 0f)
                FinishGrowthProgress();
            else PaintGrowthProgress();
        }

        void FinishGrowthProgress()
        {
            growthAnimating = false;
            growthShownAdded = growthAdded;
            growthPulse = growthFullHold = 0f;
            if (growthRoot != null && growthRoot.gameObject.activeSelf) PaintGrowthProgress();
        }

        void PaintGrowthProgress()
        {
            long displayedTotal = growthBefore + growthShownAdded;
            bool complete = growthResult.GrowthDistanceJourneyComplete &&
                displayedTotal >= growthResult.NextGrowthRewardDistanceMeters;
            long effective = GrowthEffectiveDistance(displayedTotal);
            int reached = RunRewardCalculator.GetRewardCountForDistance(effective);
            // 문턱 직후 짧은 가득 참은 방금 채운 구간, 이후에는 새 구간의 분모를 쓴다.
            int interval = RunRewardCalculator.GetRequiredMetersForNextReward(
                growthFullHold > 0f && !complete ? Math.Max(0, reached - 1) : reached);
            int progress = complete || growthFullHold > 0f ? interval :
                (int)Math.Clamp(effective - RunRewardCalculator.GetThresholdForRewardCount(reached), 0L, interval);
            float fill = (float)progress / interval;
            growthFill.fillAmount = fill;
            float pulse = growthPulse > 0f ? Mathf.Sin(Mathf.PI * growthPulse / GrowthPulseDuration) : 0f;
            growthFill.color = Color.Lerp(InkPalette.Ink, InkPalette.Red, pulse * .6f);
            growthTip.enabled = growthAnimating && fill > .005f && growthFullHold <= 0f;
            growthTip.rectTransform.anchoredPosition = new Vector2((fill - .5f) * GrowthBarWidth, 0f);
            growthTip.rectTransform.localScale = new Vector3(1f, 1f + .12f * Mathf.Sin(growthElapsed * 18f), 1f);
            // 작은 마무리 팝만 쓴다. 종이·버튼·터치 영역은 흔들거나 잠그지 않는다.
            float settle = growthPulse > 0f && !LobbySettingsProfile.ReducedMotionEnabled
                ? Mathf.Sin((1f - growthPulse / GrowthPulseDuration) * Mathf.PI * 2f) *
                    Mathf.Pow(growthPulse / GrowthPulseDuration, 2f) : 0f;
            growthEarnedText.rectTransform.localScale = new Vector3(1f - settle * .06f, 1f + settle * .18f, 1f);
            growthFill.rectTransform.localScale = new Vector3(1f, 1f + Mathf.Abs(settle) * .22f, 1f);
            SetGrowthText(growthAddedText, "+" + FormatGrowthAdded(growthShownAdded));
            SetGrowthText(growthProgressText, $"{progress} / {interval}m");
            int reward = Math.Max(0, growthResult.IsGrowthPreview ?
                growthResult.PreviewGrowthCurrency : growthResult.EarnedGrowthCurrency);
            long crossed = reached - growthStartRewardCount;
            int shownReward = growthAnimating ? (int)Math.Min(reward, crossed) : reward;
            SetGrowthText(growthEarnedText, shownReward > 0
                ? (growthResult.IsGrowthPreview ? "예상 먹빛 +" : "먹빛 +") + shownReward : string.Empty);
        }

        long GrowthEffectiveDistance(long total) => RunRewardCalculator.SaturatingAdd(
            total, growthResult.GrowthDistanceRewardOffsetMeters);

        static string FormatGrowthAdded(long meters) => meters >= 1000000L
            ? $"{meters / 1000000d:0.#}Mm" : meters >= 10000L
                ? $"{meters / 1000d:0.#}km" : $"{meters}m";

        static void SetGrowthText(Text label, string source)
        {
            if (label.text != GameLocalization.Translate(source)) InkLocalizedText.SetSource(label, source);
        }
    }
}
