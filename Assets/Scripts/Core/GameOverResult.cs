namespace MukJump.Core
{
    public enum GameOverPersistenceState
    {
        Complete,
        ScoreBaselinePending,
        GrowthRecoveryRequired,
        RecordWritePending,
    }

    /// 결과창에 전달하는 한 판의 읽기 전용 정산 결과.
    public readonly struct GameOverResult
    {
        public GameOverResult(
            int height,
            int best,
            bool reachedNewBest,
            int earnedGrowthCurrency,
            int growthCurrencyBalance,
            bool rewardsAllowed,
            bool growthRewardSaved = true,
            bool recordSaved = true,
            GameOverPersistenceState persistenceState =
                GameOverPersistenceState.Complete)
        {
            Height = height;
            Best = best;
            ReachedNewBest = reachedNewBest;
            EarnedGrowthCurrency = earnedGrowthCurrency;
            GrowthCurrencyBalance = growthCurrencyBalance;
            RewardsAllowed = rewardsAllowed;
            GrowthRewardSaved = growthRewardSaved;
            RecordSaved = recordSaved;
            PersistenceState = persistenceState !=
                               GameOverPersistenceState.Complete
                ? persistenceState
                : !growthRewardSaved
                    ? GameOverPersistenceState.GrowthRecoveryRequired
                    : !recordSaved
                        ? GameOverPersistenceState.RecordWritePending
                        : GameOverPersistenceState.Complete;
        }

        public int Height { get; }
        public int Best { get; }
        public bool ReachedNewBest { get; }
        public int EarnedGrowthCurrency { get; }
        public int GrowthCurrencyBalance { get; }
        public bool RewardsAllowed { get; }
        public bool GrowthRewardSaved { get; }
        public bool RecordSaved { get; }
        public GameOverPersistenceState PersistenceState { get; }
    }
}
