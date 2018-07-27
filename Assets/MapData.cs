using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;

[CreateAssetMenu(fileName = "Map")]
public class MapData : ScriptableObject
{

    [SerializeField] private int width, depth;
    [HideInInspector] [SerializeField] private float[] heights = new float[0];

    public float[] Heights { get { return heights; } }

    public const float sqrt3 = 1.7320508f;
    public const float cos30 = 0.8660254f;
    public Vector2 GridToWorld(int row, int column)
    {
        return new Vector2(column * sqrt3 + (row & 1) * cos30, row * 1.5f);
    }

    public int GridToIndex(int row, int column)
    {
        Vector2Int inBounds = GridPositionInBounds(row, column);
        //Debug.Log(inBounds + " " + row + " " + column);
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
        Vector2 position = GridToWorld(RowInBounds(gridPosition.y), gridPosition.x);
        return new Vector3(position.x, heights[index], position.y);
    }


    Mesh mesh;
    Vector3[] vertices;
    Vector3[] normals;
    // colors, uvs?
    int[] indices;
    
    [ContextMenu("RebuildMesh")]
    public Mesh RefreshMesh()
    {
        return RebuildParallel(1);
    }

    private void OnValidate()
    {
        int targetLength = width * depth;
        if (heights == null || heights.Length != targetLength) heights = new float[targetLength];//TODO properly rescale

    }



    public void ForEachVertex(Action<int, Vector3> handler)
    {
        if (vertices == null) return;
        for (int i = 0; i < vertices.Length; ++i) {
            handler(i, vertices[i]);
        }
    }


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
                hitInfo.triangleIndex = triIndex;//TODO avoid RaycastHit
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

    #region testB
    private class ThreadData {
        public int startIndex, endIndex;
        public RaycastHit hitInfo;
        public ManualResetEvent mre;
    }

