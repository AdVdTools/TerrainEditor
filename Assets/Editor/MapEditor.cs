using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Map))]
public class MapEditor : Editor {

    private Map map;
    private MapData data;
    Material brushProjectorMaterial;
    int mainColorID;
    int projMatrixID;
    
    private bool editing;

    private void OnEnable()
    {
        map = target as Map;
        data = map.Data;

        brushProjectorMaterial = new Material(Shader.Find("Hidden/BrushProjector"));
        brushProjectorMaterial.hideFlags = HideFlags.HideAndDontSave;
        
        mainColorID = Shader.PropertyToID("_MainColor");
        projMatrixID = Shader.PropertyToID("_ProjMatrix");
        
        brushProjectorMaterial.SetColor(mainColorID, Color.green);

        //hexMesh = new Mesh();
        //hexMesh.hideFlags = HideFlags.HideAndDontSave;
        //Vector3[] vertices = new Vector3[6];
        //float cos30 = Mathf.Sqrt(3) / 2;
        //vertices[0] = new Vector3(1, 0, 0); vertices[1] = new Vector3(0.5f, 0, -cos30); vertices[2] = new Vector3(-0.5f, 0, -cos30);
        //vertices[3] = new Vector3(-1, 0, 0); vertices[4] = new Vector3(-0.5f, 0, cos30); vertices[5] = new Vector3(0.5f, 0, cos30);
        //int[] lineIndices = new int[7];
        //for (int i = 0; i < 6; ++i) lineIndices[i] = i;//Last index remains 0
        //int[] areaIndices = new int[8];
        //for (int i = 0; i < 8; ++i) areaIndices[i] = (i - (i & 4) / 4) % 6;//TODO unwrap, you silly
        //hexMesh.subMeshCount = 2;
        //hexMesh.vertices = vertices;
        //hexMesh.SetIndices(lineIndices, MeshTopology.LineStrip, 0);
        //hexMesh.SetIndices(areaIndices, MeshTopology.Quads, 1);
    }

    private void OnDisable()
    {
        //if (gridMaterial != null) DestroyImmediate(gridMaterial, false);
        if (brushProjectorMaterial != null) DestroyImmediate(brushProjectorMaterial, false);
    }

    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        editing = GUILayout.Toggle(editing, new GUIContent("Edit"), EditorStyles.miniButton);
        Tools.hidden = editing;
        
