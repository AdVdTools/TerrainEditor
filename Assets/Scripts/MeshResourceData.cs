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

    public Mesh sharedMesh { get { return mesh; } }
    public int SubMeshIndex { get { return subMeshIndex; } }

    private bool dirty = false;//Whether lists should be reloaded

    public int verticesCount { get; private set; }
    public int indicesCount { get; private set; }

    [System.NonSerialized] public List<Vector3> verticesList = new List<Vector3>();
    [System.NonSerialized] public List<Vector3> normalsList = new List<Vector3>();
    [System.NonSerialized] public List<Vector4> tangentsList = new List<Vector4>();
    [System.NonSerialized] public List<Vector2> uvsList = new List<Vector2>();
    [System.NonSerialized] public List<Color> colorsList = new List<Color>();
    [System.NonSerialized] public List<int> trianglesList = new List<int>();

    public readonly object dataLock = new object();
    
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
            
            mesh.GetVertices(verticesList);
            mesh.GetNormals(normalsList);
            mesh.GetTangents(tangentsList);
            mesh.GetUVs(0, uvsList);
            mesh.GetColors(colorsList);

            mesh.GetTriangles(trianglesList, subMeshIndex);

            // Remove unused vertices
            RemoveUnusedVertices();

            dirty = false;
        }
    }

    private void RemoveUnusedVertices()//TODO minimize list capacity?
    {
        bool hasColors = colorsList.Count == verticesCount;
        int index;
        for (index = 0; index < verticesCount; ++index)
        {
            bool found = false;
            for (int i = 0; i < indicesCount; ++i) if (found = (trianglesList[i] == index)) break;
            if (!found) break;
        }
        int newIndex = index;
        if (index < verticesCount)
        {
            for (index = newIndex + 1; index < verticesCount; ++index)
            {
                bool found = false;
                int i;
                for (i = 0; i < indicesCount; ++i) if (found = (trianglesList[i] == index)) break;
                if (found)
                {
                    for (; i < indicesCount; ++i) if (trianglesList[i] == index) trianglesList[i] = newIndex;
                    verticesList[newIndex] = verticesList[index];
                    normalsList[newIndex] = normalsList[index];
                    tangentsList[newIndex] = tangentsList[index];
                    uvsList[newIndex] = uvsList[index];
                    if (hasColors) colorsList[newIndex] = colorsList[index];
                    newIndex++;
                }
            }
        }
        int verticesToRemove = index - newIndex;
        if (verticesToRemove > 0)
        {
            verticesList.RemoveRange(newIndex, verticesToRemove);
            normalsList.RemoveRange(newIndex, verticesToRemove);
            tangentsList.RemoveRange(newIndex, verticesToRemove);
            uvsList.RemoveRange(newIndex, verticesToRemove);
            if (hasColors) colorsList.RemoveRange(newIndex, verticesToRemove);
            verticesCount = newIndex;
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
            tangentsList = new List<Vector4>();
            uvsList = new List<Vector2>();
            colorsList = new List<Color>();
            trianglesList = new List<int>();

            dirty = false;
        }
    }

    void OnValidate()
    {
        dirty = true;
    }
}


