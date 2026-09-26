namespace MukJump.Core
{
    public static partial class PermanentGrowthProfile
    {
        public static bool OwnsCosmetic(string id)
        {
            EnsureLoaded();
            return !writeBlocked && data.cosmetics.owned.Contains(id);
        }
    }
}
