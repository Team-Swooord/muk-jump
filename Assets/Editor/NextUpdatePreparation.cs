using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Player;

namespace MukJump.EditorTools
{
    // 연결 도구가 응답하지 않을 때도 동일한 공식 빌더·컴파일·감사를 실행하는 로컬 검증 진입점.
    public static class NextUpdatePreparation
    {
        const string Output = "output/qa/next-update-scope-20260928/";

        public static void PrepareScene()
        {
            MukJumpSceneBuilder.Build();
            Save("scene-audit.json", MukJumpAgentAudit.Audit());
        }

        public static void CompileAndAudit()
        {
            foreach (BuildTarget target in new[] { BuildTarget.iOS, BuildTarget.WebGL })
            {
                string directory = Path.GetFullPath(Output + "managed-" + target);
                Directory.CreateDirectory(directory);
                var settings = new ScriptCompilationSettings
                {
                    target = target,
                    group = BuildPipeline.GetBuildTargetGroup(target),
                    options = ScriptCompilationOptions.None
                };
                var result = PlayerBuildInterface.CompilePlayerScripts(settings, directory);
                if (result.assemblies == null || result.assemblies.Count == 0)
                    throw new InvalidOperationException(target + " 관리 코드 컴파일 결과가 없습니다.");
                Save("managed-" + target + ".json", new { target = target.ToString(), assemblies = result.assemblies.Count });
            }
            AuditMainScene();
        }

        public static void AuditMainScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            // HTTP 연결 상태와 별개로 같은 패키지의 재컴파일 명령·내부 상태를 직접 확인한다.
            Type recompile = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                recompile = assembly.GetType("Unity.Pipeline.Editor.Commands.RecompileCommand");
                if (recompile != null) break;
            }
            if (recompile == null) throw new InvalidOperationException("재컴파일 검사 명령이 없습니다.");
            recompile.GetMethod("Recompile").Invoke(null, new object[] { false });
            var status = JObject.Parse((string)recompile.GetMethod("RecompileStatus").Invoke(null, null));
            Save("recompile-status.json", status);
            if (status.Value<bool?>("failed") != false ||
                (status.Value<string>("status") != "completed" && status.Value<string>("status") != "up_to_date"))
                throw new InvalidOperationException("재컴파일 완료를 확인하지 못했습니다: " + status);
            Save("final-audit.json", MukJumpAgentAudit.Audit());
        }

        static void Save(string name, object result)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + name, JsonConvert.SerializeObject(result, Formatting.Indented));
        }
    }
}
