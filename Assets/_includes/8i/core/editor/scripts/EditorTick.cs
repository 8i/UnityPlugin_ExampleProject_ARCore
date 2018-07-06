using System.Collections.Generic;
using UnityEditor;
using HVR.Interface;

namespace HVR.Editor
{
    [InitializeOnLoad]
    public class EditorTick
    {
        static EditorTick()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (!EditorApplication.isPlaying ||
                EditorApplication.isPaused)
            {
                // prevent memory keeps going up when in Unity Editor and the HvrActor is inactive
                HvrPlayerInterface.Update();
            }

            List<HvrActor> actors = new List<HvrActor>();
            HvrScene.GetObjects(actors);

            foreach (HvrActor actor in actors)
            {
                actor.EditorUpdate();
            }
        }
    }
}
