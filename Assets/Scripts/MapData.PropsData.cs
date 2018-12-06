﻿using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public partial class MapData : ScriptableObject
{
    [System.Serializable]
    public struct PropInstance
    {
        public Vector3 position;//Y is a offset from terrain height
        public float alignment;//Normal aligned
        public float rotation;//Around Y
        public float size;//All axis
        public int variantIndex;//<0 to prepare for deletion!
    }

    [System.Serializable]
    public class InstanceSet
    {
        [HideInInspector] [SerializeField] private PropInstance[] instances = new PropInstance[0];
        [HideInInspector] [SerializeField] private int count;
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
        public void RemoveMarked()
        {
            int index = 0;
            while (index < count && instances[index].variantIndex >= 0) ++index;
            for (int i = index + 1; i < count; ++i)
            {
                PropInstance inst = instances[i];
                if (inst.variantIndex >= 0) instances[index++] = inst;
            }
            count = index;
        }
    }

    //[System.Serializable]
    //public class DensityMap
    //{
    //    [HideInInspector] public Vector4[] map;
        
    //    public Vector4 SampleDensity(float x, float y, MapData mapData)
    //    {
    //        Vector3Int indices;
    //        Vector3 barycentricCoordinate;
    //        if (mapData.SampleInfo(x, y, out indices, out barycentricCoordinate))
    //        {
    //            return map[indices.x] * barycentricCoordinate.x +
    //                map[indices.y] * barycentricCoordinate.y +
    //                map[indices.z] * barycentricCoordinate.z;
    //        }
    //        else
    //        {
    //            return default(Vector4);
    //        }
    //    }
    //}

    public InstanceSet[] instanceSets = new InstanceSet[0];

    //TODO
    // InstanceSets and DensityMaps (MapTextures)
    // PropsMeshData has pointers to instance set and/or density map
    // Variants in PropsMeshData (selected with probability from density map, and with index from instance set)
    // MeshResourceData with MeshResource (reusable object), and distance range
    // MeshResourceData can represent LODs or components (such as leaves/trunk)
    // PropsMeshData instances can point to the same instanceSets and densityMaps!

    public Vector3 GetRealInstancePosition(Vector3 instancePosition)
    {
        instancePosition.y += SampleHeight(instancePosition.x, instancePosition.z);
        return instancePosition;
    }
    
    [ContextMenu("RebuildPropsMeshes")]
    public void RefreshPropMeshes()
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
    public void RefreshPropMeshesAsync(Vector3 pov, float lodScale)
    {
        for (int i = 0; i < propsMeshesData.Length; ++i)
        {
            propsMeshesData[i].CheckPropsUpdate(pov, lodScale, this);
        }
    }

    public void PropMeshesSetDirty()
    {
        for (int i = 0; i < propsMeshesData.Length; ++i)
        {
            propsMeshesData[i].SetDirty();
        }
    }

    /// <summary>
    /// Check if a PropMeshesRebuild is ongoing.
    /// Can be used to check if we can expect new meshes in future frames.
    /// </summary>
    public bool PropMeshesRebuildOngoing()
    {
        for (int i = 0; i < propsMeshesData.Length; ++i)
        {
            if (propsMeshesData[i].RebuildOngoing()) return true;
        }
        return false;
    }
    

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

        private Vector3 currPOV;
        private float redrawThreshold = 5f;//TODO serialize?

        private enum UpdateState { Idle, Updating, Ready }
        private readonly object updateStateLock = new object();
        private UpdateState currentUpdateState = UpdateState.Idle;
        private bool shouldThreadRun = false;
        private bool dirty = false;

        public Variant[] variants = new Variant[1];

        public DensityPropsLogic propsLogic;

        [System.Serializable]
        public class Variant
        {
            public MeshResource[] meshResources = new MeshResource[1];// For both LODs and instance components

            public Vector3 propsDirection = Vector3.up;
        }
        
        [System.Serializable]
        public class MeshResource
        {
            public MeshResourceData data;
            public FloatRange sqrDistanceRange = new FloatRange(0, 500);//TODO transition? use shader for that?
           
            public int targetSubMesh;
        }
        
        public abstract class DensityPropsLogic : ScriptableObject
        {
            public abstract PropInstance BuildInstanceData(Vector2 pos, float elementRand, PropDitherPattern.PatternElement element, Vector4 densityValues);
        }


        [SerializeField] int verticesLengthLimit = 900;
        [SerializeField] int trianglesLengthLimit = 900;//indices actually

        public Material[] sharedMaterials { get { return materials; } }
        [SerializeField] Material[] materials;

        public Mesh sharedMesh { get { return mesh; } }

        Mesh mesh;
        List<Vector3> vertices;
        List<Vector3> normals;
        List<Vector2> uvs;
        //TODO color
        List<int>[] triangleLists;

        MapData mapData;
        Vector3 pov;
        float lodScale = 1f;

        public void SetDirty()
        {
            dirty = true;
        }

        public bool RebuildOngoing()
        {
            return currentUpdateState != UpdateState.Idle;
        }

        public void TrySyncRebuild(Vector3 pov, float lodScale, MapData mapData)
        {
            lock (updateStateLock)
            {
                if (!RebuildOngoing())
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
                else
                {
                    //else, hope that someone is checking for updates and finishes the job
                    Debug.LogWarning("Update Ongoing");
                }
            }
        }
        
        public void CheckPropsUpdate(Vector3 pov, float lodScale, MapData mapData)
        {
            lock (updateStateLock)
            {
                if (currentUpdateState == UpdateState.Ready)
                {
                    //Vertices/Indices ready, build mesh
                    UpdateMesh();

                    currentUpdateState = UpdateState.Idle;
                }

                if (currentUpdateState == UpdateState.Idle)
                {
                    if (Vector3.SqrMagnitude(pov - this.pov) > redrawThreshold * redrawThreshold)
                    {
                        dirty = true;
                    }
                    if (dirty)
                    {
                        currentUpdateState = UpdateState.Updating;
                        this.mapData = mapData;
                        this.pov = pov;
                        this.lodScale = lodScale;
                        LoadMeshLists();

                        dirty = false;//Allow dirtying while updating
                                      //Begin Vertices/Indices update
                        BeginPropsUpdate();
                    }
                }
            }
        }

        /// <summary>
        /// Signals the thread to stop, but it might not stop immediately
        /// </summary>
        public void StopThread()//TODO OnApplicationExit?
        {
            Debug.LogWarning("Stop Thread");
            shouldThreadRun = false;
            mre.Set();
            currentUpdateState = UpdateState.Idle;//Not locking
        }

        Thread rebuildThread;
        ManualResetEvent mre = new ManualResetEvent(false);

        private void BeginPropsUpdate()
        {
            if (rebuildThread == null) {
                PropsMeshData thisPMeshData = this;
                rebuildThread = new Thread(delegate ()
                {
                    while (shouldThreadRun)
                    {
                        PropsUpdate();

                        if (!shouldThreadRun) break;//Stop sets state to Idle, don't change it again
                        
                        //Avoid blocking the main thread for an unknown amount of time
                        lock (updateStateLock) {
                            currentUpdateState = UpdateState.Ready;
                        }

                        mre.Reset();
                        mre.WaitOne();
                    }

                    rebuildThread = null;//Allows the creation of newer threads if requested
                    Debug.LogWarning("Stopped Thread");
                });
                rebuildThread.IsBackground = true;//End when main thread ends
                shouldThreadRun = true;
                rebuildThread.Start();
                Debug.LogWarning("Started Thread");
            }

            mre.Set();//Run another iteration
        }

        void LoadMeshLists()
        {
            for (var v = 0; v < variants.Length; ++v)
            {
                MeshResource[] meshResources = variants[v].meshResources;
                for (int r = 0; r < meshResources.Length; ++r)
                {
                    MeshResourceData meshResourceData = meshResources[r].data;
                    if (meshResourceData != null)
                    {
                        meshResourceData.LoadMeshLists();//Dirty data should be reloaded
                    }
                }
            }
        }

        void FreeMeshLists()
        {
            for (var v = 0; v < variants.Length; ++v)
            {
                MeshResource[] meshResources = variants[v].meshResources;
                for (int r = 0; r < meshResources.Length; ++r)
                {
                    MeshResourceData meshResourceData = meshResources[r].data;
                    if (meshResourceData != null)
                    {
                        meshResourceData.FreeMeshLists();
                    }
                }
            }
        }

        System.Diagnostics.Stopwatch updateStopWatch = new System.Diagnostics.Stopwatch();
        float updateDuration;

        void PropsUpdate()
        {
            updateStopWatch.Reset();
            updateStopWatch.Start();
            //TODO measure times

            if (vertices == null) vertices = new List<Vector3>();
            if (normals == null) normals = new List<Vector3>();
            if (uvs == null) uvs = new List<Vector2>();
            
            int subMeshCount = materials.Length;
            
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
                    
                    DoInstance(instance, ref vertexIndex, ref indexIndex);
                }
            }


            if (pattern != null && propsLogic != null && densityMapIndex >= 0 && densityMapIndex < mapData.mapTextures.Length)
            {
                PropDitherPattern.PatternElement[] elements = pattern.elements;
                int elementsLength = elements.Length;

                int povCellX = Mathf.RoundToInt(pov.x / patternScale);
                int povCellY = Mathf.RoundToInt(pov.z / patternScale);

                MapTexture densityMapTexture = mapData.mapTextures[densityMapIndex];//TODO null checks in all classes?
                
                DensityPropsLogic currentPropsLogic = propsLogic;//TODO sealed override vs delegate
                System.Func<Vector2, float, PropDitherPattern.PatternElement, Vector4, PropInstance>  BuildInstanceData = currentPropsLogic.BuildInstanceData;//TODO test performance, there might not be a huge difference
                
                Vector2 elementPosition;
                Vector4 densityValues;
                for (int e = 0; e < elements.Length; ++e)
                {
                    float elementRand = (float)(e + 1) / elementsLength;

                    elementPosition = GetElementPosition(povCellX, povCellY, elements[e]);
                    densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                    DoInstance(/*currentPropsLogic.*/BuildInstanceData(elementPosition, elementRand, elements[e], densityValues), ref vertexIndex, ref indexIndex);//TODO break if it doesnt fit
                }

                for (int offset = 1; offset <= maxCellsFromCenter; ++offset)//TODO configurable maxCellsFromCenter?
                {
                    //int sideLength = offset * 2;
                    for (int offset2 = 1 - offset; offset2 <= offset; ++offset2)
                    {
                        for (int e = 0; e < elements.Length; ++e)
                        {
                            float elementRand = (float)(e + 1) / elementsLength;

                            elementPosition = GetElementPosition(povCellX + offset, povCellY + offset2, elements[e]);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            DoInstance(/*currentPropsLogic.*/BuildInstanceData(elementPosition, elementRand, elements[e], densityValues), ref vertexIndex, ref indexIndex);//TODO break if it doesnt fit
                            elementPosition = GetElementPosition(povCellX - offset2, povCellY + offset, elements[e]);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            DoInstance(/*currentPropsLogic.*/BuildInstanceData(elementPosition, elementRand, elements[e], densityValues), ref vertexIndex, ref indexIndex);//TODO break if it doesnt fit
                            elementPosition = GetElementPosition(povCellX - offset, povCellY - offset2, elements[e]);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            DoInstance(/*currentPropsLogic.*/BuildInstanceData(elementPosition, elementRand, elements[e], densityValues), ref vertexIndex, ref indexIndex);//TODO break if it doesnt fit
                            elementPosition = GetElementPosition(povCellX + offset2, povCellY - offset, elements[e]);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            DoInstance(/*currentPropsLogic.*/BuildInstanceData(elementPosition, elementRand, elements[e], densityValues), ref vertexIndex, ref indexIndex);//TODO break if it doesnt fit
                        }
                    }
                }
            }
            
            updateStopWatch.Stop();
            updateDuration += (updateStopWatch.ElapsedMilliseconds - updateDuration) * 0.5f;
        }

        Vector2 GetElementPosition(int cellX, int cellY, PropDitherPattern.PatternElement element)
        {
            return (new Vector2(cellX, cellY) + element.pos / PropDitherPattern.CellSize) * patternScale;
        }



        void DoInstance(PropInstance instance, ref int vertexIndex, ref int indexIndex)
        {
            if (instance.variantIndex < 0 || instance.variantIndex >= variants.Length) return;
            Variant variant = variants[instance.variantIndex];

            Vector3 position = instance.position;

            Vector3 realPosition = mapData.GetRealInstancePosition(position);
            float sqrDist = (realPosition - pov).sqrMagnitude;

            Vector3 terrainNormal = mapData.SampleNormals(realPosition.x, realPosition.z);

            Vector3 direction = Vector3.Slerp(variant.propsDirection, terrainNormal, instance.alignment);
            float rotation = instance.rotation;
            float size = instance.size;

            int subMeshCount = materials.Length;

            for (int r = 0; r < variant.meshResources.Length; ++r)
            {
                MeshResource meshResource = variant.meshResources[r];
                MeshResourceData meshData = meshResource.data;
                if (meshData == null) continue;
                lock (meshData.dataLock)
                {
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
        }
        

        void UpdateMesh()
        {
            if (mesh == null)
            {
                mesh = new Mesh();
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
            Debug.Log("BTW: "+updateDuration);
            mesh.subMeshCount = triangleLists.Length;
            for (int sm = 0; sm < triangleLists.Length; ++sm)
            {
                mesh.SetTriangles(triangleLists[sm], sm, false);//TODO calculate bounds later?
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
        //int targetLength = width * depth;
        //for (int i = 0; i < densityMaps.Length; ++i)
        //{
        //    DensityMap densityMap = densityMaps[i];
        //    if (densityMap.map == null || densityMap.map.Length != targetLength) densityMap.map = new Vector4[targetLength];//TODO properly rescale
        //}
        
        //TODO load mapTextures referenced by prop meshes data (avoid redundant loading, here and with mesh color)
    }
}
