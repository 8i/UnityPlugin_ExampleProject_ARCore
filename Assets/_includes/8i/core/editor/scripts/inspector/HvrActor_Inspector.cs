using UnityEditor;
using UnityEngine;

namespace HVR.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(HvrActor))]
    public class HvrActor_Inspector : UnityEditor.Editor
    {
        private HvrActor targetActor;
        private HvrEditorGUI hvrEditorGUI;

        private float lastInspectorRepaint = -1;
        private float lastAssetTime = -1;
        const float inspectorRepaintTimeOffset = 0.01f;

        private void OnEnable()
        {
            targetActor = (HvrActor)target;
            hvrEditorGUI = new HvrEditorGUI();
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
        }

        private void OnDisable()
        {
            if (hvrEditorGUI != null)
            {
                if (hvrEditorGUI.materialEditor != null)
                    DestroyImmediate(hvrEditorGUI.materialEditor);
            }

            EditorApplication.update -= EditorUpdate;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Inspector_Utils.DrawHeader();

            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            {
                EditorGUILayout.BeginVertical("box");
                {
                    EditorGUILayout.LabelField("Asset", EditorStyles.boldLabel);
                }
                EditorGUILayout.EndVertical();

                EditorGUI.indentLevel++;
                {
                    EditorGUILayout.LabelField("Data", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    {
                        hvrEditorGUI.ActorDataMode(targetActor, serializedObject);

                        switch (targetActor.dataMode)
                        {
                            case HvrActor.eDataMode.reference:
                                EditorGUILayout.HelpBox("Reference: Drag and drop a file or folder onto the data slot.\nThis reference will be autoamtically be exported when a build is created.", MessageType.None);
                                break;
                            case HvrActor.eDataMode.path:
                                EditorGUILayout.HelpBox("Path: Enter a direct path to a file or folder located on disk.\nThis path will not be copied when a build is created.", MessageType.None);
                                break;
                        }

                        switch (targetActor.dataMode)
                        {
                            case HvrActor.eDataMode.reference:
                                hvrEditorGUI.ActorDataGuidObject(targetActor, serializedObject);
                                break;
                            case HvrActor.eDataMode.path:
                                hvrEditorGUI.ActorDataPath(targetActor, serializedObject);
                                break;
                        }
                    }
                    EditorGUI.indentLevel--;

                    EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    {
                        EditorGUILayout.HelpBox("These settings will be applied when the asset is created", MessageType.None);
                        targetActor.assetPlay = EditorGUILayout.Toggle("Play", targetActor.assetPlay);
                        targetActor.assetLoop = EditorGUILayout.Toggle("Loop", targetActor.assetLoop);
                        targetActor.assetSeekTime = EditorGUILayout.FloatField("Seek To", targetActor.assetSeekTime);
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            {
                EditorGUILayout.BeginVertical("box");
                {
                    EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
                }
                EditorGUILayout.EndVertical();

                EditorGUI.indentLevel++;
                {
                    EditorGUILayout.LabelField("Style", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    {
                        hvrEditorGUI.RenderMethod(target, serializedObject);
                        hvrEditorGUI.MaterialField(target, serializedObject);
                    }
                    EditorGUI.indentLevel--;

                    EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    {
                        if (hvrEditorGUI.UseLighting(target, serializedObject))
                        {
                            EditorGUI.indentLevel++;
                            hvrEditorGUI.CastShadows(target, serializedObject);
                            hvrEditorGUI.ReceiveShadows(target, serializedObject);
                            EditorGUI.indentLevel--;
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            {
                EditorGUILayout.BeginVertical("box");
                {
                    EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
                }
                EditorGUILayout.EndVertical();

                EditorGUI.indentLevel++;
                {
                    hvrEditorGUI.HvrActorScreenspaceQuad(target, serializedObject);

                    hvrEditorGUI.OcclusionCullingEnabled(target, serializedObject);
                    if (targetActor.occlusionCullingEnabled)
                    {
                        EditorGUI.indentLevel++;
                        {
                            hvrEditorGUI.OcclusionCullingMultipler(target, serializedObject);
                        }
                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            if (targetActor.assetInterface != null)
                hvrEditorGUI.AssetPlaybackBar(targetActor.assetInterface, serializedObject);

            if (targetActor.material != null)
                hvrEditorGUI.MaterialEditor(target, targetActor.material, serializedObject);

            if (GUI.changed)
            {
                if (!Application.isPlaying)
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(targetActor.gameObject.scene);

                EditorUtility.SetDirty(target);

                SceneView.RepaintAll();
            }
        }

        private void EditorUpdate()
        {
            // Do not repaint the inspector every frame otherwise performance will take a hit. 
            if (Time.realtimeSinceStartup >= lastInspectorRepaint + inspectorRepaintTimeOffset)
            {
                lastInspectorRepaint = Time.realtimeSinceStartup;

                if (targetActor != null &&
                    targetActor.assetInterface != null &&
                    targetActor.assetInterface.GetCurrentTime() != lastAssetTime)
                {
                    lastAssetTime = targetActor.assetInterface.GetCurrentTime();

                    Repaint();
                }
            }
        }

        private bool HasFrameBounds()
        {
            HvrActor actor = target as HvrActor;
            return (actor.assetInterface != null);
        }

        private Bounds OnGetFrameBounds()
        {
            HvrActor actor = target as HvrActor;

            if (actor.assetInterface != null)
            {
                Bounds b = actor.assetInterface.GetBounds();
                b.center += actor.transform.position;
                return b;
            }

            return new Bounds(actor.transform.position, Vector3.one * 1f);
        }
    }
}
