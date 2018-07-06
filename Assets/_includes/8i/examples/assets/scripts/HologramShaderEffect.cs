using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class HologramShaderEffect : MonoBehaviour
{
    public HVR.HvrActor hvrActor;

    int glitchPhaseFrameCount = 0;
    int glitchPhaseNextFrame = 0;
    Vector3 glitchPhaseValue = Vector3.zero;
    const int glitchPhaseKeepFrame = 1;

    Vector4 glitchColorColor = Vector4.zero;
    Vector2 glitchColorOffset = Vector2.zero;
    int glitchColorFrameCount = 0;
    int glitchColorNextFrame = 0;
    const int glitchColorKeepFrame = 4;

    // Update is called once per frame
    void Update()
    {
        if (hvrActor == null || hvrActor.material == null)
            return;

        glitchPhaseFrameCount++;
        glitchColorFrameCount++;

        if (glitchPhaseFrameCount > glitchPhaseNextFrame && glitchPhaseFrameCount <= glitchPhaseNextFrame + glitchPhaseKeepFrame)
        {
            hvrActor.material.SetVector("_phaseGlitch", glitchPhaseValue);
        }
        else if (glitchPhaseFrameCount > glitchPhaseNextFrame + glitchPhaseKeepFrame)
        {
            glitchPhaseFrameCount = 0;
            glitchPhaseNextFrame = Random.Range(15, 80);

            glitchPhaseValue = new Vector3(Random.Range(0.0f, 2.0f), Random.Range(0.0f, 2.0f), Random.Range(-0.01f, 0.01f));
        }
        else
        {
            hvrActor.material.SetVector("_phaseGlitch", Vector3.zero);
        }


        if (glitchColorFrameCount > glitchColorNextFrame && glitchColorFrameCount <= glitchColorNextFrame + glitchColorKeepFrame)
        {
            hvrActor.material.SetVector("_colorGlitchColor", glitchColorColor);
            hvrActor.material.SetVector("_colorGlitchOffset", glitchColorOffset);
        }
        else if (glitchColorFrameCount > glitchColorNextFrame + glitchColorKeepFrame)
        {
            glitchColorFrameCount = 0;
            glitchColorNextFrame = Random.Range(30, 60);

            glitchColorColor = new Vector4(Random.Range(0.2f, 0.3f), Random.Range(0, 0.2f), Random.Range(0.1f, 0.2f), 0);
            glitchColorOffset = new Vector2(Random.Range(-0.02f, 0.02f), Random.Range(-0.02f, 0.02f));
        }
        else
        {
            hvrActor.material.SetVector("_colorGlitchColor", Vector4.zero);
            hvrActor.material.SetVector("_colorGlitchOffset", Vector2.zero);
        }
    }
}
