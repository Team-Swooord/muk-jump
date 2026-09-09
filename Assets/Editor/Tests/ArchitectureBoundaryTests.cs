using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    /// 제출 직전에도 런타임 의존 경계와 입력·네트워크 정책이 되돌아가지 않게 막는다.
    public sealed class ArchitectureBoundaryTests
    {
        static readonly Regex LegacyInput = new(
            @"(?<![A-Za-z0-9_])Input\s*\.",
            RegexOptions.Compiled);
        static readonly Regex RuntimeEditorApi = new(
            @"\b(?:using\s+UnityEditor|UnityEditor\s*\.)",
            RegexOptions.Compiled);
        static readonly Regex RemoteRuntimeApi = new(
            @"\b(?:UnityWebRequest|HttpClient|WebRequest|apiEndpoint)\b",
            RegexOptions.Compiled);
        static readonly Regex MukJumpNamespace = new(
            @"\bnamespace\s+MukJump\.",
            RegexOptions.Compiled);

        [Test]
        public void RuntimeScriptsRespectSubmissionBoundaries()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "Scripts");
            string[] files = Directory.GetFiles(
                scriptsRoot, "*.cs", SearchOption.AllDirectories);

            Assert.That(files, Is.Not.Empty);
            foreach (string file in files)
            {
                string source = File.ReadAllText(file);
                string assetPath = "Assets" + file[Application.dataPath.Length..]
                    .Replace(Path.DirectorySeparatorChar, '/');

                Assert.That(LegacyInput.IsMatch(source), Is.False,
                    $"{assetPath}: 구 Input.* 대신 PointerInput/Input System을 사용해야 합니다.");
                if (!IsEntirelyEditorOnly(source))
                    Assert.That(RuntimeEditorApi.IsMatch(source), Is.False,
                        $"{assetPath}: UnityEditor API는 런타임 어셈블리에 들어갈 수 없습니다.");
                Assert.That(RemoteRuntimeApi.IsMatch(source), Is.False,
                    $"{assetPath}: 제출 빌드는 원격 API나 API 키에 의존하지 않아야 합니다.");
                Assert.That(MukJumpNamespace.IsMatch(source), Is.True,
                    $"{assetPath}: MukJump 기능 네임스페이스가 필요합니다.");
            }
        }

        static bool IsEntirelyEditorOnly(string source)
        {
            // 에디터 Play에 붙이는 MonoBehaviour는 Scripts에 있어야 하지만,
            // 파일 전체가 editor 전용일 때만 네이티브 빌드에서 확실히 제외된다.
            string[] lines = source.Trim().Split('\n');
            if (lines.Length < 2 || lines[0].Trim() != "#if UNITY_EDITOR")
                return false;
            int depth = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (Regex.IsMatch(line, @"^#if\b"))
                    depth++;
                else if (Regex.IsMatch(line, @"^#(?:else|elif)\b") && depth == 1)
                    return false;
                else if (line == "#endif")
                {
                    if (--depth == 0)
                        return i == lines.Length - 1;
                    if (depth < 0)
                        return false;
                }
            }
            return false;
        }

        [TestCase("#if UNITY_EDITOR\nusing UnityEditor;\n#endif", true)]
        [TestCase("#if UNITY_EDITOR\n#if DEBUG\n#endif\n#endif", true)]
        [TestCase("#if UNITY_EDITOR\n#else\nusing UnityEditor;\n#endif", false)]
        [TestCase("#if UNITY_EDITOR\n#elif UNITY_IOS\nusing UnityEditor;\n#endif", false)]
        [TestCase("#if UNITY_EDITOR\n#endif\nusing UnityEditor;", false)]
        [TestCase("#if UNITY_EDITOR\nusing UnityEditor;", false)]
        [TestCase("#if UNITY_EDITOR || UNITY_IOS\nusing UnityEditor;\n#endif", false)]
        public void EditorOnlyBoundaryMustCoverTheWholeFile(string source, bool expected)
        {
            Assert.That(IsEntirelyEditorOnly(source), Is.EqualTo(expected));
        }
    }
}
