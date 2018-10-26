using System.Collections.Generic;
using System.Threading;
using UnityEngine;

[System.Serializable]
public partial class MapData : ScriptableObject
{
    [System.Serializable]
    public struct PropInstance
    {
        public Vector3 position;//Y is a offset from terrain height
        public Vector3 direction;//Length scales Y
        public float rotation;//Around Y
        public float size;//All axis, <0 to prepare for deletion?

        //[System.NonSerialized] public float sqrtDist;//Alt: use this field for deletion if <0
    }

    [System.Serializable]
    public class InstanceSet
    {
        ///*[HideInInspector]*/ [SerializeField] private List<PropInstance> instances = new List<PropInstance>();
        //public List<PropInstance> Instances { get { return instances; } }
        /*[HideInInspector]*/ [SerializeField] private PropInstance[] instances = new PropInstance[0];
        /*[HideInInspector]*/ [SerializeField]private int count;
        public PropInstance[] Instances { get { return instances; } }
        public int Count
        {
            get { return count; }
            set {
                EnsureCapacity(value);
                count = value;
            }
        }
        public void EnsureCapacity(int capacity)
        {
            if (capacity > instances.Length) Resize(capacity);
        }
        public void Minimize()
        {
            Resize(count);
        }
        public void Resize(int newSize)
        {
            if (instances.Length != newSize) System.Array.Resize(ref instances, newSize);
        }
        public void RemoveMarked()//TODO test !!!!!
        {
            int index = 0;
            while (index < count && instances[index].size >= 0) ++index;
            for (int i = index + 1; i < count; ++i)
            {
                PropInstance inst = instances[i];
                if (inst.size >= 0) instances[index++] = inst;
            }
            count = index;
        }

        [System.NonSerialized] public Vector3[] instancePositions = new Vector3[0];//TODO mind multithreading
        [System.NonSerialized] public float[] instanceSqrDistances = new float[0];// Alt: use this array as flags for deletion if <0
    }
    
    public int instanceLimit = 100;//Limit for the sum of meshdata.instances.count's
    public InstanceSet[] instanceSets = new InstanceSet[0];
    public MeshData[] meshesData = new MeshData[0];//TODO wrap in more config?
                                                   //

    //[SerializeField] private Vector3[] instancePositions = new Vector3[0];
    //[SerializeField] private Vector3[] instanceDirections = new Vector3[0];
    //[SerializeField] private float[] instanceRotations = new float[0];
    //[SerializeField] private float[] instanceSizes = new float[0];



    public void RecalculateInstancePositions(int threads, InstanceSet instanceSet, MapData mapData)
    {
        PropInstance[] instances = instanceSet.Instances;
        int instanceCount = instanceSet.Count;
        if (instanceSet.instancePositions == null || instanceSet.instancePositions.Length != instanceCount) instanceSet.instancePositions = new Vector3[instanceCount];

        for (int j = 0; j < instanceCount; ++j)
        {
            PropInstance instance = instances[j];

            Vector3 relativePosition = instance.position;
            float height = mapData.SampleHeight(relativePosition.x, relativePosition.z);
            Vector3 realPosition = new Vector3(relativePosition.x, relativePosition.y + height, relativePosition.z);

            instanceSet.instancePositions[j] = realPosition;
        }
    }

    public void RecalculateInstanceDistances(int threads, InstanceSet instanceSet, Vector3 pov = default(Vector3), float lodScale = 1f)
    {
        PropInstance[] instances = instanceSet.Instances;
        int instanceCount = instanceSet.Count;
        if (instanceSet.instanceSqrDistances == null || instanceSet.instanceSqrDistances.Length != instanceCount) instanceSet.instanceSqrDistances = new float[instanceCount];

        for (int j = 0; j < instanceCount; ++j)
        {
            PropInstance instance = instances[j];

            float sqrDist = Vector3.SqrMagnitude(instanceSet.instancePositions[j] - pov);

            instanceSet.instanceSqrDistances[j] = sqrDist * lodScale;
        }
    }

    [ContextMenu("RebuildPropsMeshes")]
    public void RefreshPropMeshes()
    {
        RefreshPropMeshes(default(Vector3), 1f);
    }
    
    public void RefreshPropMeshes(Vector3 pov, float lodScale)
    {
        for (int i = 0; i < instanceSets.Length; ++i)
        {
            RecalculateInstancePositions(1, instanceSets[i], this);
            RecalculateInstanceDistances(1, instanceSets[i], pov, lodScale);
        }
        for (int i = 0; i < meshesData.Length; ++i)
        {
            meshesData[i].RebuildParallel(1, this);//TODO Y?
        }
    }

