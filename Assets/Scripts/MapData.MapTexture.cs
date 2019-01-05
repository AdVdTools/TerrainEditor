using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;


public partial class MapData : ScriptableObject
{

    [System.Serializable]
    public class MapTexture
    {
        public string label = "";
        [System.NonSerialized] public Color[] map;
        [HideInInspector] public Texture2D texture;

        private enum MapTextureFormat { Float, Half, Byte }
        [SerializeField] private MapTextureFormat format;

        public bool loadInRuntime = true;

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

        public bool IsLoaded { get { return map != null; } }//TODO use?

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

    
    public void ReadTexture(Texture2D texture, ref Color[] map)
    { 
        int targetLength = width * depth;
        if (map == null || map.Length != targetLength) map = new Color[targetLength];

        if (texture == null) return;

        Color[] colors = texture.GetPixels();
        Copy2D(colors, texture.width, texture.height, map, width, depth);
    }

    public void ReadTexture(Texture2D texture, ref float[] map)
    {
        int targetLength = width * depth;
        if (map == null || map.Length != targetLength) map = new float[targetLength];

        if (texture == null) return;

        if (texture.format != TextureFormat.RFloat) return;

        NativeArray<float> nativeArray = texture.GetRawTextureData<float>();
        CopyNative2D(nativeArray, texture.width, texture.height, map, width, depth);
    }

    void MapTextureOnEnable()
    {
#if !UNITY_EDITOR
        for (int i = 0; i < mapTextures.Length; ++i)
        {
            MapTexture mapTexture = mapTextures[i];
            
            if (mapTexture.loadInRuntime) ReadTexture(mapTexture.texture, ref mapTexture.map);
        }
#endif
    }

    void MapTextureOnDisable()
    {

    }

#if UNITY_EDITOR
    void MapTextureOnValidate()//TODO test
    {
        for (int i = 0; i < mapTextures.Length; ++i)
        {
            MapTexture mapTexture = mapTextures[i];

            ReadTexture(mapTexture.texture, ref mapTexture.map);
        }
    }
#endif
    


#if UNITY_EDITOR
    private void ValidateHeightTexture(string assetPath)
    {
        if (heightTexture != null)
        {
            string textureAssetPath = UnityEditor.AssetDatabase.GetAssetPath(heightTexture);

            if (assetPath != textureAssetPath)
            {
                Debug.LogWarning("This texture doesn't belong to this asset");
                heightTexture = null;
            }
        }
        else
        {
            UnityEditor.Undo.RecordObject(this, "Texture Assigned");
        }

        if (heightTexture == null)
        {
            heightTexture = new Texture2D(width, depth, TextureFormat.RFloat, false);
            UnityEditor.AssetDatabase.AddObjectToAsset(heightTexture, this);
            WriteToTexture(heights, heightTexture);
            UnityEditor.Undo.RegisterCreatedObjectUndo(heightTexture, "Texture Created");
        }
        else
        {
            if (heightTexture.width != width || heightTexture.height != depth || heightTexture.format != TextureFormat.RFloat)
            {
                UnityEditor.Undo.RecordObject(heightTexture, "Texture Changed");
                heightTexture.Resize(width, depth, TextureFormat.RFloat, false);
                WriteToTexture(heights, heightTexture);
            }
        }
        heightTexture.name = string.Format("{0}.{1}", this.name, "Heights");
    }

    private void ValidateMapTexture(int index, string assetPath)
    {
        MapTexture mapTexture = mapTextures[index];//Valid index assumed
        Texture2D texture = mapTexture.texture;
        TextureFormat format = mapTexture.Format;
        if (texture != null)
        {
            string textureAssetPath = UnityEditor.AssetDatabase.GetAssetPath(texture);

            if (assetPath != textureAssetPath)
            {
                Debug.LogWarning("This texture doesn't belong to this asset");
                texture = null;
            }
        }
        else
        {
            UnityEditor.Undo.RecordObject(this, "Texture Assigned");
        }

        if (texture == null)
        {
            mapTexture.texture = texture = new Texture2D(width, depth, format, false);
            UnityEditor.AssetDatabase.AddObjectToAsset(texture, this);
            WriteToTexture(mapTexture.map, texture);
            UnityEditor.Undo.RegisterCreatedObjectUndo(texture, "Texture Created");
        }
        else
        {
            if (texture.width != width || texture.height != depth || texture.format != format)
            {
                UnityEditor.Undo.RecordObject(texture, "Texture Changed");
                texture.Resize(width, depth, format, false);
                WriteToTexture(mapTexture.map, texture);
            }
        }
        texture.name = string.Format("{0}.{1}", this.name, mapTexture.GetTextureName(index));
    }
#endif
    
    //Validation ensures textures exist when these methos are used
    public void WriteToTexture(float[] map, Texture2D texture)
    {
        int targetLength = width * depth;
        if (map == null || map.Length != targetLength)
        {
            Debug.LogError("Map data might be corrupt");
            return;
        }

        NativeArray<float> nativeArray = texture.GetRawTextureData<float>();
        nativeArray.CopyFrom(map);
        texture.Apply(false, false);
    }

    public void WriteToTexture(Color[] map, Texture2D texture)
    {
        int targetLength = width * depth;
        if (map == null || map.Length != targetLength)
        {
            Debug.LogError("Map data might be corrupt");
            return;
        }

        texture.SetPixels(map);
        texture.Apply(true, false);
    }
}
