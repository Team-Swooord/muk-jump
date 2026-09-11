using System.IO;
using MukJump.Core;
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace MukJump.EditorTools
{
    public static class PrereleaseResetPostprocessor
    {
        [PostProcessBuild(10020)]
        public static void Apply(BuildTarget target, string path)
        {
#if UNITY_IOS
            if (target != BuildTarget.iOS) return;
            string file = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(file);
            if (MukJumpNativeReleaseBuildGuard.IsPrereleaseResetBuild)
                plist.root.SetBoolean(PrereleasePlayerReset.PlistKey, true);
            else plist.root.values.Remove(PrereleasePlayerReset.PlistKey);
            plist.WriteToFile(file);
#endif
        }
    }
}
