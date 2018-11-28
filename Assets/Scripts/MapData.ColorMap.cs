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
        //if (source.width != width || source.height != depth) return;

        int targetLength = width * depth;
        if (colorMap.map == null || colorMap.map.Length != targetLength) colorMap.map = new Color[targetLength];//TODO properly rescale

        if (colorMap.texture == null) return;
        Texture2D source = colorMap.texture;

        Color[] colors = source.GetPixels();
        Copy2D(colors, source.width, source.height, colorMap.map, width, depth);
    }

    public void WriteToTexture(ColorMap colorMap)
    {
        int targetLength = width * depth;
        if (colorMap.map == null || colorMap.map.Length != targetLength) return;//TODO: colorMap.map = new Color[targetLength];//TODO properly rescale, aux generic method

        if (colorMap.texture == null) {
            //TODO create or something, or return
            return;
        }
        Texture2D target = colorMap.texture;

        target.Resize(width, depth, TextureFormat.ARGB32, false);
        //target.width = width;
        //target.height = depth;

        //float minWidth = Mathf.Min(source.width, width);
        //float minHeight = Mathf.Min(source.height, depth);
        
        target.SetPixels(colorMap.map);
        target.Apply(true, false);
    }

    void ColorMapOnEnable()
    {

    }

    void ColorMapOnDisable()
    {

    }

    void ColorMapOnValidate()
    {
#if UNITY_EDITOR
        for (int i = 0; i < colorMaps.Length; ++i)
        {
            ColorMap colorMap = colorMaps[i];

            ReadTexture(colorMap);//TODO Ensure texture exists, create otherwise and store/reference somehow
                                  //TODO remove unreferenced textures? (record in the proper undo)
            //TODO Dont read Texture2D unless needed, just terrain mesh color map
        }
        SerializeMapAssets();
#endif
    }

    //TODO handle in editor class!:
    public void LoadColorMaps()//TODO read all textures
    { }

    public void ValidateColorMapsAssets()//TODO load all at this path, count references (extend to other subassets)
    { }//TODO ensure everything has an asset, and every asset is referenced once

#if UNITY_EDITOR
    public void SerializeMapAssets()
    {
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
        for (int i = 0; i < colorMaps.Length; ++i)
        {
            ColorMap colorMap = colorMaps[i];
            int prevIndex = System.Array.FindIndex(colorMaps, 0, i, (cm) => Texture2D.ReferenceEquals(cm.texture, colorMap.texture));
            if (prevIndex != -1) colorMap.texture = null;
            EnsureTextureAtPath(colorMap, i, assetPath);
            WriteToTexture(colorMap);
            //TODO check duplicate
        }
    }

    public void EnsureTextureAtPath(ColorMap colorMap, int index)
    {
        EnsureTextureAtPath(colorMap, index, UnityEditor.AssetDatabase.GetAssetPath(this));
    }

    private void EnsureTextureAtPath(ColorMap colorMap, int index, string assetPath)
    {
        Texture2D texture = colorMap.texture;
        if (texture != null)
        {
            string textureAssetPath = UnityEditor.AssetDatabase.GetAssetPath(texture);
            
            if (assetPath != textureAssetPath) texture = null;
            //TODO check duplicate
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
