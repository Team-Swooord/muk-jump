mergeInto(LibraryManager.library, {
    MukJumpTossFullScreenAdsSupported: function () {
        try {
            var sdk = window.AppsInToss;
            return sdk && typeof sdk.loadFullScreenAd === 'function' &&
                typeof sdk.showFullScreenAd === 'function' &&
                typeof sdk.loadFullScreenAd.isSupported === 'function' &&
                typeof sdk.showFullScreenAd.isSupported === 'function' &&
                sdk.loadFullScreenAd.isSupported() && sdk.showFullScreenAd.isSupported() ? 1 : 0;
        } catch (_) {
            return 0;
        }
    }
});
