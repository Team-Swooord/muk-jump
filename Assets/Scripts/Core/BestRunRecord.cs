using System;
using System.Globalization;

namespace MukJump.Core
{
    /// 최고 고도와 그 고도를 처음 달성한 실제 플레이 초는 항상 한 쌍이다.
    public readonly struct BestRunRecord
    {
        // int 최대 고도도 뒤끝의 정확한 정수 범위(2^53) 안에서 정렬한다.
        public const long RankStride = 4194304L;
        public const int MaximumTimedSeconds = (int)RankStride - 2;
        public const string TimeColumn = "bestTimeSeconds";
        public const string RankColumn = "heightTimeScore";
        public readonly int Height;
        public readonly int Seconds;
        public BestRunRecord(int height, int seconds = -1)
        {
            Height = Math.Max(0, height);
            Seconds = Height > 0 && seconds >= 0 && seconds <= MaximumTimedSeconds ? seconds : -1;
        }
        public long RankScore => Height * RankStride + (Seconds >= 0 ? RankStride - 1 - Seconds : 0);
        public bool IsBetterThan(BestRunRecord other) => RankScore > other.RankScore;
        public static BestRunRecord Better(BestRunRecord a, BestRunRecord b) => b.IsBetterThan(a) ? b : a;
        public static BestRunRecord FromRankScore(long score)
        {
            if (score < 0 || score > int.MaxValue * RankStride + RankStride - 1) return new BestRunRecord(0);
            long remainder = score % RankStride;
            return new BestRunRecord((int)(score / RankStride), remainder == 0 ? -1 : (int)(RankStride - 1 - remainder));
        }
        public static string FormatTime(int seconds) => seconds < 0 ? "--:--" :
            (seconds / 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
            (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        public string Encode() => "1|" + Height.ToString(CultureInfo.InvariantCulture) + "|" + Seconds.ToString(CultureInfo.InvariantCulture);
        public static BestRunRecord Decode(string value)
        {
            string[] parts = (value ?? "").Split('|');
            return parts.Length == 3 && parts[0] == "1" &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int height) &&
                int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)
                ? new BestRunRecord(height, seconds) : new BestRunRecord(0);
        }
    }
}
