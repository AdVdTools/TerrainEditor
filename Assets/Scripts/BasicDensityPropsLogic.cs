using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct VariantAttributes //For density maps only
{
    public FloatRange scaleRange;
    public FloatRange alignmentRange;
    public FloatRange rotationRange;
    public FloatRange yOffsetRange;

    public static VariantAttributes DefaultAttributes
    {
        get
        {
            return new VariantAttributes()
            {
                scaleRange = new FloatRange(1f, 1f),
                alignmentRange = new FloatRange(0.2f, 0.5f),
                rotationRange = new FloatRange(-180f, 180f),
                yOffsetRange = new FloatRange(0f, 0f)
            };
        }
    }
}

/// <summary>
/// XYZ: variants densities
/// W: size
/// </summary>
[CreateAssetMenu(menuName = "DensityPropsLogic/Basic", fileName = "Basic Density Props Logic")]
public sealed class BasicDensityPropsLogic : MapData.PropsMeshData.DensityPropsLogic
{
    [SerializeField]
    VariantAttributes[] variantAttributes = new VariantAttributes[] {
        VariantAttributes.DefaultAttributes
    };
    
    public readonly Vector3 propsDirection = new Vector3(0, 1, 0);

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
        instanceData.alignment = attributes.alignmentRange.GetValue(element.rand0);// Vector3.Slerp(propsDirection, normal, attributes.alignmentRange.GetValue(element.rand0));//TODO alignment instead of direction, do direction on DoInstance(PropInstance)
        instanceData.rotation = attributes.rotationRange.GetValue(element.rand1);
        instanceData.size = element.r * attributes.scaleRange.GetValue(densityValues.w);//TODO change size with density vs size map. Vector2 density map? (density, size), size map => 0..1 lerp to variant size range?
        
        return instanceData;
    }

    void OnValidate()
    {
        if (variantAttributes.Length != 3) System.Array.Resize(ref variantAttributes, 3);
    }
}
