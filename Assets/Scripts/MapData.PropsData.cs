﻿using System.Collections.Generic;
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
        public float size;//All axis
        public int variantIndex;//<0 to prepare for deletion! TODO
        //TODO variant inspector!

        //[System.NonSerialized] public float sqrtDist;//Alt: use this field for deletion if <0
    }

    [System.Serializable]
    public class InstanceSet
    {
        ///*[HideInInspector]*/ [SerializeField] private List<PropInstance> instances = new List<PropInstance>();
        //public List<PropInstance> Instances { get { return instances; } }
        /*[HideInInspector]*/ [SerializeField] private PropInstance[] instances = new PropInstance[0];
        /*[HideInInspector]*/ [SerializeField] private int count;
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
        public void RemoveMarked()//TODO test !!!!! //TODO use variant index < 0 instead!!
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

        //TODO remove precalculations?
        [System.NonSerialized] public Vector3[] instancePositions = new Vector3[0];//TODO mind multithreading
        [System.NonSerialized] public float[] instanceSqrDistances = new float[0];// Alt: use this array as flags for deletion if <0
    }
    
    public int instanceLimit = 100;//TODO Limit for the sum of meshdata.instances.count's?

    [System.Serializable]
    public class DensityMap
    {
        public float[] map;
        
        public float SampleDensity(float x, float y, MapData mapData)
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
                return 0f;
            }
        }
    }

    public InstanceSet[] instanceSets = new InstanceSet[0];
    public DensityMap[] densityMaps = new DensityMap[0];
    //public MeshData[] meshesData = new MeshData[0];//TODO wrap in more config?
                                                   //

        //TODO
        // InstanceSets and DensityMaps
        // PropsMeshData pointers to instance set and/or density map
        // Variants in PropsMeshData (selected with probability from density map, and with index from instance set)
        // MeshResourceData with MeshResource (reusable object), and distance range
        // MeshResourceData can represent LODs or components (such as leaves/trunk)
        // PropsMeshData instances can point to the same instanceSets and densityMaps!

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

    //TODO make not threaded, single time build for prop meshes


    [ContextMenu("RebuildPropsMeshes")]
    public void RefreshPropMeshes()//TODO move to inspector, use scene camera as pov
    {
        RefreshPropMeshes(default(Vector3), 1f);
    }
    
    public void RefreshPropMeshes(Vector3 pov, float lodScale)
    {
        for (int i = 0; i < propsMeshesData.Length; ++i)
        {
            propsMeshesData[i].TrySyncRebuild(pov, lodScale, this);
        }
    }

    /// <summary>
    /// Unlike RefreshPropMeshes, this one runs in background!
    /// </summary>
    /// <param name="pov"></param>
    /// <param name="lodScale"></param>
    public void BkgRefreshPropMeshes(Vector3 pov, float lodScale)
    {
        for (int i = 0; i < propsMeshesData.Length; ++i)
        {
            propsMeshesData[i].CheckDensityPropsUpdate(pov, lodScale, this);
        }
    }
    /*
    [System.Serializable]
    public class MeshData
    {
        //[System.Serializable]
        //public class Variant
        //{
        //    public MeshLOD[] meshLODs = new MeshLOD[1];
        //    public int instanceSetIndex;
        //}

        //[System.Serializable]
        //public class MeshLOD
        //{
        //    public Mesh mesh;
        //    public int subMeshIndex;
        //    public float maxSqrDistance;//TODO minDist, transition, billboarding?
        //}

        //public Variant[] variants = new Variant[0];
        //public int verticesLengthLimit = 900;
        //public int trianglesLengthLimit = 900;//indices actually

        //public Material sharedMaterial { get { return material; } }
        //[SerializeField] Material material;

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
            if (vertices == null) vertices = new List<Vector3>();
            //else { vertices.Clear(); if (vertices.Capacity < verticesLength) vertices.Capacity = verticesLength; }
            if (normals == null) normals = new List<Vector3>();
            if (uvs == null) uvs = new List<Vector2>();

            if (triangles == null) triangles = new List<int>();

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
            /////////////

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
    }*/

    ////////////////////////////// Props

    public PropsMeshData[] propsMeshesData = new PropsMeshData[0];

    [System.Serializable]
    public class PropsMeshData
    {
        const int maxCellsFromCenter = 12;

        [SerializeField]
        private PropDitherPattern pattern;
        [SerializeField]
        private float patternScale = 1f;

        [SerializeField]
        private int instanceSetIndex = -1;
        [SerializeField]
        private int densityMapIndex = -1;

        private enum UpdateState { Idle, Updating, Ready }
        private Vector3 currPOV;
        private float redrawThreshold = 5f;//TODO serialize?
        private UpdateState currentUpdateState = UpdateState.Idle;
        
        public Variant[] variants = new Variant[1];
        //public float[] densityMap;//TODO density map optional? mesh groups wont need it

        [System.Serializable]
        public class Variant
        {
            public MeshResource[] meshResources = new MeshResource[1];// For both LODs and instance components

            //TODO only mesh resources are useful to prop instances!
            //For density maps only
            public float probability = 1f;

            public float propsScale = 1f;
            public Vector3 propsDirection = Vector3.up;
            
            public FloatRange alignmentRange = new FloatRange(0.2f, 0.5f);
            public FloatRange rotationRange = new FloatRange(-180f, 180f);
            public FloatRange yOffsetRange = new FloatRange(0f, 0f);
        }
        
        //public MeshGroup[] meshGroups = new MeshGroup[1];

        //[System.Serializable]
        //public class MeshGroup
        //{
        //    public MeshLOD[] meshLODs = new MeshLOD[1];
        //    public int instanceSetIndex;
        //}

        [System.Serializable]
        public class MeshResource//TODO move to serialized object to reuse lists
        {
            public MeshResourceData data;//TODO move data to a different scriptable object?
            public FloatRange sqrDistanceRange = new FloatRange(0, 500);//TODO transition? use shader for that?
           
            //TODO im probably using lod distances wrong in the other class, since they can go to different meshes?
            
            //TODO Combine Instance Sets with density maps in the same mesh?
            public int targetSubMesh;//TODO store lods in submeshes if they require a different material?
            //public bool billboard;//TODO billboard from shader?

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

        public void TrySyncRebuild(Vector3 pov, float lodScale, MapData mapData)
        {
            if (currentUpdateState == UpdateState.Idle)
            {
                //Begin Vertices/Indices update
                this.mapData = mapData;
                this.pov = pov;
                this.lodScale = lodScale;
                LoadMeshLists();
                currentUpdateState = UpdateState.Updating;
                Debug.Log("ForceUpdate");
                PropsUpdate();
                UpdateMesh();

                currentUpdateState = UpdateState.Idle;
                currPOV = pov;
            }
        }

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
                    BeginPropsUpdate();

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

        private void BeginPropsUpdate()
        {
            if (rebuildThread == null) {
                PropsMeshData thisPMeshData = this;
                rebuildThread = new Thread(delegate ()
                {
                    while (shouldThreadsRun)
                    {
                        PropsUpdate();
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
                MeshResource[] meshResources = variants[v].meshResources;
                for (int r = 0; r < meshResources.Length; ++r)
                {
                    if (meshResources[r].data != null && !meshResources[r].data.MeshListsLoaded())
                    {
                        meshResources[r].data.LoadMeshLists();
                    }
                }
            }
        }

        void FreeMeshLists()//TODO meshes should refresh on validate, test
        {
            for (var v = 0; v < variants.Length; ++v)
            {
                MeshResource[] meshResources = variants[v].meshResources;
                for (int r = 0; r < meshResources.Length; ++r)
                {
                    if (meshResources[r].data != null && meshResources[r].data.MeshListsLoaded())
                    {
                        meshResources[r].data.FreeMeshLists();
                    }
                }
            }
        }

        void PropsUpdate()//TODO is run in the main thread too!
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

            if (instanceSetIndex >= 0 && instanceSetIndex < mapData.instanceSets.Length)
            {
                InstanceSet instanceSet = mapData.instanceSets[instanceSetIndex];
                int instanceCount = instanceSet.Count;
                PropInstance[] instances = instanceSet.Instances;

                for (int i = 0; i < instanceCount; ++i)
                {
                    PropInstance instance = instances[i];
                    
                    DoDensityInstance(instance, ref vertexIndex, ref indexIndex);
                }
            }


            if (pattern != null && densityMapIndex >= 0 && densityMapIndex < mapData.densityMaps.Length)
            {
                PropDitherPattern.PatternElement[] elements = pattern.elements;
                DensityMap densityMap = mapData.densityMaps[densityMapIndex];//TODO null checks in all classes?

                int povCellX = Mathf.RoundToInt(pov.x / patternScale);
                int povCellY = Mathf.RoundToInt(pov.z / patternScale);

                Vector2 elementPosition;
                for (int e = 0; e < elements.Length; ++e)
                {
                    elementPosition = GetElementPosition(povCellX, povCellY, elements[e]);
                    //if (e == 0) Debug.Log("HERE: " + elementPosition+" "+ DensityTest(elementPosition, e + 1, densityMap));
                    if (DensityTest(elementPosition, e + 1, densityMap)) DoDensityInstance(elementPosition, elements[e], ref vertexIndex, ref indexIndex);//TODO break if it doesnt fit
                }
                //DoPatternCell(povCellX, povCellY, ref vertexIndex, ref indexIndex);
                for (int offset = 1; offset <= maxCellsFromCenter; ++offset)//TODO configurable maxCellsFromCenter?
                {
                    //int sideLength = offset * 2;
                    for (int offset2 = 1 - offset; offset2 <= offset; ++offset2)
                    {
                        for (int e = 0; e < elements.Length; ++e)
                        {
                            elementPosition = GetElementPosition(povCellX + offset, povCellY + offset2, elements[e]);
                            if (DensityTest(elementPosition, e + 1, densityMap)) DoDensityInstance(elementPosition, elements[e], ref vertexIndex, ref indexIndex);//TODO break if it doesnt fit
                            elementPosition = GetElementPosition(povCellX - offset2, povCellY + offset, elements[e]);
                            if (DensityTest(elementPosition, e + 1, densityMap)) DoDensityInstance(elementPosition, elements[e], ref vertexIndex, ref indexIndex);//TODO break if it doesnt fit
                            elementPosition = GetElementPosition(povCellX - offset, povCellY - offset2, elements[e]);
                            if (DensityTest(elementPosition, e + 1, densityMap)) DoDensityInstance(elementPosition, elements[e], ref vertexIndex, ref indexIndex);//TODO break if it doesnt fit
                            elementPosition = GetElementPosition(povCellX + offset2, povCellY - offset, elements[e]);
                            if (DensityTest(elementPosition, e + 1, densityMap)) DoDensityInstance(elementPosition, elements[e], ref vertexIndex, ref indexIndex);//TODO break if it doesnt fit
                        }
                        //DoPatternCell(povCellX + offset, povCellY + offset2, ref vertexIndex, ref indexIndex);
                        //DoPatternCell(povCellX - offset2, povCellY + offset, ref vertexIndex, ref indexIndex);
                        //DoPatternCell(povCellX - offset, povCellY - offset2, ref vertexIndex, ref indexIndex);
                        //DoPatternCell(povCellX + offset2, povCellY - offset, ref vertexIndex, ref indexIndex);
                    }
                    //TODO break if no lods? might be too complex to detect
                }
                //Debug.Log(vertexIndex+" "+indexIndex);
            }
        }

        Vector2 GetElementPosition(int cellX, int cellY, PropDitherPattern.PatternElement element)
        {
            return (new Vector2(cellX, cellY)  + element.pos / PropDitherPattern.CellSize) * patternScale;
        }

        bool DensityTest(Vector2 elemPosition, float minDensity, DensityMap densityMap)
        {
            float density = densityMap.SampleDensity(elemPosition.x, elemPosition.y, mapData);
            return density > minDensity;
        }

        int SelectVariant(float rand)
        {
            float probSum = 0;
            for (int v = 0; v < variants.Length; ++v) probSum += variants[v].probability;
            rand *= probSum;
            float probAccum = 0;
            for (int v = 0; v < variants.Length; ++v)
            {
                probAccum += variants[v].probability;
                if (rand <= probAccum) return v;//TODO test
            }
            return -1;
        }

        void DoDensityInstance(Vector2 elemPosition, PropDitherPattern.PatternElement element, ref int vertexIndex, ref int indexIndex)
        {
            //float patternLocal2WorldScale = patternScale / PropDitherPattern.CellSize;
            int subMeshCount = materials.Length;

            //Vector2 elemPosition = new Vector2(x, y) * patternScale + element.pos * patternLocal2WorldScale;
            //float density = densityMap.SampleDensity(elemPosition.x, elemPosition.y, mapData);
            //if (density <= (e + 1)) return;
            
            float variantRand = element.rand3;
            int variantIndex = SelectVariant(variantRand);
            if (variantIndex == -1) return;

            Variant variant = variants[variantIndex];

            Vector3 position = new Vector3(elemPosition.x, variant.yOffsetRange.GetValue(element.rand2), elemPosition.y);
            float height = mapData.SampleHeight(position.x, position.z);

            Vector3 realPosition = new Vector3(position.x, height + position.y, position.z);
            float sqrDist = (realPosition - pov).sqrMagnitude;

            Vector3 terrainNormal = mapData.SampleNormals(realPosition.x, realPosition.z);

            Vector3 direction = Vector3.Slerp(variant.propsDirection, terrainNormal, variant.alignmentRange.GetValue(element.rand0));
            float rotation = variant.rotationRange.GetValue(element.rand1);
            float size = element.r * variant.propsScale;
            
            for (int r = 0; r < variant.meshResources.Length; ++r)
            {
                MeshResource meshResource = variant.meshResources[r];
                MeshResourceData meshData = meshResource.data;
                if (!meshData.MeshListsLoaded()) continue;

                if (vertexIndex + meshData.verticesCount > verticesLengthLimit) break;
                if (indexIndex + meshData.indicesCount > trianglesLengthLimit) break;

                if (!meshResource.sqrDistanceRange.CheckInRange(sqrDist)) continue;
                
                Matrix4x4 matrix = Matrix4x4.TRS(realPosition, Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.Euler(0, rotation, 0), new Vector3(size, size, size));//TODO Optimize?
                for (int i = 0; i < meshData.verticesCount; ++i)
                {
                    Vector3 vertex = matrix.MultiplyPoint3x4(meshData.verticesList[i]);
                    vertices.Add(vertex);
                }
                for (int i = 0; i < meshData.verticesCount; ++i)
                {
                    Vector3 normal = matrix.MultiplyVector(meshData.normalsList[i]);
                    normals.Add(normal);//TODO normalize?
                }
                for (int i = 0; i < meshData.verticesCount; ++i)
                {
                    Vector2 uv = meshData.uvsList[i];
                    uvs.Add(uv);
                }

                //if (vertexIndex < 300) Debug.Log(meshResource.targetSubMesh + " asdffasd fas ");
                if (meshResource.targetSubMesh >= 0 && meshResource.targetSubMesh < subMeshCount)
                {
                    List<int> triangles = triangleLists[meshResource.targetSubMesh];
                    for (int i = 0; i < meshData.indicesCount; ++i)
                    {
                        int index = vertexIndex + meshData.trianglesList[i];
                        triangles.Add(index);
                    }
                }

                vertexIndex += meshData.verticesCount;
                indexIndex += meshData.indicesCount;//TODO Split by submesh?
            }
        }

        void DoDensityInstance(PropInstance instance, ref int vertexIndex, ref int indexIndex)
        {
            int subMeshCount = materials.Length;

            if (instance.variantIndex < 0 || instance.variantIndex >= variants.Length) return;
            Variant variant = variants[instance.variantIndex];

            Vector3 position = instance.position;
            float height = mapData.SampleHeight(position.x, position.z);

            Vector3 realPosition = new Vector3(position.x, height + position.y, position.z);
            float sqrDist = (realPosition - pov).sqrMagnitude;

            Vector3 direction = instance.direction;
            float rotation = instance.rotation;
            float size = instance.size;

            for (int r = 0; r < variant.meshResources.Length; ++r)
            {
                MeshResource meshResource = variant.meshResources[r];
                MeshResourceData meshData = meshResource.data;
                if (!meshData.MeshListsLoaded()) continue;

                if (vertexIndex + meshData.verticesCount > verticesLengthLimit) break;
                if (indexIndex + meshData.indicesCount > trianglesLengthLimit) break;

                if (!meshResource.sqrDistanceRange.CheckInRange(sqrDist)) continue;

                Matrix4x4 matrix = Matrix4x4.TRS(realPosition, Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.Euler(0, rotation, 0), new Vector3(size, size, size));//TODO Optimize?
                for (int i = 0; i < meshData.verticesCount; ++i)
                {
                    Vector3 vertex = matrix.MultiplyPoint3x4(meshData.verticesList[i]);
                    vertices.Add(vertex);
                }
                for (int i = 0; i < meshData.verticesCount; ++i)
                {
                    Vector3 normal = matrix.MultiplyVector(meshData.normalsList[i]);
                    normals.Add(normal);//TODO normalize?
                }
                for (int i = 0; i < meshData.verticesCount; ++i)
                {
                    Vector2 uv = meshData.uvsList[i];
                    uvs.Add(uv);
                }

                //if (vertexIndex < 300) Debug.Log(meshResource.targetSubMesh + " asdffasd fas ");
                if (meshResource.targetSubMesh >= 0 && meshResource.targetSubMesh < subMeshCount)
                {
                    List<int> triangles = triangleLists[meshResource.targetSubMesh];
                    for (int i = 0; i < meshData.indicesCount; ++i)
                    {
                        int index = vertexIndex + meshData.trianglesList[i];
                        triangles.Add(index);
                    }
                }

                vertexIndex += meshData.verticesCount;
                indexIndex += meshData.indicesCount;//TODO Split by submesh?
            }
        }

        //void DoPatternCell(int x, int y, ref int vertexIndex, ref int indexIndex)
        //{
        //    float patternLocal2WorldScale = patternScale / PropDitherPattern.CellSize;
        //    int subMeshCount = materials.Length;
        //    PropDitherPattern.PatternElement[] elements = pattern.elements;
        //    for (int e = 0; e < elements.Length; ++e)
        //    {
        //        PropDitherPattern.PatternElement element = elements[e];

        //        float variantRand = element.rand3;
        //        Variant variant = SelectVariant(variantRand);
        //        if (variant == null) return;

        //        for (int lod = 0; lod < variant.meshLODs.Length; ++lod)
        //        {
        //            MeshResourceData meshLOD = variant.meshLODs[lod];
        //            if (!meshLOD.MeshListsLoaded()) continue;

        //            if (vertexIndex + meshLOD.verticesCount > verticesLengthLimit) break;
        //            if (indexIndex + meshLOD.indicesCount > trianglesLengthLimit) break;
                    
        //            Vector2 elemPosition = new Vector2(x, y) * patternScale + element.pos * patternLocal2WorldScale;
        //            Vector3 pos3D = new Vector3(elemPosition.x, variant.yOffsetRange.GetValue(element.rand2), elemPosition.y);

        //            float density = SampleDensity(pos3D.x, pos3D.z, mapData);
        //            float sqrDist = (pos3D - pov).sqrMagnitude;
        //            if (density <= (e + 1) || sqrDist > meshLOD.maxSqrDistance || sqrDist < meshLOD.minSqrDistance) continue;

        //            Vector3 terrainNormal = mapData.SampleNormals(pos3D.x, pos3D.z);

        //            Vector3 direction = Vector3.Slerp(variant.propsDirection, terrainNormal, variant.alignmentRange.GetValue(element.rand0));
        //            float rotation = variant.rotationRange.GetValue(element.rand1);
        //            float size = element.r * variant.propsScale;

        //            float height = mapData.SampleHeight(pos3D.x, pos3D.z);
        //            Vector3 realPosition = new Vector3(pos3D.x, height + pos3D.y, pos3D.z);

        //            Matrix4x4 matrix = Matrix4x4.TRS(realPosition, Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.Euler(0, rotation, 0), new Vector3(size, size, size));//TODO Optimize?
        //            for (int i = 0; i < meshLOD.verticesCount; ++i)
        //            {
        //                Vector3 vertex = matrix.MultiplyPoint3x4(meshLOD.verticesList[i]);
        //                vertices.Add(vertex);
        //            }
        //            for (int i = 0; i < meshLOD.verticesCount; ++i)
        //            {
        //                Vector3 normal = matrix.MultiplyVector(meshLOD.normalsList[i]);
        //                normals.Add(normal);//TODO normalize?
        //            }
        //            for (int i = 0; i < meshLOD.verticesCount; ++i)
        //            {
        //                Vector2 uv = meshLOD.uvsList[i];
        //                uvs.Add(uv);
        //            }

        //            if (e < 10 && vertexIndex < 300) Debug.Log(meshLOD.targetSubMesh + " asdffasd fas ");
        //            if (meshLOD.targetSubMesh >= 0 && meshLOD.targetSubMesh < subMeshCount)
        //            {
        //                List<int> triangles = triangleLists[meshLOD.targetSubMesh];
        //                for (int i = 0; i < meshLOD.indicesCount; ++i)
        //                {
        //                    int index = vertexIndex + meshLOD.trianglesList[i];
        //                    triangles.Add(index);
        //                }
        //            }

        //            vertexIndex += meshLOD.verticesCount;
        //            indexIndex += meshLOD.indicesCount;//TODO Split by submesh?
        //        }
        //    }
        //}

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
        for (int i = 0; i < propsMeshesData.Length; ++i) propsMeshesData[i].StopThread();
        //Things should at least start idle
    }

    void PropsDataOnDisable()
    {
        for (int i = 0; i < propsMeshesData.Length; ++i) propsMeshesData[i].StopThread();
    }

    void PropsDataOnValidate()
    {
        int targetLength = width * depth;
        for (int i = 0; i < densityMaps.Length; ++i)
        {
            DensityMap densityMap = densityMaps[i];
            if (densityMap.map == null || densityMap.map.Length != targetLength) densityMap.map = new float[targetLength];//TODO properly rescale
        }
        //dpMeshData.FreeMeshLists();//TODO ? this seems to block update, maybe crashes the thread
    }
}