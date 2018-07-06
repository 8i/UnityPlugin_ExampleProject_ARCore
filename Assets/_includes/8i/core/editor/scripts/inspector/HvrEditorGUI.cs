using HVR.Interface;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HVR.Editor
{
    public class HvrEditorGUI
    {
        public static float guiLineHeight = 0;

        static readonly string[] hvrRenderModeStrings = { "Standard", "Direct" };
        static readonly string[] actorModeStrings = { "Reference", "Path" };

        public MaterialEditor materialEditor;

        static HvrEditorGUI()
        {
            guiLineHeight = EditorGUIUtility.singleLineHeight * 1.25f;
        }

        public void ActorDataMode(HvrActor target, SerializedObject serializedObject)
        {
            HvrActor.eDataMode dataMode = (HvrActor.eDataMode)EditorGUILayout.Popup("Mode", (int)target.dataMode, actorModeStrings);

            if (dataMode != target.dataMode)
            {
                foreach (HvrActor actor in serializedObject.targetObjects)
                {
                    if (actor.dataMode != dataMode)
                    {
                        Undo.RecordObject(actor, "Changed DataMode");
                        actor.CreateAsset(string.Empty, dataMode);
                    }
                }
            }
        }

        public void ActorDataGuidObject(HvrActor target, SerializedObject serializedObject)
        {
            string pathToDataObject = AssetDatabase.GUIDToAssetPath(target.data);
            UnityEngine.Object dataObject = AssetDatabase.LoadAssetAtPath(pathToDataObject, typeof(UnityEngine.Object));

            EditorGUI.BeginChangeCheck();
            {
                dataObject = EditorGUILayout.ObjectField("Reference", dataObject, typeof(UnityEngine.Object), false);
            }

            string dataPath_relative = string.Empty;
            string dataGuid = string.Empty;

            if (dataObject != null)
            {
                dataPath_relative = AssetDatabase.GetAssetPath(dataObject);
                dataGuid = AssetDatabase.AssetPathToGUID(dataPath_relative);
            }

            if (EditorGUI.EndChangeCheck())
            {
                foreach (HvrActor actor in serializedObject.targetObjects)
                {
                    Undo.RecordObject(actor, "Changed DataGuid");
                    actor.CreateAsset(dataGuid, HvrActor.eDataMode.reference);
                }
            }
        }

        public void ActorDataPath(HvrActor target, SerializedObject serializedObject)
        {
            string dataPath = target.data;

            EditorGUI.BeginChangeCheck();
            {
                dataPath = EditorGUILayout.TextField("Path", dataPath);
            }

            if (EditorGUI.EndChangeCheck())
            {
                foreach (HvrActor actor in serializedObject.targetObjects)
                {
                    Undo.RecordObject(actor, "Changed HvrActor DataPath");
                    actor.CreateAsset(dataPath, HvrActor.eDataMode.path);
                }
            }
        }

        public void AssetPlaybackBar(Interface.AssetInterface asset, SerializedObject serializedObject)
        {
            if (asset == null)
                return;

            GUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            {
                EditorGUILayout.LabelField("Playback Preview", EditorStyles.centeredGreyMiniLabel);

                GUILayout.Space(2);
                float currentTime = asset.GetCurrentTime();
                float duration = asset.GetDuration();

                Rect progressRect = EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(18), GUILayout.MaxHeight(18));
                {
                    GUILayout.Space(18);

                    Color backgroundcolor = new Color(0.1f, 0.1f, 0.1f, 1.0f);
                    Color progressColor = new Color(0.0f, 0.37f, 0.62f, 1.0f);

                    Handles.BeginGUI();
                    {
                        Vector3[] points = new Vector3[]
                        {
                            new Vector3(progressRect.xMin,  progressRect.yMin, 0),
                            new Vector3(progressRect.xMax,  progressRect.yMin, 0),
                            new Vector3(progressRect.xMax, progressRect.yMax, 0),
                            new Vector3(progressRect.xMin, progressRect.yMax, 0)
                        };

                        Handles.color = backgroundcolor;
                        Handles.DrawAAConvexPolygon(points);
                    }
                    Handles.EndGUI();

                    float progressX = Mathf.Lerp(progressRect.xMin, progressRect.xMax, (currentTime / duration));

                    // Progress
                    Handles.BeginGUI();
                    {
                        Vector3[] points = new Vector3[]
                        {
                            new Vector3(progressRect.xMin,  progressRect.yMin, 0),
                            new Vector3(progressX,  progressRect.yMin, 0),
                            new Vector3(progressX,  progressRect.yMax, 0),
                            new Vector3(progressRect.xMin,  progressRect.yMax, 0)
                        };

                        Handles.color = progressColor;
                        Handles.DrawAAConvexPolygon(points);
                    }
                    Handles.EndGUI();

                    //ProgressLine
                    Handles.BeginGUI();
                    {
                        Vector3[] points = new Vector3[]
                        {
                            new Vector3(progressX, progressRect.yMin, 0),
                            new Vector3(progressX, progressRect.yMax, 0)
                        };
                        Handles.color = Color.white;

                        Handles.DrawLine(points[0], points[1]);
                    }
                    Handles.EndGUI();

                    GUIStyle timeStyle = new GUIStyle("label");
                    timeStyle.alignment = TextAnchor.MiddleCenter;
                    timeStyle.normal.textColor = Color.white;
                    EditorGUI.LabelField(progressRect, currentTime.ToString("f2") + " / " + asset.GetDuration().ToString("f2"), timeStyle);
                }
                EditorGUILayout.EndHorizontal();

                float mouseXPos = Event.current.mousePosition.x;

                if (progressRect.Contains(Event.current.mousePosition))
                {
                    Handles.BeginGUI();
                    {
                        Vector2 startPoint = new Vector2(mouseXPos, progressRect.yMin);
                        Vector2 endPoint = new Vector2(mouseXPos, progressRect.yMax);

                        Vector2 startTangent = new Vector2(mouseXPos, progressRect.yMax);
                        Vector2 endTangent = new Vector2(mouseXPos, progressRect.yMin);
                        Handles.DrawBezier(startPoint, endPoint, startTangent, endTangent, Color.white, null, 3);
                    }
                    Handles.EndGUI();

                    if ((Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag) && Event.current.button == 0)
                    {
                        float mouseTimeProgress = Mathf.InverseLerp(progressRect.xMin, progressRect.xMax, mouseXPos);
                        float time = Mathf.Lerp(0, duration, mouseTimeProgress);
                        asset.Seek(time);
                    }
                }

                GUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();

                    GUILayoutOption[] buttonGLO = new GUILayoutOption[]{
                        GUILayout.MinWidth(30),
                        GUILayout.MinHeight(30),
                        GUILayout.MaxHeight(30),
                        GUILayout.MaxWidth(30),
                    };

                    Color origColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.6f, 0.6f, 0.6f, 1.0f);

                    if (GUILayout.Button(EditorSharedResources.instance.hvrActorStepBack, buttonGLO))
                    {
                        foreach (HvrActor actor in serializedObject.targetObjects)
                        {
                            if (actor.assetInterface != null)
                                actor.assetInterface.Step(-1);
                        }
                    }

                    if (asset.IsPlaying())
                    {
                        if (GUILayout.Button(EditorSharedResources.instance.hvrActorPause, buttonGLO))
                        {
                            foreach (HvrActor actor in serializedObject.targetObjects)
                            {
                                if (actor.assetInterface != null)
                                    actor.assetInterface.Pause();
                            }
                        }
                    }
                    else
                    {
                        if (GUILayout.Button(EditorSharedResources.instance.hvrActorPlay, buttonGLO))
                        {
                            foreach (HvrActor actor in serializedObject.targetObjects)
                            {
                                if (actor.assetInterface != null)
                                    actor.assetInterface.Play();
                            }
                        }
                    }

                    if (GUILayout.Button(EditorSharedResources.instance.hvrActorStop, buttonGLO))
                    {
                        foreach (HvrActor actor in serializedObject.targetObjects)
                        {
                            if (actor.assetInterface != null)
                                actor.assetInterface.Stop();
                        }
                    }

                    if (GUILayout.Button(EditorSharedResources.instance.hvrActorStepForward, buttonGLO))
                    {
                        foreach (HvrActor actor in serializedObject.targetObjects)
                        {
                            if (actor.assetInterface != null)
                                actor.assetInterface.Step(1);
                        }
                    }

                    Texture2D loopTex = EditorSharedResources.instance.hvrActorStepLoopOff;

                    if (asset.IsLooping())
                        loopTex = EditorSharedResources.instance.hvrActorStepLoopOn;

                    if (GUILayout.Button(loopTex, buttonGLO))
                    {
                        foreach (HvrActor actor in serializedObject.targetObjects)
                        {
                            if (actor.assetInterface != null)
                                actor.assetInterface.SetLooping(!actor.assetInterface.IsLooping());
                        }
                    }

                    GUI.backgroundColor = origColor;

                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        public void OcclusionCullingEnabled(UnityEngine.Object target, SerializedObject serializedObject)
        {
            if (target == null)
                return;

            bool occlusionCullingEnabled = false;

            if (target.GetType() == typeof(HvrActor))
            {
                HvrActor actor = target as HvrActor;
                occlusionCullingEnabled = actor.occlusionCullingEnabled;
            }
            else
            {
                return;
            }

            EditorGUI.BeginChangeCheck();

            occlusionCullingEnabled = EditorGUILayout.Toggle("Use Occlusion Culling", occlusionCullingEnabled);

            if (EditorGUI.EndChangeCheck())
            {
                foreach (UnityEngine.Object obj in serializedObject.targetObjects)
                {
                    if (obj.GetType() == target.GetType())
                    {
                        Undo.RecordObject(obj, "Changed occlusionCullingEnabled");

                        if (obj.GetType() == typeof(HvrActor))
                        {
                            HvrActor actor = (HvrActor)obj;
                            actor.occlusionCullingEnabled = occlusionCullingEnabled;
                        }
                    }
                }
            }
        }


        public void HvrActorScreenspaceQuad(UnityEngine.Object target, SerializedObject serializedObject)
        {
            if (target == null)
                return;

            bool useScreenspaceQuad = false;

            if (target.GetType() == typeof(HvrActor))
            {
                HvrActor actor = target as HvrActor;
                useScreenspaceQuad = actor.useScreenSpaceQuad;
            }
            else
            {
                return;
            }

            EditorGUI.BeginChangeCheck();

            useScreenspaceQuad = EditorGUILayout.Toggle("Use ScreenSpace Quad", useScreenspaceQuad);

            if (EditorGUI.EndChangeCheck())
            {
                foreach (UnityEngine.Object obj in serializedObject.targetObjects)
                {
                    if (obj.GetType() == target.GetType())
                    {
                        Undo.RecordObject(obj, "Changed useScreenSpaceQuad");

                        if (obj.GetType() == typeof(HvrActor))
                        {
                            HvrActor actor = (HvrActor)obj;
                            actor.useScreenSpaceQuad = useScreenspaceQuad;
                        }
                    }
                }
            }
        }

        public void OcclusionCullingMultipler(UnityEngine.Object target, SerializedObject serializedObject)
        {
            if (target == null)
                return;

            float occlusionSizeOffset = 0;

            if (target.GetType() == typeof(HvrActor))
            {
                HvrActor actor = target as HvrActor;
                occlusionSizeOffset = actor.occlusionCullingMultipler;
            }
            else
            {
                return;
            }

            EditorGUI.BeginChangeCheck();

            occlusionSizeOffset = EditorGUILayout.Slider("Occlusion Radius Multiplier", occlusionSizeOffset, 0.01f, 4f);

            if (EditorGUI.EndChangeCheck())
            {
                foreach (UnityEngine.Object obj in serializedObject.targetObjects)
                {
                    if (obj.GetType() == target.GetType())
                    {
                        Undo.RecordObject(obj, "Changed occlusionCullingMultipler");

                        if (obj.GetType() == typeof(HvrActor))
                        {
                            HvrActor actor = (HvrActor)obj;
                            actor.occlusionCullingMultipler = occlusionSizeOffset;
                        }
                    }
                }
            }
        }

        public static void ForceRedrawHvrWorldAndRepaintView()
        {
            Helper.HvrWorldForceRedraw();
            SceneView.RepaintAll();
            EditorHelper.RepaintAllGameViews();
        }

        public bool UseLighting(UnityEngine.Object target, SerializedObject serializedObject)
        {
            bool useLighting = false;

            if (target.GetType() == typeof(HvrActor))
            {
                HvrActor actor = target as HvrActor;
                useLighting = actor.useLighting;
            }
            else
            {
                return useLighting;
            }

            EditorGUI.BeginChangeCheck();

            useLighting = EditorGUILayout.Toggle(new GUIContent("Use Lighting", "Requires HvrLight component attached to a Light"), useLighting);

            if (EditorGUI.EndChangeCheck())
            {
                foreach (UnityEngine.Object obj in serializedObject.targetObjects)
                {
                    if (obj.GetType() == target.GetType())
                    {
                        Undo.RecordObject(obj, "Changed useLighting");

                        if (obj.GetType() == typeof(HvrActor))
                        {
                            HvrActor actor = (HvrActor)obj;
                            actor.useLighting = useLighting;
                        }
                    }
                }

                ForceRedrawHvrWorldAndRepaintView();
            }

            if (useLighting)
                EditorGUILayout.HelpBox("Lighting requires a HvrLight component attached to a Light", MessageType.None);

            return useLighting;
        }

        public void CastShadows(UnityEngine.Object target, SerializedObject serializedObject)
        {
            if (target == null)
                return;

            bool castShadows = false;

            if (target.GetType() == typeof(HvrActor))
            {
                HvrActor actor = target as HvrActor;
                castShadows = actor.castShadows;
            }
            else
            {
                return;
            }

            EditorGUI.BeginChangeCheck();

            castShadows = EditorGUILayout.Toggle(new GUIContent("Cast Shadows", "Requires HvrLight component attached to light of interest."), castShadows);

            if (EditorGUI.EndChangeCheck())
            {
                foreach (UnityEngine.Object obj in serializedObject.targetObjects)
                {
                    if (obj.GetType() == target.GetType())
                    {
                        Undo.RecordObject(obj, "Changed castShadows");

                        if (obj.GetType() == typeof(HvrActor))
                        {
                            HvrActor actor = (HvrActor)obj;
                            actor.castShadows = castShadows;
                        }
                    }
                }

                ForceRedrawHvrWorldAndRepaintView();
            }
        }

        public void ReceiveShadows(UnityEngine.Object target, SerializedObject serializedObject)
        {
            if (target == null)
                return;

            bool receiveShadows = false;

            if (target.GetType() == typeof(HvrActor))
            {
                HvrActor actor = target as HvrActor;
                receiveShadows = actor.receiveShadows;
            }
            else
            {
                return;
            }

            EditorGUI.BeginChangeCheck();

#if !UNITY_5_6_OR_NEWER
            receiveShadows = EditorGUILayout.Toggle(new GUIContent("Receive Shadows", "Receiving shadows from a point light source will have known artifacts."), receiveShadows);
#else
            receiveShadows = EditorGUILayout.Toggle("Receive Shadows", receiveShadows);
#endif            

            if (EditorGUI.EndChangeCheck())
            {
                foreach (UnityEngine.Object obj in serializedObject.targetObjects)
                {
                    if (obj.GetType() == target.GetType())
                    {
                        Undo.RecordObject(obj, "Changed receiveShadows");

                        if (obj.GetType() == typeof(HvrActor))
                        {
                            HvrActor actor = (HvrActor)obj;
                            actor.receiveShadows = receiveShadows;
                        }
                    }
                }

                ForceRedrawHvrWorldAndRepaintView();
            }

#if !UNITY_5_6_OR_NEWER
            if (receiveShadows)
                EditorGUILayout.HelpBox("Receiving shadows from a point light source will have known artifacts.", MessageType.None);
#endif                
        }

        public void MaterialField(UnityEngine.Object target, SerializedObject serializedObject)
        {
            if (target == null)
                return;

            UnityEngine.Object materialObject;

            if (target.GetType() == typeof(HvrActor))
            {
                HvrActor actor = target as HvrActor;
                materialObject = actor.material;
            }
            else
            {
                return;
            }

            EditorGUI.BeginChangeCheck();

            materialObject = EditorGUILayout.ObjectField("Material", materialObject, typeof(Material), false);

            if (EditorGUI.EndChangeCheck())
            {
                Material material = (Material)materialObject;

                foreach (UnityEngine.Object obj in serializedObject.targetObjects)
                {
                    if (obj.GetType() == target.GetType())
                    {
                        Undo.RecordObject(obj, "Changed material");

                        if (obj.GetType() == typeof(HvrActor))
                        {
                            HvrActor actor = (HvrActor)obj;
                            actor.material = material;
                        }
                    }
                }

                ForceRedrawHvrWorldAndRepaintView();
            }
        }

        public void HvrRenderMode(UnityEngine.Object target, SerializedObject serializedObject)
        {
            if (target == null)
                return;

            HvrRender.eMode mode = HvrRender.eMode.direct;

            if (target.GetType() == typeof(HvrRender))
            {
                HvrRender render = target as HvrRender;
                mode = render.mode;
            }
            else
            {
                return;
            }

            EditorGUI.BeginChangeCheck();

            mode = (HvrRender.eMode)EditorGUILayout.Popup("Render Mode", (int)mode, hvrRenderModeStrings);

            if (EditorGUI.EndChangeCheck())
            {
                foreach (UnityEngine.Object obj in serializedObject.targetObjects)
                {
                    if (obj.GetType() == target.GetType())
                    {
                        Undo.RecordObject(obj, "Changed Render Mode");

                        if (obj.GetType() == typeof(HvrRender))
                        {
                            HvrRender render = (HvrRender)obj;
                            render.mode = mode;
                        }
                    }
                }
            }
        }

        public void RenderMethod(UnityEngine.Object target, SerializedObject serializedObject)
        {
            if (target == null)
                return;

            string renderMethodType = string.Empty;

            if (target.GetType() == typeof(HvrActor))
            {
                HvrActor actor = target as HvrActor;
                renderMethodType = actor.renderMethodType;
            }
            else
            {
                return;
            }

            int index = HVR.Interface.HvrPlayerInterface.RenderMethod_GetIndexForType(renderMethodType);

            EditorGUI.BeginChangeCheck();

            index = EditorGUILayout.Popup("Render Method", index, HVR.Interface.HvrPlayerInterface.RenderMethod_GetSupportedTypes());

            if (EditorGUI.EndChangeCheck())
            {
                renderMethodType = HVR.Interface.HvrPlayerInterface.RenderMethod_GetSupportedTypes()[index];

                foreach (UnityEngine.Object o in serializedObject.targetObjects)
                {
                    if (o.GetType() == target.GetType())
                    {
                        HvrActor actor = (HvrActor)o;

                        if (actor.renderMethodInterface == null ||
                            actor.renderMethodInterface.type != renderMethodType)
                        {
                            Undo.RecordObject(o, "Changed RenderMethod");

                            if (o.GetType() == typeof(HvrActor))
                            {
                                actor.CreateRenderMethod(renderMethodType);
                            }
                        }
                    }
                }
            }
        }

        public void MaterialEditor(UnityEngine.Object target, Material material, SerializedObject serializedObject)
        {
            if (target == null ||
                material == null)
                return;

            HashSet<Material> mats = new HashSet<Material>();

            foreach (UnityEngine.Object obj in serializedObject.targetObjects)
            {
                if (obj.GetType() == target.GetType())
                {
                    if (obj.GetType() == typeof(HvrActor))
                    {
                        HvrActor actor = (HvrActor)obj;
                        mats.Add(actor.material);
                    }
                }
            }

            if (materialEditor == null)
            {
                materialEditor = (MaterialEditor)UnityEditor.Editor.CreateEditor(material);
            }
            else
            {
                if (materialEditor.target != material)
                {
                    materialEditor = (MaterialEditor)UnityEditor.Editor.CreateEditor(material);
                }
            }

            if (mats.Count > 1)
            {
                EditorGUILayout.HelpBox("Material properties on selected components with different materials cannot be multi-edited", MessageType.Warning);
            }

            MaterialProperty[] properties_previous = UnityEditor.MaterialEditor.GetMaterialProperties(new UnityEngine.Object[1] { materialEditor.target });

            EditorGUI.BeginChangeCheck();
            {
                EditorGUI.BeginDisabledGroup(mats.Count > 1);
                {
                    // Draw the material's foldout and the material shader field
                    // Required to call _materialEditor.OnInspectorGUI ();
                    materialEditor.DrawHeader();

                    // Draw the material properties
                    // Works only if the foldout of _materialEditor.DrawHeader () is open
                    materialEditor.OnInspectorGUI();
                }
                EditorGUI.EndDisabledGroup();
            }

            if (EditorGUI.EndChangeCheck())
            {
                MaterialProperty[] properties_new = UnityEditor.MaterialEditor.GetMaterialProperties(new UnityEngine.Object[1] { materialEditor.target });

                foreach (MaterialProperty prop_new in properties_new)
                {
                    MaterialProperty match = properties_previous.First(x => (x.name == prop_new.name) && (x.type == prop_new.type));

                    if (match != null)
                    {
                        foreach (Material mat in mats)
                        {
                            switch (prop_new.type)
                            {
                                case MaterialProperty.PropType.Color:
                                    if (prop_new.colorValue != match.colorValue)
                                        mat.SetColor(prop_new.name, prop_new.colorValue);
                                    break;
                                case MaterialProperty.PropType.Float:
                                    if (prop_new.floatValue != match.floatValue)
                                        mat.SetFloat(prop_new.name, prop_new.floatValue);
                                    break;
                                case MaterialProperty.PropType.Range:
                                    if (prop_new.floatValue != match.floatValue)
                                        mat.SetFloat(prop_new.name, prop_new.floatValue);
                                    break;
                                case MaterialProperty.PropType.Texture:
                                    if (prop_new.textureValue != match.textureValue)
                                        mat.SetTexture(prop_new.name, prop_new.textureValue);
                                    break;
                                case MaterialProperty.PropType.Vector:
                                    if (prop_new.vectorValue != match.vectorValue)
                                        mat.SetVector(prop_new.name, prop_new.vectorValue);
                                    break;
                            }
                        }
                    }
                }
            }
        }

        public static string DrawObjectSlot(Rect rect, string name, string guid)
        {
            string pathToDataObject = AssetDatabase.GUIDToAssetPath(guid);
            Object dataObject = AssetDatabase.LoadAssetAtPath(pathToDataObject, typeof(Object));

            dataObject = EditorGUI.ObjectField(rect, name, dataObject, typeof(Object), false);

            string path = AssetDatabase.GetAssetPath(dataObject);
            guid = AssetDatabase.AssetPathToGUID(path);

            return guid;
        }
    }

    public static class HvrEditorGUILayout
    {
        public static string DrawObjectSlot(string name, string guid)
        {
            string pathToDataObject = AssetDatabase.GUIDToAssetPath(guid);
            Object dataObject = AssetDatabase.LoadAssetAtPath(pathToDataObject, typeof(Object));

            dataObject = EditorGUILayout.ObjectField(name, dataObject, typeof(Object), false);

            string path = AssetDatabase.GetAssetPath(dataObject);
            guid = AssetDatabase.AssetPathToGUID(path);

            return guid;
        }
    }
}

