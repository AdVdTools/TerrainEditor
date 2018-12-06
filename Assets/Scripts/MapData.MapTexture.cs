﻿using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;


public partial class MapData : ScriptableObject
{

    [System.Serializable]
    public class MapTexture
    {
        public string label = "";//TODO label for editor
        [System.NonSerialized] public Color[] map;
        [HideInInspector] public Texture2D texture;

        private enum MapTextureFormat { Float, Half, Byte }
        [SerializeField] private MapTextureFormat format;

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

        public bool IsLoaded { get { return map != null; } }

        public string GetTextureName(int index)
        {
            return string.IsNullOrEmpty(label) ? string.Format("Tex{0}", index) : string.Format("Tex{0}.{1}", index, label);
        }

        public TextureFormat Format
        {
            get {
                switch (format) {
                    case MapTextureFormat.Float:
                        return TextureFormat.RGBAFloat;
                    case MapTextureFormat.Half:
                        return TextureFormat.RGBAHalf;
                    case MapTextureFormat.Byte:
                        return TextureFormat.RGBA32;
                    default:
                        return TextureFormat.RGBA32;
                }
            }
        }
    }
    
    public MapTexture[] mapTextures = new MapTexture[0];


    //public void ReadTexture(MapTexture mapTexture)
    //{
    //    ReadTexture(mapTexture.texture, ref mapTexture.map);
    //}
    
    public void ReadTexture(Texture2D texture, ref Color[] map)
    { 
        int targetLength = width * depth;
        if (map == null || map.Length != targetLength) map = new Color[targetLength];

        if (texture == null) return;
        //Debug.Log(texture.name + " " + texture.format);

        Color[] colors = texture.GetPixels();
        Copy2D(colors, texture.width, texture.height, map, width, depth);
    }

    public void ReadTexture(Texture2D texture, ref float[] map)
    {
        int targetLength = width * depth;
        if (map == null || map.Length != targetLength) map = new float[targetLength];

        if (texture == null) return;
        //Debug.Log(texture.name + " " + texture.format);

        if (texture.format != TextureFormat.RFloat) return;

        NativeArray<float> nativeArray = texture.GetRawTextureData<float>();
        
        CopyNative2D(nativeArray, texture.width, texture.height, map, width, depth);
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
            
            ReadTexture(mapTexture.texture, ref mapTexture.map);
        }
#else
        if (meshColorMapIndex > 0 && meshColorMapIndex < mapTextures.Length)
        {
            MapTexture mapTexture = mapTextures[meshColorMapIndex];
            
            ReadTexture(mapTexture.texture, ref mapTexture.map);
        }
#endif
    }


#if UNITY_EDITOR

    //public void EnsureTexture(int index)
    //{
    //    EnsureTextureAtPath(index, UnityEditor.AssetDatabase.GetAssetPath(this));
    //}

    //private void EnsureTextureAtPath(int index, string assetPath)
    //{
    //    //MapTexture mapTexture = mapTextures[index];
    //    //Texture2D texture = mapTexture.texture;
    //    //if (texture != null)
    //    //{
    //    //    string textureAssetPath = UnityEditor.AssetDatabase.GetAssetPath(texture);

    //    //    if (assetPath != textureAssetPath)
    //    //    {
    //    //        Debug.LogWarning("Current texture doesn't belong to this asset");
    //    //        texture = null;
    //    //    }
    //    //}

    //    //if (texture == null)
    //    //{
    //    //    texture = new Texture2D(width, depth);
    //    //    UnityEditor.AssetDatabase.AddObjectToAsset(texture, this);
    //    //    mapTexture.texture = texture;
    //    //}
    //    //texture.name = string.Format("{0}.Tex{1}", this.name, index);
    //    //if (!string.IsNullOrEmpty(mapTexture.label)) texture.name = string.Format("{0}.{1}", texture.name, mapTexture.label);

    //    MapTexture mapTexture = mapTextures[index];
    //    string textureName = string.IsNullOrEmpty(mapTexture.label) ? string.Format("Tex{0}", index) : string.Format("Tex{0}.{1}", index, mapTexture.label);
    //    EnsureTextureAtPath(ref mapTexture.texture, textureName, assetPath);
    //}
    
    private void EnsureTextureAtPath(ref Texture2D texture, TextureFormat textureFormat, string assetPath)
    {
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
            texture = new Texture2D(width, depth, textureFormat, false);
            UnityEditor.AssetDatabase.AddObjectToAsset(texture, this);
        }
        else
        {
            if (texture.width != width || texture.height != depth || texture.format != textureFormat)
            {
                texture.Resize(width, depth, textureFormat, false);
            }
        }
    }
#endif


    public void WriteToTexture(Color[] map, Texture2D texture)//TODO distribute methods in files
    {
        int targetLength = width * depth;
        if (map == null || map.Length != targetLength)
        {
            Debug.LogError("Map data might be corrupt");
            return;
        }

        //if (texture == null)
        //{
        //    Debug.LogError("Missing map texture");
        //    return;
        //}

        //if (texture.width != width || texture.height != depth || texture.format != textureFormat)
        //{
        //    texture.Resize(width, depth, textureFormat, false);
        //}

        texture.SetPixels(map);
        texture.Apply(true, false);
    }

    public void WriteToTexture(float[] map, Texture2D texture)//TODO distribute methods in files
    {
        int targetLength = width * depth;
        if (map == null || map.Length != targetLength)
        {
            Debug.LogError("Map data might be corrupt");
            return;
        }

        //if (texture == null)
        //{
        //    Debug.LogError("Missing map texture");
        //    return;
        //}

        //if (texture.width != width || texture.height != depth || texture.format != TextureFormat.RFloat)
        //{
        //    texture.Resize(width, depth, TextureFormat.RFloat, false);
        //}

        //texture.SetPixels(map);
        NativeArray<float> nativeArray = texture.GetRawTextureData<float>();
        nativeArray.CopyFrom(map);
        texture.Apply(false, false);
    }
}
