using System.Collections;
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
    private bool maskR = true, maskG = true, maskB = true, maskA = true;

    private bool pickingValue;
    private float pickingHeight;
    private Color pickingColor;

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
    readonly GUIContent pickValueButtonContent = new GUIContent("Pick Value");

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        editing = GUILayout.Toggle(editing, editButtonContent, EditorStyles.miniButton);
        Tools.hidden = editing;

        if (editing)
        {
            pickingValue = GUILayout.Toggle(pickingValue, pickValueButtonContent, EditorStyles.miniButton);

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
                    maskA = GUILayout.Toggle(maskA, new GUIContent("A"), EditorStyles.miniButtonRight);
                    EditorGUILayout.EndHorizontal();
                    ColorMath.mask = new Color(maskR ? 1f : 0f, maskG ? 1f : 0f, maskB ? 1f : 0f, maskA ? 1f : 0f);
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
        EventType currentType = Event.current.type;//TODO use when posible
        eventsStopWatch.Reset();
        eventsStopWatch.Start();

        Matrix4x4 matrix = map.transform.localToWorldMatrix;

        Handles.matrix = matrix;

        if (editing)
        {
            if (Event.current.type == EventType.KeyUp || Event.current.type == EventType.Used)
            {
                Debug.Log(Event.current.type + " " + Brush.currentBrush.mode);
            }
            //TODO the mesh might be lost from the scriptable object but not the monobehaviour on git changes?
            //TODO sometimes ctrl+tab[+shift] may only work 1 every 2 times
            //TODO sometimes MouseMove wont reach
            //Debug.Log(Event.current.type);
            if (Event.current.type == EventType.Repaint)
            {
                repaintPeriod += (repaintStopWatch.ElapsedMilliseconds - repaintPeriod) * 0.5f;
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

            if (pickingValue)
            {
                int controlId = GUIUtility.GetControlID(new GUIContent("ValuePicker"), FocusType.Passive);
                EventType type = Event.current.type;
                bool leftClick = Event.current.button == 0;
                
                if (type == EventType.Layout)
                {//This will allow clicks to be eaten
                    HandleUtility.AddDefaultControl(controlId);
                }
                if (rayHits && (type == EventType.MouseMove || type == EventType.MouseDrag))
                {
                    switch (brushTarget)
                    {
                        case HEIGHT_TARGET:
                            pickingHeight = GetRaycastValue<float>(data.Heights, data.Indices, FloatMath.sharedHandler);
                            break;
                        case COLOR_TARGET:
                            pickingColor = GetRaycastValue<Color>(data.Colors, data.Indices, ColorMath.sharedHandler);
                            break;
                    }
                }
                if (rayHits && leftClick && (type == EventType.MouseDown || type == EventType.MouseDrag))
                {
                    switch (brushTarget)
                    {
                        case HEIGHT_TARGET:
                            height = pickingHeight;
                            break;
                        case COLOR_TARGET:
                            color = pickingColor;
                            break;
                    }
                    Event.current.Use();
                }
                if (type == EventType.MouseUp) pickingValue = false;
            }
            else
            {
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
                        Debug.LogWarningFormat("Draw {0} {1} {2}", currentType, Event.current.mousePosition, Event.current.delta);
                        break;
                    case BrushEvent.BrushPaintStart:
                        shouldApplyBrush = true;

                        Undo.RegisterCompleteObjectUndo(data, "Map Paint");
                        Debug.LogWarningFormat("PaintStart {0} {1} {2}", currentType, Event.current.mousePosition, Event.current.delta);
                        break;
                    case BrushEvent.BrushPaint:
                        // BrushApply moved to Repaint since Raycast is too expensive to be used on MouseDrag
                        shouldApplyBrush = true;
                        Debug.LogWarningFormat("Paint {0} {1} {2}", currentType, Event.current.mousePosition, Event.current.delta);
                        break;
                    case BrushEvent.BrushPaintEnd:
                        data.RebuildParallel(8);
                        // TODO Undo won't work after mouseUp if a mouseDrag happens afterwards, 
                        // but will once some other event happens (such as right click)
                        // first click outside of the scene window wont work either

                        Debug.LogWarningFormat("PaintEnd {0} {1} {2}", currentType, Event.current.mousePosition, Event.current.delta);
                        break;
                }
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

                EditorGUILayout.EndVertical();
            }
            Brush.DrawBrushWindow();
            Handles.EndGUI();
        }

        accumTime += eventsStopWatch.ElapsedMilliseconds;
        eventsStopWatch.Stop();

    }

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
        // Copy Parallel (System.Array.Copy(auxArray, srcArray, pointCount);)
        for (int i = 0; i < threads; ++i)
        {
            ThreadData threadData = threadsData[i];
            threadData.Reset(i * pointsPerThread, Mathf.Min((i + 1) * pointsPerThread, pointCount));
            
            ThreadPool.QueueUserWorkItem((d) =>
            {
                ThreadData td = (ThreadData)d;

                System.Array.Copy(auxArray, td.startIndex, srcArray, td.startIndex, td.endIndex - td.startIndex);
                
                td.mre.Set();
            }, threadData);
        }
        foreach (var threadData in threadsData)
        {
            threadData.mre.WaitOne();
        }

        applyStopWatch.Stop();
        applyDuration += (applyStopWatch.ElapsedMilliseconds - applyDuration) * 0.5f;
    }
    //TODO serialize MapData object!!
}
