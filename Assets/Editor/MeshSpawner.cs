using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/*
public class MeshSpawner : ScriptableObject {

    public class MeshData
    {
        public Mesh mesh;
        public float probability;
        //Extra per mesh config
    }

    [SerializeField]
    private MeshData[] meshes = new MeshData[0];
    public MeshData[] Meshes
    {
        get { return meshes; }
    }
    
    void OnValidate()
    {
        float sum = 0f;
        for (int i = 0; i < meshes.Length; ++i)
        {
            if (meshes[i].probability < 0) meshes[i].probability = 0;
            sum += meshes[i].probability;
        }
        if (sum == 0f)
        {
            float probability = 1f / meshes.Length;
            for (int i = 0; i < meshes.Length; ++i)
            {
                meshes[i].probability = probability;
            }
        }
        else
        {
            for (int i = 0; i < meshes.Length; ++i)
            {
                meshes[i].probability /= sum;
            }
        }
    }

}
*/