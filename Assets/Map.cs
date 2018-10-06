using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class Map : MonoBehaviour
{
    [SerializeField] MapData mapData;
    MeshFilter meshFilter;

    [SerializeField] MeshFilter[] propsMeshFilters = new MeshFilter[0];
    [SerializeField] Camera povCamera;

    public MapData Data { get { return mapData; } }
    public Camera POVCamera { get { return povCamera; } }

    // Use this for initialization
    void Start()
    {
        //TODO get main camera if none selected?
    }

    // Update is called once per frame
    void Update()
    {

    }

    [ContextMenu("Refresh Mesh")]
    public void Refresh()
    {
        if (mapData != null) meshFilter.sharedMesh = mapData.RefreshTerrainMesh();
        else meshFilter.sharedMesh = null;
    }

    [ContextMenu("Refresh Props Meshes")]
    public void RefreshProps()
    {
        MapData.MeshData[] meshesData = mapData.meshesData;
        if (mapData != null)
        {
            Camera povCamera = this.povCamera;
            if (povCamera == null) povCamera = Camera.main;
            Vector3 pov = povCamera != null ? povCamera.transform.position : default(Vector3);
            pov = transform.InverseTransformPoint(pov);
            Debug.Log(pov);
            mapData.RefreshPropMeshes(pov, 1f);
            int count = Mathf.Min(mapData.meshesData.Length, propsMeshFilters.Length);
            for (int i = 0; i < count; ++i)
            {
                MeshFilter meshFilter = propsMeshFilters[i];
                meshFilter.sharedMesh = meshesData[i].sharedMesh;
                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = meshesData[i].sharedMaterial;
            }
        }
        else
        {
            for (int i = 0; i < propsMeshFilters.Length; ++i)
            {
                MeshFilter meshFilter = propsMeshFilters[i];
                meshFilter.sharedMesh = null;
            }
        }
    }

    private void OnValidate()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    //TODO Editor class that draws gizmos and handles sculpting
}
