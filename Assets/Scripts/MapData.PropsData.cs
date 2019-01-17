//#define BATCHING // AKA !GPU_INSTANCING

//#define BATCHING_TANGENTS // Used in BATCHING only
//#define BATCHING_UV2 // this makes no sense..

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

public partial class MapData : ScriptableObject
{
    [System.Serializable]
    public struct PropInstance
    {
        public Vector3 position;//Y is a offset from terrain height
        public float alignment;//Normal aligned
        public float rotation;//Around Y
        public float size;//All axis
        public Color tint;//Prop variance
        public int variantIndex;//<0 to prepare for deletion!
    }

    [System.Serializable]
    public class InstanceSet
    {
        public string label = "";
        [HideInInspector] [SerializeField] private List<PropInstance> instances = new List<PropInstance>();
        public List<PropInstance> Instances { get { return instances; } }
        public int Count
        {
            get { return instances.Count; }
        }

        public void RemoveMarked()
        {
            instances.RemoveAll((instance) => instance.variantIndex < 0);
        }

        public string GetInstanceSetName(int index)
        {
            return string.IsNullOrEmpty(label) ? string.Format("Set{0}", index) : string.Format("Set{0}.{1}", index, label);
        }
    }

    public InstanceSet[] instanceSets = new InstanceSet[0];
    
    public Vector3 GetRealInstancePosition(Vector3 instancePosition)
    {
        instancePosition.y += SampleHeight(instancePosition.x, instancePosition.z);
        return instancePosition;
    }
    
    public void RefreshPropMeshes(Vector3 pov, float lodScale, Matrix4x4 localToWorld)
    {
        for (int i = 0; i < propsMeshesData.Length; ++i)
        {
            propsMeshesData[i].TrySyncRebuild(pov, lodScale, this, localToWorld);
        }
    }

    /// <summary>
    /// Unlike RefreshPropMeshes, this one runs in background!
    /// </summary>
    /// <param name="pov"></param>
    /// <param name="lodScale"></param>
    public void RefreshPropMeshesAsync(Vector3 pov, float lodScale, Matrix4x4 localToWorld)
    {
        for (int i = 0; i < propsMeshesData.Length; ++i)
        {
            propsMeshesData[i].CheckPropsUpdate(pov, lodScale, this, localToWorld);
        }
    }

