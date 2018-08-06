using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Threading;

[CustomEditor(typeof(Map))]
public class MapEditor : Editor {

    private Map map;
    private MapData data;
    Material brushProjectorMaterial;
    int mainColorID;
    int projMatrixID;
    int opacityID;
    
    private bool editing;

    private void OnEnable()
    {
        map = target as Map;
        data = map.Data;

        brushProjectorMaterial = new Material(Shader.Find("Hidden/BrushProjector"));
        brushProjectorMaterial.hideFlags = HideFlags.HideAndDontSave;

        mainColorID = Shader.PropertyToID("_MainColor");
        projMatrixID = Shader.PropertyToID("_ProjMatrix");
        opacityID = Shader.PropertyToID("_Opacity");

        brushProjectorMaterial.SetColor(mainColorID, Color.green);

        Undo.undoRedoPerformed += OnUndoRedo;

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

        for (int i = 0; i < threads; ++i)
        {
            threadsData[i] = new ThreadData();
        }
    }

    private void OnDisable()
    {
        //if (gridMaterial != null) DestroyImmediate(gridMaterial, false);
        if (brushProjectorMaterial != null) DestroyImmediate(brushProjectorMaterial, false);

        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    readonly GUIContent editButtonContent = new GUIContent("Edit");

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        editing = GUILayout.Toggle(editing, editButtonContent, EditorStyles.miniButton);
        Tools.hidden = editing;
        
        if (GUI.changed) SceneView.RepaintAll();
    }

    private void OnUndoRedo()
    {
        data.RebuildParallel(8);
    }

    System.Diagnostics.Stopwatch raycastStopWatch = new System.Diagnostics.Stopwatch();
    float raycastDuration;

    System.Diagnostics.Stopwatch rebuildStopWatch = new System.Diagnostics.Stopwatch();
    float rebuildDuration;

