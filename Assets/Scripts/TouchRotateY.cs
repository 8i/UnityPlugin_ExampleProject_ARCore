using UnityEngine;


public class TouchRotateY : MonoBehaviour
{

    enum TouchState
    {
        idle,
        waitingForUp
    }

    private float yAxisStart;

    private Vector2 startPos; 

    private TouchState touchState;

    protected virtual void Update()
    {
        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);

            if (t.phase == TouchPhase.Began)
            {
                touchState = TouchState.waitingForUp;
                startPos = t.position;
                yAxisStart = transform.localEulerAngles.y;
            }
            else
            {
                if (touchState == TouchState.waitingForUp)
                {
                    if (t.phase == TouchPhase.Moved)
                    {
                        float xPixelsDelta = startPos.x - t.position.x;
                        float xDistanceDelta = (xPixelsDelta / Screen.dpi) * 2.54f; // Convert to CM

                        float yAxisAdd = xDistanceDelta * 45f;

                        Vector3 rot = transform.localEulerAngles;
                        rot.y = yAxisStart + yAxisAdd;

                        transform.localEulerAngles = rot;
                    }
                    
                    if (t.phase == TouchPhase.Ended)
                        touchState = TouchState.idle;
                }
            }
        }
        else
        {
            touchState = TouchState.idle;
            yAxisStart = 0;
        }
    }
}
