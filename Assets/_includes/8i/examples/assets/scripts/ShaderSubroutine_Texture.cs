using UnityEngine;
using HVR;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ShaderSubroutine_Texture : HvrActorShaderSubroutineBase
{
    public Texture texture;

    private string val_texture;

    private void OnEnable()
    {
        val_texture = "_" + id + "Texture";
    }

    private void Update()
    {
        if (texture != null)
            hvrActor.SetSubroutineUniformTexture2D(val_texture, texture);
    }
}
