namespace MukJump.Core
{
    /// 결과창에 전달하는 한 판의 읽기 전용 정산 결과.
    public readonly struct GameOverResult
    {
        public GameOverResult(
            int height,
            int best,
            bool reachedNewBest,
            int earnedGrowthCurrency,
            int growthCurrencyBalance,
            bool rewardsAllowed)
        {
            Height = height;
            Best = best;
            ReachedNewBest = reachedNewBest;
            EarnedGrowthCurrency = earnedGrowthCurrency;
            GrowthCurrencyBalance = growthCurrencyBalance;
            RewardsAllowed = rewardsAllowed;
        }

        public int Height { get; }
        public int Best { get; }
        public bool ReachedNewBest { get; }
        public int EarnedGrowthCurrency { get; }
        public int GrowthCurrencyBalance { get; }
        public bool RewardsAllowed { get; }
    }
}
