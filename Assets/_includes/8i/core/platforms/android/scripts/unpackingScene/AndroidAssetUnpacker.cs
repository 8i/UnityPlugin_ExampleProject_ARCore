using UnityEngine;

namespace HVR.Android
{
    public class AndroidAssetUnpacker
    {
        protected AndroidJavaObject currentActivity;
        protected AndroidJavaObject assetUnpacker;

        public AndroidAssetUnpacker()
        {
            currentActivity = AndroidUtils.GetCurrentActivity();
            assetUnpacker = new AndroidJavaObject("com.eighti.unity.androidutils.ObbUnpacker", currentActivity, Uniforms.buildDataPath);
        }

        public void Start()
        {
            if (!IsDone())
            {
                assetUnpacker.Call("unpack");
            }
        }

        public bool IsDone()
        {
            return assetUnpacker.Call<bool>("isDone");
        }

        public float PercentComplete()
        {
            return assetUnpacker.Call<float>("completedPercentage");
        }
    }
}
