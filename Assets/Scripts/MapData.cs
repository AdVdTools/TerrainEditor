using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

[Serializable]
[CreateAssetMenu(fileName = "Map")]
public partial class MapData : ScriptableObject
{

    [SerializeField] private int width, depth;
    [SerializeField] private int meshColorMapIndex;
    private float[] heights = new float[0];

    [HideInInspector] [SerializeField] private Texture2D heightTexture;

    
    //[SerializeField]//TODO assign to renderer? ConfigureRenderer(Renderer) method? assign materials too
    //private ShadowCastingMode castShadows = ShadowCastingMode.Off;
    //[SerializeField]
    //private bool receiveShadows = false;

    [SerializeField] private Material terrainMaterial;


    public float[] Heights { get { return heights; } }
    public Texture2D HeightTexture { get { return heightTexture; } }

    public int MeshColorMapIndex { get { return meshColorMapIndex; } }
    public Material TerrainMaterial { get { return terrainMaterial; } }
    

    public const float sqrt3 = 1.7320508f;
    public const float cos30 = 0.8660254f;
    public Vector2 GridToLocal2D(int row, int column)
    {
        return new Vector2(column * sqrt3 + (row & 1) * cos30, row * 1.5f);
    }

    public int GridToIndex(int row, int column)
    {
        Vector2Int inBounds = GridPositionInBounds(row, column);

        return inBounds.y * width + inBounds.x;
    }

    public Vector2Int IndexToGrid(int index)
    {
        int i = index / width;
        int j = index % width;
        return new Vector2Int(j, i);
    }

    public Vector2Int GridPositionInBounds(int row, int column)
    {
        return new Vector2Int(ColumnInBounds(column), RowInBounds(row));
    }

    public int ColumnInBounds(int column) { return Mathf.Clamp(column, 0, width - 1); }
    public int RowInBounds(int row) { return Mathf.Clamp(row, 0, depth - 1); }

    public Vector3 GetPosition(int index)
    {
        Vector2Int gridPosition = IndexToGrid(index);
        Vector2 position = GridToLocal2D(RowInBounds(gridPosition.y), gridPosition.x);
        return new Vector3(position.x, heights[index], position.y);
    }
    

    //Returns false if outside
    private bool SampleInfo(float x, float y, out Vector3Int indices, out Vector3 barycentricCoordinate)
    {
        indices = default(Vector3Int);
        barycentricCoordinate = default(Vector3);

        Vector2 normalizedCoords = new Vector2(x / sqrt3, y / 1.5f);
        int oddToEven = Mathf.FloorToInt(normalizedCoords.y) & 1;
        normalizedCoords.x += -Mathf.PingPong(normalizedCoords.y, 1f) * 0.5f;
        
        int i = Mathf.FloorToInt(normalizedCoords.y);
        int j = Mathf.FloorToInt(normalizedCoords.x);

        if (i < 0 || i >= depth - 1) return false;
        if (j < 0 || j >= width - 1) return false;
        int index = i * width + j;

        float dx = normalizedCoords.x - j;
        float dy = normalizedCoords.y - i;
        
        if (oddToEven == 0)
        {
            float dXY = dx + dy;
            if (dXY < 1f)
            {
                indices = new Vector3Int(index, index + 1, index + width);
                barycentricCoordinate = new Vector3(1 - dXY, dx, dy);
            }
            else
            {
                indices = new Vector3Int(index + width + 1, index + 1, index + width);
                barycentricCoordinate = new Vector3(dXY - 1, 1 - dy, 1 - dx);
            }
        }
        else
        {
            float dXY = 1 - dx + dy;
            if (dx > dy)
            {
                indices = new Vector3Int(index, index + 1, index + width + 1);
                barycentricCoordinate = new Vector3(1 - dx, 1 - dXY, dy);
            }
            else
            {
                indices = new Vector3Int(index, index + width, index + width + 1);
                barycentricCoordinate = new Vector3(1 - dy, dXY - 1, dx);
            }
        }
        return true;
    }

