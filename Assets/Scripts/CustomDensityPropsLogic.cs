using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/// <summary>
/// XYZ: variants densities
/// W: size
/// </summary>
[CreateAssetMenu(menuName = "DensityPropsLogic/Custom", fileName = "Custom Density Props Logic")]
public sealed class CustomDensityPropsLogic : MapData.PropsMeshData.DensityPropsLogic
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
    VariantAttributes variantAttributes = VariantAttributes.DefaultAttributes;
    [SerializeField] int minVariantIndex = 0;
    [SerializeField] int maxVariantIndex = 0;

    //[SerializeField] private MapData mapData;
    //[SerializeField] private int mapIndex;
    //private MapData.MapTexture mapTexture;

    public sealed override MapData.PropInstance BuildInstanceData(Vector2 pos, float elementRand, PropDitherPattern.PatternElement element, Vector4 densityValues)
    {
        MapData.PropInstance instanceData = default(MapData.PropInstance);
        instanceData.variantIndex = -1;//Null instance

        float densityValue = densityValues.w;
        
        if (elementRand > densityValue) return instanceData;//Density filter

        int variantRange = maxVariantIndex - minVariantIndex + 1;
        instanceData.variantIndex = minVariantIndex + Mathf.Clamp(Mathf.FloorToInt(element.rand3 * variantRange), 0, variantRange - 1); //Random.Range(minVariantIndex, maxVariantIndex + 1);
        
        VariantAttributes attributes = variantAttributes;

        instanceData.position = new Vector3(pos.x, attributes.yOffsetRange.GetValue(/*element.rand2*/elementRand), pos.y);
        instanceData.alignment = attributes.alignmentRange.GetValue(element.rand0);
        instanceData.rotation = attributes.rotationRange.GetValue(element.rand1);
        instanceData.size = element.r * attributes.scaleRange.GetValue(element.rand2);//repurposed
        //if (mapTexture != null && mapData != null)//TODO handle density maps like this? bring patterns here too? IEnumerator GetInstances?
        //{
        //    instanceData.tint = mapTexture.SampleValue(pos.x, pos.y, mapData);
        //}
        //else if (attributes.colorGradient != null)
        //{
        //    instanceData.tint = attributes.colorGradient.Evaluate(element.rand3);
        //}
        //else {
        //    instanceData.tint = Color.white;
        //}

        instanceData.tint = attributes.colorGradient.Evaluate(element.rand3) * new Color(densityValues.x, densityValues.y, densityValues.z);

        return instanceData;
    }

    //void OnValidate()
    //{
    //    if (mapData != null && mapIndex >= 0 && mapIndex < mapData.mapTextures.Length) mapTexture = mapData.mapTextures[mapIndex];
    //}
}