    /// <summary>
    /// Unlike RefreshPropMeshes, this one runs in background!
    /// </summary>
    /// <param name="pov"></param>
    /// <param name="lodScale"></param>
    public void BkgRefreshDensityPropMeshes(Vector3 pov, float lodScale)
    {
        for (int i = 0; i < densityPropsMeshData.Length; ++i)
        {
            densityPropsMeshData[i].CheckDensityPropsUpdate(pov, lodScale, this);
        }
    }

    [System.Serializable]
    public class MeshData
    {
        [System.Serializable]
        public class Variant
        {
            public MeshLOD[] meshLODs = new MeshLOD[1];
            public int instanceSetIndex;
        }

        [System.Serializable]
        public class MeshLOD
        {
            public Mesh mesh;
            public int subMeshIndex;
            public float maxSqrDistance;//TODO minDist, transition, billboarding?
        }

        public Variant[] variants = new Variant[0];
        public int verticesLengthLimit = 900;
        public int trianglesLengthLimit = 900;//indices actually

        public Material sharedMaterial { get { return material; } }
        [SerializeField] Material material;

        public Mesh sharedMesh { get { return mesh; } }

        Mesh mesh;
        List<Vector3> vertices;
        List<Vector3> normals;
        List<Vector2> uvs;
        List<int> triangles;
        //Vector3[] vertices;
        //Vector3[] normals;
        //// TODO colors?
        //Vector2[] uvs;
        //int[] indices;

        List<Vector3> auxVerticesList = new List<Vector3>();
        List<Vector3> auxNormalsList = new List<Vector3>();
        List<Vector2> auxUVsList = new List<Vector2>();
        List<int> auxTrianglesList = new List<int>();

        private class ThreadData
        {
            public int startIndex, endIndex;
            public ManualResetEvent mre;
        }

