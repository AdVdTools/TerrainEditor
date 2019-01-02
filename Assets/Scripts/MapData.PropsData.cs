#define GPU_INSTANCING

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
        //[HideInInspector] [SerializeField] private int count;//TODO change to List?
        public List<PropInstance> Instances { get { return instances; } }
        public int Count
        {
            get { return instances.Count; }
            //get { return count; }
            //set {
            //    EnsureCapacity(value);
            //    count = value;
            //}
        }
        //public void EnsureCapacity(int capacity)
        //{
        //    if (capacity > instances.Length) Resize(capacity);
        //}
        //public void Minimize()
        //{
        //    Resize(count);
        //}
        //public void Resize(int newSize)
        //{
        //    if (instances.Length != newSize) System.Array.Resize(ref instances, newSize);
        //}
        public void RemoveMarked()
        {
            //int index = 0;
            //while (index < count && instances[index].variantIndex >= 0) ++index;
            //for (int i = index + 1; i < count; ++i)
            //{
            //    PropInstance inst = instances[i];
            //    if (inst.variantIndex >= 0) instances[index++] = inst;
            //}
            //count = index;
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
    
    [ContextMenu("RebuildPropsMeshes")]
    public void RefreshPropMeshes()
    {
        RefreshPropMeshes(default(Vector3), 1f, Matrix4x4.identity);//TODO this is just a method that ensures the existence of the meshes..
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
    public void DrawPropMeshes()//TODO this should be called in edit mode somehow..
    {
        for (int i = 0; i < propsMeshesData.Length; ++i)
        {
            propsMeshesData[i].DrawProps();
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
        private float maxCellOffset = 5f;

        [SerializeField]
        private FloatRange distanceRange = new FloatRange(0, 20);//TODO transition? use shader for that?


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
            
            public int targetSubMesh;
        }

        public abstract class DensityPropsLogic : ScriptableObject
        {
            public abstract PropInstance BuildInstanceData(Vector2 pos, float elementRand, PropDitherPattern.PatternElement element, Vector4 densityValues);
        }


        [SerializeField] int verticesLengthLimit = 900;
        [SerializeField] int trianglesLengthLimit = 900;//indices actually

        [Range(1, 1023)]
        [SerializeField] int variantInstanceLimit = 1000;

        public Material[] sharedMaterials { get { return materials; } }
        [SerializeField] Material[] materials;

        public Mesh sharedMesh { get { return mesh; } }

        Mesh mesh;
#if !GPU_INSTANCING
        List<Vector3> vertices;
        List<Vector3> normals;
        List<Vector4> tangents;
        List<Vector2> uvs;
        List<Vector2> uvs2;//Common sampling point and pivot for each instance
        List<Color> colors;
        List<int>[] triangleLists;
#else
        private struct VariantInstancesData
        {
            private static int _ColorPropertyID = Shader.PropertyToID("_Color");//TODO does this work here?

            //Background workspace
            private List<Matrix4x4> _instanceMatrices;
            private List<Vector4> _instanceColors;
            
            public List<Matrix4x4> instanceMatrices;
            public MaterialPropertyBlock instanceProperties;

            public int InternalCount { get { return _instanceMatrices.Count; } }

            /// <summary>
            /// Called from the main thread before the work on the building thread starts.
            /// Do NOT change capacity for each list independently!
            /// </summary>
            public void Initialize(int capacity)//TODO try growing size?
            {
                if (_instanceMatrices == null) _instanceMatrices = new List<Matrix4x4>(capacity);
                else if (_instanceMatrices.Capacity != capacity) _instanceMatrices.Capacity = capacity;
                if (_instanceColors == null) _instanceColors = new List<Vector4>(capacity);
                else if (_instanceColors.Capacity != capacity) _instanceColors.Capacity = capacity;

                //TODO make sure everything is used in its proper thread and time!
                //TODO comment
                
                _instanceMatrices.Clear();
                _instanceColors.Clear();

                if (instanceMatrices == null) instanceMatrices = new List<Matrix4x4>(capacity);
                else if (instanceMatrices.Capacity != capacity) instanceMatrices.Capacity = capacity;
                if (instanceProperties == null) instanceProperties = new MaterialPropertyBlock();
            }

            /// <summary>
            /// Called from the main thread when the work on the building thread is done
            /// </summary>
            public void UpdateData()
            {
                instanceMatrices.Clear();
                instanceProperties.Clear();

                instanceMatrices.AddRange(_instanceMatrices);
                instanceProperties.SetVectorArray(_ColorPropertyID, _instanceColors);
            }

            /// <summary>
            /// Called from the building thread
            /// </summary>
            /// <param name="matrix"></param>
            /// <param name="color"></param>
            public void AddInstance(Matrix4x4 matrix, Vector4 color)
            {
                _instanceMatrices.Add(matrix);
                _instanceColors.Add(color);
            }
        }
        VariantInstancesData[] variantsInstancesData;
#endif

        MapData mapData;
        Matrix4x4 localToWorld;//TODO split in "bkg working data" (for building) and "current mesh data" (for drawing)
        Vector3 pov;//TODO include pov and lod in the split? update "current" on updatemesh (or equivalent)
        float lodScale = 1f;

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
                    this.localToWorld = localToWorld;
                    this.pov = pov;//TODO transform pov to local here? or not
                    this.lodScale = lodScale;

#if !GPU_INSTANCING
                    LoadMeshLists();
                    InitializeLists();
                    currentUpdateState = UpdateState.Updating;
                    Debug.Log("ForceUpdate");
                    PropsUpdate();//TODO toggle?
                    UpdateMesh();
#else
                    InitializeVariantsInstancesData();
                    currentUpdateState = UpdateState.Updating;
                    Debug.Log("ForceUpdate");
                    PropsBuildData();
                    RetrieveInstanceData();
#endif

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

        public void DrawProps()
        {
#if !GPU_INSTANCING
            PropsDraw();       
#else
            PropsDrawInstanced();
#endif
        }

        public void CheckPropsUpdate(Vector3 pov, float lodScale, MapData mapData, Matrix4x4 localToWorld)
        {
            lock (updateStateLock)
            {
                if (currentUpdateState == UpdateState.Ready)
                {
#if !GPU_INSTANCING
                    //Vertices/Indices ready, build mesh
                    UpdateMesh();
#else
                    RetrieveInstanceData();
#endif

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
                        this.localToWorld = localToWorld;
                        this.pov = pov;//TODO transform pov to local here? or not
                        this.lodScale = lodScale;

#if !GPU_INSTANCING
                        LoadMeshLists();
                        InitializeLists();
#else
                        InitializeVariantsInstancesData();
#endif

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
#if !GPU_INSTANCING
                            PropsUpdate();
#else
                            PropsBuildData();
#endif
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
                shouldThreadRun = true;
                rebuildThread.Start();
                Debug.LogWarning("Started Thread");
            }

            mre.Set();//Run another iteration
        }

#if !GPU_INSTANCING
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
            if (tangents == null) tangents = new List<Vector4>();
            if (uvs == null) uvs = new List<Vector2>();
            if (uvs2 == null) uvs2 = new List<Vector2>();
            if (colors == null) colors = new List<Color>();
            
            int subMeshCount = materials.Length;
            
            if (triangleLists == null || triangleLists.Length != subMeshCount) {
                triangleLists = new List<int>[subMeshCount];
                for (int i = 0; i < subMeshCount; ++i) triangleLists[i] = new List<int>();
            }

            // Clear lists
            vertices.Clear();
            normals.Clear();
            tangents.Clear();
            uvs.Clear();
            uvs2.Clear();
            colors.Clear();
            for (int i = 0; i < subMeshCount; ++i) triangleLists[i].Clear();       
        }
#else
        void InitializeVariantsInstancesData()
        {
            int variantsLength = variants.Length;
            if (variantsInstancesData == null || variantsInstancesData.Length != variantsLength)
            {
                variantsInstancesData = new VariantInstancesData[variantsLength];
            }
            for (int i = 0; i < variantsLength; ++i) variantsInstancesData[i].Initialize(variantInstanceLimit);
        }
#endif

        System.Diagnostics.Stopwatch updateStopWatch = new System.Diagnostics.Stopwatch();
        float updateDuration;

#if !GPU_INSTANCING
        void PropsUpdate()
        {
            updateStopWatch.Reset();
            updateStopWatch.Start();

            int vertexIndex = 0;
            int indexIndex = 0;

            if (instanceSetIndex >= 0 && instanceSetIndex < mapData.instanceSets.Length)
            {
                InstanceSet instanceSet = mapData.instanceSets[instanceSetIndex];
                int instanceCount = instanceSet.Count;
                List<PropInstance> instances = instanceSet.Instances;//We could sync this, but it should not be a problem outside the edtir

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

                DensityPropsLogic currentPropsLogic = propsLogic;
                //System.Func<Vector2, float, PropDitherPattern.PatternElement, Vector4, PropInstance> BuildInstanceData = currentPropsLogic.BuildInstanceData;//No apparent improvement

                Vector2 elementPosition;
                Vector4 densityValues;
                for (int e = 0; e < elements.Length; ++e)
                {
                    float elementRand = (float)(e + 1) / elementsLength;

                    elementPosition = GetElementPosition(povCellX, povCellY, elements[e]);
                    densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                    DoInstance(currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues), ref vertexIndex, ref indexIndex);
                }
                
                for (int offset = 1; offset <= maxCellOffset; ++offset)
                {
                    //int sideLength = offset * 2;
                    for (int offset2 = 1 - offset; offset2 <= offset; ++offset2)
                    {
                        for (int e = 0; e < elements.Length; ++e)
                        {
                            float elementRand = (float)(e + 1) / elementsLength;

                            elementPosition = GetElementPosition(povCellX + offset, povCellY + offset2, elements[e]);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            DoInstance(currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues), ref vertexIndex, ref indexIndex);
                            elementPosition = GetElementPosition(povCellX - offset2, povCellY + offset, elements[e]);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            DoInstance(currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues), ref vertexIndex, ref indexIndex);
                            elementPosition = GetElementPosition(povCellX - offset, povCellY - offset2, elements[e]);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            DoInstance(currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues), ref vertexIndex, ref indexIndex);
                            elementPosition = GetElementPosition(povCellX + offset2, povCellY - offset, elements[e]);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            DoInstance(currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues), ref vertexIndex, ref indexIndex);
                        }
                    }
                }
            }
            
            updateStopWatch.Stop();
            updateDuration += (updateStopWatch.ElapsedMilliseconds - updateDuration) * 0.5f;
        }