    /// <summary>
    /// Uses Graphics.DrawMesh calls!
    /// </summary>
    public void DrawPropMeshes(Camera cam)
    {
        //Debug.Log("Draw " + cam);
        for (int i = 0; i < propsMeshesData.Length; ++i)
        {
#if !BATCHING
            propsMeshesData[i].DrawProps(cam);
#else
            propsMeshesData[i].DrawProps();/*Unused cam*/
#endif
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
        [SerializeField]
        private PropDitherPattern pattern;
        [SerializeField]
        private float patternScale = 1f;

        [SerializeField]
        private int maxCellOffset = 5;
        [SerializeField]
        private float maxInstanceDistance = 30f;
        

        [SerializeField]
        private int instanceSetIndex = -1;
        [SerializeField]
        private int densityMapIndex = -1;

        private Vector3 currPOV;

        [SerializeField]
        private float redrawThreshold = 5f;

        private enum UpdateState { Idle, Updating, Ready }
        private readonly object updateStateLock = new object();
        private UpdateState currentUpdateState = UpdateState.Idle;
        private bool shouldThreadRun = false;
        private bool dirty = false;

        [SerializeField] private Variant[] variants = new Variant[1];

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
            
            public int targetSubMesh;

            public FloatRange distanceRange = new FloatRange(0, 30);
            public float fadeIn = 0f, fadeOut = 5f;

            public float GetLODFade(float distance)
            {
                if (!distanceRange.CheckInRange(distance)) return 0f;

                float distanceIn = distance - distanceRange.min;
                if (distanceIn < fadeIn) return distanceIn / fadeIn;
                float distanceOut = distanceRange.max - distance;
                if (distanceOut < fadeOut) return distanceOut / fadeOut;
                return 1f;
            }
        }

        public abstract class DensityPropsLogic : ScriptableObject
        {
            public abstract PropInstance BuildInstanceData(Vector2 pos, PropDitherPattern.PatternElement element, Vector4 densityValues);
        }
        
        [Header("Batching Mode")]
        [SerializeField] int verticesLengthLimit = 900;
        [SerializeField] int trianglesLengthLimit = 900;//indices actually
        
        [Header("GPU Instancing Mode")]
        [SerializeField] int variantInstanceLimit = 1000;

        
        [Serializable]
        private struct PropsSubmeshRendering
        {
            public ShadowCastingMode castShadows;
            public bool receiveShadows;
            public Material material;
            public bool includeColorProperties;
            public bool includeLODFadeProperties;// GPU_INSTANCING only!
        }
        [Header("Rendering")]
        [SerializeField]
        private PropsSubmeshRendering[] submeshRenderingConfig;

        
        /// <summary>
        /// Unused in GPU_INSTANCING mode
        /// </summary>
        public Mesh sharedMesh { get { return mesh; } }

        Mesh mesh;
#if BATCHING
        List<Vector3> vertices;
        List<Vector3> normals;
#if BATCHING_TANGENTS
        List<Vector4> tangents;
#endif
        List<Vector2> uvs;
#if BATCHING_UV2
        List<Vector2> uvs2;//Common sampling point and pivot for each instance
#endif
        List<Color> colors;
        List<int>[] triangleLists;
        int[] startIndices;
#endif

        private class VariantInstancesData
        {
            private static int _ColorPropertyID = Shader.PropertyToID("_Color");

            //Background workspace
            private Matrix4x4[] _instanceMatrices;
            private Vector4[] _instanceColors;
            private int _count;

#if !BATCHING
            public Matrix4x4[] instanceMatrices;
            public Vector4[] instanceColors;
            private int count;
            public int Count { get { return count; } }
#endif

            /// <summary>
            /// Called from the main thread before the work on the building thread starts.
            /// Do NOT change capacity for each list independently!
            /// </summary>
            public void Initialize(int capacity)//TODO growing size? shared indexed buffers?
            {
                if (_instanceMatrices == null || _instanceMatrices.Length != capacity) _instanceMatrices = new Matrix4x4[capacity];
                if (_instanceColors == null || _instanceColors.Length != capacity) _instanceColors = new Vector4[capacity];

                _count = 0;

#if !BATCHING
                if (instanceMatrices == null) instanceMatrices = new Matrix4x4[capacity];
                if (instanceColors == null) instanceColors = new Vector4[capacity];
#endif
            }

#if !BATCHING
            /// <summary>
            /// Called from the main thread when the work on the building thread is done
            /// </summary>
            public void UpdateData()
            {
                if (instanceMatrices.Length != _instanceMatrices.Length) instanceMatrices = new Matrix4x4[_instanceMatrices.Length];
                if (instanceColors.Length != _instanceColors.Length) instanceColors = new Vector4[_instanceColors.Length];


                for (count = 0; count < _count; ++count) instanceMatrices[count] = _instanceMatrices[count];
                for (count = 0; count < _count; ++count) instanceColors[count] = _instanceColors[count];
            }
#endif


            /// <summary>
            /// Called from the building thread
            /// </summary>
            public int InternalCapacity { get { return _instanceMatrices.Length; } }

            /// <summary>
            /// Called from the building thread
            /// </summary>
            public int InternalCount { get { return _count; } }

            /// <summary>
            /// Called from the building thread
            /// </summary>
            /// <param name="matrix"></param>
            /// <param name="color"></param>
            public void AddInstance(Matrix4x4 matrix, Vector4 color)
            {
                _instanceMatrices[_count] = matrix;
                _instanceColors[_count] = color;
                _count++;
            }

#if BATCHING
            /// <summary>
            /// Called from the building thread
            /// Batching mode only
            /// </summary>
            public void GetInstance(int instanceIndex, out Matrix4x4 matrix, out Color color)
            {
                matrix = _instanceMatrices[instanceIndex];
                color = _instanceColors[instanceIndex];
            }
#endif
        }
        VariantInstancesData[] variantsInstancesData;


        MapData mapData;

        // Building thread parameters
        Matrix4x4 _localToWorld;
        Vector3 _pov;
        float _lodScale = 1f;

        // Drawing (Main) thread parameters
        Matrix4x4 localToWorld = Matrix4x4.identity;
        Vector3 pov = Vector3.zero;
        float lodScale = 1f;

        public Matrix4x4 CurrentLocalToWorld { get { return localToWorld; } }
        public Vector3 CurrentPOV { get { return pov; } }
        public float CurrentLODScale { get { return lodScale; } }

        public void SetDirty()
        {
            dirty = true;
        }

        public bool RebuildOngoing()
        {
            return currentUpdateState != UpdateState.Idle;
        }

        public void TrySyncRebuild(Vector3 pov, float lodScale, MapData mapData, Matrix4x4 localToWorld)
        {
            lock (updateStateLock)
            {
                if (!RebuildOngoing())
                {
                    //Begin Vertices/Indices update
                    this.mapData = mapData;
                    _localToWorld = localToWorld;
                    _pov = pov;
                    _lodScale = lodScale;

#if BATCHING
                    LoadMeshLists();
                    InitializeLists();
#endif
                    InitializeVariantsInstancesData();
                    currentUpdateState = UpdateState.Updating;
                    Debug.Log("ForceUpdate");
                    PropsUpdate();
#if BATCHING
                    UpdateMesh();
#else
                    RetrieveInstanceData();
#endif

                    this.localToWorld = _localToWorld;
                    this.pov = _pov;
                    this.lodScale = _lodScale;

                    currentUpdateState = UpdateState.Idle;
                    currPOV = pov;
                }
                else
                {
                    //else, hope that someone is checking for updates and finishes the job
                    Debug.LogWarning("Update Ongoing");
                    if (rebuildThread == null || !rebuildThread.IsAlive)
                    {
                        Debug.Log("Thread is dead");
                        StopThread();
                    }
                }
            }
        }

#if BATCHING
        public void DrawProps()
        {
            PropsDrawBatched();    
        }
#else
        public void DrawProps(Camera cullingCam)
        {
            this.cullingCam = Utils.IsEditMode ? null : cullingCam;
            PropsDrawInstanced();
        }
#endif

        public void CheckPropsUpdate(Vector3 pov, float lodScale, MapData mapData, Matrix4x4 localToWorld)
        {
            lock (updateStateLock)
            {
                if (currentUpdateState == UpdateState.Ready)
                {
#if BATCHING
                    //Vertices/Indices ready, build mesh
                    UpdateMesh();
#else
                    RetrieveInstanceData();
#endif
                    this.localToWorld = _localToWorld;
                    this.pov = _pov;
                    this.lodScale = _lodScale;

                    currentUpdateState = UpdateState.Idle;
                }

                if (currentUpdateState == UpdateState.Idle)
                {
                    if (Vector3.SqrMagnitude(pov - _pov) > redrawThreshold * redrawThreshold)
                    {
                        dirty = true;
                    }
                    if (dirty)
                    {
                        currentUpdateState = UpdateState.Updating;
                        this.mapData = mapData;
                        _localToWorld = localToWorld;
                        _pov = pov;
                        _lodScale = lodScale;

#if BATCHING
                        LoadMeshLists();
                        InitializeLists();
#endif
                        InitializeVariantsInstancesData();


                        dirty = false;//Allow dirtying while updating
                                      //Begin Vertices/Indices update
                        BeginPropsUpdate();
                    }
                }
                //TODO else check if thread is running?
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
                        try
                        {
                            PropsUpdate();
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogErrorFormat("{0}\n{1}", e.Message, e.StackTrace);
                            break;
                        }

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
                //rebuildThread.Priority = System.Threading.ThreadPriority.AboveNormal;//Not implemented in this version of Mono
                shouldThreadRun = true;
                rebuildThread.Start();
                Debug.LogWarning("Started Thread");
            }

            mre.Set();//Run another iteration
        }

#if BATCHING
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

        void InitializeLists() 
        {
            if (vertices == null) vertices = new List<Vector3>();
            if (normals == null) normals = new List<Vector3>();
#if BATCHING_TANGENTS
            if (tangents == null) tangents = new List<Vector4>();
#endif
            if (uvs == null) uvs = new List<Vector2>();
#if BATCHING_UV2
            if (uvs2 == null) uvs2 = new List<Vector2>();
#endif
            if (colors == null) colors = new List<Color>();
            
            int subMeshCount = submeshRenderingConfig.Length;
            
            if (triangleLists == null || triangleLists.Length != subMeshCount) {
                triangleLists = new List<int>[subMeshCount];
                for (int i = 0; i < subMeshCount; ++i) triangleLists[i] = new List<int>();
            }
            if (startIndices == null || startIndices.Length != subMeshCount) {
                startIndices = new int[subMeshCount];
            }

            // Clear lists
            vertices.Clear();
            normals.Clear();
#if BATCHING_TANGENTS
            tangents.Clear();
#endif
            uvs.Clear();
#if BATCHING_UV2
            uvs2.Clear();
#endif
            colors.Clear();
            for (int i = 0; i < subMeshCount; ++i) triangleLists[i].Clear();       
            //StartIndices data is overwritten on mesh build
        }
#endif

        void InitializeVariantsInstancesData()
        {
            int variantsLength = variants.Length;
            if (variantsInstancesData == null || variantsInstancesData.Length != variantsLength)
            {
                variantsInstancesData = new VariantInstancesData[variantsLength];
                for (int i = 0; i < variantsLength; ++i) variantsInstancesData[i] = new VariantInstancesData();
            }
            for (int i = 0; i < variantsLength; ++i) variantsInstancesData[i].Initialize(variantInstanceLimit);
        }


        System.Diagnostics.Stopwatch updateStopWatch = new System.Diagnostics.Stopwatch();
        float updateDuration;


        /// <summary>
        /// Builds each instance data
        /// </summary>
        void PropsUpdate()
        {
            updateStopWatch.Reset();
            updateStopWatch.Start();

            if (instanceSetIndex >= 0 && instanceSetIndex < mapData.instanceSets.Length)
            {
                InstanceSet instanceSet = mapData.instanceSets[instanceSetIndex];
                int instanceCount = instanceSet.Count;
                List<PropInstance> instances = instanceSet.Instances;//We could sync this, but it should not be a problem outside the editor

                for (int i = 0; i < instanceCount; ++i)
                {
                    PropInstance instance = instances[i];

                    DoInstance(instance);
                }
            }


            if (pattern != null && propsLogic != null && densityMapIndex >= 0 && densityMapIndex < mapData.mapTextures.Length)
            {
                PropDitherPattern.PatternElement[] elements = pattern.elements;
                int elementsLength = elements.Length;

                int povCellX = Mathf.RoundToInt(_pov.x / patternScale);
                int povCellY = Mathf.RoundToInt(_pov.z / patternScale);

                MapTexture densityMapTexture = mapData.mapTextures[densityMapIndex];//TODO null checks in all classes?

                DensityPropsLogic currentPropsLogic = propsLogic;
                //System.Func<Vector2, float, PropDitherPattern.PatternElement, Vector4, PropInstance> BuildInstanceData = currentPropsLogic.BuildInstanceData;//No apparent improvement

                
                Vector2 elementPosition;
                Vector4 densityValues;
                for (int e = 0; e < elementsLength; ++e)
                {
                    PropDitherPattern.PatternElement element = elements[e];

                    elementPosition = GetElementPosition(povCellX, povCellY, element);
                    densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                    DoInstance(currentPropsLogic.BuildInstanceData(elementPosition, element, densityValues));
                }

                for (int offset = 1; offset <= maxCellOffset; ++offset)
                {
                    for (int offset2 = 1 - offset; offset2 <= offset; ++offset2)
                    {
                        for (int e = 0; e < elementsLength; ++e)
                        {
                            PropDitherPattern.PatternElement element = elements[e];
                            
                            elementPosition = GetElementPosition(povCellX + offset, povCellY + offset2, element);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            DoInstance(currentPropsLogic.BuildInstanceData(elementPosition, element, densityValues) );
                            elementPosition = GetElementPosition(povCellX - offset2, povCellY + offset, element);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            DoInstance(currentPropsLogic.BuildInstanceData(elementPosition, element, densityValues) );
                            elementPosition = GetElementPosition(povCellX - offset, povCellY - offset2, element);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            DoInstance(currentPropsLogic.BuildInstanceData(elementPosition, element, densityValues));
                            elementPosition = GetElementPosition(povCellX + offset2, povCellY - offset, element);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            DoInstance(currentPropsLogic.BuildInstanceData(elementPosition, element, densityValues));
                        }
                    }
                }
            }

#if BATCHING
            BuildMesh();
#endif

            updateStopWatch.Stop();
            updateDuration += (updateStopWatch.ElapsedMilliseconds - updateDuration) * 0.5f;

            //Debug.Log("PropsUpdate: " + updateDuration);
        }

#if BATCHING
        void BuildMesh()
        {
            //TODO predict offsets and limits, parallelize? is it worth it?
            int subMeshStartIndex = 0;
            int variantsLength = variants.Length;

            int subMeshCount = triangleLists.Length;
            for (int subMesh = 0; subMesh < subMeshCount; ++subMesh)
            {
                PropsSubmeshRendering renderingConfig = submeshRenderingConfig[subMesh];

                startIndices[subMesh] = subMeshStartIndex;

                int indexIndex = 0;
                int vertexIndex = 0;
                for (int v = 0; v < variantsLength; ++v)
                {
                    Variant variant = variants[v];
                    VariantInstancesData instancesData = variantsInstancesData[v];

                    int instanceCount = instancesData.InternalCount;
                    if (instanceCount == 0) continue;
                    
                    for (int r = 0; r < variant.meshResources.Length; ++r)
                    {
                        MeshResource meshResource = variant.meshResources[r];
                        if (meshResource.targetSubMesh != subMesh) continue;

                        MeshResourceData meshData = meshResource.data;
                        if (meshData == null) continue;

                        lock (meshData.dataLock)
                        {
                            if (!meshData.MeshListsLoaded()) continue;

                            for (int instanceIndex = 0; instanceIndex < instanceCount; ++instanceIndex)
                            {
                                if (vertexIndex + meshData.verticesCount > verticesLengthLimit) break;
                                if (indexIndex + meshData.indicesCount > trianglesLengthLimit) break;

                                Matrix4x4 matrix;
                                Color tint;
                                instancesData.GetInstance(instanceIndex, out matrix, out tint);
                                if (!renderingConfig.includeColorProperties) tint = Color.white;
                                Vector2 position2D = new Vector2(matrix.m03, matrix.m23);//TODO hide in directive

                                if (!meshResource.distanceRange.CheckInSqrRange(Vector3.SqrMagnitude(new Vector3(matrix.m03, matrix.m13, matrix.m23) - _pov) * _lodScale * _lodScale)) continue;
                                // lodFade transition should be handled in shader

                                for (int i = 0; i < meshData.verticesCount; ++i)
                                {
                                    Vector3 vertex = matrix.MultiplyPoint3x4(meshData.verticesList[i]);
                                    vertices.Add(vertex);
                                }
                                for (int i = 0; i < meshData.verticesCount; ++i)
                                {
                                    Vector3 normal = matrix.MultiplyVector(meshData.normalsList[i]).normalized;
                                    normals.Add(normal);
                                }
#if BATCHING_TANGENTS
                                if (meshData.tangentsList.Count == meshData.verticesCount)//If it has tangents
                                {
                                    for (int i = 0; i < meshData.verticesCount; ++i)
                                    {
                                        Vector4 origTangent = meshData.tangentsList[i];
                                        Vector4 tangent = matrix.MultiplyVector(origTangent).normalized * origTangent.w;
                                        tangent.w = 1;
                                        tangents.Add(tangent);
                                    }
                                }
                                else
                                {
                                    Vector4 rightTangent = matrix.MultiplyVector(new Vector3(1, 0, 0)).normalized;
                                    rightTangent.w = 1;
                                    for (int i = 0; i < meshData.verticesCount; ++i)
                                    {
                                        tangents.Add(rightTangent);
                                    }
                                }
#endif
                                if (meshData.uvsList.Count == meshData.verticesCount)//If uvs loaded
                                {
                                    for (int i = 0; i < meshData.verticesCount; ++i)
                                    {
                                        Vector2 uv = meshData.uvsList[i];
                                        uvs.Add(uv);
#if BATCHING_UV2
                                        uvs2.Add(position2D);
#endif
                                    }
                                }
                                else
                                {
                                    Vector2 zero = default(Vector2);
                                    for (int i = 0; i < meshData.verticesCount; ++i)
                                    {
                                        uvs.Add(zero);
#if BATCHING_UV2
                                        uvs2.Add(position2D);
#endif
                                    }
                                }
                                if (meshData.colorsList.Count == meshData.verticesCount)//If colors loaded, TODO create arrays in mesh resource and assume here? same for uvs?
                                {
                                    for (int i = 0; i < meshData.verticesCount; ++i)
                                    {
                                        Color color = meshData.colorsList[i];
                                        colors.Add(color * tint);
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < meshData.verticesCount; ++i)
                                    {
                                        colors.Add(tint);
                                    }
                                }
                                
                                List<int> triangles = triangleLists[subMesh];
                                for (int i = 0; i < meshData.indicesCount; ++i)
                                {
                                    int index = vertexIndex + meshData.trianglesList[i];
                                    triangles.Add(index);
                                }

                                vertexIndex += meshData.verticesCount;
                                indexIndex += meshData.indicesCount;
                            }
                        }
                    }
                }
                subMeshStartIndex += vertexIndex;
            }
        }
#endif


#if BATCHING
        void PropsDrawBatched()
        {
            int submeshCount = submeshRenderingConfig.Length;
            for (int i = 0; i < submeshCount; ++i)
            {
                PropsSubmeshRendering renderingConfig = submeshRenderingConfig[i];
                Graphics.DrawMesh(mesh, localToWorld, renderingConfig.material, 0, null, i, null, renderingConfig.castShadows, renderingConfig.receiveShadows, null, LightProbeUsage.Off);
            }
        }
#else

        [System.NonSerialized]
        Matrix4x4[] instanceMatrices;
        [System.NonSerialized]
        Vector4[] instanceColors;
        [System.NonSerialized]
        float[] instanceLODFades;
        [System.NonSerialized]
        MaterialPropertyBlock instanceProperties;
        private static int _ColorPropertyID = Shader.PropertyToID("_Color");
        private static int _LODFadePropertyID = Shader.PropertyToID("_LODFade");
        const int maxSubCount = 500;//TODO rename?

        private Camera cullingCam;
        
        void PropsDrawInstanced()
        {
            int variantsLength = variants.Length;
            if (variantsInstancesData == null || variantsInstancesData.Length != variantsLength) return;

            bool culling = cullingCam != null;
            Matrix4x4 projMatrix = default(Matrix4x4);
            Vector3 camUpRight = default(Vector3);
            Vector3 camBottomLeft = default(Vector3);
            Vector3 cameraPosition = default(Vector3);
            if (culling) {
                projMatrix = GL.GetGPUProjectionMatrix(cullingCam.projectionMatrix, false) * cullingCam.worldToCameraMatrix;//TODO jittered vs nonjittered projMatrix
                camUpRight = cullingCam.transform.TransformVector(new Vector3(1f, 1f, 1f));
                camBottomLeft = cullingCam.transform.TransformVector(new Vector3(-1f, -1f, 1f));
                cameraPosition = cullingCam.transform.position;
            }

            //Debug.Log("***"+instanceMatrices);
            //TODO move initialization to onenable?
            if (instanceMatrices == null) instanceMatrices = new Matrix4x4[maxSubCount];
            if (instanceColors == null) instanceColors = new Vector4[maxSubCount];
            if (instanceLODFades == null) instanceLODFades = new float[maxSubCount];
            if (instanceProperties == null) instanceProperties = new MaterialPropertyBlock();
            //Debug.Log(instanceMatrices.Length + " " + maxSubCount + "****");

            for (int i = 0; i < variantsLength; ++i)
            {
                Variant variant = variants[i];
                VariantInstancesData instancesData = variantsInstancesData[i];

                if (instancesData.instanceMatrices == null)
                {
                    Debug.LogErrorFormat("VariantInstancesData at {0} is not initialized!", i);
                    continue;
                }

                if (instancesData.Count == 0) continue;
                

                for (int r = 0; r < variant.meshResources.Length; ++r)
                {
                    MeshResource meshResource = variant.meshResources[r];
                    MeshResourceData meshData = meshResource.data;
                    if (meshData == null || meshResource.targetSubMesh < 0 || meshResource.targetSubMesh >= submeshRenderingConfig.Length) continue;

                    Bounds bounds = meshData.sharedMesh.bounds;
                    Vector3 extents = bounds.extents;
                    Vector3 boundsOffset = bounds.center;
                    float maxBoundSize = Mathf.Max(Mathf.Max(extents.x, extents.y), extents.z);

                    int instanceIndex = 0;
                    int instancesCount = instancesData.Count;
                    //Debug.Log("Drawing " + instancesCount + " instances");
                    while (instanceIndex < instancesCount)
                    {
                        int subCount = 0;
                        while (subCount < maxSubCount && instanceIndex < instancesCount)
                        {
                            Matrix4x4 instanceMatrix = instancesData.instanceMatrices[instanceIndex];
                            Vector4 instanceColor = instancesData.instanceColors[instanceIndex];

                            if (culling)
                            {
                                Vector3 position = instanceMatrix.MultiplyPoint(boundsOffset);

                                Vector3 urPosition = position + camUpRight * maxBoundSize;
                                Vector3 blPosition = position + camBottomLeft * maxBoundSize;
                                Vector3 urCamCoords = projMatrix.MultiplyPoint(urPosition);
                                Vector3 blCamCoords = projMatrix.MultiplyPoint(blPosition);

                                // Ignoring far plane, it would need another matrix multiplication on the nearest point in bounds
                                if (urCamCoords.z > -1f && urCamCoords.x > -1f && blCamCoords.x < 1f && urCamCoords.y > -1f && blCamCoords.y < 1f)
                                {
                                    //TODO optimize, avoid sqrtt?
                                    float distance = Vector3.Distance(position, cameraPosition);//TODO currPOV vs camPos vs another ref
                                    float lodFade = meshResource.GetLODFade(distance * lodScale);//distance < 20 ? 1 : distance < 30 ? 1 - (distance - 20) / (30 - 20) : 0;//TODO
                                    
                                    if (lodFade > 0f)
                                    {
                                        instanceMatrices[subCount] = instanceMatrix;
                                        instanceColors[subCount] = instanceColor;
                                        instanceLODFades[subCount] = lodFade;
                                        
                                        subCount++;
                                    }
                                }
                            }
                            else
                            {
                                instanceMatrices[subCount] = instanceMatrix;
                                instanceColors[subCount] = instanceColor;
                                instanceLODFades[subCount] = 1f;

                                subCount++;
                            }

                            instanceIndex++;
                        }

                        PropsSubmeshRendering renderingConfig = submeshRenderingConfig[meshResource.targetSubMesh];
                        instanceProperties.Clear();
                        if (renderingConfig.includeColorProperties) instanceProperties.SetVectorArray(_ColorPropertyID, instanceColors);
                        if (renderingConfig.includeLODFadeProperties) instanceProperties.SetFloatArray(_LODFadePropertyID, instanceLODFades);

                        //TODO configurable properties added? define? (color/lodBlend?/pov+lodScale)
                        Graphics.DrawMeshInstanced(meshData.sharedMesh, meshData.SubMeshIndex, renderingConfig.material, instanceMatrices, subCount, instanceProperties, renderingConfig.castShadows, renderingConfig.receiveShadows, 0, null, LightProbeUsage.Off);
                    }
                }
            }
        }
#endif

        Vector2 GetElementPosition(int cellX, int cellY, PropDitherPattern.PatternElement element)
        {
            return (new Vector2(cellX, cellY) + element.pos / PropDitherPattern.CellSize) * patternScale;
        }
        

        void DoInstance(PropInstance instance)
        {
            if (instance.variantIndex < 0 || instance.variantIndex >= variants.Length) return;
            Variant variant = variants[instance.variantIndex];

            VariantInstancesData instancesData = variantsInstancesData[instance.variantIndex];
            if (instancesData.InternalCount >= instancesData.InternalCapacity) return;

            Vector3 position = instance.position;
            
            Vector3 realPosition = mapData.GetRealInstancePosition(position);
            float sqrDistance = (realPosition - _pov).sqrMagnitude * _lodScale * _lodScale;
            
            if (sqrDistance > maxInstanceDistance * maxInstanceDistance) return;

            Vector3 terrainNormal = mapData.SampleNormals(realPosition.x, realPosition.z);

            Vector3 direction = Vector3.Slerp(variant.propsDirection, terrainNormal, instance.alignment);
            float rotation = instance.rotation;
            float size = instance.size;
            Color tint = instance.tint;

            Matrix4x4 matrix = Matrix4x4.TRS(realPosition, Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.Euler(0, rotation, 0), new Vector3(size, size, size));//TODO Optimize?

#if BATCHING
            instancesData.AddInstance(matrix, tint);
#else
            instancesData.AddInstance(_localToWorld * matrix, tint);
#endif
        }


#if BATCHING
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
#if BATCHING_TANGENTS
            mesh.SetTangents(tangents);//mesh.tangents = tangents;
#endif
            mesh.SetUVs(0, uvs);//mesh.uv = uvs;
#if BATCHING_UV2
            mesh.SetUVs(1, uvs2);//mesh.uv2 = uvs2;
#endif
            mesh.SetColors(colors);//mesh.colors = colors;

            //Debug.Log("BTW: " + updateDuration);

            mesh.subMeshCount = triangleLists.Length;
            for (int sm = 0; sm < triangleLists.Length; ++sm)
            {
                mesh.SetTriangles(triangleLists[sm], sm, false, startIndices[sm]);//TODO calculate bounds later?
            }
            //mesh.RecalculateBounds();
        }
#endif

#if !BATCHING
        void RetrieveInstanceData()
        {
            //Debug.Log("RetrieveInstanceData");
            int variantsLength = variants.Length;
            if (variantsInstancesData == null || variantsInstancesData.Length != variantsLength) return;

            for (int i = 0; i < variantsLength; ++i)
            {
                variantsInstancesData[i].UpdateData();
            }
        }
#endif

#if DEBUG
        public string GetDebug()
        {
#if BATCHING
            int indexCountSum = 0;
            string indexCounts = "";
            int submeshCount = mesh.subMeshCount;
            for (int i = 0; i < submeshCount; ++i)
            {
                indexCounts += "+" + mesh.GetIndexCount(i);
                indexCountSum += (int) mesh.GetIndexCount(i);
            }
            return string.Format("{0} ms, {1} vertices, {2} = {3} indices", updateDuration, mesh.vertexCount, indexCounts, indexCountSum);
#else
            string instanceCounts = "-";
            int variantsLength = variants.Length;
            if (variantsInstancesData == null) return instanceCounts;
            for (int i = 0; i < variantsLength; ++i)
            {
                instanceCounts += variantsInstancesData[i].Count + "-";
            }
            return string.Format("{0} ms, {1} instances", updateDuration, instanceCounts);
#endif
        }
#endif
        }

#if DEBUG
    string[] debugLines;
    public string[] GetDebug()
    {
        int propsMeshesDataLength = propsMeshesData.Length;
        if (debugLines == null || debugLines.Length != propsMeshesDataLength) debugLines = new string[propsMeshesDataLength];
        for(int i = 0; i < propsMeshesDataLength; ++i)
        {
            PropsMeshData pmd = propsMeshesData[i];

            debugLines[i] = pmd.GetDebug();
        }
        return debugLines;
    }
#endif


    void PropsDataOnEnable()
    {
        //for (int i = 0; i < propsMeshesData.Length; ++i) propsMeshesData[i].StopThread();//TODO test if still needed
        //Things should at least start idle
    }

    void PropsDataOnDisable()
    {
        for (int i = 0; i < propsMeshesData.Length; ++i) propsMeshesData[i].StopThread();
    }

#if UNITY_EDITOR
    void PropsDataOnValidate()
    {

    }
#endif
}