        public Mesh RebuildParallel(int threads, MapData mapData)
        {
            //TODO instanceSets should be already done at this point, move pov and lodScale earlier in callstack and remove here
            if (mesh == null)
            {
                mesh = new Mesh();
                //mesh.name = this.name;//TODO move naming elsewhere here and in mapdata
                mesh.hideFlags = HideFlags.HideAndDontSave;
            }
            else
            {
                mesh.Clear();
            }

            //TODO use native pointer to speed up copying?
            //TODO optimize lists capacity on creation & ensure capacity if existing
            if (vertices == null/* || vertices.Length != verticesLength*/) vertices = new List<Vector3>();
            //else { vertices.Clear(); if (vertices.Capacity < verticesLength) vertices.Capacity = verticesLength; }
            if (normals == null/* || normals.Length != verticesLength*/) normals = new List<Vector3>();
            if (uvs == null/* || uvs.Length != verticesLength*/) uvs = new List<Vector2>();

            if (triangles == null/* || indices.Length != indicesLength*/) triangles = new List<int>();

            //TODO split instances between threads? later!

            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            triangles.Clear();
            int vertexIndex = 0;
            int indexIndex = 0;

            int lod = 0;
            bool existingLOD;
            do
            {
                existingLOD = false;
                for (int v = 0; v < variants.Length; ++v)
                {
                    Variant variant = variants[v];
                    if (lod < variant.meshLODs.Length)
                    {
                        if (variant.instanceSetIndex < 0 || variant.instanceSetIndex >= mapData.instanceSets.Length) continue;
                        InstanceSet instanceSet = mapData.instanceSets[variant.instanceSetIndex];

                        DoVariantLOD(instanceSet, variant, lod, ref vertexIndex, ref indexIndex);
                        existingLOD = true;
                    }
                }
                lod++;
            } while (existingLOD);

            ////////////////
            /*
            for (int i = 0; i < meshesData.Length; ++i)
            {
                MeshData meshData = meshesData[i];

                List<PropInstance> instances = meshData.Instances;
                int instanceCount = instances.Count;
                if (meshData.instanceSqrDistances == null || meshData.instanceSqrDistances.Length != instanceCount) meshData.instanceSqrDistances = new float[instanceCount];
                float[] sqrDistances = meshData.instanceSqrDistances;
                //TODO calculate positions if needed?
                Vector3[] realPositions = meshData.instancePositions;//TODO mind multithreading

                if (meshData.meshLODs.Length >= 1)
                {
                    MeshLOD meshLOD0 = meshData.meshLODs[0];
                    int verticesInMesh = meshLOD0.mesh.vertexCount;
                    int indicesInMesh = 0;
                    int subMeshCount = meshLOD0.mesh.subMeshCount;
                    for (int sm = 0; sm < subMeshCount; ++sm) indicesInMesh += (int)meshLOD0.mesh.GetIndexCount(sm);

                    meshLOD0.mesh.GetVertices(auxVerticesList);
                    meshLOD0.mesh.GetNormals(auxNormalsList);
                    meshLOD0.mesh.GetUVs(0, auxUVsList);

                    //meshLOD0.mesh.GetTriangles()
                    meshLOD0.mesh.GetIndices(auxTrianglesList, subMesh);//TODO have many indices arrays / lists

                    for (int j = 0; j < instanceCount; ++j)
                    {
                        PropInstance instance = instances[j];
                        //TODO break if no space for more!!!!!!!

                        //instance.realPosition = //TODO get realPosition from instance.position
                        //TODO realPosition should be calculated in the beginning!
                        float sqrDistance = sqrDistances[j] = Vector3.SqrMagnitude(pov - realPositions[j]);

                        if (sqrDistance < meshLOD0.maxSqrDistance)//Allow LOD dist = 0?
                        {
                            //TODO write mesh to arrays, get mesh data in lists for efficiency
                            auxVerticesList.CopyTo(vertices, currentVertexIndex);
                            auxNormalsList.CopyTo(normals, currentVertexIndex);
                            auxUVsList.CopyTo(uvs, currentVertexIndex);

                            //TODO indices crazyness
                            currentVertexIndex += auxVertexList.Count;
                            currentIndexIndex += ???
                        }

                        instances[j] = instance;
                        //TODO break if no space for more!!!!!!!
                    }
                }
                for (int lod = 1; lod < meshData.meshLODs.Length; ++lod)
                {
                    MeshLOD meshLOD = meshData.meshLODs[lod];
                    float previousLODSqrDist = meshData.meshLODs[lod - 1].maxSqrDistance;

                    int verticesInMesh = meshLOD.mesh.vertexCount;
                    int indicesInMesh = 0;
                    int subMeshCount = meshLOD.mesh.subMeshCount;
                    for (int sm = 0; sm < subMeshCount; ++sm) indicesInMesh += (int)meshLOD.mesh.GetIndexCount(sm);

                    for (int j = 0; j < instanceCount; ++j)
                    {
                        PropInstance instance = instances[j];

                        float sqrDistance = sqrDistances[j];

                        if (sqrDistance >= previousLODSqrDist && sqrDistance < meshLOD.maxSqrDistance)
                        {
                            //TODO write mesh to arrays
                        }
                        //TODO break if no space for more!
                    }
                    //TODO break if no space for more!
                }
            }
            */
            /////////////
            /*

            int verticesLength = width * depth;//TODO count and limit!
            int indicesLength = (width - 1) * (depth - 1) * 6;//TODO count and limit!

            if (vertices == null || vertices.Length != verticesLength) vertices = new Vector3[verticesLength];
            if (normals == null || normals.Length != verticesLength) normals = new Vector3[verticesLength];
            if (uvs == null || uvs.Length != verticesLength) uvs = new Vector2[verticesLength];

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
                    //for (int vertexIndex = td.startIndex; vertexIndex < td.endIndex; ++vertexIndex)
                    //{
                    //    vertices[vertexIndex] = GetPosition(vertexIndex);
                    //    uvs[vertexIndex] = new Vector2(vertices[vertexIndex].x, vertices[vertexIndex].z);//TODO normalize?
                    //    normals[vertexIndex] = Vector3.zero;// Vector3.up;
                    //}
                    td.mre.Set();
                }, data);
                threadsData.Add(data);
            }
            foreach (var data in threadsData)
            {
                data.mre.WaitOne();
            }
            */
            //int indexIndex = 0;
            //for (int r = 1; r < depth; ++r)
            //{
            //    for (int c = 1; c < width; ++c)
            //    {
            //        int r_1 = r - 1;
            //        int c_1 = c - 1;
            //        int baseIndex = GridToIndex(r_1, c_1);
            //        int oddRow = r_1 & 1;

            //        indices[indexIndex++] = baseIndex;
            //        indices[indexIndex++] = baseIndex + width;
            //        indices[indexIndex++] = baseIndex + 1 + width * oddRow;
            //        indices[indexIndex++] = baseIndex + width * (1 - oddRow);
            //        indices[indexIndex++] = baseIndex + 1 + width;
            //        indices[indexIndex++] = baseIndex + 1;
            //    }
            //}

            //for (indexIndex = 0; indexIndex < indicesLength; ++indexIndex)
            //{
            //    int i0 = indices[indexIndex];
            //    int i1 = indices[++indexIndex];
            //    int i2 = indices[++indexIndex];

            //    Vector3 v0 = vertices[i0], v1 = vertices[i1], v2 = vertices[i2];
            //    Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            //    normals[i0] += normal;
            //    normals[i1] += normal;
            //    normals[i2] += normal;
            //}
            //for (int vertexIndex = 0; vertexIndex < verticesLength; ++vertexIndex)
            //{
            //    normals[vertexIndex] = normals[vertexIndex].normalized;
            //}

            mesh.MarkDynamic();

            mesh.SetVertices(vertices);//mesh.vertices = vertices;
            mesh.SetNormals(normals);//mesh.normals = normals;
            mesh.SetUVs(0, uvs);//mesh.uv = uvs;
            //mesh.colors = colors;
            mesh.SetTriangles(triangles, 0);//mesh.triangles = indices; //TODO extra parameters?

            return mesh;
        }

