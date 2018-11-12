using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lock dataLock when using or updating any data
/// </summary>
[CreateAssetMenu(fileName = "Mesh Resource Data")]
public class MeshResourceData : ScriptableObject
{
    [SerializeField] private Mesh mesh;
    [SerializeField] private int subMeshIndex;

    private bool dirty = false;//Whether lists should be reloaded

    public int verticesCount { get; private set; }
    public int indicesCount { get; private set; }

    [System.NonSerialized] public List<Vector3> verticesList = new List<Vector3>();
    [System.NonSerialized] public List<Vector3> normalsList = new List<Vector3>();
    [System.NonSerialized] public List<Vector2> uvsList = new List<Vector2>();
    [System.NonSerialized] public List<int> trianglesList = new List<int>();

    public readonly object dataLock = new object();

    //TODO when to load
    /// <summary>
    /// This method should not be called on background threads.
    /// Loading might lock users of the data for some time
    /// </summary>
    public void LoadMeshLists()
    {
        lock (dataLock)
        {
            if (MeshListsLoaded() && !dirty) return;//data already loaded and up to date

            Debug.Log(name+" is being loaded");

            if (mesh == null || subMeshIndex < 0 || subMeshIndex >= mesh.subMeshCount)
            {
                verticesCount = indicesCount = 0;//Fail load
                return;
            }
            verticesCount = mesh.vertexCount;
            indicesCount = (int)mesh.GetIndexCount(subMeshIndex);

            //TODO should lists be cleared?

            mesh.GetVertices(verticesList);
            mesh.GetNormals(normalsList);
            mesh.GetUVs(0, uvsList);

            mesh.GetTriangles(trianglesList, subMeshIndex);
            //mesh.GetIndices(indicesList, subMesh);//TODO have many indices arrays / lists

            dirty = false;
        }
    }

    public bool MeshListsLoaded() { return verticesCount > 0 && indicesCount > 0; }

    public void FreeMeshLists() // Just in case
    {
        lock (dataLock)
        {
            if (!MeshListsLoaded() && !dirty) return;

            verticesList = new List<Vector3>();
            normalsList = new List<Vector3>();
            uvsList = new List<Vector2>();
            trianglesList = new List<int>();

            dirty = false;
        }
    }

    void OnValidate()
    {
        dirty = true;
        //if (MeshListsLoaded()) LoadMeshLists();//Reload TODO mind multithreading!! lock somehow?
    }
}


