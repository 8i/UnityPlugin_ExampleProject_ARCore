using UnityEditor;
using UnityEngine;

namespace HVR.Android.Editor
{
    public class AndroidEditorMenuItems : MonoBehaviour
    {
        [MenuItem("8i/Android/Build", false, 0)]
        static void Android_Build()
        {
            AndroidEditorUtilities.PrepareBuild();
            string path = AndroidEditorUtilities.GetBuildPath(true);
            AndroidEditorUtilities.BuildPlayer(path, BuildOptions.None);
        }

        [MenuItem("8i/Android/Build and Run", false, 1)]
        static void Android_BuildAndRun()
        {
            AndroidEditorUtilities.PrepareBuild();
            string path = AndroidEditorUtilities.GetBuildPath(false);
            AndroidEditorUtilities.BuildPlayer(path, BuildOptions.AutoRunPlayer);
        }

        [MenuItem("8i/Android/Prepare Build", false, 101)]
        static void Android_PrepareForBuild()
        {
            string message = "This will prepare this project for Android by copying the required hvr data into Assets/StreamingAssets and allow you to use Unity's built in 'Build and Run' system.";
            message += "\nThis process needs to be run again after adding new hvr data, or making changes to HvrActors";
            message += "\n";
            message += "\nDo you want to proceed?";

            if (EditorUtility.DisplayDialog("About 'Build StreamingAssets'", message, "Ok", "Cancel"))
            {
                AndroidEditorUtilities.PrepareBuild();
            }

            AssetDatabase.Refresh();
        }
    }
}