        readonly Vector3 vectorUp = new Vector3(0, 1, 0);
        public void DoVariantLOD(InstanceSet instanceSet, Variant variant, int lod, ref int vertexIndex, ref int indexIndex)
        {
            MeshLOD meshLOD = variant.meshLODs[lod];

            PropInstance[] instances = instanceSet.Instances;
            int instanceCount = instanceSet.Count;
            float[] sqrDistances = instanceSet.instanceSqrDistances;//Precalculated
            Vector3[] realPositions = instanceSet.instancePositions;//TODO mind multithreading

            if (meshLOD.subMeshIndex < 0 || meshLOD.subMeshIndex >= meshLOD.mesh.subMeshCount) return;
            int verticesInMesh = meshLOD.mesh.vertexCount;
            int indicesInMesh = (int)meshLOD.mesh.GetIndexCount(meshLOD.subMeshIndex);

            meshLOD.mesh.GetVertices(auxVerticesList);
            meshLOD.mesh.GetNormals(auxNormalsList);
            meshLOD.mesh.GetUVs(0, auxUVsList);

            meshLOD.mesh.GetTriangles(auxTrianglesList, meshLOD.subMeshIndex);
            //meshLOD.mesh.GetIndices(auxIndicesList, subMesh);//TODO have many indices arrays / lists

            for (int j = 0; j < instanceCount; ++j)
            {
                PropInstance instance = instances[j];
                //TODO break if no space for more!!!!!!!
                if (vertexIndex + verticesInMesh > verticesLengthLimit) break;
                if (indexIndex + indicesInMesh > trianglesLengthLimit) break;

                //instance.realPosition = //TODO get realPosition from instance.position
                //TODO realPosition should be calculated in the beginning!
                Vector3 realPosition = realPositions[j];
                float sqrDistance = sqrDistances[j];
                //Debug.Log(sqrDistance + " <? " + meshLOD.maxSqrDistance);
                if (sqrDistance < meshLOD.maxSqrDistance)//TODO add complexity, allow LOD dist = 0?
                {
                    Matrix4x4 matrix = Matrix4x4.TRS(realPosition, Quaternion.FromToRotation(vectorUp, instance.direction) * Quaternion.Euler(0, instance.rotation, 0), new Vector3(instance.size, instance.size, instance.size));//TODO Optimize?
                    //TODO write mesh to arrays, get mesh data in lists for efficiency
                    //auxVerticesList.CopyTo(vertices, vertexIndex);
                    for (int i = 0; i < verticesInMesh; ++i)
                    {
                        Vector3 vertex = matrix.MultiplyPoint3x4(auxVerticesList[i]);
                        vertices.Add(vertex);
                    }
                    //auxNormalsList.CopyTo(normals, vertexIndex);
                    for (int i = 0; i < verticesInMesh; ++i)
                    {
                        Vector3 normal = matrix.MultiplyVector(auxNormalsList[i]);
                        normals.Add(normal);//TODO normalize?
                    }
                    //auxUVsList.CopyTo(uvs, vertexIndex);
                    for (int i = 0; i < verticesInMesh; ++i)
                    {
                        Vector2 uv = auxUVsList[i];//TODO atlas?
                        uvs.Add(uv);
                    }

                    //Debug.Log(vertexIndex + " " + indexIndex + " " + verticesInMesh + " " + indicesInMesh + " " + vertices.Count + " " + triangles.Count);
                    //auxTrianglesList.CopyTo(triangles, indexIndex);
                    for (int i = 0; i < indicesInMesh; ++i)
                    {
                        int index = vertexIndex + auxTrianglesList[i];
                        triangles.Add(index);
                    }

                    vertexIndex += verticesInMesh;
                    indexIndex += indicesInMesh;
                }

                //TODO break if no space for more!!!!!!!
            }

        }
    }

    ////////////////////////////// Density Props

    public DensityPropsMeshData[] densityPropsMeshData = new DensityPropsMeshData[0];

    [System.Serializable]
    public class DensityPropsMeshData
    {
        const int maxCellsFromCenter = 12;

        [SerializeField]
        private PropDitherPattern pattern;
        [SerializeField]
        private float patternScale = 1f;

        private enum UpdateState { Idle, Updating, Ready }
        private Vector3 currPOV;
        private float redrawThreshold = 5f;//TODO serialize?
        private UpdateState currentUpdateState = UpdateState.Idle;

