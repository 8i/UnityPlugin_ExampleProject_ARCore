using UnityEngine;


public class TouchRotateY : MonoBehaviour
{

    enum TouchState
    {
        idle,
        waitingForUp
    }

    private float yAxisStart;
    private float yAxisAdd;

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
                yAxisAdd = 0;
            }
            else
            {
                if (touchState == TouchState.waitingForUp)
                {
                    if (t.phase == TouchPhase.Moved)
                    {
                        yAxisAdd = startPos.x - t.position.x;

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
            yAxisAdd = 0;
        }
    }
}
