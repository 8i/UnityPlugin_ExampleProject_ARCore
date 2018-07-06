using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HVR.Android
{
    public class LoadingSceneManager : MonoBehaviour
    {
        public Canvas rootCanvas;
        public RectTransform loadingBar;
        public RectTransform loadingBarBackground;

        protected AndroidAssetUnpacker assetUnpacker;

        protected float startWidth;
        protected float goalWidth;

        protected void Awake()
        {
            rootCanvas.gameObject.SetActive(false);
        }

        protected void Start()
        {
#if UNITY_ANDROID
            startWidth = loadingBar.sizeDelta.x;
            goalWidth = loadingBarBackground.sizeDelta.x;

            assetUnpacker = new AndroidAssetUnpacker();

            if (assetUnpacker.IsDone())
            {
                LoadNextScene();
            }
            else
            {
                assetUnpacker.Start();
                LoadAssets();
            }
#else
            LoadNextScene();
#endif
        }

        protected void LoadNextScene()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            int buildIndex = currentScene.buildIndex;

            if (SceneManager.sceneCount >= buildIndex + 1)
            {
                SceneManager.LoadScene(buildIndex + 1);
            }
        }

        protected void LoadAssets()
        {
            StartCoroutine(_LoadAssets());
        }

        protected IEnumerator _LoadAssets()
        {
            rootCanvas.gameObject.SetActive(true);

            while (!assetUnpacker.IsDone())
            {
                float completed = assetUnpacker.PercentComplete();
                loadingBar.sizeDelta = new Vector2(startWidth + ((goalWidth * completed) - startWidth), loadingBar.sizeDelta.y);
                yield return null;
            }
            LoadNextScene();
        }
    }
}