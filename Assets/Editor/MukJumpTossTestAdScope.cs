using System;
using MukJump.Core;
using UnityEditor;
using UnityEditor.Build;

namespace MukJump.EditorTools
{
    /// 실제 토스 앱의 QR 테스트용. 운영 ID를 보존하고 공식 테스트 광고만 패키징한다.
    public sealed class MukJumpTossTestAdScope : IDisposable
    {
        readonly AppsInTossAdSettings settings;
        readonly string rewarded, interstitial, banner;

        public MukJumpTossTestAdScope()
        {
            settings = AssetDatabase.LoadAssetAtPath<AppsInTossAdSettings>(MukJumpAppsInTossSetup.AdSettingsPath);
            if (settings == null) throw new BuildFailedException("토스 광고 설정이 없습니다.");
            rewarded = settings.RewardedAdGroupId;
            interstitial = settings.InterstitialAdGroupId;
            banner = settings.BannerAdGroupId;
            settings.Configure(AppsInTossAdRuntime.TestRewardedId, AppsInTossAdRuntime.TestInterstitialId, AppsInTossAdRuntime.TestBannerId);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        public void Dispose()
        {
            // BuildPipeline이 미사용 에셋을 해제하므로 생성 시의 객체 참조를 재사용하지 않는다.
            var current = AssetDatabase.LoadAssetAtPath<AppsInTossAdSettings>(MukJumpAppsInTossSetup.AdSettingsPath);
            if (current == null) throw new BuildFailedException("테스트 후 토스 광고 설정을 복원할 수 없습니다.");
            current.Configure(rewarded, interstitial, banner);
            EditorUtility.SetDirty(current);
            AssetDatabase.SaveAssets();
        }
    }
}
