using UnityEngine;
using HVR;

public class ShaderSubroutine_3DNoise : HvrActorShaderSubroutineBase
{
    private string val_time;

    private void OnEnable()
    {
        val_time = "_" + id + "Time";
    }

    private void Update()
    {
        hvrActor.SetSubroutineUniformFloat(val_time, Time.time);
    }
}