        //TODO variants, with probability, decided by pattern rand
        
        public Variant[] variants = new Variant[1];
        public float[] densityMap;

        [System.Serializable]
        public class Variant//TODO move data to a different scriptable object?
        {
            public MeshLOD[] meshLODs = new MeshLOD[1];
            public float probability;

            public float propsScale = 1f;
            public Vector3 propsDirection = Vector3.up;

            public float minAlignment = 0.2f, maxAlignment = 0.5f;//TODO better inspector?
            public float minRotation = -180f, maxRotation = 180f;
            public float minYOffset = 0f, maxYOffset = 0f;
        }

        [System.Serializable]
        public class MeshLOD
        {
            public Mesh mesh;
            public int subMeshIndex;
            public float maxSqrDistance;//TODO minDist, transition, billboarding?
            public float minSqrDistance;
            //TODO im probably using lod distances wrong in the other class, since they can go to different meshes?
            //Combine Instance Sets with density maps in the same mesh?
            public int targetSubMesh;//TODO store lods in submeshes if they require a different material?

            [System.NonSerialized] public int verticesCount;
            [System.NonSerialized] public int indicesCount;

            [System.NonSerialized] public List<Vector3> verticesList = new List<Vector3>();
            [System.NonSerialized] public List<Vector3> normalsList = new List<Vector3>();
            [System.NonSerialized] public List<Vector2> uvsList = new List<Vector2>();
            [System.NonSerialized] public List<int> trianglesList = new List<int>();

            //TODO when to load
            public void LoadMeshLists()
            {
                if (subMeshIndex < 0 || subMeshIndex >= mesh.subMeshCount)
                {
                    verticesCount = indicesCount = 0;//Fail load
                    return;
                }
                verticesCount = mesh.vertexCount;
                indicesCount = (int)mesh.GetIndexCount(subMeshIndex);

                mesh.GetVertices(verticesList);
                mesh.GetNormals(normalsList);
                mesh.GetUVs(0, uvsList);

                mesh.GetTriangles(trianglesList, subMeshIndex);
                //mesh.GetIndices(indicesList, subMesh);//TODO have many indices arrays / lists
            }

            public bool MeshListsLoaded() { return verticesCount > 0 && indicesCount > 0; }

            public void FreeMeshLists() // Just in case
            {
                verticesList = new List<Vector3>();
                normalsList = new List<Vector3>();
                uvsList = new List<Vector2>();
                trianglesList = new List<int>();
            }
        }

        public float SampleDensity(float x, float y, MapData mapData)
        {
            Vector3Int indices;
            Vector3 barycentricCoordinate;
            if (mapData.SampleInfo(x, y, out indices, out barycentricCoordinate))
            {
                return densityMap[indices.x] * barycentricCoordinate.x +
                    densityMap[indices.y] * barycentricCoordinate.y +
                    densityMap[indices.z] * barycentricCoordinate.z;
            }
            else
            {
                return 0f;
            }
        }

        public int verticesLengthLimit = 900;
        public int trianglesLengthLimit = 900;//indices actually

        public Material[] sharedMaterials { get { return materials; } }
        [SerializeField] Material[] materials;

        public Mesh sharedMesh { get { return mesh; } }

        Mesh mesh;
        List<Vector3> vertices;
        List<Vector3> normals;
        List<Vector2> uvs;
        List<int>[] triangleLists;

        MapData mapData;
        Vector3 pov;
        float lodScale = 1f;

        public void CheckDensityPropsUpdate(Vector3 pov, float lodScale, MapData mapData)
        {
            if (currentUpdateState != UpdateState.Idle) Debug.Log(currentUpdateState);

            if (currentUpdateState == UpdateState.Ready)
            {
                //Vertices/Indices ready, build mesh
                UpdateMesh();

                currentUpdateState = UpdateState.Idle;
            }

            if (currentUpdateState == UpdateState.Idle)
            {
                if (Vector3.SqrMagnitude(pov - currPOV) > redrawThreshold * redrawThreshold)
                {
                    //Begin Vertices/Indices update
                    this.mapData = mapData;
                    this.pov = pov;
                    this.lodScale = lodScale;
                    LoadMeshLists();
                    BeginDensityPropsUpdate();

                    currPOV = pov;
                    currentUpdateState = UpdateState.Updating;
                }
            }
        }

        bool shouldThreadsRun = false;
        public void StopThread()
        {
            Debug.LogWarning("Stop Thread");
            shouldThreadsRun = true;
            mre.Set();
            currentUpdateState = UpdateState.Idle;
        }

        Thread rebuildThread;
        ManualResetEvent mre = new ManualResetEvent(false);

