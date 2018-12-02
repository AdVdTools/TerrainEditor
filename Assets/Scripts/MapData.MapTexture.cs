using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public partial class MapData : ScriptableObject
{

    [System.Serializable]
    public class MapTexture//TODO label for editor
    {
        [System.NonSerialized] public Color[] map;
        [HideInInspector] public Texture2D texture;

        //Implicit conversión Color-Vector4
        public Color SampleValue(float x, float y, MapData mapData)
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

    [UnityEngine.Serialization.FormerlySerializedAs("colorMaps")]//TODO remove
    public MapTexture[] mapTextures = new MapTexture[0];

    
    public void ReadTexture(MapTexture mapTexture)
    {
        int targetLength = width * depth;
        if (mapTexture.map == null || mapTexture.map.Length != targetLength) mapTexture.map = new Color[targetLength];

        if (mapTexture.texture == null) return;
        Texture2D source = mapTexture.texture;

        Color[] colors = source.GetPixels();
        Copy2D(colors, source.width, source.height, mapTexture.map, width, depth);
    }

    public void WriteToTexture(MapTexture mapTexture)
    {
        int targetLength = width * depth;
        if (mapTexture.map == null || mapTexture.map.Length != targetLength)
        {
            Debug.LogError("Color map data might be corrupt");
            return;
        }

        if (mapTexture.texture == null) {
            Debug.LogError("Missing color map texture");
            return;
        }
        Texture2D target = mapTexture.texture;

        if (target.width != width || target.height != depth)
        {
            target.Resize(width, depth, TextureFormat.ARGB32, false);
        }
        
        target.SetPixels(mapTexture.map);
        target.Apply(true, false);
    }

    void MapTextureOnEnable()
    {

    }

    void MapTextureOnDisable()
    {

    }

    void MapTextureOnValidate()//TODO test
    {
#if UNITY_EDITOR
        for (int i = 0; i < mapTextures.Length; ++i)
        {
            MapTexture mapTexture = mapTextures[i];

            ReadTexture(mapTexture);
        }
        ValidateMapTexturesAssets();// SerializeMapAssets();
#endif
        //TODO #else load main color map only, do not reserialize
    }


#if UNITY_EDITOR
    //TODO trigger assets/project update (as in save) on certain events (mouse up?)
    public void ValidateMapTexturesAssets()//TODO extend to other subassets: move to main cs file, wrap colorMap loop, add other maps
    {
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
        Object[] assetsAtPath = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
        int[] refCounters = new int[assetsAtPath.Length];

        int mainAssetIndex = System.Array.FindIndex(assetsAtPath, (asset) => ReferenceEquals(asset, this));
        if (mainAssetIndex < 0) Debug.LogWarning("Main asset is not part of 'all assets at path'");
        else refCounters[mainAssetIndex]++;

        for (int i = 0; i < mapTextures.Length; ++i)
        {
            MapTexture mapTexture = mapTextures[i];

            if (mapTexture.texture != null)
            {
                int assetIndex = System.Array.FindIndex(assetsAtPath, (asset) => ReferenceEquals(asset, mapTexture.texture));
                if (assetIndex >= 0) 
                {
                    refCounters[assetIndex]++;
                    if (refCounters[assetIndex] > 1)
                    {
                        Debug.LogWarningFormat("Texture '{0}' referenced more than once", mapTexture.texture);
                        mapTexture.texture = null;
                    }
                }
                else {
                    Debug.LogWarningFormat("Texture '{0}' is not part of 'all assets at path'", mapTexture.texture);
                }
            }

            if (mapTexture.texture != null)//TODO reuse for other maps, and in editor if possible
            {
                UnityEditor.Undo.RecordObject(mapTexture.texture, "Color Map Change");
                EnsureTextureAtPath(i, assetPath);
            }
            else
            {
                EnsureTextureAtPath(i, assetPath);
                UnityEditor.Undo.RegisterCreatedObjectUndo(mapTexture.texture, "Color Map Create");
            }
            WriteToTexture(mapTexture);
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
        MapTexture mapTexture = mapTextures[index];
        Texture2D texture = mapTexture.texture;
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
            mapTexture.texture = texture;
        }
        texture.name = string.Format("{0}.Texture{1}", this.name, index);
    }
#endif
}
