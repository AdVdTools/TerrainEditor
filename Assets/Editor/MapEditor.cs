﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Threading;
using System;
[CustomEditor(typeof(Map))]
public class MapEditor : Editor {

    private Map map;
    private MapData data;
    Material brushProjectorMaterial;
    int mainTexID;
    int mainColorID;
    int projMatrixID;
    int opacityID;

    private bool editing;
    private int brushTarget;
    private const int HEIGHT_TARGET = 0;
    private const int COLOR_TARGET = 1;
    private const int PROPS_TARGET = 2;
    private const int SELECT_TARGET = 3;
    private GUIContent[] brushTargetGUIContents = new GUIContent[]
    {
        new GUIContent("Height"), new GUIContent("Color"), new GUIContent("Props"), new GUIContent("Select")
    };

    //private int instanceProperty;
    //private const int SIZE_PROPERTY = 0;
    //private const int ROTATION_PROPERTY = 1;
    //private GUIContent[] instancePropertyGUIContents = new GUIContent[]
    //{
    //    new GUIContent("Scale"), new GUIContent("Rotation")
    //};

    private static MapPropsInstanceValues instanceValues = new MapPropsInstanceValues();
    private static int currentInstanceSetIndex;


    private void OnEnable()
    {
        map = target as Map;
        data = map.Data;

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
        SceneView.onSceneGUIDelegate -= OnSceneHandler;
        Debug.LogWarning("OnDisable");
    }

    private void OnUndoRedo()
    {
        data.RebuildParallel(8);
        data.RefreshPropMeshes();
    }

    readonly GUIContent editButtonContent = new GUIContent("Edit");

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        editing = GUILayout.Toggle(editing, editButtonContent, EditorStyles.miniButton);
        Tools.hidden = editing;

        if (editing)
        {
            brushTarget = GUILayout.SelectionGrid(brushTarget, brushTargetGUIContents, 4/*brushTargetGUIContents.Length*/);

            bool enableValueFields = Brush.currentBrush.mode != Brush.Mode.Average && Brush.currentBrush.mode != Brush.Mode.Smooth;

            switch (brushTarget)//TODO test vector values?
            {
                case HEIGHT_TARGET:
                    Brush.currentBrush.currentValueType = Brush.ValueType.Float;
                    Brush.currentBrush.DrawBrushValueInspector(enableValueFields, true);
                    break;
                case COLOR_TARGET:
                    Brush.currentBrush.currentValueType = Brush.ValueType.Color;
                    Brush.currentBrush.DrawBrushValueInspector(enableValueFields, true);
                    ColorMath.mask = Brush.currentBrush.ColorMask; //new Color(maskR ? 1f : 0f, maskG ? 1f : 0f, maskB ? 1f : 0f, maskA ? 1f : 0f);
                    break;
                case PROPS_TARGET:
                    currentInstanceSetIndex = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Instance Set"), currentInstanceSetIndex), 0, data.instanceSets.Length - 1);
                    //TODO use rand value inspector separate from brush inspector, for add (all fields), set (target field/all fields with mask!) and smooth (separation)
                    switch (Brush.currentBrush.mode)
                    {
                        case Brush.Mode.Set:
                            instanceValues.DoInstancePropertiesInspector();
                            //TODO fields mask!!
                            //instanceProperty = GUILayout.SelectionGrid(instanceProperty, instancePropertyGUIContents, 2);
                            //Brush.currentBrush.currentValueType = Brush.ValueType.Float;//TODO do others?
                            //Brush.currentBrush.DrawBrushValueInspector(true, false);
                            //if (instanceProperty == SIZE_PROPERTY && Brush.currentBrush.floatValue < 0f) Brush.currentBrush.floatValue = 0f;
                            break;
                        case Brush.Mode.Add:
                            EditorGUILayout.LabelField(new GUIContent("Amount"), GUI.skin.button);
                            Brush.currentBrush.currentValueType = Brush.ValueType.Int;//TODO do others?
                            Brush.currentBrush.DrawBrushValueInspector(true, false);
                            if (Brush.currentBrush.intValue <= 0) Brush.currentBrush.intValue = 1;

                            instanceValues.DoInstancePropertiesInspector();
                            break;
                        case Brush.Mode.Substract:
                            GUI.enabled = false;
                            EditorGUILayout.LabelField(new GUIContent(""), GUI.skin.button);
                            GUI.enabled = true;
                            break;
                        default:
                            GUI.enabled = false;
                            EditorGUILayout.LabelField(new GUIContent(""), GUI.skin.button);
                            GUI.enabled = true;
                            break;
                    }
                    

                    //TODO Draw warn box if mode does nothing
                    //TODO Props instance set selector
                    break;
                    //TODO toggle in select mode to autoapply instanceValues to selection & button for single time apply
            }
        }