        if (GUI.changed) SceneView.RepaintAll();
    }

    System.Diagnostics.Stopwatch raycastStopWatch = new System.Diagnostics.Stopwatch();
    float raycastDuration;

    System.Diagnostics.Stopwatch rebuildStopWatch = new System.Diagnostics.Stopwatch();
    float rebuildDuration;

    System.Diagnostics.Stopwatch repaintStopWatch = new System.Diagnostics.Stopwatch();
    float repaintPeriod;

    Ray ray;
    bool rayHits;
    Vector3 intersection;
    private const float raycastDistance = 200f;
        
    private void OnSceneGUI()
    {
        Matrix4x4 matrix = map.transform.localToWorldMatrix;
        Matrix4x4 invMatrix = map.transform.worldToLocalMatrix;

        Handles.matrix = matrix;
        
        if (editing)
        {
            //Debug.Log(Event.current.type);
            int controlId = GUIUtility.GetControlID(new GUIContent("MapEditor"), FocusType.Passive);

            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag) {
                Repaint();
            }
            if (Event.current.type == EventType.Repaint) {//TODO Layout vs Repaint?
                Vector2 screenPoint = Event.current.mousePosition;
                Ray worldRay = HandleUtility.GUIPointToWorldRay(screenPoint);
                ray = new Ray(invMatrix.MultiplyPoint(worldRay.origin),
                    invMatrix.MultiplyVector(worldRay.direction));
                //Event.current.Use();//TODO check LC prefab placer test
            
                //Debug.Log(screenPoint+" "+ray);
                MapData.RaycastHit hitInfo;
                //TODO measure and optimize Raycast methods
                raycastStopWatch.Reset();
                raycastStopWatch.Start();
                //rayHits = data.Raycast(ray, out hitInfo, raycastDistance);
                rayHits = data.RaycastParallel(ray, out hitInfo, raycastDistance, 8);
                raycastStopWatch.Stop();
                raycastDuration += (raycastStopWatch.ElapsedMilliseconds - raycastDuration) * 0.5f;
                //Debug.Log(intersection+" "+stopWatch.ElapsedMilliseconds);

                if (rayHits) {
                    if (intersection != hitInfo.point) Repaint();
                    intersection = hitInfo.point;//ray.GetPoint(ray.origin.y / -ray.direction.y);//TEMP Y-0 cast
                }
            }
            if (Event.current.type == EventType.Repaint)
            {
                repaintPeriod += (repaintStopWatch.ElapsedMilliseconds - repaintPeriod) * 0.5f;
                repaintStopWatch.Reset();
                repaintStopWatch.Start();

                //Handles.color = Color.red;
                //data.ForEachVertex((index, vertex) =>
                //{
                //    float strength = currentBrush.GetStrength(vertex, intersection);
                //    if (strength > 0) {
                //        Handles.DrawWireCube(vertex, Vector3.one * (strength * 0.5f));
                //    }
                //});
                //Handles.SphereHandleCap(controlId, intersection, Quaternion.identity, 1f, Event.current.type);

                Mesh mesh = data.sharedMesh;
                if (mesh != null)
                {
                    Matrix4x4 projMatrix = Brush.currentBrush.GetProjectionMatrix(intersection, map.transform, SceneView.currentDrawingSceneView.camera);

                    brushProjectorMaterial.SetMatrix(projMatrixID, projMatrix);
                    brushProjectorMaterial.SetPass(0);
                    Graphics.DrawMeshNow(data.sharedMesh, Handles.matrix, 0);
                }
                
            }
            if (Event.current.type == EventType.Layout)
            {//This will allow us to eat the click
                HandleUtility.AddDefaultControl(controlId);
            }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (rayHits)
                {
                    if (Event.current.control)
                    {
                        // Do spceial stuff
                    }
                    else
                    {
                        // Do stuff
                        ApplyBrush();//TODO handle overtime!!
                    }
                }
                Event.current.Use();
            }
            if (Event.current.type == EventType.MouseDrag && Event.current.button == 0)
            {
                if (rayHits)
                {
                    ApplyBrush();//TODO handle overtime!!
                }
                Event.current.Use();
            }
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                data.RebuildParallel(8);//TODO rebuilds normals and other things, not just vertices
                Event.current.Use();
            }

            Brush.DoBrushControls();

            Handles.BeginGUI();
            GUI.skin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(40f));
            EditorGUILayout.LabelField(string.Format("{0} ms", raycastDuration), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Format("{0} ms", rebuildDuration), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Format("{0} ms", repaintPeriod), EditorStyles.label);

            //EditorGUILayout.Space();
            //EditorGUILayout.LabelField(new GUIContent("Proj " + currentBrush.GetProjectionMatrix(intersection, map.transform, SceneView.currentDrawingSceneView.camera) * new Vector4(intersection.x, intersection.y, intersection.z, 1f)));
            EditorGUILayout.EndVertical();

            Brush.DrawBrushWindow();
            Handles.EndGUI();
        }
    }

    void ApplyBrush()
    {
        //Undo.RecordObject(mapData, "Map Changed");//TODO records per MouseUp?
        Brush brush = Brush.currentBrush;
        Matrix4x4 projMatrix = brush.GetProjectionMatrix(intersection, map.transform, SceneView.currentDrawingSceneView.camera);
        float[] heights = data.Heights;
        data.ForEachVertex((index, vertex) =>
        {
            float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
            if (strength > 0)
            {
                //TODO handle data serialization/dirtying somehow and trigger rebuild mesh
                switch (brush.mode)
                {
                    case Brush.Mode.Add:
                        heights[index] += brush.amount * strength;
                        break;
                    case Brush.Mode.Substract:
                        heights[index] -= brush.amount * strength;
                        break;
                    case Brush.Mode.Set:
                        heights[index] += (brush.amount - heights[index]) * strength;
                        break;
                }
                
            }
        });


        rebuildStopWatch.Reset();
        rebuildStopWatch.Start();
        data.QuickRebuildParallel(8);
        rebuildStopWatch.Stop();
        rebuildDuration += (rebuildStopWatch.ElapsedMilliseconds - rebuildDuration) * 0.5f;
        
        Repaint();
    }
    //TODO serialize MapData object!!
    //TODO do raycasting in background jobs?
}