#else
        /// <summary>
        /// Builds each instance data
        /// </summary>
        void PropsBuildData()
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

                int povCellX = Mathf.RoundToInt(pov.x / patternScale);
                int povCellY = Mathf.RoundToInt(pov.z / patternScale);

                MapTexture densityMapTexture = mapData.mapTextures[densityMapIndex];//TODO null checks in all classes?

                DensityPropsLogic currentPropsLogic = propsLogic;
                //System.Func<Vector2, float, PropDitherPattern.PatternElement, Vector4, PropInstance> BuildInstanceData = currentPropsLogic.BuildInstanceData;//No apparent improvement

                Vector2 elementPosition;
                Vector4 densityValues;
                for (int e = 0; e < elements.Length; ++e)
                {
                    float elementRand = (float)(e + 1) / elementsLength;

                    elementPosition = GetElementPosition(povCellX, povCellY, elements[e]);
                    densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                    densityValues = Vector4.one;//TODO test, remove
                    DoInstance(/*new PropInstance() { variantIndex = 4, size = 0.1f, position = new Vector3(elementPosition.x, 1f, elementPosition.y) }*/ currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues));
                }

                for (int offset = 1; offset <= maxCellOffset; ++offset)
                {
                    //int sideLength = offset * 2;
                    for (int offset2 = 1 - offset; offset2 <= offset; ++offset2)
                    {
                        for (int e = 0; e < elements.Length; ++e)
                        {
                            float elementRand = (float)(e + 1) / elementsLength;

                            elementPosition = GetElementPosition(povCellX + offset, povCellY + offset2, elements[e]);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            densityValues = Vector4.one;//TODO test, remove
                            DoInstance(/*new PropInstance() { variantIndex = 0, size = 0.1f, position = new Vector3(elementPosition.x, 1f, elementPosition.y) }*/ currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues) );
                            elementPosition = GetElementPosition(povCellX - offset2, povCellY + offset, elements[e]);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            densityValues = Vector4.one;//TODO test, remove
                            DoInstance(/*new PropInstance() { variantIndex = 1, size = 0.1f, position = new Vector3(elementPosition.x, 1f, elementPosition.y) }*/ currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues) );
                            elementPosition = GetElementPosition(povCellX - offset, povCellY - offset2, elements[e]);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            densityValues = Vector4.one;//TODO test, remove
                            DoInstance(/*new PropInstance() { variantIndex = 2, size = 0.1f, position = new Vector3(elementPosition.x, 1f, elementPosition.y) }*/ currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues));
                            elementPosition = GetElementPosition(povCellX + offset2, povCellY - offset, elements[e]);
                            densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
                            densityValues = Vector4.one;//TODO test, remove
                            DoInstance(/*new PropInstance() { variantIndex = 3, size = 0.1f, position = new Vector3(elementPosition.x, 1f, elementPosition.y) }*/ currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues));
                        }
                    }
                }
            }

            // 0. Base loop, GetElementPosition, null instance:                              ~30 ms
            // 1. Base loop, GetElementPosition + DoInstance:                             60-110 ms (+ ~45 ms)
            // 2. Base loop, GetElementPosition + density sample, null instance:          40-100 ms (+ ~35 ms)
            // 3. Base loop, GetElementPosition + density sample + DoInstance:          ~160+-40 ms (+ ~130 ms) 3. > 2. + 1.
            // 4. Full loop, including BuildInstanceData (may vary), null instance:       ~45-50 ms (+ ~20 ms) this should be slower than 2.!
            // 5. Full loop, including BuildInstanceData (may vary) + DoInstance:  ~130-185-280+ ms (+ ~155 ms)


            // DoInstance includes height and normal sample + 2 list adds + other operations
            // Sampling math can be reused! or samplings can be merged in a single call

            // TODO faster sampling?

            updateStopWatch.Stop();
            updateDuration += (updateStopWatch.ElapsedMilliseconds - updateDuration) * 0.5f;

            Debug.Log("PropsInstanceDataBuild: " + updateDuration);
        }
