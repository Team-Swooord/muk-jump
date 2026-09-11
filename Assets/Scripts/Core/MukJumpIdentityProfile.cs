using System;
using System.Collections.Generic;
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

        /// 탈퇴한 계정의 표시 캐시만 비우고 새 오프라인 게스트를 만든다.
        /// 재실행·로그아웃에서는 호출하지 않으며 다른 계정의 캐시는 보존한다.
        public static bool TryResetForAccountDeletion(string deletedScope)
        {
            try
            {
                string previous = GuestNickname;
                string nextUid = "local-" + Guid.NewGuid().ToString("N");
                string nextNickname = CreateGuestNickname(nextUid);
                // 5자리 난수가 우연히 같아도 삭제 전 이름을 다시 배정하지 않는다.
                if (nextNickname == previous)
                    nextNickname = "guest" + ((int.Parse(nextNickname.Substring(5),
                        CultureInfo.InvariantCulture) + 1) % 100000).ToString("D5", CultureInfo.InvariantCulture);
                store.Write("LocalUid", nextUid);
                store.Write(Key(LocalScope, "Nickname"), nextNickname);
                store.Write(Key(LocalScope, "Uid"), string.Empty);
                if (!string.IsNullOrWhiteSpace(deletedScope) && deletedScope != LocalScope)
                {
                    store.Write(Key(deletedScope, "Uid"), string.Empty);
                    store.Write(Key(deletedScope, "Nickname"), string.Empty);
                }
                store.Save();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[MukJump] 탈퇴 계정의 표시 정보 삭제를 다시 시도합니다: " + exception.Message);
                return false;
            }
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

        static readonly Dictionary<char, char> nicknameDisplayJamo = CreateNicknameDisplayJamo();

        static Dictionary<char, char> CreateNicknameDisplayJamo()
        {
            var map = new Dictionary<char, char>();
            // ㅄ 같은 겹자음은 현대 초성 범위 밖으로 정규화되므로 실제 호환 자모 전체에서 역매핑한다.
            for (char c = '\u3131'; c <= '\u318e'; c++)
            {
                string normalized = c.ToString().Normalize(NormalizationForm.FormKC);
                if (normalized.Length == 1) map[normalized[0]] = c;
            }
            const string trailing = "ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";
            for (int i = 0; i < trailing.Length; i++) map[(char)('\u11a8' + i)] = trailing[i];
            return map;
        }

        /// 정규화된 조합용 낱자만 표시용 자모로 바꾼다. 서버/캐시는 쓰지 않는다.
        public static string FormatNicknameForDisplay(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            try { value = value.Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { return value; }
            var display = new StringBuilder(value.Length);
            foreach (char c in value)
                display.Append(nicknameDisplayJamo.TryGetValue(c, out char visible) ? visible : c);
            return display.ToString();
        }

        public static bool TryNormalizeNickname(string input, out string value, out string error)
        {
            value = string.Empty;
            error = string.Empty;
            // 기존 이름 중복 판정/서버 저장 규칙은 유지하고, 낱자의 글리프 보정은 화면에서만 한다.
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
