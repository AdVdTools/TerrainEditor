using UnityEngine;
using System.Collections;

public class FPSDisplay : MonoBehaviour
{
#if DEBUG
    float deltaTime = 0.0f;

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        int w = Screen.width, h = Screen.height;
        int fontSize = h / 20;

        GUIStyle style = new GUIStyle();

        Rect rect = new Rect(0, 0, w, fontSize);
        style.alignment = TextAnchor.UpperCenter;
        style.fontSize = fontSize;
        //style.normal.textColor = new Color(0.0f, 0.0f, 0.5f, 1.0f);
        float msec = deltaTime * 1000.0f;
        float fps = 1.0f / deltaTime;

        if (fps < 30)
            style.normal.textColor = Color.yellow;
        else if (fps < 10)
            style.normal.textColor = Color.red;
        else
            style.normal.textColor = Color.green;

        string text = string.Format("{0:0.0} ms ({1:0.} fps)", msec, fps);
        GUI.Label(rect, text, style);
    }
#endif
}