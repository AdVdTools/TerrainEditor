using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Map))]
public class MapEditor : Editor {

    private Map map;
    private MapData data;

    private bool editing;

    private void OnEnable()
    {
        map = target as Map;
        data = map.Data;

        //gridMaterial = new Material(Shader.Find("Hidden/AdVd/GridShader"));
        //gridMaterial.hideFlags = HideFlags.HideAndDontSave;

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

    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        editing = GUILayout.Toggle(editing, new GUIContent("Edit"), EditorStyles.miniButton);
        Tools.hidden = editing;

        if (GUI.changed) SceneView.RepaintAll();
    }

    System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
    float raycastDuration;

    System.Diagnostics.Stopwatch repaintStopWatch = new System.Diagnostics.Stopwatch();
    float repaintPeriod;

    Ray ray;
    bool rayHits;
    Vector3 intersection;
    private const float raycastDistance = 100f;
        
    private void OnSceneGUI()
    {
        Matrix4x4 matrix = map.transform.localToWorldMatrix;
        Matrix4x4 invMatrix = map.transform.worldToLocalMatrix;

        Handles.matrix = matrix;
        
        if (editing)
        {
            //Debug.Log(Event.current.type);
            int controlId = GUIUtility.GetControlID(new GUIContent("MapEditor"), FocusType.Passive);

            if (Event.current.type == EventType.MouseMove) {
                Repaint();
            }
            if (Event.current.type == EventType.Repaint) {//TODO Layout vs Repaint?
                Vector2 screenPoint = Event.current.mousePosition;
                Ray worldRay = HandleUtility.GUIPointToWorldRay(screenPoint);
                ray = new Ray(invMatrix.MultiplyPoint(worldRay.origin),
                    invMatrix.MultiplyVector(worldRay.direction));
                //Event.current.Use();//TODO check LC prefab placer test
            
                //Debug.Log(screenPoint+" "+ray);
                RaycastHit hitInfo;
                //TODO measure and optimize Raycast methods
                stopWatch.Reset();
                stopWatch.Start();
                //rayHits = data.Raycast(ray, out hitInfo, raycastDistance);
                rayHits = data.RaycastParallel(ray, out hitInfo, raycastDistance, 8);
                stopWatch.Stop();
                raycastDuration += (stopWatch.ElapsedMilliseconds - raycastDuration) * 0.5f;
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
            }
            if (Event.current.type == EventType.Layout)
            {//This will allow us to eat the click
                HandleUtility.AddDefaultControl(controlId);
            }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (rayHits)
                {
                    //Undo.RecordObject(map, "Map Changed");//mapData?
                    if (Event.current.control)
                    {
                        // Do spceial stuff
                    }
                    else
                    {
                        // Do stuff
                    }
                }
                Event.current.Use();
            }
            

            Handles.SphereHandleCap(controlId, intersection, Quaternion.identity, 1f, Event.current.type);

            Handles.BeginGUI();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(40f));
            EditorGUILayout.LabelField(string.Format("{0} ms", raycastDuration), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Format("{0} ms", repaintPeriod), EditorStyles.label);
            EditorGUILayout.EndVertical();
            Handles.EndGUI();
        }
    }
    //TODO serialize MapData object!!
    //TODO do raycasting in background jobs?
}
