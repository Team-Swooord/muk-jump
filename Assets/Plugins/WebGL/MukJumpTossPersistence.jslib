mergeInto(LibraryManager.library, {
    MukJumpTossStorageReady: function () {
        try {
            var persistence = window.__AIT_PP;
            if (!persistence || !persistence.captured || !persistence.status) return 0;
            var status = persistence.status();
            return status.mode === 'ait' && !status.disabled && !status.foreign ? 1 : 0;
        } catch (_) { return 0; }
    }
});
