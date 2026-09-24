mergeInto(LibraryManager.library, {
    MukJumpWebAudioSetRecoveryAllowed__deps: ['$WEBAudio'],
    MukJumpWebAudioSetRecoveryAllowed: function (allowed) {
        // 엔진 AudioContext와 소리 설정을 유지한다. 채널·리스너의 일시정지는 게임이 소유한다.
        var recovery = Module.mukjumpAudioRecovery;
        if (!recovery) {
            recovery = { allowed: false, generation: 0, timer: 0, cycle: null, context: null };
            Module.mukjumpAudioRecovery = recovery;
            recovery.valid = function (context, generation) {
                return recovery.allowed && !document.hidden && recovery.generation === generation &&
                    context === WEBAudio.audioContext && context.state !== 'closed';
            };
            recovery.checkClock = function (repairs) {
                var context = WEBAudio.audioContext;
                if (!context || recovery.timer || recovery.cycle || !recovery.valid(context, recovery.generation)) return;
                var generation = recovery.generation;
                var before = context.currentTime;
                recovery.timer = setTimeout(function () {
                    recovery.timer = 0;
                    if (!recovery.valid(context, generation)) return;
                    if (context.state === 'running' && context.currentTime > before + 0.001) return;
                    // 일부 WebView는 running을 보고하면서 오디오 시계가 멈춘다.
                    // 복귀당 한 번만 같은 context를 순환하며, 광고 재진입/숨김 뒤에는 재개하지 않는다.
                    if (repairs || typeof context.suspend !== 'function') return;
                    if (context.state !== 'running' && context.state !== 'interrupted') return;
                    var token = {};
                    recovery.cycle = token;
                    try {
                        Promise.resolve(context.suspend()).then(function () {
                            if (recovery.valid(context, generation)) return context.resume();
                        }).then(function () {
                            if (recovery.cycle === token) recovery.cycle = null;
                            if (recovery.valid(context, generation)) recovery.checkClock(1);
                        }).catch(function () {
                            if (recovery.cycle === token) recovery.cycle = null;
                        });
                    } catch (_) { if (recovery.cycle === token) recovery.cycle = null; }
                }, 500);
            };
            recovery.resume = function (event) {
                // 광고 native 화면이 남긴 오래된 blur/pause는 visible 화면의 실제 사용자 입력으로만 재평가한다.
                // SDK의 dismissed를 대신 만들지 않으므로 진행 중 광고 음소거·보상 경계는 유지된다.
                if (event && event.isTrusted === true && !document.hidden &&
                    /^(pointerdown|pointerup|touchstart|touchend|mousedown|keydown)$/.test(event.type)) {
                    try { SendMessage('MobileApplicationLifecycle', 'OnWebForegroundInteraction', 'visible'); }
                    catch (_) { /* 다음 실제 입력에서 다시 전달한다. */ }
                }
                if (!recovery.allowed || document.hidden) return;
                var context = WEBAudio.audioContext;
                if (!context || context.state === 'closed') return;
                if (recovery.context !== context) {
                    if (recovery.context) recovery.context.removeEventListener('statechange', recovery.onStateChange);
                    recovery.context = context;
                    context.addEventListener('statechange', recovery.onStateChange);
                }
                if (context.state === 'suspended' || context.state === 'interrupted') {
                    try {
                        var promise = context.resume();
                        if (promise && promise.catch) promise.catch(function () {});
                    } catch (_) { /* 다음 실제 터치 또는 화면 복귀에서 다시 시도한다. */ }
                }
                recovery.checkClock(0);
            };
            recovery.onStateChange = function () { if (!recovery.cycle) recovery.resume(); };
            var events = ['pointerdown', 'pointerup', 'touchstart', 'touchend', 'mousedown', 'keydown', 'focus', 'pageshow'];
            events.forEach(function (name) { window.addEventListener(name, recovery.resume, true); });
            document.addEventListener('visibilitychange', recovery.resume, true);
            Module.deinitializers.push(function () {
                recovery.allowed = false;
                recovery.generation++;
                clearTimeout(recovery.timer);
                events.forEach(function (name) { window.removeEventListener(name, recovery.resume, true); });
                document.removeEventListener('visibilitychange', recovery.resume, true);
                if (recovery.context) recovery.context.removeEventListener('statechange', recovery.onStateChange);
                delete Module.mukjumpAudioRecovery;
            });
        }
        if (!allowed) {
            recovery.generation++;
            clearTimeout(recovery.timer);
            recovery.timer = 0;
            recovery.cycle = null;
        }
        recovery.allowed = !!allowed;
        if (recovery.allowed) recovery.resume();
    }
});
