mergeInto(LibraryManager.library, {
    MukJumpWebAudioSetRecoveryAllowed__deps: ['$WEBAudio'],
    MukJumpWebAudioSetRecoveryAllowed: function (allowed) {
        // 엔진 AudioContext를 그대로 사용한다. 소리 설정·리스너 정지·채널은 변경하지 않는다.
        var recovery = Module.mukjumpAudioRecovery;
        if (!recovery) {
            recovery = { allowed: false };
            Module.mukjumpAudioRecovery = recovery;
            recovery.resume = function () {
                if (!recovery.allowed || document.hidden) return;
                var context = WEBAudio.audioContext;
                if (!context || (context.state !== 'suspended' && context.state !== 'interrupted')) return;
                try {
                    var promise = context.resume();
                    if (promise && promise.catch) promise.catch(function () {});
                } catch (_) { /* 다음 실제 터치 또는 화면 복귀에서 다시 시도한다. */ }
            };
            // iOS 사용자 활성화는 touchend 시점에도 필요하다. capture에서 연결하여
            // 캔버스 입력이 버블링을 막더라도 첫 터치의 재개 기회를 잃지 않는다.
            var events = ['pointerdown', 'pointerup', 'touchstart', 'touchend', 'mousedown', 'keydown', 'focus', 'pageshow'];
            events.forEach(function (name) { window.addEventListener(name, recovery.resume, true); });
            document.addEventListener('visibilitychange', recovery.resume, true);
            Module.deinitializers.push(function () {
                events.forEach(function (name) { window.removeEventListener(name, recovery.resume, true); });
                document.removeEventListener('visibilitychange', recovery.resume, true);
                delete Module.mukjumpAudioRecovery;
            });
        }
        recovery.allowed = !!allowed;
        if (recovery.allowed) recovery.resume();
    }
});