#endif

#if !GPU_INSTANCING
        //TODO more submeshes?
        void PropsDraw()
        {
            int materialsLength = materials.Length;
            for (int i = 0; i < materialsLength; ++i)
            {
                Graphics.DrawMesh(mesh, localToWorld, materials[i], 0, null, i, null, ShadowCastingMode.Off, true, null, LightProbeUsage.Off);
            }
        }
#else
        /// <summary>
        /// Call Graphics.DrawMesh in main thread and rely on gpu instancing material?
        /// </summary>
        void PropsDrawInstanced()//TODO draw meshes with Graphics.DrawMesh if GPU_INSTANCING disabled?
        {
            /*DrawMesh*/
            //int colorPropertyID = Shader.PropertyToID("_Color");//TODO cache further?
            //MaterialPropertyBlock properties = new MaterialPropertyBlock();//TODO save this allocation? but clear in here
            

            int variantsLength = variants.Length;
            if (variantsInstancesData == null || variantsInstancesData.Length != variantsLength) return;


            for (int i = 0; i < variantsLength; ++i)
            {
                Variant variant = variants[i];
                VariantInstancesData instancesData = variantsInstancesData[i];

                if (instancesData.instanceMatrices == null)
                {
                    Debug.LogErrorFormat("VariantInstancesData at {0} is not initialized!", i);
                    continue;
                }
                if (instancesData.instanceMatrices.Count == 0) continue;
                //MaterialPropertyBlock properties = new MaterialPropertyBlock();// store in VariantInstancesData?, save one of the lists, have the aux matrix list in there too?

                /*DrawMeshInstanced*/
                //TODO test on device/android
                for (int r = 0; r < variant.meshResources.Length; ++r)
                {
                    MeshResource meshResource = variant.meshResources[r];
                    MeshResourceData meshData = meshResource.data;
                    if (meshData == null) continue;

                    //TODO shadows options for both gpu instanced and mesh renderers, and configurable properties added? (color/lodBlend?)
                    Graphics.DrawMeshInstanced(meshData.sharedMesh, meshData.SubMeshIndex, materials[meshResource.targetSubMesh], instancesData.instanceMatrices, instancesData.instanceProperties, ShadowCastingMode.Off, true, 0, null, LightProbeUsage.Off);
                }

                /*DrawMesh*/
                //int instanceCount = instancesData.instanceMatrices.Count;
                //for (int instanceIndex = 0; instanceIndex < instanceCount; ++instanceIndex)
                //{
                //    properties.SetColor(colorPropertyID, instancesData.instanceColors[instanceIndex]);

                //    Matrix4x4 matrix = instancesData.instanceMatrices[instanceIndex];

                //    for (int r = 0; r < variant.meshResources.Length; ++r)
                //    {
                //        MeshResource meshResource = variant.meshResources[r];
                //        MeshResourceData meshData = meshResource.data;
                //        if (meshData == null) continue;
                        
                //        //TODO shadows options for both gpu instanced and mesh renderers
                //        Graphics.DrawMesh(meshData.sharedMesh, matrix, materials[meshResource.targetSubMesh], 0, null, meshData.SubMeshIndex, properties, false, true, false);
                //    }
                //}
            }

            //int colorPropertyID = Shader.PropertyToID("_Color");
            //MaterialPropertyBlock properties = new MaterialPropertyBlock();//TODO save this allocation?
            //Matrix4x4 matrix = Matrix4x4.TRS(realPosition, Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.Euler(0, rotation, 0), new Vector3(size, size, size));//TODO Optimize?

            //properties.SetColor(colorPropertyID, instance.tint);

            //for (int r = 0; r < variant.meshResources.Length; ++r)
            //{
            //    MeshResource meshResource = variant.meshResources[r];
            //    MeshResourceData meshData = meshResource.data;
            //    if (meshData == null) continue;
            //    lock (meshData.dataLock)
            //    {
            //        if (!meshResource.distanceRange.CheckInRange(distance)) continue;

            //        Graphics.DrawMesh(meshData.sharedMesh, localToWorld * matrix, materials[meshResource.targetSubMesh], 0, null, meshData.SubMeshIndex, properties, false, true, false);
            //    }
            //}
        
    


            //updateStopWatch.Reset();
            //updateStopWatch.Start();

            //if (instanceSetIndex >= 0 && instanceSetIndex < mapData.instanceSets.Length)
            //{
            //    InstanceSet instanceSet = mapData.instanceSets[instanceSetIndex];
            //    int instanceCount = instanceSet.Count;
            //    List<PropInstance> instances = instanceSet.Instances;//We could sync this, but it should not be a problem outside the edtir

            //    for (int i = 0; i < instanceCount; ++i)
            //    {
            //        PropInstance instance = instances[i];

            //        DrawInstance(instance);
            //    }
            //}


            //if (pattern != null && propsLogic != null && densityMapIndex >= 0 && densityMapIndex < mapData.mapTextures.Length)
            //{
            //    PropDitherPattern.PatternElement[] elements = pattern.elements;
            //    int elementsLength = elements.Length;

            //    int povCellX = Mathf.RoundToInt(pov.x / patternScale);
            //    int povCellY = Mathf.RoundToInt(pov.z / patternScale);

            //    MapTexture densityMapTexture = mapData.mapTextures[densityMapIndex];//TODO null checks in all classes?

            //    DensityPropsLogic currentPropsLogic = propsLogic;
            //    //System.Func<Vector2, float, PropDitherPattern.PatternElement, Vector4, PropInstance> BuildInstanceData = currentPropsLogic.BuildInstanceData;//No apparent improvement

            //    Vector2 elementPosition;
            //    Vector4 densityValues;
            //    for (int e = 0; e < elements.Length; ++e)
            //    {
            //        float elementRand = (float)(e + 1) / elementsLength;

            //        elementPosition = GetElementPosition(povCellX, povCellY, elements[e]);
            //        densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
            //        DrawInstance(currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues));
            //    }

            //    for (int offset = 1; offset <= maxCellOffset; ++offset)
            //    {
            //        //int sideLength = offset * 2;
            //        for (int offset2 = 1 - offset; offset2 <= offset; ++offset2)
            //        {
            //            for (int e = 0; e < elements.Length; ++e)
            //            {
            //                float elementRand = (float)(e + 1) / elementsLength;

            //                elementPosition = GetElementPosition(povCellX + offset, povCellY + offset2, elements[e]);
            //                densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
            //                DrawInstance(currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues));
            //                elementPosition = GetElementPosition(povCellX - offset2, povCellY + offset, elements[e]);
            //                densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
            //                DrawInstance(currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues));
            //                elementPosition = GetElementPosition(povCellX - offset, povCellY - offset2, elements[e]);
            //                densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
            //                DrawInstance(currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues));
            //                elementPosition = GetElementPosition(povCellX + offset2, povCellY - offset, elements[e]);
            //                densityValues = densityMapTexture.SampleValue(elementPosition.x, elementPosition.y, mapData);
            //                DrawInstance(currentPropsLogic.BuildInstanceData(elementPosition, elementRand, elements[e], densityValues));
            //            }
            //        }
            //    }
            //}

            //updateStopWatch.Stop();
            //updateDuration += (updateStopWatch.ElapsedMilliseconds - updateDuration) * 0.5f;

            //Debug.Log("PropsDraw: " + updateDuration);
        }