    public float SampleHeight(float x, float y)
    {
        Vector3Int indices;
        Vector3 barycentricCoordinate;
        if (SampleInfo(x, y, out indices, out barycentricCoordinate))
        {
            return heights[indices.x] * barycentricCoordinate.x +
                heights[indices.y] * barycentricCoordinate.y +
                heights[indices.z] * barycentricCoordinate.z;
        }
        else
        {
            return 0f;
        }
    }

    public Vector3 SampleNormals(float x, float y)
    {
        Vector3Int indices;
        Vector3 barycentricCoordinate;
        if (SampleInfo(x, y, out indices, out barycentricCoordinate))
        {
            return (normals[indices.x] * barycentricCoordinate.x +
                normals[indices.y] * barycentricCoordinate.y +
                normals[indices.z] * barycentricCoordinate.z).normalized;
        }
        else
        {
            return Vector3.up;
        }
    }
    

    public Mesh sharedTerrainMesh { get { return terrainMesh; } }

    public Vector3[] Vertices { get { return vertices; } }

    public int[] Indices { get { return indices; } }

    Mesh terrainMesh;
    Vector3[] vertices;
    Vector3[] normals;
    // color comes from a map texture
    Vector2[] uvs;
    Vector2[] uvs2;
    int[] indices;
    
    [ContextMenu("RebuildTerrainMesh")]
    public Mesh RefreshTerrainMesh()
    {
        return RebuildParallel(8);
    }

    private void OnEnable()
    {
        PropsDataOnEnable();
        MapTextureOnEnable();
    }


    private void OnDisable()
    {
        PropsDataOnDisable();
        MapTextureOnDisable();
    }


    private void OnValidate()
    {
        HeightTextureLoad();

        PropsDataOnValidate();
        MapTextureOnValidate();//Load colorMapIndex map for mesh build even if !EDITOR

#if UNITY_EDITOR
        ValidateSubassets();// SerializeMapAssets();
#endif
    }

    private void HeightTextureLoad()
    {
        ReadTexture(heightTexture, ref heights);//ReadTexture ensures a properly sized array
    }
    
#if UNITY_EDITOR
    public void ValidateSubassets()
    {
        Debug.Log("Validate Subassets");
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
        UnityEngine.Object[] assetsAtPath = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
        int[] refCounters = new int[assetsAtPath.Length];

        // Main Asset
        int mainAssetIndex = System.Array.FindIndex(assetsAtPath, (asset) => ReferenceEquals(asset, this));
        if (mainAssetIndex < 0) Debug.LogWarning("Main asset is not part of 'all assets at path'");
        else refCounters[mainAssetIndex]++;

        // Heights
        if (heightTexture != null)
        {
            int heightAssetIndex = System.Array.FindIndex(assetsAtPath, (asset) => ReferenceEquals(asset, heightTexture));
            if (heightAssetIndex < 0) Debug.LogWarning("Height map asset is not part of 'all assets at path'");
            else refCounters[heightAssetIndex]++;
        }
        ValidateHeightTexture(assetPath);
        

        // MapTextures
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
                else
                {
                    Debug.LogWarningFormat("Texture '{0}' is not part of 'all assets at path'", mapTexture.texture);
                }
            }
            ValidateMapTexture(i, assetPath);
        }

        for (int i = 0; i < refCounters.Length; ++i)
        {
            if (refCounters[i] == 0)
            {
                Debug.LogWarning("Unreferenced texture: " + assetsAtPath[i]);
                UnityEditor.Undo.DestroyObjectImmediate(assetsAtPath[i]);
                //DestroyImmediate(assetsAtPath[i], true);
            }
        }
    }
