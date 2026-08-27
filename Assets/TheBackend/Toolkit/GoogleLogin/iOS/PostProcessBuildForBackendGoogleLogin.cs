#if UNITY_EDITOR && UNITY_IOS
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

using TheBackend.ToolKit.GoogleLogin.Settings.iOS;

public class PostProcessBuildForBackendGoogleLogin
{
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target == BuildTarget.iOS)
        {
            AddURLSchemeToXcode(pathToBuiltProject);
        }
    }

    private static void AddURLSchemeToXcode(string path)
    {
        var settings = (TheBackendGoogleSettingsForIOS)Resources.Load(nameof(TheBackendGoogleSettingsForIOS), typeof(TheBackendGoogleSettingsForIOS));

        if (settings == null || string.IsNullOrEmpty(settings.iosURLSchema))
        {
            return;
        }

        string projPath = PBXProject.GetPBXProjectPath(path);
        PBXProject proj = new PBXProject();
        proj.ReadFromFile(projPath);

        string targetGUID = proj.GetUnityFrameworkTargetGuid();
        string plistPath = Path.Combine(path, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementDict rootDict = plist.root;

        rootDict.SetString("GIDClientID", settings.iosClientID);

        PlistElementArray urlTypes = rootDict.CreateArray("CFBundleURLTypes");
        PlistElementDict urlTypeDict = urlTypes.AddDict();
        PlistElementArray urlSchemes = urlTypeDict.CreateArray("CFBundleURLSchemes");
        urlSchemes.AddString(settings.iosURLSchema);

        plist.WriteToFile(plistPath);
    }
}


#endif