#endif

        Vector2 GetElementPosition(int cellX, int cellY, PropDitherPattern.PatternElement element)
        {
            return (new Vector2(cellX, cellY) + element.pos / PropDitherPattern.CellSize) * patternScale;
        }

#if !GPU_INSTANCING
        void DoInstance(PropInstance instance, ref int vertexIndex, ref int indexIndex)
        {
            if (instance.variantIndex < 0 || instance.variantIndex >= variants.Length) return;
            Variant variant = variants[instance.variantIndex];

            Vector3 position = instance.position;
            Vector2 position2D = new Vector2(position.x, position.z);

            Vector3 realPosition = mapData.GetRealInstancePosition(position);
            float sqrDistance = (realPosition - pov).sqrMagnitude * lodScale * lodScale;

            if (!distanceRange.CheckInSqrRange(sqrDistance)) return;

            Vector3 terrainNormal = mapData.SampleNormals(realPosition.x, realPosition.z);

            Vector3 direction = Vector3.Slerp(variant.propsDirection, terrainNormal, instance.alignment);
            float rotation = instance.rotation;
            float size = instance.size;
            Color tint = instance.tint;

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

                    Matrix4x4 matrix = Matrix4x4.TRS(realPosition, Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.Euler(0, rotation, 0), new Vector3(size, size, size));//TODO Optimize?
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
                    if (meshData.uvsList.Count == meshData.verticesCount)//If uvs loaded
                    {
                        for (int i = 0; i < meshData.verticesCount; ++i)
                        {
                            Vector2 uv = meshData.uvsList[i];
                            uvs.Add(uv);
                            uvs2.Add(position2D);
                        }
                    }
                    else
                    {
                        Vector2 zero = default(Vector2);
                        for (int i = 0; i < meshData.verticesCount; ++i)
                        {
                            uvs.Add(zero);
                            uvs2.Add(position2D);
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
                    indexIndex += meshData.indicesCount;
                }
            }
        }
