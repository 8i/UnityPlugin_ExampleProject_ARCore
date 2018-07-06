using UnityEngine;
using HVR;

public class ShaderSubroutine_Noise : HvrActorShaderSubroutineBase
{
    [Header("Parameters")]
    public float noisePower = 1.0f;
    public float noiseStrength = 1.0f;
    public Vector3 noiseStrengthXYZ = new Vector3(1, 1, 1);

    private string val_time;
    private string val_power;
    private string val_strength;
    private string val_strengthXYZ;

    private void OnEnable()
    {
        val_time = "_" + id + "Time";
        val_power = "_" + id + "NoisePower";
        val_strength = "_" + id + "NoiseStrength";
        val_strengthXYZ = "_" + id + "NoiseStrengthXYZ";
    }

    private void Update()
    {
        hvrActor.SetSubroutineUniformFloat(val_time, Time.time);
        hvrActor.SetSubroutineUniformFloat(val_power, noisePower);
        hvrActor.SetSubroutineUniformFloat(val_strength, noiseStrength);
        hvrActor.SetSubroutineUniformVec3(val_strengthXYZ, noiseStrengthXYZ);
    }
}
