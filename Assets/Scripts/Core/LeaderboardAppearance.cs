using System.Globalization;

namespace MukJump.Core
{
    /// 기존 리더보드의 단일 추가 항목을 지역|형식 버전|착용 ID로 확장한다.
    /// 구형 KR/US/JP 값은 기본 먹으로 읽는다. 점수·계정 정보는 담지 않는다.
    public static class LeaderboardAppearance
    {
        public static string Encode(string region, InkSkinId skin) =>
            DeviceRegion.Normalize(region) + "|1|" + ((int)InkSkinCatalog.Normalize(skin)).ToString(CultureInfo.InvariantCulture);

        public static string Current => Encode(DeviceRegion.Current, InkSkinPreferences.PersistedEquipped);

        public static void Decode(string value, out string region, out InkSkinId skin)
        {
            skin = InkSkinId.Ink;
            region = string.Empty;
            if (string.IsNullOrEmpty(value) || value.Length > 32) return;
            string[] fields = value.Split('|');
            region = DeviceRegion.Normalize(fields[0]);
            if (fields.Length != 3 || fields[1] != "1" ||
                !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int id)) return;
            skin = InkSkinCatalog.Normalize((InkSkinId)id);
        }
    }
}