    System.Diagnostics.Stopwatch applyStopWatch = new System.Diagnostics.Stopwatch();
    float applyDuration;

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
            }

            switch (Brush.CheckBrushEvent())
            {
                case BrushEvent.BrushDraw:
                    Mesh mesh = data.sharedMesh;
                    if (mesh != null)
                    {
                        Matrix4x4 projMatrix = Brush.currentBrush.GetProjectionMatrix(intersection, map.transform, SceneView.currentDrawingSceneView.camera);

                        brushProjectorMaterial.SetMatrix(projMatrixID, projMatrix);
                        brushProjectorMaterial.SetFloat(opacityID, Brush.currentBrush.opacity * 0.5f);
                        brushProjectorMaterial.SetPass(Brush.currentBrush.type == Brush.Type.Smooth ? 1 : 0);
                        Graphics.DrawMeshNow(data.sharedMesh, Handles.matrix, 0);
                    }
                    break;
                case BrushEvent.BrushPaintStart:
                    if (rayHits)
                    {
                        //Undo.RecordObject(data, "Map Paint");
                        Undo.RegisterCompleteObjectUndo(data, "Map Paint");
                        ApplyBrush();
                    }
                    break;
                case BrushEvent.BrushPaint:
                    if (rayHits)
                    {
                        ApplyBrush();
                    }
                    break;
                case BrushEvent.BrushPaintEnd:
                    data.RebuildParallel(8);
                    break;
            }

            Handles.BeginGUI();
            GUI.skin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(40f));
            EditorGUILayout.LabelField(string.Format("{0} ms", raycastDuration), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Format("{0} ms", rebuildDuration), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Format("{0} ms", applyDuration), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Format("{0} ms", repaintPeriod), EditorStyles.label);

            //EditorGUILayout.Space();
            //EditorGUILayout.LabelField(new GUIContent("Proj " + currentBrush.GetProjectionMatrix(intersection, map.transform, SceneView.currentDrawingSceneView.camera) * new Vector4(intersection.x, intersection.y, intersection.z, 1f)));
            EditorGUILayout.EndVertical();

            Brush.DrawBrushWindow();
            Handles.EndGUI();
        }
    }

    private class ThreadData
    {
        public int startIndex, endIndex;
        public ManualResetEvent mre = new ManualResetEvent(false);

        public void Reset(int startIndex, int endIndex)
        {
            this.startIndex = startIndex;
            this.endIndex = endIndex;
            mre.Reset();
        }
    }
    const int threads = 8;
    ThreadData[] threadsData = new ThreadData[threads];
    float[] auxHeight;

    void ApplyBrush()
    {
        Brush brush = Brush.currentBrush;
        Matrix4x4 projMatrix = brush.GetProjectionMatrix(intersection, map.transform, SceneView.currentDrawingSceneView.camera);
        float[] heights = data.Heights;
        Vector3[] vertices = data.Vertices;
        int pointCount = vertices.Length;
        //TODO check null vertices?
        applyStopWatch.Reset();
        applyStopWatch.Start();

        if (auxHeight == null || auxHeight.Length != pointCount) auxHeight = new float[pointCount];

        float avgAmount = 0f;
        if (brush.mode == Brush.Mode.Average)
        {
            double heightSum = 0;
            double weightSum = 0;
            for(int index = 0; index < pointCount; ++index)
            {
                Vector3 vertex = vertices[index];
                float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
                if (strength > 0)
                {
                    heightSum += heights[index] * strength;
                    weightSum += strength;
                }
            }
            avgAmount = weightSum != 0 ? (float)(heightSum / weightSum) : 0f;
        }

        //TODO is this slower?
        int pointsPerThread = (pointCount - 1) / threads + 1;//TODO change to process all points!! round down is the ruin!
        for (int i = 0; i < threads; ++i)
        {
            ThreadData threadData = threadsData[i];
            threadData.Reset(i * pointsPerThread, Mathf.Min((i + 1) * pointsPerThread, pointCount));
            //Debug.Log(i * pointsPerThread + " " + (i + 1) * pointsPerThread + " " + pointCount);
            ThreadPool.QueueUserWorkItem((d) =>
            {
                ThreadData td = (ThreadData)d;
                //TODO work on different heights array? smooth needs to read and write the same array in uncontroled indices
                switch (brush.mode)
                {
                    case Brush.Mode.Add:
                        for (int index = td.startIndex; index < td.endIndex; ++index)
                        {
                            Vector3 vertex = vertices[index];
                            float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
                            if (strength > 0f) auxHeight[index] = heights[index] + brush.amount * strength;
                            else auxHeight[index] = heights[index];
                        }
                        break;
                    case Brush.Mode.Substract:
                        for (int index = td.startIndex; index < td.endIndex; ++index)
                        {
                            Vector3 vertex = vertices[index];
                            float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
                            if (strength > 0f) auxHeight[index] = heights[index] - brush.amount * strength;
                            else auxHeight[index] = heights[index];
                        }
                        break;
                    case Brush.Mode.Set:
                        for (int index = td.startIndex; index < td.endIndex; ++index)
                        {
                            Vector3 vertex = vertices[index];
                            float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
                            if (strength > 0f) auxHeight[index] = heights[index] + (brush.amount - heights[index]) * strength;
                            else auxHeight[index] = heights[index];
                        }
                        break;
                    case Brush.Mode.Average:
                        for (int index = td.startIndex; index < td.endIndex; ++index)
                        {
                            Vector3 vertex = vertices[index];
                            float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
                            if (strength > 0f) auxHeight[index] = heights[index] + (avgAmount - heights[index]) * strength;
                            else auxHeight[index] = heights[index];
                        }
                        break;
                    case Brush.Mode.Smooth:
                        for (int index = td.startIndex; index < td.endIndex; ++index)
                        {
                            Vector3 vertex = vertices[index];
                            float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
                            if (strength > 0f)
                            {
                                float neighbourAverage = 0f;
                                Vector2Int coords = data.IndexToGrid(index);
                                int columnOffset = coords.y & 1;
                                int eastIndex = data.GridToIndex(coords.y, coords.x + 1);//Checks bounds!
                                int neIndex = data.GridToIndex(coords.y + 1, coords.x + columnOffset);//Checks bounds!
                                int nwIndex = data.GridToIndex(coords.y + 1, coords.x + columnOffset - 1);//Checks bounds!
                                int westIndex = data.GridToIndex(coords.y, coords.x - 1);//Checks bounds!
                                int swIndex = data.GridToIndex(coords.y - 1, coords.x + columnOffset - 1);//Checks bounds!
                                int seIndex = data.GridToIndex(coords.y - 1, coords.x + columnOffset);//Checks bounds!

                                neighbourAverage += heights[eastIndex];
                                neighbourAverage += heights[neIndex];
                                neighbourAverage += heights[nwIndex];
                                neighbourAverage += heights[westIndex];
                                neighbourAverage += heights[swIndex];
                                neighbourAverage += heights[seIndex];
                                neighbourAverage /= 6f;//DO weighted average?

                                auxHeight[index] = heights[index] + (neighbourAverage - heights[index]) * strength * 0.5f;
                            }
                            else auxHeight[index] = heights[index];
                        }
                        break;
                }
                td.mre.Set();
            }, threadData);
        }
        foreach (var threadData in threadsData)
        {
            threadData.mre.WaitOne();
        }
        System.Array.Copy(auxHeight, heights, pointCount);

        applyStopWatch.Stop();
        applyDuration += (applyStopWatch.ElapsedMilliseconds - applyDuration) * 0.5f;

        //data.ForEachVertex((index, vertex) =>
        //{
        //    float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
        //    if (strength > 0)
        //    {
        //        //TODO handle data serialization/dirtying somehow and trigger rebuild mesh
        //        switch (brush.mode)
        //        {
        //            case Brush.Mode.Add:
        //                heights[index] += brush.amount * strength;
        //                break;
        //            case Brush.Mode.Substract:
        //                heights[index] -= brush.amount * strength;
        //                break;
        //            case Brush.Mode.Set:
        //                heights[index] += (brush.amount - heights[index]) * strength;
        //                break;
        //            case Brush.Mode.Average:
        //                heights[index] += (targetAmount - heights[index]) * strength;
        //                break;
        //            case Brush.Mode.Smooth:
        //                float neighbourAverage = 0f;
        //                Vector2Int coords = data.IndexToGrid(index);
        //                int columnOffset = coords.y & 1;
        //                int eastIndex = data.GridToIndex(coords.y, coords.x + 1);//Checks bounds!
        //                int neIndex = data.GridToIndex(coords.y + 1, coords.x + columnOffset);//Checks bounds!
        //                int nwIndex = data.GridToIndex(coords.y + 1, coords.x + columnOffset - 1);//Checks bounds!
        //                int westIndex = data.GridToIndex(coords.y, coords.x - 1);//Checks bounds!
        //                int swIndex = data.GridToIndex(coords.y - 1, coords.x + columnOffset - 1);//Checks bounds!
        //                int seIndex = data.GridToIndex(coords.y - 1, coords.x + columnOffset);//Checks bounds!

        //                neighbourAverage += heights[eastIndex];
        //                neighbourAverage += heights[neIndex];
        //                neighbourAverage += heights[nwIndex];
        //                neighbourAverage += heights[westIndex];
        //                neighbourAverage += heights[swIndex];
        //                neighbourAverage += heights[seIndex];
        //                neighbourAverage /= 6f;//DO weighted average?

        //                heights[index] += (neighbourAverage - heights[index]) * strength * 0.5f;
        //                break;
        //        }

        //    }
        //});


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
