using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public partial class MapData : ScriptableObject
{

    [System.Serializable]
    public class ColorMap
    {
        [System.NonSerialized] public Color[] map;
        [HideInInspector] public Texture2D texture;

        public Color SampleColor(float x, float y, MapData mapData)
        {
            Vector3Int indices;
            Vector3 barycentricCoordinate;
            if (mapData.SampleInfo(x, y, out indices, out barycentricCoordinate))
            {
                return map[indices.x] * barycentricCoordinate.x +
                    map[indices.y] * barycentricCoordinate.y +
                    map[indices.z] * barycentricCoordinate.z;
            }
            else
            {
                return default(Color);
            }
        }
    }

    public ColorMap[] colorMaps = new ColorMap[0];

    
    public void ReadTexture(ColorMap colorMap)
    {
        int targetLength = width * depth;
        if (colorMap.map == null || colorMap.map.Length != targetLength) colorMap.map = new Color[targetLength];

        if (colorMap.texture == null) return;
        Texture2D source = colorMap.texture;

        Color[] colors = source.GetPixels();
        Copy2D(colors, source.width, source.height, colorMap.map, width, depth);
    }

    public void WriteToTexture(ColorMap colorMap)
    {
        int targetLength = width * depth;
        if (colorMap.map == null || colorMap.map.Length != targetLength)
        {
            Debug.LogError("Color map data might be corrupt");
            return;
        }

        if (colorMap.texture == null) {
            Debug.LogError("Missing color map texture");
            return;
        }
        Texture2D target = colorMap.texture;

        if (target.width != width || target.height != depth)
        {
            target.Resize(width, depth, TextureFormat.ARGB32, false);
        }
        
        target.SetPixels(colorMap.map);
        target.Apply(true, false);
    }

    void ColorMapOnEnable()
    {

    }

    void ColorMapOnDisable()
    {

    }

    void ColorMapOnValidate()//TODO test
    {
#if UNITY_EDITOR
        for (int i = 0; i < colorMaps.Length; ++i)
        {
            ColorMap colorMap = colorMaps[i];

            ReadTexture(colorMap);
        }
        ValidateColorMapsAssets();// SerializeMapAssets();
#endif
        //TODO #else load main color map only, do not reserialize
    }


#if UNITY_EDITOR
    public void ValidateColorMapsAssets()//TODO extend to other subassets: move to main cs file, wrap colorMap loop, add other maps
    {
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
        Object[] assetsAtPath = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
        int[] refCounters = new int[assetsAtPath.Length];

        int mainAssetIndex = System.Array.FindIndex(assetsAtPath, (asset) => ReferenceEquals(asset, this));
        if (mainAssetIndex < 0) Debug.LogWarning("Main asset is not part of 'all assets at path'");
        else refCounters[mainAssetIndex]++;

        for (int i = 0; i < colorMaps.Length; ++i)
        {
            ColorMap colorMap = colorMaps[i];

            if (colorMap.texture != null)
            {
                int assetIndex = System.Array.FindIndex(assetsAtPath, (asset) => ReferenceEquals(asset, colorMap.texture));
                if (assetIndex >= 0) 
                {
                    refCounters[assetIndex]++;
                    if (refCounters[assetIndex] > 1)
                    {
                        Debug.LogWarningFormat("Texture '{0}' referenced more than once", colorMap.texture);
                        colorMap.texture = null;
                    }
                }
                else {
                    Debug.LogWarningFormat("Texture '{0}' is not part of 'all assets at path'", colorMap.texture);
                }
            }

            if (colorMap.texture != null)//TODO reuse for other maps, and in editor if possible
            {
                UnityEditor.Undo.RecordObject(colorMap.texture, "Color Map Change");
                EnsureTextureAtPath(i, assetPath);
            }
            else
            {
                EnsureTextureAtPath(i, assetPath);
                UnityEditor.Undo.RegisterCreatedObjectUndo(colorMap.texture, "Color Map Create");
            }
            WriteToTexture(colorMap);
        }

        for (int i = 0; i < refCounters.Length; ++i)
        {
            if (refCounters[i] == 0)
            {
                Debug.LogWarning("Unreferenced texture: " + assetsAtPath[i]);
                UnityEditor.Undo.DestroyObjectImmediate(assetsAtPath[i]);//TODO allow map destruction in mapeditor?
                //DestroyImmediate(assetsAtPath[i], true);
            }
        }
    }
    

    public void EnsureTexture(int index)
    {
        EnsureTextureAtPath(index, UnityEditor.AssetDatabase.GetAssetPath(this));
    }

    private void EnsureTextureAtPath(int index, string assetPath)
    {
        ColorMap colorMap = colorMaps[index];
        Texture2D texture = colorMap.texture;
        if (texture != null)
        {
            string textureAssetPath = UnityEditor.AssetDatabase.GetAssetPath(texture);

            if (assetPath != textureAssetPath)
            {
                Debug.LogWarning("Current texture doesn't belong to this asset");
                texture = null;
            }
        }

        if (texture == null)
        {
            texture = new Texture2D(width, depth);
            UnityEditor.AssetDatabase.AddObjectToAsset(texture, this);
            colorMap.texture = texture;
        }
        texture.name = string.Format("{0}.Color{1}", this.name, index);
    }
#endif
}
