using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MukJump.Core
{
    // 게임 저장과 분리된 표시용 신원이다. 이 값을 서버 기록의 소유권 판단에 쓰지 않는다.
    public static class MukJumpIdentityProfile
    {
        public const string LocalScope = "local-guest";
        public const int MaxNicknameLength = 10;
        const string Prefix = "MukJump.Identity.";
        public interface IStore
        {
            string Read(string key);
            void Write(string key, string value);
            void Save();
        }
        sealed class PreferenceStore : IStore
        {
            public string Read(string key) => PlayerPrefs.GetString(Prefix + key, string.Empty);
            public void Write(string key, string value) => PlayerPrefs.SetString(Prefix + key, value);
            public void Save() => PlayerPrefs.Save();
        }
        static IStore store = new PreferenceStore();

        static string Key(string scope, string field) =>
            Hash128.Compute(scope ?? string.Empty) + "." + field;

        public static string LocalUid
        {
            get
            {
                string uid = store.Read("LocalUid");
                if (!string.IsNullOrEmpty(uid)) return uid;
                // 오프라인 ID는 뒤끝 UID로 오인되지 않도록 접두어를 붙인다.
                uid = "local-" + Guid.NewGuid().ToString("N");
                store.Write("LocalUid", uid);
                store.Save();
                return uid;
            }
        }

        public static string GuestNickname
        {
            get
            {
                string nickname = ReadNickname(LocalScope);
                if (IsGeneratedNickname(nickname) && nickname.Length <= MaxNicknameLength) return nickname;
                nickname = CreateGuestNickname(LocalUid);
                SaveNickname(LocalScope, nickname);
                return nickname;
            }
        }

        public static string CreateGuestNickname(string seed)
        {
            // 플레이 난수 스트림을 소비하지 않으며 재접속마다 이름이 바뀌지 않는다.
            uint value = 2166136261;
            foreach (char c in seed ?? string.Empty) value = unchecked((value ^ c) * 16777619);
            return "guest" + (value % 100000).ToString("D5", CultureInfo.InvariantCulture);
        }

        public static bool IsGeneratedNickname(string value)
        {
            if (value == null) return false;
            int start, end;
            if (value.Length == 10 && value.StartsWith("guest", StringComparison.Ordinal))
            { start = 5; end = 10; }
            // 구 guest(난수)도 기본 이름으로 인식해 Apple 첫 닉네임 설정을 유지한다.
            else if (value.Length == 17 && value.StartsWith("guest(", StringComparison.Ordinal) && value[16] == ')')
            { start = 6; end = 16; }
            else return false;
            for (int i = start; i < end; i++) if (value[i] < '0' || value[i] > '9') return false;
            return true;
        }

        public static string ReadUid(string scope) => store.Read(Key(scope, "Uid"));
        public static string ReadNickname(string scope) => store.Read(Key(scope, "Nickname"));
        public static void SaveUid(string scope, string uid)
        {
            if (string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(uid)) return;
            store.Write(Key(scope, "Uid"), uid.Trim());
            store.Save();
        }
        public static void SaveNickname(string scope, string nickname)
        {
            store.Write(Key(scope, "Nickname"), nickname ?? string.Empty);
            store.Save();
        }

        public static bool TryNormalizeNickname(string input, out string value, out string error)
        {
            value = string.Empty;
            error = string.Empty;
            try { value = (input ?? string.Empty).Trim().Normalize(NormalizationForm.FormKC); }
            catch (ArgumentException)
            { error = "한글, 영문, 숫자, _와 -만 사용할 수 있어요"; return false; }
            if (value.Length < 2 || value.Length > MaxNicknameLength)
                error = "닉네임은 2~10자로 입력해 주세요";
            else
            {
                foreach (char c in value)
                    if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    {
                        error = "한글, 영문, 숫자, _와 -만 사용할 수 있어요";
                        break;
                    }
            }
            if (error.Length == 0 && IsGeneratedNickname(value))
                error = "자동 생성 형식 대신 다른 닉네임을 입력해 주세요";
            if (error.Length == 0 && NicknameContentFilter.ContainsDisallowedContent(value))
                error = "사용할 수 없는 표현이 포함되어 있어요";
            return error.Length == 0;
        }

#if UNITY_EDITOR
        public static void UseStoreForTests(IStore value) => store = value ?? new PreferenceStore();
#endif
    }
}