#else
        void DoInstance(PropInstance instance)
        {
            //return;//TODO TEST
            if (instance.variantIndex < 0 || instance.variantIndex >= variants.Length) return;
            Variant variant = variants[instance.variantIndex];

            VariantInstancesData instancesData = variantsInstancesData[instance.variantIndex];
            if (instancesData.InternalCount >= variantInstanceLimit) return;

            Vector3 position = instance.position;
            Vector2 position2D = new Vector2(position.x, position.z);

            Vector3 realPosition = mapData.GetRealInstancePosition(position);
            float sqrDistance = (realPosition - pov).sqrMagnitude * lodScale * lodScale;
            
            if (!distanceRange.CheckInSqrRange(sqrDistance)) return;

            Vector3 terrainNormal = mapData.SampleNormals(realPosition.x, realPosition.z);

            Vector3 direction = Vector3.Slerp(variant.propsDirection, terrainNormal, instance.alignment);
            float rotation = instance.rotation;
            float size = instance.size;
            Color tint = instance.tint;

            Matrix4x4 matrix = Matrix4x4.TRS(realPosition, Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.Euler(0, rotation, 0), new Vector3(size, size, size));//TODO Optimize?
            
            instancesData.AddInstance(localToWorld * matrix, tint);
        }
#endif
        //void DrawInstance(PropInstance instance)
        //{
        //    TODO?
        //    //if (instance.variantIndex < 0 || instance.variantIndex >= variants.Length) return;
        //    //Variant variant = variants[instance.variantIndex];

        //    //Vector3 position = instance.position;
        //    //Vector2 position2D = new Vector2(position.x, position.z);

        //    //Vector3 realPosition = mapData.GetRealInstancePosition(position);
        //    //float distance = (realPosition - pov).magnitude * lodScale;

        //    //Vector3 terrainNormal = mapData.SampleNormals(realPosition.x, realPosition.z);

        //    //Vector3 direction = Vector3.Slerp(variant.propsDirection, terrainNormal, instance.alignment);
        //    //float rotation = instance.rotation;
        //    //float size = instance.size;
        //    //Color tint = instance.tint;

        //    //int colorPropertyID = Shader.PropertyToID("_Color");
        //    //MaterialPropertyBlock properties = new MaterialPropertyBlock();//TODO save this allocation?
        //    //Matrix4x4 matrix = Matrix4x4.TRS(realPosition, Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.Euler(0, rotation, 0), new Vector3(size, size, size));//TODO Optimize?

        //    //properties.SetColor(colorPropertyID, instance.tint);

        //    //for (int r = 0; r < variant.meshResources.Length; ++r)
        //    //{
        //    //    MeshResource meshResource = variant.meshResources[r];
        //    //    MeshResourceData meshData = meshResource.data;
        //    //    if (meshData == null) continue;
        //    //    lock (meshData.dataLock)
        //    //    {
        //    //        if (!meshResource.distanceRange.CheckInRange(distance)) continue;
                    
        //    //        Graphics.DrawMesh(meshData.sharedMesh, localToWorld * matrix, materials[meshResource.targetSubMesh], 0, null, meshData.SubMeshIndex, properties, false, true, false);
        //    //    }
        //    //}
        //}

#if !GPU_INSTANCING
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
            mesh.SetTangents(tangents);//mesh.tangents = tangents;
            mesh.SetUVs(0, uvs);//mesh.uv = uvs;
            mesh.SetUVs(1, uvs2);//mesh.uv2 = uvs2;
            mesh.SetColors(colors);//mesh.colors = colors;

            Debug.Log("BTW: " + updateDuration);

            mesh.subMeshCount = triangleLists.Length;
            for (int sm = 0; sm < triangleLists.Length; ++sm)
            {
                mesh.SetTriangles(triangleLists[sm], sm, false);//TODO calculate bounds later?
            }
            //mesh.RecalculateBounds();
        }
#else
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
        //TODO load mapTextures referenced by prop meshes data (avoid redundant loading, here and with mesh color) (if !EDITOR?)
    }
}
