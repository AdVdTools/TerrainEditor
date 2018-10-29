using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct VariantAttributes //For density maps only
{
    //InstanceSet PropInstances ignore the following fields
    //public float probability;

    //public Vector3 propsDirection;

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
                //probability = 1f,
                //propsDirection = Vector3.up,
                scaleRange = new FloatRange(1f, 1f),
                alignmentRange = new FloatRange(0.2f, 0.5f),
                rotationRange = new FloatRange(-180f, 180f),
                yOffsetRange = new FloatRange(0f, 0f)
            };
        }
    }
}

[CreateAssetMenu(menuName = "DensityPropsLogic/Basic", fileName = "Basic Density Props Logic")]
public sealed class BasicDensityPropsLogic : MapData.PropsMeshData.DensityPropsLogic
{
    [SerializeField]
    VariantAttributes[] variantAttributes = new VariantAttributes[] {
        VariantAttributes.DefaultAttributes
    };//TODO force length = 3 (XYZ)

    public readonly Vector3 propsDirection = new Vector3(0, 1, 0);

    public sealed override bool BuildInstanceData(Vector2 pos, float elementRand, PropDitherPattern.PatternElement element, Vector4 densityValues, ref MapData.PropInstance instanceData)
    {
        float densitySum = densityValues.x + densityValues.y + densityValues.z;
        
        if (elementRand > densitySum) return false;//Density filter
        
        Vector3 densityLimits = densityValues;
        if (densitySum > 1f) {
            densityLimits *= 1f / densitySum;
        }
        densityLimits.y += densityLimits.x;
        densityLimits.z += densityLimits.y;

        if (elementRand <= densityLimits.x) instanceData.variantIndex = 0;
        else if (elementRand <= densityLimits.y) instanceData.variantIndex = 1;
        else if (elementRand <= densityLimits.z) instanceData.variantIndex = 2;
        else return false;

        if (variantAttributes.Length <= instanceData.variantIndex) return false;
        VariantAttributes attributes = variantAttributes[instanceData.variantIndex];

        instanceData.position = new Vector3(pos.x, attributes.yOffsetRange.GetValue(element.rand2), pos.y);
        instanceData.alignment = attributes.alignmentRange.GetValue(element.rand0);// Vector3.Slerp(propsDirection, normal, attributes.alignmentRange.GetValue(element.rand0));//TODO alignment instead of direction, do direction on DoInstance(PropInstance)
        instanceData.rotation = attributes.rotationRange.GetValue(element.rand1);
        instanceData.size = element.r * attributes.scaleRange.GetValue(densityValues.w);//TODO change size with density vs size map. Vector2 density map? (density, size), size map => 0..1 lerp to variant size range?
        
        return true;
    }

    const string definitions = "XYZ: variants densities\nW: size";

    public sealed override string GetDefinitions()
    {
        return definitions;
    }
}
