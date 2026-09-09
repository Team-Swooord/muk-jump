using System;
using System.Globalization;
using System.Text;

namespace MukJump.Core
{
    // 오프라인 게스트도 같은 규칙을 쓰는 닉네임 전용 필터다. 입력 원문을 외부에 전송하지 않는다.
    public static class NicknameContentFilter
    {
        static readonly string[] KoreanTerms = BuildKoreanTerms(new[]
        {
            "씨발", "시발", "씨팔", "시팔", "씨바", "ㅅㅂ", "ㅆㅂ", "개새끼", "개세끼", "개색기",
            "개새기", "새끼", "쌔끼", "씹", "좆", "좃", "지랄", "ㅈㄹ", "병신", "븅신", "빙신", "ㅂㅅ",
            "미친놈", "미친년", "개년", "썅년", "쌍년", "느금마", "느그엄마", "니애미", "니애비",
            "니미럴", "애미뒤진", "뒤져", "디져", "죽어버려", "죽여버려", "자살해", "꺼져",
            "섹스", "쎅스", "섹쓰", "쎅쓰", "야동", "야설", "포르노", "자위", "딸딸이", "딸잡이",
            "보지", "자지", "잠지", "좆물", "정액", "사정액", "질싸", "얼싸", "입싸", "육변기",
            "창녀", "창남", "매춘", "성매매", "강간", "윤간", "페니스", "딜도", "오르가즘", "떡치",
            "빠구리", "걸레년", "섹파", "성노예", "성폭행", "성폭력", "로리야동",
            "한남충", "한녀충", "김치년", "김치녀", "된장녀", "맘충", "틀딱", "짱깨", "쪽바리", "깜둥이"
        });

        static readonly string[] EnglishTerms =
        {
            "fuck", "shit", "bitch", "cunt", "motherfucker", "asshole", "bastard", "bullshit", "dickhead",
            "porn", "hentai", "dildo", "blowjob", "handjob", "cumshot", "semen", "orgasm", "penis", "vagina",
            "pussy", "whore", "slut", "horny", "sexy", "rapist", "pedophile", "paedophile",
            "nigger", "nigga", "faggot", "retard", "chink", "killyourself"
        };

        // 짧은 영단어는 전체 이름으로만 비교해 assassin, classic, Essex 같은 정상 이름을 보존한다.
        static readonly string[] ExactEnglishTerms = { "ass", "sex", "cum", "dick", "cock", "rape", "tit", "tits", "fag", "kys", "stfu", "wtf" };

        public static bool ContainsDisallowedContent(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            string decomposed;
            try { decomposed = value.Normalize(NormalizationForm.FormKD).ToLowerInvariant(); }
            catch (ArgumentException) { return true; }

            string korean = KoreanKey(decomposed);
            foreach (string term in KoreanTerms)
                if (korean.Contains(term)) return true;

            string english = EnglishKey(decomposed, true);
            string lettersOnly = EnglishKey(decomposed, false);
            // 알려진 정상 지명 전체만 예외 처리한다. 앞뒤에 욕설을 붙인 이름까지 허용하지 않는다.
            if (english == "scunthorpe") return false;
            string repeated = CollapseRepeatedLetters(english);
            foreach (string term in EnglishTerms)
                if (english.Contains(term) || lettersOnly.Contains(term) ||
                    repeated.Contains(CollapseRepeatedLetters(term))) return true;
            foreach (string term in ExactEnglishTerms)
                if (english == term || lettersOnly == term) return true;
            return false;
        }

        static string[] BuildKoreanTerms(string[] words)
        {
            for (int i = 0; i < words.Length; i++)
                words[i] = KoreanKey(words[i].Normalize(NormalizationForm.FormKD));
            return words;
        }

        static string KoreanKey(string value)
        {
            const string leading = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
            const string vowels = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
            const string trailing = "ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";
            var key = new StringBuilder(value.Length);
            // 완성형/분리 자모를 같은 키로 만들고 사이에 끼운 숫자·기호를 제외한다.
            foreach (char c in value)
            {
                if (c >= '\u1100' && c <= '\u1112') key.Append(leading[c - '\u1100']);
                else if (c >= '\u1161' && c <= '\u1175') key.Append(vowels[c - '\u1161']);
                else if (c >= '\u11a8' && c <= '\u11c2') key.Append(trailing[c - '\u11a8']);
            }
            return key.ToString();
        }

        static string EnglishKey(string value, bool convertDigits)
        {
            var key = new StringBuilder(value.Length);
            foreach (char original in value)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(original) == UnicodeCategory.NonSpacingMark) continue;
                char c = original;
                // 영문 비속어의 전각·숫자 치환·자주 쓰이는 동형 문자 우회를 함께 검사한다.
                if (convertDigits)
                    c = c switch { '0' => 'o', '1' => 'i', '3' => 'e', '4' => 'a', '5' => 's', '7' => 't', '8' => 'b', _ => c };
                c = c switch { 'а' => 'a', 'е' => 'e', 'о' => 'o', 'р' => 'p', 'с' => 'c', 'у' => 'y', 'х' => 'x', 'і' => 'i', 'ο' => 'o', _ => c };
                if (c >= 'a' && c <= 'z') key.Append(c);
            }
            return key.ToString();
        }

        static string CollapseRepeatedLetters(string value)
        {
            var result = new StringBuilder(value.Length);
            foreach (char c in value)
                if (result.Length == 0 || result[result.Length - 1] != c) result.Append(c);
            return result.ToString();
        }
    }
}
