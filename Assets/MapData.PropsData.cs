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
        /*[HideInInspector]*/ [SerializeField] private List<PropInstance> instances = new List<PropInstance>();
        public List<PropInstance> Instances { get { return instances; } }
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
        List<PropInstance> instances = instanceSet.Instances;
        int instanceCount = instances.Count;
        if (instanceSet.instancePositions == null || instanceSet.instancePositions.Length != instanceCount) instanceSet.instancePositions = new Vector3[instanceCount];

        for (int j = 0; j < instanceCount; ++j)
        {
            PropInstance instance = instances[j];

            Vector3 relativePosition = instance.position;
            float height = mapData.SampleHeight(relativePosition.x, relativePosition.z);
            Vector3 realPosition = new Vector3(relativePosition.x, height, relativePosition.z);//TODO get realPosition from instance.position
            realPosition += instance.direction * relativePosition.y;

            instanceSet.instancePositions[j] = realPosition;
        }
    }

    public void RecalculateInstanceDistances(int threads, InstanceSet instanceSet, Vector3 pov = default(Vector3), float lodScale = 1f)
    {
        List<PropInstance> instances = instanceSet.Instances;
        int instanceCount = instances.Count;
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
        for (int i = 0; i < instanceSets.Length; ++i)
        {
            RecalculateInstancePositions(1, instanceSets[i], this);
            RecalculateInstanceDistances(1, instanceSets[i]);
        }
        for (int i = 0; i < meshesData.Length; ++i)
        {
            meshesData[i].RebuildParallel(1, this);//TODO Y?
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
            Debug.Log("HERE");
            //TODO stuff get lengts, load meshes
            //int verticesLength = 0, indicesLength = 0;
            //for (int i = 0; i < meshesData.Length; ++i)
            //{
            //    MeshData meshData = meshesData[i];
            //    verticesLength += meshData.verticesLimit;// meshData.Instances.Count * meshData.mesh.vertexCount;
            //    indicesLength += meshData.trianglesLimit * 3;// meshData.Instances.Count * (int) meshData.mesh.GetIndexCount(0);
            //                                                 // TODO support multiple submeshes?
            //}

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
                        Debug.Log("??");
                        if (variant.instanceSetIndex < 0 || variant.instanceSetIndex >= mapData.instanceSets.Length) continue;
                        InstanceSet instanceSet = mapData.instanceSets[variant.instanceSetIndex];
                        Debug.Log("Doing variantLOD");
                        DoVariantLOD(instanceSet, variant, lod, ref vertexIndex, ref indexIndex);
                        existingLOD = true;
                    }
                }
                lod++;
            } while(existingLOD);
            
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
            
            List<PropInstance> instances = instanceSet.Instances;
            int instanceCount = instances.Count;
            float[] sqrDistances = instanceSet.instanceSqrDistances;//Precalculated
            Vector3[] realPositions = instanceSet.instancePositions;//TODO mind multithreading

            if (meshLOD.subMeshIndex < 0 || meshLOD.subMeshIndex >= meshLOD.mesh.subMeshCount) return;
            int verticesInMesh = meshLOD.mesh.vertexCount;
            int indicesInMesh = (int) meshLOD.mesh.GetIndexCount(meshLOD.subMeshIndex);
            
            meshLOD.mesh.GetVertices(auxVerticesList);
            meshLOD.mesh.GetNormals(auxNormalsList);
            meshLOD.mesh.GetUVs(0, auxUVsList);

            meshLOD.mesh.GetTriangles(auxTrianglesList, meshLOD.subMeshIndex);
            //meshLOD.mesh.GetIndices(auxIndicesList, subMesh);//TODO have many indices arrays / lists
            Debug.Log("Checking instances "+instanceCount);
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
    
}
