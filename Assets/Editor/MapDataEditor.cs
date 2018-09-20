using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

//TODO map holder editor and monobehaviour { Meshfilter[] terrainMesh, propsMeshes }
/*
[CustomEditor(typeof(MapData))]
public class MapDataEditor : Editor {

    readonly GUIContent editButtonContent = new GUIContent("Edit");
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MapDataEditorWindow.IsEditing = GUILayout.Toggle(MapDataEditorWindow.IsEditing, editButtonContent, EditorStyles.miniButton);
        if (MapDataEditorWindow.IsEditing) MapDataEditorWindow.SetMap(target as MapData);

        if (GUI.changed) SceneView.RepaintAll();
    }
}
*/