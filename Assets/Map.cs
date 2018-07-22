using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class Map : MonoBehaviour
{
    [SerializeField] MapData mapData;
    MeshFilter meshFilter;

    public MapData Data { get { return mapData; } }

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    [ContextMenu("Refresh Mesh")]
    public void Refresh()
    {
        if (mapData != null) meshFilter.sharedMesh = mapData.RefreshMesh();
        else meshFilter.sharedMesh = null;
    }

    private void OnValidate()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    //TODO Editor class that draws gizmos and handles sculpting
}
