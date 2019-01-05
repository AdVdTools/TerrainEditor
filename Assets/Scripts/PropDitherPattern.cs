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
        
        /// <summary>
        /// Element order matches densityRand order.
        /// 0..1, 0 not included.
        /// </summary>
        public float densityRand;

        /// <summary>
        /// Alignment
        /// </summary>
        public float rand0;
        /// <summary>
        /// Rotation
        /// </summary>
        public float rand1;
        /// <summary>
        /// YOffset
        /// </summary>
        public float rand2;
        /// <summary>
        /// Other
        /// </summary>
        public float rand3;
    }
    [HideInInspector]
    public PatternElement[] elements = new PatternElement[0];

}

