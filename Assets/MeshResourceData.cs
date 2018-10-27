using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Mesh Resource Data")]
public class MeshResourceData : ScriptableObject
{
    public Mesh mesh;
    public int subMeshIndex;


    [System.NonSerialized] public int verticesCount;
    [System.NonSerialized] public int indicesCount;

    [System.NonSerialized] public List<Vector3> verticesList = new List<Vector3>();
    [System.NonSerialized] public List<Vector3> normalsList = new List<Vector3>();
    [System.NonSerialized] public List<Vector2> uvsList = new List<Vector2>();
    [System.NonSerialized] public List<int> trianglesList = new List<int>();

    //TODO when to load
    public void LoadMeshLists()
    {
        if (mesh == null || subMeshIndex < 0 || subMeshIndex >= mesh.subMeshCount)
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


