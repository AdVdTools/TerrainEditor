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
        if (mapData != null)
        {
            meshFilter.sharedMesh = mapData.RefreshTerrainMesh();
            
            MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshRenderer != null) meshRenderer.sharedMaterial = mapData.TerrainMaterial;
        }
        else meshFilter.sharedMesh = null;
    }

    [ContextMenu("Refresh Props Meshes")]
    public void RefreshProps()
    {
        if (mapData != null)
        {
            MapData.PropsMeshData[] propsMeshData = mapData.propsMeshesData;
            Transform povTransform = this.povTransform;
            if (povTransform == null) { Camera mainCam = Camera.main; povTransform = mainCam != null ? mainCam.transform : null; }
            Vector3 pov = povTransform != null ? povTransform.position : default(Vector3);
            pov = transform.InverseTransformPoint(pov);
            //Debug.Log(pov);
            mapData.RefreshPropMeshes(pov, 1f);
            int count = Mathf.Min(propsMeshData.Length, propsMeshFilters.Length);
            for (int i = 0; i < count; ++i)
            {
                MeshFilter meshFilter = propsMeshFilters[i];
                meshFilter.sharedMesh = propsMeshData[i].sharedMesh;
                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterials = propsMeshData[i].sharedMaterials;
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
