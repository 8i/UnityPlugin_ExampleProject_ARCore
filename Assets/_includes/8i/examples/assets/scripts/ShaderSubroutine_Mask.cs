using UnityEngine;
using HVR;

public class ShaderSubroutine_Mask : HvrActorShaderSubroutineBase
{
    [Header("Parameters")]
    public Transform maskObject;

    private string val_position;
    private string val_scale;

    private void OnEnable()
    {
        val_position = "_" + id + "position";
        val_scale = "_" + id + "scale";
    }

    private void Update()
    {
        hvrActor.SetSubroutineUniformVec3(val_position, maskObject ? maskObject.position : Vector3.zero);
        hvrActor.SetSubroutineUniformVec3(val_scale, maskObject ? maskObject.lossyScale : Vector3.zero);
    }
}
