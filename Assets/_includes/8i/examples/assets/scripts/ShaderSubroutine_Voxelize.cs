using UnityEngine;
using HVR;

public class ShaderSubroutine_Voxelize : HvrActorShaderSubroutineBase
{
    [Header("Parameters")]
    [Range(0.05f, 1f)]
    public float voxelSize = 0.2f;
    [Range(0f, 1f)]
    public float noisePower = 1.0f;
    [Range(0f, 1f)]
    public float noiseStrength = 1.0f;
    public Vector3 noiseStrengthXYZ = new Vector3(1, 1, 1);

    private string val_time;
    private string val_voxelSize;
    private string val_noisePower;
    private string val_noiseStrength;
    private string val_noiseStrength_XYZ;

    private void OnEnable()
    {
        val_time = "_" + id + "Time";
        val_voxelSize = "_" + id + "VoxelSize";
        val_noisePower = "_" + id + "NoisePower";
        val_noiseStrength = "_" + id + "NoiseStrength";
        val_noiseStrength_XYZ = "_" + id + "NoiseStrength_XYZ";
    }

    private void Update()
    {
        hvrActor.SetSubroutineUniformFloat(val_time, Time.time);
        hvrActor.SetSubroutineUniformFloat(val_voxelSize, voxelSize);
        hvrActor.SetSubroutineUniformFloat(val_noisePower, noisePower);
        hvrActor.SetSubroutineUniformFloat(val_noiseStrength, noiseStrength);
        hvrActor.SetSubroutineUniformVec3(val_noiseStrength_XYZ, noiseStrengthXYZ);
    }
}
