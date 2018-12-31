using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// XYZ: variants densities
/// W: size
/// </summary>
[CreateAssetMenu(menuName = "DensityPropsLogic/Basic", fileName = "Basic Density Props Logic")]
public sealed class BasicDensityPropsLogic : MapData.PropsMeshData.DensityPropsLogic
{
    [System.Serializable]
    private struct VariantAttributes //For density maps only
    {
        public FloatRange scaleRange;
        public FloatRange alignmentRange;
        public FloatRange rotationRange;
        public FloatRange yOffsetRange;
        public Gradient colorGradient;

        public static VariantAttributes DefaultAttributes
        {
            get
            {
                return new VariantAttributes()
                {
                    scaleRange = new FloatRange(1f, 1f),
                    alignmentRange = new FloatRange(0.2f, 0.5f),
                    rotationRange = new FloatRange(-180f, 180f),
                    yOffsetRange = new FloatRange(0f, 0f),
                    colorGradient = new Gradient()
                };
            }
        }
    }

    [SerializeField]
    VariantAttributes[] variantAttributes = new VariantAttributes[] {
        VariantAttributes.DefaultAttributes
    };

    [SerializeField] private MapData mapData;
    [SerializeField] private int mapIndex;
    private MapData.MapTexture mapTexture;

    public sealed override MapData.PropInstance BuildInstanceData(Vector2 pos, float elementRand, PropDitherPattern.PatternElement element, Vector4 densityValues)
    {
        MapData.PropInstance instanceData = default(MapData.PropInstance);
        instanceData.variantIndex = -1;//Null instance

        float densitySum = densityValues.x + densityValues.y + densityValues.z;
        
        if (elementRand > densitySum) return instanceData;//Density filter
        
        Vector3 densityLimits = densityValues;
        if (densitySum > 1f) {
            densityLimits *= 1f / densitySum;
        }
        densityLimits.y += densityLimits.x;
        densityLimits.z += densityLimits.y;

        if (elementRand <= densityLimits.x) instanceData.variantIndex = 0;
        else if (elementRand <= densityLimits.y) instanceData.variantIndex = 1;
        else if (elementRand <= densityLimits.z) instanceData.variantIndex = 2;
        else return instanceData;//Null instance still

        if (variantAttributes.Length <= instanceData.variantIndex)
        {
            instanceData.variantIndex = -1;//Back to null instance
            return instanceData;
        }
        VariantAttributes attributes = variantAttributes[instanceData.variantIndex];

        instanceData.position = new Vector3(pos.x, attributes.yOffsetRange.GetValue(element.rand2), pos.y);
        instanceData.alignment = attributes.alignmentRange.GetValue(element.rand0);
        instanceData.rotation = attributes.rotationRange.GetValue(element.rand1);
        instanceData.size = element.r * attributes.scaleRange.GetValue(densityValues.w);
        if (mapTexture != null && mapData != null)//TODO handle density maps like this? bring patterns here too? IEnumerator GetInstances?
        {
            instanceData.tint = mapTexture.SampleValue(pos.x, pos.y, mapData);
        }
        else if (attributes.colorGradient != null)
        {
            instanceData.tint = attributes.colorGradient.Evaluate(element.rand3);
        }
        else {
            instanceData.tint = Color.white;
        }

        return instanceData;
    }

    void OnValidate()
    {
        if (variantAttributes.Length != 3) System.Array.Resize(ref variantAttributes, 3);

        if (mapData != null && mapIndex >= 0 && mapIndex < mapData.mapTextures.Length) mapTexture = mapData.mapTextures[mapIndex];
    }
}