        if (GUI.changed) SceneView.RepaintAll();

        //TODO handle brush shortcuts here too?
        //Brush.HandleBrushShortcuts();// If not drawing it makes no sense
    }
    

    #region StopWatches
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
    #endregion

    bool shouldApplyBrush;

    private void OnSceneHandler(SceneView sceneView)
    {
        //SceneView sceneView = SceneView.currentDrawingSceneView;

        EventType currentType = Event.current.type;//TODO use when posible
        eventsStopWatch.Reset();
        eventsStopWatch.Start();

        Matrix4x4 matrix = map.transform.localToWorldMatrix;

        Handles.matrix = matrix;

        if (editing)
        {
            bool editingCursorHandles = instanceValues.DoCursorHandles();

            //TODO sometimes MouseMove wont reach
            //Debug.Log(Event.current.type);
            if (Event.current.type == EventType.Repaint)
            {
                repaintPeriod += (repaintStopWatch.ElapsedMilliseconds - repaintPeriod) * 0.5f;
                repaintStopWatch.Reset();
                repaintStopWatch.Start();

                Handles.color = Color.red;
                Handles.DrawWireCube(new Vector3(intersection.x, data.SampleHeight(intersection.x, intersection.z), intersection.z), Vector3.one);
            }

            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
            {
                Repaint();
            }
            if (Event.current.type == EventType.Repaint)
            {//TODO Layout vs Repaint?
                Vector2 screenPoint = Event.current.mousePosition;
                Ray worldRay = HandleUtility.GUIPointToWorldRay(screenPoint);
                Matrix4x4 invMatrix = map.transform.worldToLocalMatrix;
                ray = new Ray(invMatrix.MultiplyPoint(worldRay.origin), invMatrix.MultiplyVector(worldRay.direction));

                HandleRaycast();

                if (shouldApplyBrush)
                {// Don't apply brush unless there is need for it
                    if (rayHits) ApplyBrush();

                    shouldApplyBrush = false;
                }
            }

            if (!editingCursorHandles) { 
                switch (Brush.currentBrush.CheckBrushEvent())
                {
                    case BrushEvent.BrushDraw:
                        Mesh mesh = data.sharedTerrainMesh;
                        if (mesh != null)
                        {
                            Matrix4x4 projMatrix = Brush.currentBrush.GetProjectionMatrix(intersection, matrix, sceneView.camera);

                            brushProjectorMaterial.SetMatrix(projMatrixID, projMatrix);
                            brushProjectorMaterial.SetFloat(opacityID, Brush.currentBrush.opacity * 0.5f);
                            brushProjectorMaterial.SetTexture(mainTexID, Brush.currentBrush.currentTexture);
                            brushProjectorMaterial.SetPass(0);
                            //TODO move material to brush class?
                            Graphics.DrawMeshNow(data.sharedTerrainMesh, matrix, 0);
                        }
                        if (currentInstanceSetIndex >= 0 && currentInstanceSetIndex < data.instanceSets.Length)
                        {
                            for (int i = 0; i < data.instanceSets[currentInstanceSetIndex].Count; ++i)
                            {
                                MapData.PropInstance inst = data.instanceSets[currentInstanceSetIndex].Instances[i];
                                Handles.DrawLine(inst.position, inst.position + Vector3.up * 2);
                                Handles.DrawWireDisc(inst.position, Vector3.up, inst.size);
                            }
                        }
                        //Debug.LogWarningFormat("Draw {0} {1} {2}", currentType, Event.current.mousePosition, Event.current.delta);
                        break;
                    case BrushEvent.BrushPaintStart:
                        shouldApplyBrush = true;

                        Undo.RegisterCompleteObjectUndo(data, "Map Paint");
                        //Debug.LogWarningFormat("PaintStart {0} {1} {2}", currentType, Event.current.mousePosition, Event.current.delta);
                        break;
                    case BrushEvent.BrushPaint:
                        // BrushApply moved to Repaint since Raycast is too expensive to be used on MouseDrag
                        shouldApplyBrush = true;
                        //Debug.LogWarningFormat("Paint {0} {1} {2}", currentType, Event.current.mousePosition, Event.current.delta);
                        break;
                    case BrushEvent.BrushPaintEnd:
                        data.RebuildParallel(8);
                        data.RefreshPropMeshes();//TODO do parallel method?
                                                 // TODO Undo won't work after mouseUp if a mouseDrag happens afterwards, 
                                                 // but will once some other event happens (such as right click)
                                                 // first click outside of the scene window wont work either

                        //Debug.LogWarningFormat("PaintEnd {0} {1} {2}", currentType, Event.current.mousePosition, Event.current.delta);
                        break;
                    case BrushEvent.BrushChanged:
                        Repaint();
                        break;
                    case BrushEvent.ValuePick:
                        if (rayHits)
                        {
                            switch (brushTarget)
                            {
                                case HEIGHT_TARGET:
                                    Brush.currentBrush.SetPeekValue(GetRaycastValue<float>(data.Heights, data.Indices, FloatMath.sharedHandler));
                                    break;
                                case COLOR_TARGET:
                                    Brush.currentBrush.SetPeekValue(GetRaycastValue<Color>(data.Colors, data.Indices, ColorMath.sharedHandler));
                                    break;
                            }
                            Brush.currentBrush.AcceptPeekValue();
                            Repaint();
                        }
                        break;
                }
            }

            Handles.BeginGUI();
            if (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout)
            {
                GUI.skin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene);

                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(40f));
                EditorGUILayout.LabelField(string.Format("{0} ms", raycastDuration), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(string.Format("{0} ms", rebuildDuration), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(string.Format("{0} ms", applyDuration), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(string.Format("{0} ms", repaintPeriod), EditorStyles.label);

                EditorGUILayout.LabelField(string.Format("{0} - {1}", intersection, data.SampleHeight(intersection.x, intersection.z)), EditorStyles.boldLabel);

                GUILayout.Space(20f);

                EditorGUILayout.EndVertical();
            }
            Brush.currentBrush.DrawBrushWindow();
            Handles.EndGUI();
        }

        accumTime += eventsStopWatch.ElapsedMilliseconds;
        eventsStopWatch.Stop();

    }

    Ray ray;
    bool rayHits;
    Vector3 intersection;
    private const float raycastDistance = 500f;
    MapData.RaycastHit hitInfo;

    private void HandleRaycast()
    {   
        raycastStopWatch.Reset();
        raycastStopWatch.Start();
        rayHits = data.RaycastParallel(ray, out hitInfo, raycastDistance, 8);

        raycastStopWatch.Stop();
        raycastDuration += (raycastStopWatch.ElapsedMilliseconds - raycastDuration) * 0.5f;
        
        if (rayHits)
        {
            intersection = hitInfo.point;
        }
    }

    private T GetRaycastValue<T>(T[] srcArray, int[] indices, IMathHandler<T> mathHandler) where T: struct
    {
        T value = default(T);
        int index0 = hitInfo.triangleIndex * 3;
        Vector3 weights = hitInfo.barycentricCoordinate;
        value = mathHandler.WeightedSum(value, srcArray[indices[index0]], weights.x);
        value = mathHandler.WeightedSum(value, srcArray[indices[index0 + 1]], weights.y);
        value = mathHandler.WeightedSum(value, srcArray[indices[index0 + 2]], weights.z);
        return value;
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
    MapData.InstanceSet auxInstanceSet = null;

    void ApplyBrush()
    {
        applyStopWatch.Reset();
        applyStopWatch.Start();
        switch (brushTarget)
        {
            case HEIGHT_TARGET:
                ApplyBrush<float>(data.Vertices, data.Heights, auxHeights, Brush.currentBrush.floatValue, FloatMath.sharedHandler);
                break;
            case COLOR_TARGET:
                ApplyBrush<Color>(data.Vertices, data.Colors, auxColors, Brush.currentBrush.colorValue, ColorMath.sharedHandler);
                break;
            case PROPS_TARGET:
                if (currentInstanceSetIndex >= 0 && currentInstanceSetIndex < data.instanceSets.Length)
                {
                    MapData.InstanceSet instanceSet = data.instanceSets[currentInstanceSetIndex];
                    data.RecalculateInstancePositions(1, instanceSet, data);
                    ApplyBrush(data.Vertices, instanceSet, auxInstanceSet);
                }
                else
                {
                    Debug.LogWarningFormat("No instance set at index {0}", currentInstanceSetIndex);
                }
                break;
        }
        applyStopWatch.Stop();
        applyDuration += (applyStopWatch.ElapsedMilliseconds - applyDuration) * 0.5f;

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
            case PROPS_TARGET:
                //data.RefreshPropMesh(0); //quick?
                data.RefreshPropMeshes();//
                break;
        }
        rebuildStopWatch.Stop();
        rebuildDuration += (rebuildStopWatch.ElapsedMilliseconds - rebuildDuration) * 0.5f;
        
        Repaint();
    }

    void ApplyBrush<T>(Vector3[] vertices, T[] srcArray, T[] auxArray, T value, IMathHandler<T> mathHandler) where T: struct
    {
        Brush brush = Brush.currentBrush;
        Matrix4x4 projMatrix = brush.GetProjectionMatrix(intersection, map.transform.localToWorldMatrix, SceneView.currentDrawingSceneView.camera);

        int pointCount = vertices.Length;
        //TODO check null vertices?

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
        int pointsPerThread = (pointCount - 1) / threads + 1;
        for (int i = 0; i < threads; ++i)
        {
            ThreadData threadData = threadsData[i];
            threadData.Reset(i * pointsPerThread, Mathf.Min((i + 1) * pointsPerThread, pointCount));
            //Debug.Log(i * pointsPerThread + " " + (i + 1) * pointsPerThread + " " + pointCount);
            ThreadPool.QueueUserWorkItem((d) =>
            {
                ThreadData td = (ThreadData)d;
                //ThreadData td = threadData;
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

        Array.Copy(auxArray, srcArray, pointCount);//Parallel Copy not worth it
        
    }
    //TODO serialize MapData object!!

    //void ApplyPropsBrush(propSetData?, T[] srcArray, T[] auxArray, T value, IMathHandler<T> mathHandler)
    //{

    //}



    void ApplyBrush(Vector3[] vertices, MapData.InstanceSet instanceSet, MapData.InstanceSet auxInstanceSet)
    {
        Brush brush = Brush.currentBrush;
        Matrix4x4 projMatrix = brush.GetProjectionMatrix(intersection, map.transform.localToWorldMatrix, SceneView.currentDrawingSceneView.camera);
        
        int pointCount = vertices.Length;
        //TODO check null vertices?

        //if (auxInstanceSet == null) auxInstanceSet = new MapData.InstanceSet();
        //TODO clear instanceSet?, calculate positions / distances?
        
        float rand, strength;
        int instanceCount = instanceSet.Count;
        switch (brush.mode)
        {
            case Brush.Mode.Add:
                instanceSet.EnsureCapacity(instanceSet.Count + Brush.currentBrush.intValue * 2);//TODO greater margin?
                for (int i = 0; i < Brush.currentBrush.intValue; ++i)
                {
                    Vector3 randOffset = new Vector3(UnityEngine.Random.value * 2f - 1f, 0f, UnityEngine.Random.value * 2f - 1f);
                    Vector3 position = new Vector3(intersection.x + randOffset.x * brush.size, 0f, intersection.z + randOffset.z * brush.size);
                    position.y = data.SampleHeight(position.x, position.z);
                    rand = UnityEngine.Random.value;
                    strength = brush.GetStrength(projMatrix.MultiplyPoint(position));
                    Debug.Log(rand + " " + strength + " " + instanceSet.Count + " " + randOffset);

                    //TODO force vertical projection?
                    if (rand < strength)
                    {
                        Debug.Log(rand + " < " + strength);
                        position.y = 0;//random heightOff?
                        //TODO Instances as array?
                        int instanceIndex = instanceCount;
                        instanceSet.Count = instanceIndex + 1;
                        instanceSet.Instances[instanceIndex] = new MapData.PropInstance()
                        {
                            position = position,
                            direction = Vector3.up,
                            rotation = UnityEngine.Random.value * 360f,
                            size = 1f
                        };
                    }
                }
                break;
            case Brush.Mode.Substract:
                //TODO multithreading
                for (int index = 0; index < instanceCount; ++index)
                {
                    Vector3 position = instanceSet.instancePositions[index];
                    rand = UnityEngine.Random.value;
                    strength = brush.GetStrength(projMatrix.MultiplyPoint(position));
                    if (rand < strength)
                    {
                        instanceSet.Instances[index].size = -1;
                    }
                }
                instanceSet.RemoveMarked();
                break;
            case Brush.Mode.Set:

                //switch (instanceProperty)
                //{
                //    case SIZE_PROPERTY:
                for (int index = 0; index < instanceSet.Count; ++index)
                {
                    Vector3 position = instanceSet.instancePositions[index];

                    strength = brush.GetStrength(projMatrix.MultiplyPoint(position));
                    if (strength > 0f)
                    {
                        MapData.PropInstance instance = instanceSet.Instances[index];
                        //TODO this will become a mesh, optimize!
                        //TODO mind that properties might be disabled!
                        instance = instanceValues.ApplyValues(instance, strength);

                        //instance.size += (Brush.currentBrush.floatValue - instance.size) * strength;
                        //instance.rotation += (Brush.currentBrush.floatValue - instance.rotation) * strength;



                        instanceSet.Instances[index] = instance;
                    }
                }

                //        break;
                //    case ROTATION_PROPERTY:
                //        for (int index = 0; index < instanceSet.Count; ++index)
                //        {
                //            Vector3 position = instanceSet.instancePositions[index];

                //            strength = brush.GetStrength(projMatrix.MultiplyPoint(position));
                //            if (strength > 0f)
                //            {
                //                MapData.PropInstance instance = instanceSet.Instances[index];
                //                instance.rotation += (Brush.currentBrush.floatValue - instance.rotation) * strength;
                //                instanceSet.Instances[index] = instance;
                //            }
                //        }
                //        break;
                //}

                break;
            default:
                break;
        }

        //TODO is this slower?
    //    int pointsPerThread = (pointCount - 1) / threads + 1;
    //    for (int i = 0; i < threads; ++i)
    //    {
    //        ThreadData threadData = threadsData[i];
    //        threadData.Reset(i * pointsPerThread, Mathf.Min((i + 1) * pointsPerThread, pointCount));
    //        //Debug.Log(i * pointsPerThread + " " + (i + 1) * pointsPerThread + " " + pointCount);
    //        ThreadPool.QueueUserWorkItem((d) =>
    //        {
    //            ThreadData td = (ThreadData)d;
    //            //ThreadData td = threadData;
    //            switch (brush.Mode)
    //            {
    //                case "Add":
    //                    for (int index = td.startIndex; index < td.endIndex; ++index)
    //                    {
    //                        Vector3 vertex = vertices[index];
    //                        float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
    //                        if (strength > 0f) auxArray[index] = mathHandler.WeightedSum(srcArray[index], value, strength);
    //                        else auxArray[index] = srcArray[index];
    //                    }
    //                    break;
    //                case "Substract":
    //                    for (int index = td.startIndex; index < td.endIndex; ++index)
    //                    {
    //                        Vector3 vertex = vertices[index];
    //                        float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
    //                        if (strength > 0f) auxArray[index] = mathHandler.WeightedSum(srcArray[index], value, -strength);
    //                        else auxArray[index] = srcArray[index];
    //                    }
    //                    break;
    //                case "Set":
    //                    for (int index = td.startIndex; index < td.endIndex; ++index)
    //                    {
    //                        Vector3 vertex = vertices[index];
    //                        float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
    //                        if (strength > 0f) auxArray[index] = mathHandler.Blend(srcArray[index], value, strength);
    //                        else auxArray[index] = srcArray[index];
    //                    }
    //                    break;
    //                case "Average":
    //                    for (int index = td.startIndex; index < td.endIndex; ++index)
    //                    {
    //                        Vector3 vertex = vertices[index];
    //                        float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
    //                        if (strength > 0f) auxArray[index] = mathHandler.Blend(srcArray[index], avgValue, strength);
    //                        else auxArray[index] = srcArray[index];
    //                    }
    //                    break;
    //                case "Smooth":
    //                    for (int index = td.startIndex; index < td.endIndex; ++index)
    //                    {
    //                        Vector3 vertex = vertices[index];
    //                        float strength = brush.GetStrength(projMatrix.MultiplyPoint(vertex));
    //                        if (strength > 0f)
    //                        {
    //                            Vector2Int coords = data.IndexToGrid(index);
    //                            int columnOffset = coords.y & 1;
    //                            int eastIndex = data.GridToIndex(coords.y, coords.x + 1);//Checks bounds!
    //                            int neIndex = data.GridToIndex(coords.y + 1, coords.x + columnOffset);//Checks bounds!
    //                            int nwIndex = data.GridToIndex(coords.y + 1, coords.x + columnOffset - 1);//Checks bounds!
    //                            int westIndex = data.GridToIndex(coords.y, coords.x - 1);//Checks bounds!
    //                            int swIndex = data.GridToIndex(coords.y - 1, coords.x + columnOffset - 1);//Checks bounds!
    //                            int seIndex = data.GridToIndex(coords.y - 1, coords.x + columnOffset);//Checks bounds!

    //                            T neighbourAverage = mathHandler.Product(
    //                                mathHandler.Sum(
    //                                    mathHandler.Sum(srcArray[eastIndex], srcArray[neIndex]),
    //                                    mathHandler.Sum(
    //                                        mathHandler.Sum(srcArray[nwIndex], srcArray[westIndex]),
    //                                        mathHandler.Sum(srcArray[swIndex], srcArray[seIndex]))),
    //                                1f / 6);
                                
    //                            auxArray[index] = mathHandler.Blend(srcArray[index], neighbourAverage, strength * 0.5f);
    //                        }
    //                        else auxArray[index] = srcArray[index];
    //                    }
    //                    break;
    //            }
    //        td.mre.Set();
    //    }, threadData);
    //}
    //    foreach (var threadData in threadsData)
    //    {
    //        threadData.mre.WaitOne();
    //    }

        //Array.Copy(auxArray, srcArray, pointCount);//Parallel Copy not worth it
        
    }
}