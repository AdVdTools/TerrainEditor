using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public struct FloatRange
{
    public float min, max;

    public FloatRange(float min, float max) {
        this.min = min;
        this.max = max;
    }

    public bool CheckInRange(float value)
    {
        return value >= min && value <= max;
    }

    public float GetValue(float rand)
    {
        return min + rand * (max - min);
    }
}
