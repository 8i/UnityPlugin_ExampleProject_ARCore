using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FrameRateRegulator : MonoBehaviour {

    public int minimalFps = 10;
    public int maximumFps = 60;
    public float bumpCheckInterval = 5.0f;

    float bumpTimer;
    float graceWaitTimer;

    void Start()
    {
        bumpTimer = 0;
        graceWaitTimer = 0;
    }

    void Update()
    {
        bumpTimer += Time.unscaledDeltaTime;
        graceWaitTimer -= Time.unscaledDeltaTime;
        if (bumpTimer > bumpCheckInterval)
        {
            Application.targetFrameRate = Mathf.Min(maximumFps, Application.targetFrameRate * 2);
            bumpTimer = 0;
            graceWaitTimer = 2.0f;
        }

        if (graceWaitTimer <= 0)
        {
            if (Application.targetFrameRate > 1.0f / Time.smoothDeltaTime)
            {
                Application.targetFrameRate = Mathf.Max(minimalFps, (int)(1.0f / Time.smoothDeltaTime));
                bumpTimer = 0;
            }
        }
        
    }

#if DEVELOPMENT_BUILD
    void OnGUI()
    {
        GUI.Label(new Rect(0, 40, 200, 20), "dt: " + Time.unscaledDeltaTime);
        GUI.Label(new Rect(0, 60, 200, 20), "target FPS: " + Application.targetFrameRate);
        GUI.Label(new Rect(0, 80, 200, 20), "smooth FPS: " + (int)(1.0f / Time.smoothDeltaTime));
        GUI.Label(new Rect(0, 100, 200, 20), "Bump: " + bumpTimer);
        GUI.Label(new Rect(0, 120, 200, 20), "Grace: " + graceWaitTimer);
    }
#endif

}