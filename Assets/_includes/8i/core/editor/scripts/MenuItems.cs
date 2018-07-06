using UnityEditor;
using UnityEngine;
using HVR;

namespace HVR.Editor
{
    public class MenuItems : MonoBehaviour
    {
        [MenuItem("GameObject/8i/HvrActor", false, 10)]
        static void CreateObject_HVRActor(MenuCommand menuCommand)
        {
            HvrActor actor = CreateObject<HvrActor>(menuCommand, "HvrActor").GetComponent<HvrActor>();

            // Create default material for the newly created HvrActor
            Shader shader = Shader.Find(Uniforms.ShaderNames.shader_HvrStandard);
            Material mat = new Material(shader);
            mat.name = "HvrStandard";
            actor.material = mat;
        }

        [MenuItem("Assets/Create/8i/HvrDataReference", false, 100)]
        static void CreateObject_HvrDataBuildInclude(MenuCommand menuCommand)
        {
            ScriptableObjectUtility.CreateAsset<HvrDataReference>();
        }

        static GameObject CreateObject<T>(MenuCommand menuCommand, string name)
        {
            // Create a custom game object
            GameObject go = new GameObject(name);
            go.AddComponent(typeof(T));

            // Ensure it gets re-parented if this was a context click (otherwise does nothing)
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;

            return go;
        }
    }
}

