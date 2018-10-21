using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Dither Pattern")]
public class PropDitherPattern : ScriptableObject
{
    public const float CellSize = 32f;

    public int amount = 255;

    public float minR = 1f, maxR = 1.5f;

    [System.Serializable]
    public struct PatternElement
    {
        public Vector2 pos;
        public float r;

        //TODO other data, rands (for z, rotation, alignment)
        public float rand0, rand1, rand2;
    }
    [HideInInspector]
    public PatternElement[] elements = new PatternElement[0];

    //private byte[] pattern;
    //public byte[] Pattern
    //{
    //    get { return pattern; }
    //}

    //public void GeneratePattern()
    //{
    //    if (pattern == null || pattern.Length != 256) pattern = new byte[256];

    //    for (int i = 0; i < height; ++i)
    //    {
    //        for (int j = 0; j < width; ++j)
    //        {
    //            int index = i * width + j;
    //            pattern[index] = (byte)((GenerateXValue(j) + GenerateYValue(i)) & 255);
    //        }
    //    }
    //}

    void OnValidate()
    {
        //GeneratePattern();
    }

}

