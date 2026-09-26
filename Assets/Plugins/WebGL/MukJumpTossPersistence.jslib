mergeInto(LibraryManager.library, {
    $MukJumpTossStorageWatch__deps: ['$FS'],
    $MukJumpTossStorageWatch__postset: 'MukJumpTossStorageWatch();',
    $MukJumpTossStorageWatch: function () {
        var lookup = FS.lookupPath;
        FS.lookupPath = function (path) {
            var persistence = window.__AIT_PP;
            if (typeof path === 'string' && persistence && persistence.preparePrefsPath)
                persistence.preparePrefsPath(path);
            return lookup.apply(this, arguments);
        };
    },
    MukJumpTossStorageReady__deps: ['$MukJumpTossStorageWatch'],
    MukJumpTossStorageReady: function () {
        try {
            var persistence = window.__AIT_PP;
            if (!persistence || !persistence.captured || !persistence.status) return 0;
            var status = persistence.status();
            return status.mode === 'ait' && status.restoreReady === true && !status.disabled && !status.foreign ? 1 : 0;
        } catch (_) { return 0; }
    },
    MukJumpTossStorageCommitState: function () {
        try { return window.__AIT_PP.status().ownerCommitState || 0; }
        catch (_) { return -1; }
    },
    MukJumpTossConfirmStorageOwner: function () {
        try {
            return window.__AIT_PP && window.__AIT_PP.confirmOwner && window.__AIT_PP.confirmOwner() ? 1 : 0;
        } catch (_) { return 0; }
    }
});
