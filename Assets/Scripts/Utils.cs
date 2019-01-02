using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utils {
    public static bool IsEditMode
    {
#if UNITY_EDITOR
        get { return !UnityEditor.EditorApplication.isPlaying; }
#else
        get { return false; }
#endif
    }
}
