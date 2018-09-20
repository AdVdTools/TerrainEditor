using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class MapProps : MonoBehaviour {

    [SerializeField] MapData mapData;
    MeshFilter meshFilter;

    public MapData Data { get { return mapData; } }

    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    [ContextMenu("Refresh Mesh")]
    public void Refresh()
    {
        if (mapData != null) meshFilter.sharedMesh = mapData.RefreshTerrainMesh();
        else meshFilter.sharedMesh = null;
    }

    private void OnValidate()
    {
        meshFilter = GetComponent<MeshFilter>();
    }
}