#endif
    
    public struct RaycastHit {
        public float distance;
        public Vector3 point;
        public Vector3 barycentricCoordinate;
        public int triangleIndex;
    }

    const float epsilon = 1e-5f;
    public bool Raycast(Ray ray, out RaycastHit hitInfo, float raycastDistance)
    {
        hitInfo = new RaycastHit() { distance = raycastDistance };
        if (indices == null || vertices == null) return false;

        int trianglesCount = indices.Length / 3;
        for (int triIndex = 0; triIndex < trianglesCount; ++triIndex)
        {
            int index = triIndex * 3;
            Vector3 v0 = vertices[indices[index]];
            Vector3 v1 = vertices[indices[index+1]];
            Vector3 v2 = vertices[indices[index+2]];
            Vector3 e1 = v1 - v0, e2 = v2 - v0;

            Vector3 crossE1R = Vector3.Cross(e1, ray.direction);
            float projArea = Vector3.Dot(crossE1R, e2);
            if (projArea < epsilon && projArea > -epsilon) continue;
            float invArea = 1 / projArea;

            Vector3 offset = ray.origin - v0;
            Vector3 crossE2O = Vector3.Cross(e2, offset);

            float u = Vector3.Dot(ray.direction, crossE2O) * invArea;
            if (u < 0 || u > 1) continue;
            float v = Vector3.Dot(offset, crossE1R) * invArea;
            float uPlusV = u + v;
            if (v < 0 || uPlusV > 1) continue;

            float distance = Vector3.Dot(e1, crossE2O) * invArea;// offset · (e1 x e2) / r · (e1 x e2)
            if (distance > epsilon && distance < hitInfo.distance) {
                hitInfo.distance = distance;
                hitInfo.point = ray.GetPoint(distance);
                hitInfo.barycentricCoordinate = new Vector3(1 - uPlusV, u, v);
                hitInfo.triangleIndex = triIndex;
            }
        }
        if (hitInfo.distance >= raycastDistance) return false;
        return true;
    }

    //  e1 · (r x e2) = e1x (ry e2z - rz e2y) + e1y (rz e2x - rx e2z) + e1z (rx e2y - ry e2x)
    // r · (e2 x e1) = rx (e1z e2y - e1y e2z) + ry (e1x e2z - e1z e2x) + rz (e1y e2x - e1x e2y)
    // ~0 -> parallel

    // (or - v0) · (r x e2) = e2 · (r x (v0 - or)) = r · ((v0 - or) x e2) -> u (r · (e2 x e1))
    // r · ((or - v0) x e1) = e1 · ((v0 - or) x r) = (or - v0) · (e1 x r) -> v (r · (e2 x e1))

    // e2 · ((or - v0) x e1)

    #region RaycastParallel
    private class RaycastThreadData {
        public int startIndex, endIndex;
        public RaycastHit hitInfo;
        public ManualResetEvent mre;
    }

    public bool RaycastParallel(Ray ray, out RaycastHit hitInfo, float raycastDistance, int threads)
    {
        hitInfo = new RaycastHit() { distance = raycastDistance };
        if (indices == null || vertices == null) return false;

        int trianglesCount = indices.Length / 3;
        int trianglesPerThread = (trianglesCount - 1) / threads + 1;
        var threadsData = new List<RaycastThreadData>(threads);
        //var sampler = CustomSampler.Create("ParallelRaycast");
        for (int i = 0; i < threads; ++i)
        {
            var data = new RaycastThreadData()
            {
                startIndex = i * trianglesPerThread,
                endIndex = Mathf.Min((i + 1) * trianglesPerThread, trianglesCount),
                hitInfo = new RaycastHit() { distance = raycastDistance },
                mre = new ManualResetEvent(false)
            };
            ThreadPool.QueueUserWorkItem((d) =>
            {
                RaycastThreadData td = (RaycastThreadData)d;
                //Profiler.BeginThreadProfiling("ParallelRaycast", td.startIndex + "-" + td.endIndex);
                for (int triIndex = td.startIndex; triIndex < td.endIndex; ++triIndex)
                {
                    //sampler.Begin();
                    int index = triIndex * 3;
                    Vector3 v0 = vertices[indices[index]];
                    Vector3 v1 = vertices[indices[index + 1]];
                    Vector3 v2 = vertices[indices[index + 2]];
                    Vector3 e1 = v1 - v0, e2 = v2 - v0;

                    Vector3 crossE1R = Vector3.Cross(e1, ray.direction);
                    float projArea = Vector3.Dot(crossE1R, e2);
                    if (projArea < epsilon && projArea > -epsilon) continue;
                    float invArea = 1 / projArea;

                    Vector3 offset = ray.origin - v0;
                    Vector3 crossE2O = Vector3.Cross(e2, offset);

                    float u = Vector3.Dot(ray.direction, crossE2O) * invArea;
                    if (u < 0 || u > 1) continue;
                    float v = Vector3.Dot(offset, crossE1R) * invArea;
                    float uPlusV = u + v;
                    if (v < 0 || uPlusV > 1) continue;

                    float distance = Vector3.Dot(e1, crossE2O) * invArea;// offset · (e1 x e2) / r · (e1 x e2)
                    if (distance > epsilon && distance < td.hitInfo.distance)
                    {
                        td.hitInfo.distance = distance;
                        td.hitInfo.point = ray.GetPoint(distance);
                        td.hitInfo.barycentricCoordinate = new Vector3(1 - uPlusV, u, v);
                        td.hitInfo.triangleIndex = triIndex;
                    }
                    //sampler.End();
                }
                //Profiler.EndThreadProfiling();
                td.mre.Set();
            }, data);
            threadsData.Add(data);
        }
        foreach (var data in threadsData)
        {
            data.mre.WaitOne();

            if (hitInfo.distance > data.hitInfo.distance) hitInfo = data.hitInfo;
        }

        if (hitInfo.distance >= raycastDistance) return false;
        return true;
    }
    #endregion

   

    private class ThreadData
    {
        public int startIndex, endIndex;
        public ManualResetEvent mre;
    }

    public Mesh RebuildParallel(int threads)//TODO async rebuild might be needed eventually
    {
        if (terrainMesh == null)
        {
            terrainMesh = new Mesh();
            terrainMesh.name = this.name;
            terrainMesh.hideFlags = HideFlags.HideAndDontSave;
        }
        else
        {
            terrainMesh.Clear();
        }

        if (width < 2 || depth < 2) return terrainMesh;

        int verticesLength = width * depth;
        int indicesLength = (width - 1) * (depth - 1) * 6;
        
        if (vertices == null || vertices.Length != verticesLength) vertices = new Vector3[verticesLength];
        if (normals == null || normals.Length != verticesLength) normals = new Vector3[verticesLength];
        if (uvs == null || uvs.Length != verticesLength) uvs = new Vector2[verticesLength];
        if (uvs2 == null || uvs2.Length != verticesLength) uvs2 = new Vector2[verticesLength];

        if (indices == null || indices.Length != indicesLength) indices = new int[indicesLength];

        // Fill arrays
        int verticesPerThread = (verticesLength - 1) / threads + 1;
        var threadsData = new List<ThreadData>();

        for (int i = 0; i < threads; ++i)
        {
            var data = new ThreadData()
            {
                startIndex = i * verticesPerThread,
                endIndex = Mathf.Min((i + 1) * verticesPerThread, verticesLength),
                mre = new ManualResetEvent(false)
            };
            ThreadPool.QueueUserWorkItem((d) =>
            {
                var td = (ThreadData)d;
                for (int vertexIndex = td.startIndex; vertexIndex < td.endIndex; ++vertexIndex)
                {
                    vertices[vertexIndex] = GetPosition(vertexIndex);
                    uvs[vertexIndex] = new Vector2(vertices[vertexIndex].x, vertices[vertexIndex].z);
                    uvs2[vertexIndex] = new Vector2(vertexIndex % width, vertexIndex / width);
                    normals[vertexIndex] = Vector3.zero;
                }
                td.mre.Set();
            }, data);
            threadsData.Add(data);
        }
        foreach (var data in threadsData)
        {
            data.mre.WaitOne();
        }

        int indexIndex = 0;
        for (int r = 1; r < depth; ++r)
        {
            for (int c = 1; c < width; ++c)
            {
                int r_1 = r - 1;
                int c_1 = c - 1;
                int baseIndex = GridToIndex(r_1, c_1);
                int oddRow = r_1 & 1;

                indices[indexIndex++] = baseIndex;
                indices[indexIndex++] = baseIndex + width;
                indices[indexIndex++] = baseIndex + 1 + width * oddRow;
                indices[indexIndex++] = baseIndex + width * (1 - oddRow);
                indices[indexIndex++] = baseIndex + 1 + width;
                indices[indexIndex++] = baseIndex + 1;
            }
        }

        for (indexIndex = 0; indexIndex < indicesLength; ++indexIndex)
        {
            int i0 = indices[indexIndex];
            int i1 = indices[++indexIndex];
            int i2 = indices[++indexIndex];

            Vector3 v0 = vertices[i0], v1 = vertices[i1], v2 = vertices[i2];
            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            normals[i0] += normal;
            normals[i1] += normal;
            normals[i2] += normal;
        }
        for (int vertexIndex = 0; vertexIndex < verticesLength; ++vertexIndex)
        {
            normals[vertexIndex] = normals[vertexIndex].normalized;
        }

        terrainMesh.vertices = vertices;
        terrainMesh.normals = normals;
        terrainMesh.uv = uvs;
        terrainMesh.uv2 = uvs2;
        UpdateMeshColor();
        terrainMesh.triangles = indices;

        return terrainMesh;
    }

    public void QuickRebuildParallel(int threads)
    {
        if (terrainMesh == null) return;

        int verticesLength = width * depth;

        if (vertices == null) return;
        if (vertices.Length != verticesLength) vertices = new Vector3[verticesLength];
        
        // Fill arrays
        int verticesPerThread = (verticesLength - 1) / threads + 1;
        var threadsData = new List<ThreadData>();

        for (int i = 0; i < threads; ++i)
        {
            var data = new ThreadData()
            {
                startIndex = i * verticesPerThread,
                endIndex = Mathf.Min((i + 1) * verticesPerThread, verticesLength),
                mre = new ManualResetEvent(false)
            };
            ThreadPool.QueueUserWorkItem((d) =>
            {
                var td = (ThreadData)d;
                for (int vertexIndex = td.startIndex; vertexIndex < td.endIndex; ++vertexIndex)
                {
                    vertices[vertexIndex] = GetPosition(vertexIndex);
                }
                td.mre.Set();
            }, data);
            threadsData.Add(data);
        }
        foreach (var data in threadsData)
        {
            data.mre.WaitOne();
        }

        terrainMesh.vertices = vertices;
    }

    public void UpdateMeshColor()
    {
        if (terrainMesh == null) return;

        if (meshColorMapIndex < 0 || meshColorMapIndex >= mapTextures.Length) return;
        MapTexture meshColorMapTexture = mapTextures[meshColorMapIndex];
        if (meshColorMapTexture.map == null)
        {
            Debug.LogError("Mesh color map has not been loaded");
            return;
        }

        terrainMesh.colors = meshColorMapTexture.map;
    }
    

    #region 2DUtility
    public static void Resize2D<T>(ref T[] array, int srcWidth, int srcHeight, int tgtWidth, int tgtHeight) where T : struct
    {
        if (srcWidth != tgtWidth || srcHeight != tgtHeight) return;

        T[] aux = new T[tgtWidth * tgtHeight];
        Copy2D(array, srcWidth, srcHeight, aux, tgtWidth, tgtHeight);
    }

    public static void Copy2D<T>(T[] srcArray, int srcWidth, int srcHeight, T[] tgtArray, int tgtWidth, int tgtHeight) where T : struct
    {
        float minWidth = Mathf.Min(srcWidth, tgtWidth);
        float minHeight = Mathf.Min(srcHeight, tgtHeight);

        for (int i = 0; i < minHeight; ++i)
        {
            int srcRowIndex = i * srcWidth;
            int tgtRowIndex = i * tgtWidth;
            for (int j = 0; j < minWidth; ++j)
            {
                tgtArray[tgtRowIndex + j] = srcArray[srcRowIndex + j];
            }
        }
    }

    public static void CopyNative2D<T>(NativeArray<T> srcArray, int srcWidth, int srcHeight, T[] tgtArray, int tgtWidth, int tgtHeight) where T : struct
    {
        float minWidth = Mathf.Min(srcWidth, tgtWidth);
        float minHeight = Mathf.Min(srcHeight, tgtHeight);

        for (int i = 0; i < minHeight; ++i)
        {
            int srcRowIndex = i * srcWidth;
            int tgtRowIndex = i * tgtWidth;
            for (int j = 0; j < minWidth; ++j)
            {
                tgtArray[tgtRowIndex + j] = srcArray[srcRowIndex + j];
            }
        }
    }
    #endregion
}