        private void BeginDensityPropsUpdate()
        {
            if (rebuildThread == null) {
                DensityPropsMeshData thisDPMeshData = this;
                rebuildThread = new Thread(delegate ()
                {
                    while (shouldThreadsRun)
                    {
                        DensityPropsUpdate();
                        currentUpdateState = UpdateState.Ready;

                        if (!shouldThreadsRun) break;

                        mre.Reset();
                        mre.WaitOne();//Is this safe? can it exit? TODO call Set after setting shouldThreadsRun to false
                    }

                    rebuildThread = null;//Allows the creation of newer threads if requested
                    Debug.LogWarning("Stopped Thread");
                });
                rebuildThread.IsBackground = true;//End when main thread ends
                shouldThreadsRun = true;
                rebuildThread.Start();
                Debug.LogWarning("Started Thread");
            }

            mre.Set();
        }

        void LoadMeshLists()//TODO meshes should refresh on validate
        {
            for (var v = 0; v < variants.Length; ++v)
            {
                MeshLOD[] meshLODs = variants[v].meshLODs;
                for (int lod = 0; lod < meshLODs.Length; ++lod)
                {
                    if (!meshLODs[lod].MeshListsLoaded()) meshLODs[lod].LoadMeshLists();
                }
            }
        }

        void FreeMeshLists()//TODO meshes should refresh on validate, test
        {
            for (var v = 0; v < variants.Length; ++v)
            {
                MeshLOD[] meshLODs = variants[v].meshLODs;
                for (int lod = 0; lod < meshLODs.Length; ++lod)
                {
                    if (meshLODs[lod].MeshListsLoaded()) meshLODs[lod].FreeMeshLists();
                }
            }
        }

