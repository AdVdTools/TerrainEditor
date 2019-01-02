using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class Map : MonoBehaviour
{
    [SerializeField] MapData mapData;
    MeshFilter meshFilter;
    
    [SerializeField] MeshFilter[] propsMeshFilters = new MeshFilter[0];
    [SerializeField] Transform povTransform;
    
    [SerializeField] float lodScale = 1f;

    public MapData Data { get { return mapData; } }
    public Transform POVTransform { get { return povTransform; } set { povTransform = value; } }
    public float LODScale { get { return lodScale; } }

    // Use this for initialization
    void Start()
    {
        //TODO get main camera as pov if none selected?
        if (povTransform == null)
        {
            if (!Utils.IsEditMode)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null) povTransform = mainCam.transform;
            }
        }

        Refresh();
        RefreshProps();
    }

    // Update is called once per frame
    void Update()
    {
        if (mapData != null) {
            if (povTransform != null)//TODO avoid refresh from editor while this is running?
            {
                if (!Utils.IsEditMode)
                {
                    Matrix4x4 localToWorld = transform.localToWorldMatrix;
                    mapData.RefreshPropMeshesAsync(transform.InverseTransformPoint(povTransform.position), lodScale, localToWorld);
                }//TODO else follow editcam?, let the editor do the work?, TODO implement disable on multiple failures + callback to handle both in game and in editor
            }

            mapData.DrawPropMeshes();
        }
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
    public void RefreshProps()//TODO remove mesh assign?
    {
        if (mapData != null)
        {
            MapData.PropsMeshData[] propsMeshData = mapData.propsMeshesData;
            Transform povTransform = this.povTransform;
            if (povTransform == null) { Camera mainCam = Camera.main; povTransform = mainCam != null ? mainCam.transform : null; }
            Vector3 pov = povTransform != null ? povTransform.position : default(Vector3);
            pov = transform.InverseTransformPoint(pov);
            //Debug.Log(pov);
            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            mapData.RefreshPropMeshes(pov, 1f, localToWorld);//TODO this is used to ensure meshes mostly
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

        lodScale = Mathf.Clamp(lodScale, 0.001f, 1000);
    }
}