    public bool RaycastParallel(Ray ray, out RaycastHit hitInfo, float raycastDistance, int threads)
    {
        hitInfo = new RaycastHit() { distance = raycastDistance };
        if (indices == null || vertices == null) return false;

        int trianglesCount = indices.Length / 3;
        int trianglesPerThread = trianglesCount / threads;
        var threadsData = new List<ThreadData>();
        //var sampler = CustomSampler.Create("ParallelRaycast");
        for (int i = 0; i < threads; ++i)
        {
            var data = new ThreadData()
            {
                startIndex = i * trianglesPerThread,
                endIndex = Mathf.Min((i + 1) * trianglesPerThread, trianglesCount),
                hitInfo = new RaycastHit() { distance = raycastDistance },
                mre = new ManualResetEvent(false)
            };
            ThreadPool.QueueUserWorkItem((d) =>
            {
                ThreadData td = (ThreadData)d;
                Profiler.BeginThreadProfiling("ParallelRaycast", td.startIndex + "-" + td.endIndex);
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
                        td.hitInfo.triangleIndex = triIndex;//TODO avoid RaycastHit
                    }
                    //sampler.End();
                }
                Profiler.EndThreadProfiling();
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

    #region testA
    //public class Worker {
    //    Thread thread;
    //    public RaycastHit hitInfo;
    //    Ray ray;
    //    Vector3[] vertices;
    //    int[] indices;
    //    float raycastDistance;

    //    public void Setup(Ray ray, Vector3[] vertices, int[] indices, float raycastDistance)
    //    {
    //        this.ray = ray;
    //        this.vertices = vertices;
    //        this.indices = indices;
    //        this.raycastDistance = raycastDistance;
    //    }

    //    public void Run(int startIndex, int endIndex)
    //    {
    //        hitInfo = new RaycastHit() { distance = raycastDistance };
    //        if (thread == null) thread = new Thread(() => {
    //            //TODO wait for thread to be unlocked?
    //            for (int i = startIndex; i < endIndex; ++i) {
    //                it(i);
    //            }
    //        });
    //    }

    //    void it(int i)
    //    {
    //        int index = i * 3;
    //        Vector3 v0 = vertices[indices[index]];
    //        Vector3 v1 = vertices[indices[index + 1]];
    //        Vector3 v2 = vertices[indices[index + 2]];
    //        Vector3 e1 = v1 - v0, e2 = v2 - v0;

    //        Vector3 crossE1R = Vector3.Cross(e1, ray.direction);
    //        float projArea = Vector3.Dot(crossE1R, e2);
    //        if (projArea < epsilon && projArea > -epsilon) return;
    //        float invArea = 1 / projArea;

    //        Vector3 offset = ray.origin - v0;
    //        Vector3 crossE2O = Vector3.Cross(e2, offset);

    //        float u = Vector3.Dot(ray.direction, crossE2O) * invArea;
    //        if (u < 0 || u > 1) return;
    //        float v = Vector3.Dot(offset, crossE1R) * invArea;
    //        float uPlusV = u + v;
    //        if (v < 0 || uPlusV > 1) return;

    //        float distance = Vector3.Dot(e1, crossE2O) * invArea;// offset · (e1 x e2) / r · (e1 x e2)
    //        if (distance > epsilon && distance < hitInfo.distance)
    //        {
    //            hitInfo.distance = distance;
    //            hitInfo.point = ray.GetPoint(distance);
    //            hitInfo.barycentricCoordinate = new Vector3(1 - uPlusV, u, v);
    //            //hitInfo.triangleIndex = triIndex;//TODO avoid RaycastHit
    //        }
    //    }
    //}
    //Worker[] workersPool;

    //public bool RaycastParallel(Ray ray, out RaycastHit hitInfo, float raycastDistance)
    //{
    //    hitInfo = new RaycastHit() { distance = raycastDistance };
    //    if (indices == null || vertices == null) return false;

    //    if (workersPool == null) workersPool = new Worker[10];
    //    for (int i = 0; i < workersPool.Length; ++i)
    //    {

    //    }

    //    int trianglesCount = indices.Length / 3;

    //    for (int triIndex = 0; triIndex < trianglesCount; ++triIndex)
    //    {

    //    }

    //    for (int i = 0; i < workersPool.Length; ++i)
    //    {
    //        Worker w = workersPool[i];
    //        if (w.hitInfo.distance < hitInfo.distance) hitInfo = w.hitInfo;
    //    }
    //    return hitInfo;
    //    if (hitInfo.distance >= raycastDistance) return false;
    //    return true;
    //}
    #endregion
        
    

    private class ThreadData<T>
    {
        public int startIndex, endIndex;
        public T[] array;
        public ManualResetEvent mre;
    }
    private class ThreadData<T, U>
    {
        public int startIndex, endIndex;
        public T[] array0;
        public U[] array1;
        public ManualResetEvent mre;
    }

    public Mesh RebuildParallel(int threads)
    {
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = this.name;
            mesh.hideFlags = HideFlags.HideAndDontSave;
        }
        else
        {
            mesh.Clear();
        }

        int verticesLength = width * depth;
        int indicesLength = (width - 1) * (depth - 1) * 6;
        
        if (vertices == null || vertices.Length != verticesLength) vertices = new Vector3[verticesLength];
        if (normals == null || normals.Length != verticesLength) normals = new Vector3[verticesLength];
        
        if (indices == null || indices.Length != indicesLength) indices = new int[indicesLength];

        // Fill arrays
        int verticesPerThread = verticesLength / threads;
        var threadsData = new List<ThreadData<Vector3, Vector3>>();
        //var sampler = CustomSampler.Create("ParallelRaycast");
        for (int i = 0; i < threads; ++i)
        {
            var data = new ThreadData<Vector3, Vector3>()
            {
                startIndex = i * verticesPerThread,
                endIndex = Mathf.Min((i + 1) * verticesPerThread, verticesLength),
                array0 = vertices,
                array1 = normals,
                mre = new ManualResetEvent(false)
            };
            ThreadPool.QueueUserWorkItem((d) =>
            {
                var td = (ThreadData<Vector3, Vector3>)d;
                for (int vertexIndex = td.startIndex; vertexIndex < td.endIndex; ++vertexIndex)
                {
                    vertices[vertexIndex] = GetPosition(vertexIndex);
                    normals[vertexIndex] = Vector3.zero;// Vector3.up;
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

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = indices;

        return mesh;
    }

    public void QuickRebuildParallel(int threads)
    {
        if (mesh == null) return;

        int verticesLength = width * depth;

        if (vertices == null || vertices.Length != verticesLength) vertices = new Vector3[verticesLength];
        
        // Fill arrays
        int verticesPerThread = verticesLength / threads;
        var threadsData = new List<ThreadData<Vector3>>();
        //var sampler = CustomSampler.Create("ParallelRaycast");
        for (int i = 0; i < threads; ++i)
        {
            var data = new ThreadData<Vector3>()
            {
                startIndex = i * verticesPerThread,
                endIndex = Mathf.Min((i + 1) * verticesPerThread, verticesLength),
                array = vertices,
                mre = new ManualResetEvent(false)
            };
            ThreadPool.QueueUserWorkItem((d) =>
            {
                var td = (ThreadData<Vector3>)d;
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

        mesh.vertices = vertices;
    }
}