        void DensityPropsUpdate()
        {
            //TODO ensure threads don't accumulate somehow
            //TODO measure times

            if (vertices == null) vertices = new List<Vector3>();
            if (normals == null) normals = new List<Vector3>();
            if (uvs == null) uvs = new List<Vector2>();
            
            int subMeshCount = materials.Length;

            Debug.Log("Hi why is this null?");//TODO
            if (triangleLists == null || triangleLists.Length != subMeshCount) {
                triangleLists = new List<int>[subMeshCount];
                for (int i = 0; i < subMeshCount; ++i) triangleLists[i] = new List<int>();
            }

            // Clear lists
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            for (int i = 0; i < subMeshCount; ++i) triangleLists[i].Clear();

            int vertexIndex = 0;
            int indexIndex = 0;

            if (pattern == null) return;
            PropDitherPattern.PatternElement[] elements = pattern.elements;
            
            int povCellX = Mathf.RoundToInt(pov.x / patternScale);
            int povCellY = Mathf.RoundToInt(pov.z / patternScale);
            
            DoPatternCell(povCellX, povCellY, ref vertexIndex, ref indexIndex);
            for (int offset = 1; offset <= maxCellsFromCenter; ++offset)
            {
                //int sideLength = offset * 2;
                for (int offset2 = 1 - offset; offset2 <= offset; ++offset2)
                {
                    DoPatternCell(povCellX + offset, povCellY + offset2, ref vertexIndex, ref indexIndex);
                    DoPatternCell(povCellX - offset2, povCellY + offset, ref vertexIndex, ref indexIndex);
                    DoPatternCell(povCellX - offset, povCellY - offset2, ref vertexIndex, ref indexIndex);
                    DoPatternCell(povCellX + offset2, povCellY - offset, ref vertexIndex, ref indexIndex);
                }
                //TODO break if no lods? might be too complex to detect
            }

            //int prevOffset = 0;
            //for (int lod = 0; lod < meshLODs.Length; ++lod)//prioritize first lods
            //{
            //    MeshLOD meshLOD = meshLODs[lod];
            //    if (!meshLOD.MeshListsLoaded()) continue;

            //    int povCellX = Mathf.RoundToInt(pov.x / patternScale);
            //    int povCellY = Mathf.RoundToInt(pov.z / patternScale);
            //    int maxOffset = Mathf.Min(maxCellsFromCenter, Mathf.RoundToInt(Mathf.Sqrt(meshLOD.maxSqrDistance) / patternScale));
            //    int minOffset = prevOffset;

            //    //if (lod == 0) Debug.Log(povCellX + "," + povCellY + "+-" + maxOffset);

            //    int minCellX = povCellX - maxOffset, maxCellX = povCellX + maxOffset;
            //    int minCellY = povCellY - maxOffset, maxCellY = povCellY + maxOffset;

            //    for (int x = minCellX; x <= maxCellX; ++x)
            //    {
            //        for (int y = minCellY; y <= maxCellY; ++y)
            //        {
            //            //TODO check actual lod distances here, or by element?
            //            for (int e = 0; e < elements.Length; ++e)
            //            {
            //                if (vertexIndex + meshLOD.verticesCount > verticesLengthLimit) break;
            //                if (indexIndex + meshLOD.indicesCount > trianglesLengthLimit) break;

            //                PropDitherPattern.PatternElement element = elements[e];

            //                Vector2 elemPosition = new Vector2(x, y) * patternScale + element.pos * patternLocal2WorldScale;
            //                Vector3 pos3D = new Vector3(elemPosition.x, minYOffset + element.rand2 * (maxYOffset - minYOffset), elemPosition.y);

            //                float density = SampleDensity(pos3D.x, pos3D.z, mapData);
            //                if (density <= (e + 1) || (pos3D - pov).sqrMagnitude > meshLOD.maxSqrDistance) continue;

            //                Vector3 terrainNormal = mapData.SampleNormals(pos3D.x, pos3D.z);


            //                PropInstance instance = new PropInstance()
            //                {
            //                    position = pos3D,
            //                    direction = Vector3.Slerp(propsDirection, terrainNormal, minAlignment + element.rand0 * (maxAlignment - minAlignment)),
            //                    rotation = minRotation + element.rand1 * (maxRotation - minRotation),//TODO billboard?
            //                    size = element.r * propsScale
            //                    //TODO variant (float 0..1)
            //                };

            //                if (e == 0) Debug.Log(instance.rotation);

            //                float height = mapData.SampleHeight(pos3D.x, pos3D.z);
            //                Vector3 realPosition = new Vector3(pos3D.x, height + pos3D.y, pos3D.z);

            //                Matrix4x4 matrix = Matrix4x4.TRS(realPosition, Quaternion.FromToRotation(Vector3.up, instance.direction) * Quaternion.Euler(0, instance.rotation, 0), new Vector3(instance.size, instance.size, instance.size));//TODO Optimize?
            //                for (int i = 0; i < meshLOD.verticesCount; ++i)
            //                {
            //                    Vector3 vertex = matrix.MultiplyPoint3x4(meshLOD.verticesList[i]);
            //                    vertices.Add(vertex);
            //                }
            //                for (int i = 0; i < meshLOD.verticesCount; ++i)
            //                {
            //                    Vector3 normal = matrix.MultiplyVector(meshLOD.normalsList[i]);
            //                    normals.Add(normal);//TODO normalize?
            //                }
            //                for (int i = 0; i < meshLOD.verticesCount; ++i)
            //                {
            //                    Vector2 uv = meshLOD.uvsList[i];
            //                    uvs.Add(uv);
            //                }

            //                if (meshLOD.targetSubMesh >= 0 && meshLOD.targetSubMesh < subMeshCount) {
            //                    List<int> triangles = triangleLists[meshLOD.targetSubMesh];
            //                    for (int i = 0; i < meshLOD.indicesCount; ++i)
            //                    {
            //                        int index = vertexIndex + meshLOD.trianglesList[i];
            //                        triangles.Add(index);
            //                    }
            //                }

            //                vertexIndex += meshLOD.verticesCount;
            //                indexIndex += meshLOD.indicesCount;//TODO Split by submesh?
            //            }
            //        }
            //    }


            //    prevOffset = maxOffset;
            //}

        }

        Variant SelectVariant(float rand)
        {
            float probSum = 0;
            for (int v = 0; v < variants.Length; ++v) probSum += variants[v].probability;
            rand *= probSum;
            float probAccum = 0;
            for (int v = 0; v < variants.Length; ++v)
            {
                probAccum += variants[v].probability;
                if (rand <= probAccum) return variants[v];//TODO test
            }
            return null;
        }

