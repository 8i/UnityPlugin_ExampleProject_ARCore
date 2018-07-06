using UnityEngine;
using HVR;

#if UNITY_EDITOR
using UnityEditor;
#endif
public class ShaderSubroutine_Transition : HvrActorShaderSubroutineBase
{
    [Header("Parameters")]
    public float fadeHeight = 0.1f;
    public float fadeSize = 0.2f;

    private string param_fadeHeight;
    private string param_fadeSize;

    private void OnEnable()
    {
        param_fadeHeight = "_" + id + "FadeHeight";
        param_fadeSize = "_" + id + "FadeSize";
    }

    private void Update()
    {
        hvrActor.SetSubroutineUniformFloat(param_fadeHeight, fadeHeight);
        hvrActor.SetSubroutineUniformFloat(param_fadeSize, fadeSize);
    }
}
