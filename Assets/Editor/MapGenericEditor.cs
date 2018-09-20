using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
/*
public abstract class MapBuilderEditor : Editor {

    protected MapBuilder builder;
    protected MapData data;
    Material brushProjectorMaterial;
    int mainTexID;
    int mainColorID;
    int projMatrixID;
    int opacityID;

    protected bool editing;

    protected virtual void OnEnable()
    {
        mapProps = target as MapBuilder;
        data = mapProps.Data;

        brushProjectorMaterial = new Material(Shader.Find("Hidden/BrushProjector"));
        brushProjectorMaterial.hideFlags = HideFlags.HideAndDontSave;

        mainTexID = Shader.PropertyToID("_MainTex");
        mainColorID = Shader.PropertyToID("_MainColor");
        projMatrixID = Shader.PropertyToID("_ProjMatrix");
        opacityID = Shader.PropertyToID("_Opacity");

        brushProjectorMaterial.SetColor(mainColorID, Color.green);

        Undo.undoRedoPerformed += OnUndoRedo;
        SceneView.onSceneGUIDelegate += OnSceneHandler;
        Debug.LogWarning("OnEnable");

        //for (int i = 0; i < threads; ++i)
        //{
        //    threadsData[i] = new ThreadData();
        //}
    }

}
*/