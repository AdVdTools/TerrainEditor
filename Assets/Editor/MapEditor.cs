﻿using System.Collections;
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
    private int brushTarget;
    private const int HEIGHT_TARGET = 0;
    private const int COLOR_TARGET = 1;
    private GUIContent[] brushTargetGUIContents = new GUIContent[]
    {
        new GUIContent("Height"), new GUIContent("Color")
    };
    private float height = 1f;
    private Color color = Color.white;
    private bool maskR = true, maskG = true, maskB = true, maskA;

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

        if (editing)
        {
            brushTarget = GUILayout.SelectionGrid(brushTarget, brushTargetGUIContents, brushTargetGUIContents.Length);

            GUI.enabled = Brush.currentBrush.mode != Brush.Mode.Average && Brush.currentBrush.mode != Brush.Mode.Smooth;
            switch (brushTarget)
            {
                case HEIGHT_TARGET:
                    height = EditorGUILayout.FloatField(new GUIContent("Height"), height);
                    break;
                case COLOR_TARGET:
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Mask"));
                    maskR = GUILayout.Toggle(maskR, new GUIContent("R"), EditorStyles.miniButtonLeft);
                    maskG = GUILayout.Toggle(maskG, new GUIContent("G"), EditorStyles.miniButtonMid);
                    maskB = GUILayout.Toggle(maskB, new GUIContent("B"), EditorStyles.miniButtonMid);
                    maskA = GUILayout.Toggle(maskA, new GUIContent("R"), EditorStyles.miniButtonRight);
                    EditorGUILayout.EndHorizontal();
                    if (GUI.changed) ColorMath.mask = new Color(maskR ? 1f : 0f, maskG ? 1f : 0f, maskB ? 1f : 0f, maskA ? 1f : 0f);
                    color = EditorGUILayout.ColorField(new GUIContent("Color"), color);
                    break;
            }
            GUI.enabled = true;
        }

        if (GUI.changed) SceneView.RepaintAll();

        //TODO handle brush shortcuts here too?
        //Brush.HandleBrushShortcuts();// If not drawing it makes no sense
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


    System.Diagnostics.Stopwatch eventsStopWatch = new System.Diagnostics.Stopwatch();
    long accumTime;

    Ray ray;
    bool rayHits;
    Vector3 intersection;
    private const float raycastDistance = 500f;

    bool shouldApplyBrush;

    private void OnSceneGUI()
    {
        EventType currentTypeForGraph = Event.current.type;
        eventsStopWatch.Reset();
        eventsStopWatch.Start();

        Matrix4x4 matrix = map.transform.localToWorldMatrix;

        Handles.matrix = matrix;

        if (editing)
        {
            //Debug.Log(Event.current.type);
            if (Event.current.type == EventType.Repaint)
            {
                repaintPeriod += (repaintStopWatch.ElapsedMilliseconds - repaintPeriod) * 0.5f;
                //AdVd.Graphs.Graph.AddData("Repaint", accumTime * 0.001f, repaintPeriod);
                repaintStopWatch.Reset();
                repaintStopWatch.Start();
            }

            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag) {
                Repaint();
            }
            if (Event.current.type == EventType.Repaint) {//TODO Layout vs Repaint?
                Vector2 screenPoint = Event.current.mousePosition;
                Ray worldRay = HandleUtility.GUIPointToWorldRay(screenPoint);
                Matrix4x4 invMatrix = map.transform.worldToLocalMatrix;
                ray = new Ray(invMatrix.MultiplyPoint(worldRay.origin), invMatrix.MultiplyVector(worldRay.direction));

                HandleRaycast();

                if (shouldApplyBrush) {// Don't apply brush unless there is need for it
                    if (rayHits) ApplyBrush();
                    
                    shouldApplyBrush = false;
                }
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
                        Graphics.DrawMeshNow(data.sharedMesh, matrix, 0);
                    }
                    //Debug.LogWarningFormat("Draw {0} {1} {2}", currentTypeForGraph, Event.current.mousePosition, Event.current.delta);
                    break;
                case BrushEvent.BrushPaintStart:
                    shouldApplyBrush = true;
                    //Undo.RecordObject(data, "Map Paint");
                    Undo.RegisterCompleteObjectUndo(data, "Map Paint");

                    //if (updatedRay && rayHits)
                    //{
                    //    ApplyBrush();
                    //    updatedRay = false;
                    //}
                    //Debug.LogWarningFormat("PaintStart {0} {1} {2}", currentTypeForGraph, Event.current.mousePosition, Event.current.delta);
                    break;
                case BrushEvent.BrushPaint:
                    // 3-4 MouseDrag for each Repaint, but updating ray is costly
                    // so moving it to MouseDrag might be undesirable
                    shouldApplyBrush = true;
                    //if (updatedRay && rayHits)
                    //{
                    //    ApplyBrush();
                    //    updatedRay = false;
                    //}
                    //Debug.LogWarningFormat("Paint {0} {1} {2}", currentTypeForGraph, Event.current.mousePosition, Event.current.delta);
                    break;
                case BrushEvent.BrushPaintEnd:
                    data.RebuildParallel(8);
                    // TODO Undo won't work after mouseUp if a mouseDrag happens afterwards, 
                    // but will once some other event happens (such as right click)
                    // first click outside of the scene window wont work either

                    //Debug.LogWarningFormat("PaintEnd {0} {1} {2}", currentTypeForGraph, Event.current.mousePosition, Event.current.delta);
                    break;
            }

            Handles.BeginGUI();
            if (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout)
            {
                GUI.skin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(40f));
                EditorGUILayout.LabelField(string.Format("{0} ms", raycastDuration), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(string.Format("{0} ms", rebuildDuration), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(string.Format("{0} ms", applyDuration), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(string.Format("{0} ms", repaintPeriod), EditorStyles.label);

                //EditorGUILayout.Space();
                //EditorGUILayout.LabelField(new GUIContent("Proj " + currentBrush.GetProjectionMatrix(intersection, map.transform, SceneView.currentDrawingSceneView.camera) * new Vector4(intersection.x, intersection.y, intersection.z, 1f)));
                EditorGUILayout.EndVertical();
            }
            Brush.DrawBrushWindow();
            Handles.EndGUI();
        }

        accumTime += eventsStopWatch.ElapsedMilliseconds;
        //AdVd.Graphs.Graph.AddData(currentTypeForGraph.ToString(), accumTime * 0.001f, eventsStopWatch.ElapsedMilliseconds);
        eventsStopWatch.Stop();

    }

    private void HandleRaycast()
    {   
        MapData.RaycastHit hitInfo;
        raycastStopWatch.Reset();
        raycastStopWatch.Start();
        rayHits = data.RaycastParallel(ray, out hitInfo, raycastDistance, 8);

        raycastStopWatch.Stop();
        raycastDuration += (raycastStopWatch.ElapsedMilliseconds - raycastDuration) * 0.5f;
        //AdVd.Graphs.Graph.AddData("Raycast", accumTime * 0.001f, raycastDuration);
        //Debug.Log(intersection+" "+stopWatch.ElapsedMilliseconds);

        if (rayHits)
        {
            //if (intersection != hitInfo.point) Repaint();
            intersection = hitInfo.point;//ray.GetPoint(ray.origin.y / -ray.direction.y);//TEMP Y-0 cast
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
    float[] auxHeights = null;
    Color[] auxColors = null;

    void ApplyBrush()
    {
        switch (brushTarget)
        {
            case HEIGHT_TARGET:
                ApplyBrush<float>(data.Vertices, data.Heights, auxHeights, height, FloatMath.sharedHandler);
                break;
            case COLOR_TARGET:
                ApplyBrush<Color>(data.Vertices, data.Colors, auxColors, color, ColorMath.sharedHandler);//TODO handle masks in inspector
                break;
        }

        rebuildStopWatch.Reset();
        rebuildStopWatch.Start();
        switch (brushTarget)
        {
            case HEIGHT_TARGET:
                data.QuickRebuildParallel(8);
                break;
            case COLOR_TARGET:
                data.UpdateMeshColor();
                break;
        }
        rebuildStopWatch.Stop();
        rebuildDuration += (rebuildStopWatch.ElapsedMilliseconds - rebuildDuration) * 0.5f;
        //AdVd.Graphs.Graph.AddData("Rebuild", accumTime * 0.001f, rebuildDuration);

        Repaint();
    }

    #region Crazyness
    interface IMathHandler<T> {
        T Product(T value1, float value2);
        T Product(T value1, T value2);
        T Sum(T value1, T value2);
        T WeightedSum(T value1, T value2, float weight2);
        T Blend(T value1, T value2, float t);
    }

    public class FloatMath : IMathHandler<float>
    {
        public static FloatMath sharedHandler = new FloatMath();
        public float Product(float value1, float value2)
        {
            return value1 * value2;
        }

        public float Sum(float value1, float value2)
        {
            return value1 + value2;
        }

        public float WeightedSum(float value1, float value2, float weight2)
        {
            return value1 + value2 * weight2;
        }

        public float Blend(float value1, float value2, float t)
        {
            return value1 + (value2 - value1) * t;
        }
    }


    public class ColorMath : IMathHandler<Color>
    {
        public static Color mask = new Color(1f, 1f, 1f, 0f);
        public static ColorMath sharedHandler = new ColorMath();
        public Color Product(Color value1, float value2)
        {
            return value1 * value2;
        }

        public Color Product(Color value1, Color value2)
        {
            return value1 * value2;
        }

        public Color Sum(Color value1, Color value2)
        {
            return value1 + value2;
        }

        public Color WeightedSum(Color value1, Color value2, float weight2)
        {
            return value1 + value2 * (mask * weight2);
        }

        public Color Blend(Color value1, Color value2, float t)
        {
            return value1 + (value2 - value1) * (mask * t);
        }
    }
    #endregion

    void ApplyBrush<T>(Vector3[] vertices, T[] srcArray, T[] auxArray, T value, IMathHandler<T> mathHandler) where T: struct
    {
        Brush brush = Brush.currentBrush;
        Matrix4x4 projMatrix = brush.GetProjectionMatrix(intersection, map.transform, SceneView.currentDrawingSceneView.camera);

        int pointCount = vertices.Length;
        //TODO check null vertices?
        applyStopWatch.Reset();
        applyStopWatch.Start();

        if (auxArray == null || auxArray.Length != pointCount) auxArray = new T[pointCount];

        T avgValue = default(T);
        if (brush.mode == Brush.Mode.Average)
        {
            T valueSum = default(T);
            float weightSum = 0;
            for(int index = 0; index < pointCount; ++index)
            {
                Vector3 vertex = vertices[index];
                float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
                if (strength > 0)
                {
                    valueSum = mathHandler.WeightedSum(valueSum, srcArray[index], strength);
                    weightSum += strength;
                }
            }
            if (weightSum > 0.001f) avgValue = mathHandler.Product(valueSum, 1f / weightSum);
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
                            if (strength > 0f) auxArray[index] = mathHandler.WeightedSum(srcArray[index], value, strength);
                            else auxArray[index] = srcArray[index];
                        }
                        break;
                    case Brush.Mode.Substract:
                        for (int index = td.startIndex; index < td.endIndex; ++index)
                        {
                            Vector3 vertex = vertices[index];
                            float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
                            if (strength > 0f) auxArray[index] = mathHandler.WeightedSum(srcArray[index], value, -strength);
                            else auxArray[index] = srcArray[index];
                        }
                        break;
                    case Brush.Mode.Set:
                        for (int index = td.startIndex; index < td.endIndex; ++index)
                        {
                            Vector3 vertex = vertices[index];
                            float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
                            if (strength > 0f) auxArray[index] = mathHandler.Blend(srcArray[index], value, strength);
                            else auxArray[index] = srcArray[index];
                        }
                        break;
                    case Brush.Mode.Average:
                        for (int index = td.startIndex; index < td.endIndex; ++index)
                        {
                            Vector3 vertex = vertices[index];
                            float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
                            if (strength > 0f) auxArray[index] = mathHandler.Blend(srcArray[index], avgValue, strength);
                            else auxArray[index] = srcArray[index];
                        }
                        break;
                    case Brush.Mode.Smooth:
                        for (int index = td.startIndex; index < td.endIndex; ++index)
                        {
                            Vector3 vertex = vertices[index];
                            float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
                            if (strength > 0f)
                            {
                                Vector2Int coords = data.IndexToGrid(index);
                                int columnOffset = coords.y & 1;
                                int eastIndex = data.GridToIndex(coords.y, coords.x + 1);//Checks bounds!
                                int neIndex = data.GridToIndex(coords.y + 1, coords.x + columnOffset);//Checks bounds!
                                int nwIndex = data.GridToIndex(coords.y + 1, coords.x + columnOffset - 1);//Checks bounds!
                                int westIndex = data.GridToIndex(coords.y, coords.x - 1);//Checks bounds!
                                int swIndex = data.GridToIndex(coords.y - 1, coords.x + columnOffset - 1);//Checks bounds!
                                int seIndex = data.GridToIndex(coords.y - 1, coords.x + columnOffset);//Checks bounds!

                                T neighbourAverage = mathHandler.Product(
                                    mathHandler.Sum(
                                        mathHandler.Sum(srcArray[eastIndex], srcArray[neIndex]),
                                        mathHandler.Sum(
                                            mathHandler.Sum(srcArray[nwIndex], srcArray[westIndex]),
                                            mathHandler.Sum(srcArray[swIndex], srcArray[seIndex]))),
                                    1f / 6);
                                
                                //neighbourAverage += srcArray[eastIndex];
                                //neighbourAverage += srcArray[neIndex];
                                //neighbourAverage += srcArray[nwIndex];
                                //neighbourAverage += srcArray[westIndex];
                                //neighbourAverage += srcArray[swIndex];
                                //neighbourAverage += srcArray[seIndex];
                                //neighbourAverage /= 6f;//DO weighted average?

                                auxArray[index] = mathHandler.Blend(srcArray[index], neighbourAverage, strength * 0.5f);
                            }
                            else auxArray[index] = srcArray[index];
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
        System.Array.Copy(auxArray, srcArray, pointCount);

        applyStopWatch.Stop();
        applyDuration += (applyStopWatch.ElapsedMilliseconds - applyDuration) * 0.5f;
        //AdVd.Graphs.Graph.AddData("Apply", accumTime * 0.001f, applyDuration);
    }
    //TODO serialize MapData object!!
}
