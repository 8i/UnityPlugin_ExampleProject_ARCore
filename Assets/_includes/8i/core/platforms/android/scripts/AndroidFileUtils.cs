using UnityEngine;

namespace HVR.Android
{
    public class AndroidFileUtils
    {
        public static string GetExternalPublicDirectory(string externalPublicName)
        {
            AndroidJavaClass environmentClass = new AndroidJavaClass("android.os.Environment");
            AndroidJavaObject externalStorageFile = environmentClass.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", externalPublicName);
            string externalStoragePath = externalStorageFile.Call<string>("getAbsolutePath");
            return externalStoragePath;
        }

        public static string GetExternalPublicDirectory()
        {
            return GetExternalPublicDirectory("");
        }

        public static string GetInternalStorageDirectory()
        {
            AndroidJavaObject currentActivity = AndroidUtils.GetCurrentActivity();
            AndroidJavaObject internalStorageFile = currentActivity.Call<AndroidJavaObject>("getFilesDir");
            string internalStoragePath = internalStorageFile.Call<string>("getAbsolutePath");
            return internalStoragePath;
        }
    }
}