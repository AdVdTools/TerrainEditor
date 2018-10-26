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
    [SerializeField] MeshFilter[] densityPropsMeshFilters = new MeshFilter[0];
    [SerializeField] Transform povTransform;

    public MapData Data { get { return mapData; } }
    public Transform POVTransform { get { return povTransform; } }

    // Use this for initialization
    void Start()
    {
        //TODO get main camera as pov if none selected?
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
        MapData.DensityPropsMeshData[] densityPropsMeshData = mapData.densityPropsMeshData;
        if (mapData != null)
        {
            Transform povTransform = this.povTransform;
            if (povTransform == null) { Camera mainCam = Camera.main; povTransform = mainCam != null ? mainCam.transform : null; }
            Vector3 pov = povTransform != null ? povTransform.position : default(Vector3);
            pov = transform.InverseTransformPoint(pov);
            Debug.Log(pov);
            mapData.RefreshPropMeshes(pov, 1f);
            int count = Mathf.Min(meshesData.Length, propsMeshFilters.Length);
            for (int i = 0; i < count; ++i)
            {
                MeshFilter meshFilter = propsMeshFilters[i];
                meshFilter.sharedMesh = meshesData[i].sharedMesh;
                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = meshesData[i].sharedMaterial;
            }

            mapData.BkgRefreshDensityPropMeshes(pov, 1f);
            count = Mathf.Min(densityPropsMeshData.Length, densityPropsMeshFilters.Length);
            for (int i = 0; i < count; ++i)
            {
                MeshFilter meshFilter = densityPropsMeshFilters[i];
                meshFilter.sharedMesh = densityPropsMeshData[i].sharedMesh;
                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterials = densityPropsMeshData[i].sharedMaterials;
            }
        }
        else
        {
            for (int i = 0; i < propsMeshFilters.Length; ++i)
            {
                MeshFilter meshFilter = propsMeshFilters[i];
                meshFilter.sharedMesh = null;
            }
            for (int i = 0; i < densityPropsMeshFilters.Length; ++i)
            {
                MeshFilter meshFilter = densityPropsMeshFilters[i];
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
