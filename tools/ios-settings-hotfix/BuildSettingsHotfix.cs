using System;
using System.IO;
using System.Reflection;
using MukJump.EditorTools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// 승인된 소스 스냅샷에서만 실행하며, 기존 main이나 기존 Archive에는 쓰지 않는다.
public static class BuildSettingsHotfix
{
    static string Folder => Path.GetFullPath("..");
    static string activeJob;
    static bool simulator;
    static void Guard()
    {
        if (!Application.dataPath.EndsWith("/2026-09-27-build-44/source/Assets"))
            throw new InvalidOperationException("빌드 44 전용 소스 스냅샷이 아닙니다.");
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("에디터가 작업 중입니다.");
    }
    public static object StartDevice() => Start(false);
    public static object StartSimulator() => Start(true);
    public static object Prepare()
    {
        Guard();
        PlayerSettings.bundleVersion = "1.0.3";
        PlayerSettings.iOS.buildNumber = "44";
        const string pipeline = "Assets/Settings/Pipeline/EditorPipelineManager.asset";
        if (File.Exists(pipeline))
        {
            string error = AssetDatabase.MoveAsset(pipeline, "Assets/Editor/HotfixEditorPipeline.asset");
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            if (Directory.GetFiles("Assets/Settings/Pipeline").Length == 0)
                AssetDatabase.DeleteAsset("Assets/Settings/Pipeline");
        }
        AssetDatabase.SaveAssets();
        MukJumpSceneBuilder.Build();
        return MukJumpAgentAudit.Audit();
    }
    static object Start(bool forSimulator)
    {
        Guard();
        if (PlayerSettings.bundleVersion != "1.0.3" || PlayerSettings.iOS.buildNumber != "44")
            throw new InvalidOperationException("앱 버전 또는 빌드 번호가 다릅니다.");
        simulator = forSimulator;
        activeJob = Path.Combine(Folder, forSimulator ? "simulator-export.json" : "device-export.json");
        if (File.Exists(activeJob)) throw new InvalidOperationException("기존 작업 결과를 먼저 확인해야 합니다.");
        File.WriteAllText(activeJob, new JObject { ["status"] = "queued", ["build"] = "44", ["simulator"] = simulator }.ToString());
        EditorApplication.update += RunOnce;
        return new { queued = true, stateFile = activeJob };
    }
    static void RunOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= RunOnce;
        var state = JObject.Parse(File.ReadAllText(activeJob));
        var sdk = PlayerSettings.iOS.sdkVersion;
        var architecture = PlayerSettings.iOS.simulatorSdkArchitecture;
        state["status"] = "running";
        state["startedUtc"] = DateTime.UtcNow.ToString("o");
        File.WriteAllText(activeJob, state.ToString());
        try
        {
            MukJumpStoreBuild.ValidateAppStoreReadiness();
            PlayerSettings.iOS.sdkVersion = simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
            if (simulator) PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.X86_64;
            AssetDatabase.SaveAssets();
            string output = Path.Combine(Folder, simulator ? "Xcode-Simulator-44" : "Xcode-AppStore-44");
            if (Directory.Exists(output)) throw new InvalidOperationException("새 Xcode 출력 경로가 이미 존재합니다.");
            typeof(MukJumpStoreBuild).GetMethod("BuildIosProjectAt", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { output, "설정창 최소 수정 빌드 44", true,
                    MukJumpNativeBuildIntent.IosAppStore, BuildOptions.None, null });
            state["status"] = "succeeded";
            state["output"] = output;
        }
        catch (Exception e)
        {
            state["status"] = "failed";
            state["error"] = (e.InnerException ?? e).ToString();
            Debug.LogException(e.InnerException ?? e);
        }
        finally
        {
            PlayerSettings.iOS.sdkVersion = sdk;
            PlayerSettings.iOS.simulatorSdkArchitecture = architecture;
            AssetDatabase.SaveAssets();
            state["finishedUtc"] = DateTime.UtcNow.ToString("o");
            state["restoredSdk"] = PlayerSettings.iOS.sdkVersion.ToString();
            state["restoredSimulatorArchitecture"] = PlayerSettings.iOS.simulatorSdkArchitecture.ToString();
            File.WriteAllText(activeJob, state.ToString());
        }
    }
}
