using System;
using UnityEditor;
using UnityEngine;

public class ShaderGUI_HvrStandard : ShaderGUI
{
    public enum BlendMode
    {
        Opaque,
        AlphaBlend,
        Dither
    }

    private static class Styles
    {
        public static string renderingMode = "Rendering Mode";
        public static readonly string[] blendNames = Enum.GetNames(typeof(BlendMode));
    }

    MaterialProperty blendMode = null;

    MaterialEditor m_MaterialEditor;

    public void FindProperties(MaterialProperty[] props)
    {
        blendMode = FindProperty("_Mode", props);
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
    {
        FindProperties(props); // MaterialProperties can be animated so we do not cache them but fetch them every event to ensure animated values are updated correctly

        m_MaterialEditor = materialEditor;

        Material material = materialEditor.target as Material;

        bool editable = material.hideFlags == HideFlags.NotEditable;

        EditorGUI.BeginDisabledGroup(editable);
        {
            ShaderPropertiesGUI(material);

            // draw the default GUI
            base.OnGUI(materialEditor, props);
        }
        EditorGUI.EndDisabledGroup();
    }

    public void ShaderPropertiesGUI(Material material)
    {
        // Use default labelWidth
        EditorGUIUtility.labelWidth = 0f;

        // Detect any changes to the material
        EditorGUI.BeginChangeCheck();
        {
            TransparencyMethodPopup();
        }
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var obj in blendMode.targets)
                MaterialChanged((Material)obj);
        }
    }

    public static void SetupMaterialWithBlendMode(Material material, BlendMode _transparencyMethod)
    {
        switch (_transparencyMethod)
        {
            case BlendMode.Opaque:
                material.SetOverrideTag("RenderType", "Opaque");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 1);
                material.DisableKeyword("TRANSPARENCY_ALPHABLEND");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("TRANSPARENCY_DITHER");
                material.renderQueue = 2000;
                break;
            case BlendMode.AlphaBlend:
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("TRANSPARENCY_ALPHABLEND");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("TRANSPARENCY_DITHER");
                material.renderQueue = 3000;
                break;
            case BlendMode.Dither:
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("TRANSPARENCY_ALPHABLEND");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.EnableKeyword("TRANSPARENCY_DITHER");
                material.renderQueue = 3000;
                break;
        }
    }

    static void MaterialChanged(Material material)
    {
        SetupMaterialWithBlendMode(material, (BlendMode)material.GetFloat("_Mode"));
    }

    void TransparencyMethodPopup()
    {
        EditorGUI.showMixedValue = blendMode.hasMixedValue;
        var mode = (BlendMode)blendMode.floatValue;

        EditorGUI.BeginChangeCheck();
        mode = (BlendMode)EditorGUILayout.Popup(Styles.renderingMode, (int)mode, Styles.blendNames);
        if (EditorGUI.EndChangeCheck())
        {
            m_MaterialEditor.RegisterPropertyChangeUndo("Transparency Method");
            blendMode.floatValue = (float)mode;
        }

        EditorGUI.showMixedValue = false;
    }
}