        void DoPatternCell(int x, int y, ref int vertexIndex, ref int indexIndex)
        {
            float patternLocal2WorldScale = patternScale / PropDitherPattern.CellSize;
            int subMeshCount = materials.Length;
            PropDitherPattern.PatternElement[] elements = pattern.elements;
            for (int e = 0; e < elements.Length; ++e)
            {
                PropDitherPattern.PatternElement element = elements[e];

                float variantRand = element.rand3;
                Variant variant = SelectVariant(variantRand);
                if (variant == null) return;

                for (int lod = 0; lod < variant.meshLODs.Length; ++lod)
                {
                    MeshLOD meshLOD = variant.meshLODs[lod];
                    if (!meshLOD.MeshListsLoaded()) continue;

                    if (vertexIndex + meshLOD.verticesCount > verticesLengthLimit) break;
                    if (indexIndex + meshLOD.indicesCount > trianglesLengthLimit) break;
                    
                    Vector2 elemPosition = new Vector2(x, y) * patternScale + element.pos * patternLocal2WorldScale;
                    Vector3 pos3D = new Vector3(elemPosition.x, variant.minYOffset + element.rand2 * (variant.maxYOffset - variant.minYOffset), elemPosition.y);

                    float density = SampleDensity(pos3D.x, pos3D.z, mapData);
                    float sqrDist = (pos3D - pov).sqrMagnitude;
                    if (density <= (e + 1) || sqrDist > meshLOD.maxSqrDistance || sqrDist < meshLOD.minSqrDistance) continue;

                    Vector3 terrainNormal = mapData.SampleNormals(pos3D.x, pos3D.z);


                    PropInstance instance = new PropInstance()
                    {
                        position = pos3D,
                        direction = Vector3.Slerp(variant.propsDirection, terrainNormal, variant.minAlignment + element.rand0 * (variant.maxAlignment - variant.minAlignment)),
                        rotation = variant.minRotation + element.rand1 * (variant.maxRotation - variant.minRotation),//TODO billboard?
                        size = element.r * variant.propsScale
                        //TODO variant? (float 0..1)
                    };

                    float height = mapData.SampleHeight(pos3D.x, pos3D.z);
                    Vector3 realPosition = new Vector3(pos3D.x, height + pos3D.y, pos3D.z);

                    Matrix4x4 matrix = Matrix4x4.TRS(realPosition, Quaternion.FromToRotation(Vector3.up, instance.direction) * Quaternion.Euler(0, instance.rotation, 0), new Vector3(instance.size, instance.size, instance.size));//TODO Optimize?
                    for (int i = 0; i < meshLOD.verticesCount; ++i)
                    {
                        Vector3 vertex = matrix.MultiplyPoint3x4(meshLOD.verticesList[i]);
                        vertices.Add(vertex);
                    }
                    for (int i = 0; i < meshLOD.verticesCount; ++i)
                    {
                        Vector3 normal = matrix.MultiplyVector(meshLOD.normalsList[i]);
                        normals.Add(normal);//TODO normalize?
                    }
                    for (int i = 0; i < meshLOD.verticesCount; ++i)
                    {
                        Vector2 uv = meshLOD.uvsList[i];
                        uvs.Add(uv);
                    }

                    if (e < 10 && vertexIndex < 300) Debug.Log(meshLOD.targetSubMesh + " asdffasd fas ");
                    if (meshLOD.targetSubMesh >= 0 && meshLOD.targetSubMesh < subMeshCount)
                    {
                        List<int> triangles = triangleLists[meshLOD.targetSubMesh];
                        for (int i = 0; i < meshLOD.indicesCount; ++i)
                        {
                            int index = vertexIndex + meshLOD.trianglesList[i];
                            triangles.Add(index);
                        }
                    }

                    vertexIndex += meshLOD.verticesCount;
                    indexIndex += meshLOD.indicesCount;//TODO Split by submesh?
                }
            }
        }

        void UpdateMesh()
        {
            if (mesh == null)
            {
                mesh = new Mesh();
                //mesh.name = this.name;//TODO move naming elsewhere here and in mapdata
                mesh.hideFlags = HideFlags.HideAndDontSave;
            }
            else
            {
                mesh.Clear();
            }

            //Get vertices/indices and build mesh
            mesh.MarkDynamic();

            mesh.SetVertices(vertices);//mesh.vertices = vertices;
            mesh.SetNormals(normals);//mesh.normals = normals;
            mesh.SetUVs(0, uvs);//mesh.uv = uvs;
            //mesh.colors = colors;
            Debug.Log("WTF: "+triangleLists);//TODO avoid null exception
            mesh.subMeshCount = triangleLists.Length;
            for (int sm = 0; sm < triangleLists.Length; ++sm)
            {
                mesh.SetTriangles(triangleLists[sm], sm, false);//mesh.triangles = indices; //TODO extra parameters? calculate bounds later?
            }
            //mesh.RecalculateBounds();
        }

    }



    void PropsDataOnEnable()
    {
        for (int i = 0; i < densityPropsMeshData.Length; ++i) densityPropsMeshData[i].StopThread();
        //Things should at least start idle
    }

    void PropsDataOnDisable()
    {
        for (int i = 0; i < densityPropsMeshData.Length; ++i) densityPropsMeshData[i].StopThread();
    }

    void PropsDataOnValidate()
    {
        int targetLength = width * depth;
        for (int i = 0; i < densityPropsMeshData.Length; ++i)
        {
            DensityPropsMeshData dpMeshData = densityPropsMeshData[i];
            if (dpMeshData == null) { Debug.LogError("WTF"); continue; }//TODO what? 
            if (dpMeshData.densityMap == null || dpMeshData.densityMap.Length != targetLength) dpMeshData.densityMap = new float[targetLength];//TODO properly rescale
            //dpMeshData.FreeMeshLists();//TODO this seems to block update, maybe crashes the thread
        }
    }
}
