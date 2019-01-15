using UnityEngine;
using UnityEditor;
using System.Threading;
using System;
using System.Collections.Generic;

[CustomEditor(typeof(Map))]
public class MapEditor : Editor {

    private Map map;
    private MapData data;

    private static Brush currentBrush;
    
    private bool editing;
    private int brushTarget;
    private const int HEIGHT_TARGET = 0;
    private const int TEXTURES_TARGET = 1;
    private const int PROPS_TARGET = 2;

    private GUIContent[] brushTargetGUIContents = new GUIContent[]
    {
        new GUIContent("Height"), new GUIContent("Textures"), new GUIContent("Props")/*, new GUIContent("Select Props")*/
    };
    
    private static MapPropsInstanceValues instanceValues = new MapPropsInstanceValues();
    private static bool autoApplyValues = false;
    private static int currentInstanceSetIndex;
    private static int currentMapTextureIndex;

    private static bool colorBrushMode;

    private static bool displayMapTexture;
    private static bool clampMapValues;

    private Material mapTextureMaterial;
    private int mainTexID;

    //private static bool selectMode;

    //If multiple object registers required at once, group them in a single undo/redo
    private UnityEngine.Object[] objectsToRecord = new UnityEngine.Object[2];

    private void OnEnable()
    {
        map = target as Map;
        data = map.Data;

        if (currentBrush == null) currentBrush = new Brush();//Initialize

        Undo.undoRedoPerformed += OnUndoRedo;
        SceneView.onSceneGUIDelegate += OnSceneHandler;
        Debug.LogWarning("OnEnable");

        for (int i = 0; i < threads; ++i)
        {
            threadsData[i] = new ThreadData();
        }


        mapTextureMaterial = new Material(Shader.Find("Hidden/AdVd/MapTextureShader"));
        mapTextureMaterial.hideFlags = HideFlags.HideAndDontSave;
        mainTexID = Shader.PropertyToID("_MainTex");

        InvalidateSelection();
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        SceneView.onSceneGUIDelegate -= OnSceneHandler;
        Debug.LogWarning("OnDisable");

        if (mapTextureMaterial != null) DestroyImmediate(mapTextureMaterial, true);

        if (editing) Tools.hidden = false;
    }

    private void OnUndoRedo()
    {
        RebuildMapTerrain();
        SetPropsDirtyAndRepaintScene();//Just set dirty since pov would be unavailable
        //Unfortunately props won't update if editor is not updating
    }

    readonly GUIContent rebuildTerrainGUIContent = new GUIContent("Rebuild Terrain");
    readonly GUIContent rebuildPropsGUIContent = new GUIContent("Rebuild Props");

    readonly GUIContent editButtonContent = new GUIContent("Edit");
    readonly GUIContent applyGUIContent = new GUIContent("Apply To Selection");
    readonly GUIContent autoApplyGUIContent = new GUIContent("Auto Apply");

    readonly GUIContent minusGUIContent = new GUIContent("-");
    readonly GUIContent plusGUIContent = new GUIContent("+");
    readonly GUIContent instanceSetGUIContent = new GUIContent("Instance Set");
    readonly GUIContent mapTextureGUIContent = new GUIContent("Map Texture");

    readonly GUIContent pacingGUIContent = new GUIContent("Pacing");

    readonly GUIContent displayMapTextureGUIContent = new GUIContent("Display Map Texture");
    readonly GUIContent clampMapValuesGUIContent = new GUIContent("Clamp Map Values");

    readonly GUIContent brushColorModeGUIContent = new GUIContent("Brush Color Mode");
    readonly GUIContent[] brushColorModesGUIContents = new GUIContent[] { new GUIContent("Vector4"), new GUIContent("Color") };

    readonly GUIContent deleteSelectionGUIContent = new GUIContent("Delete Selection");
    
    readonly GUILayoutOption miniButtonsWidthLayoutOption = GUILayout.Width(120f);
    readonly GUILayoutOption plusMinusWidthLayoutOption = GUILayout.Width(32f);
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(rebuildTerrainGUIContent))
        {
            map.Refresh();
        }
        if (GUILayout.Button(rebuildPropsGUIContent))
        {
            map.RefreshProps();//Not that useful, doesn't use scene camera
        }
        EditorGUILayout.EndHorizontal();


        editing = GUILayout.Toggle(editing, editButtonContent, EditorStyles.miniButton);
        Tools.hidden = editing;
        
        data = map.Data;
        if (editing && data != null)
        {
            brushTarget = GUILayout.SelectionGrid(brushTarget, brushTargetGUIContents, 3);

            bool enableValueFields = currentBrush.mode != Brush.Mode.Average && currentBrush.mode != Brush.Mode.Smooth;
            
            DrawHelp();
            switch (brushTarget)
            {
                case HEIGHT_TARGET:
                    currentBrush.currentValueType = Brush.ValueType.Float;
                    currentBrush.DrawBrushValueInspector(enableValueFields, true);
                    break;

                case TEXTURES_TARGET:
                    DrawMapTextureSelector();

                    colorBrushMode = EditorGUILayout.Popup(brushColorModeGUIContent, colorBrushMode ? 1 : 0, brushColorModesGUIContents) == 1;

                    if (colorBrushMode)
                    {
                        currentBrush.currentValueType = Brush.ValueType.Color;
                        ColorMath.mask = currentBrush.ColorMask;
                        currentBrush.DrawBrushValueInspector(enableValueFields, true);
                        if (clampMapValues)
                        {
                            currentBrush.colorValue = ColorMath.sharedHandler.Clamp01(currentBrush.colorValue);
                        }
                    }
                    else
                    {
                        currentBrush.currentValueType = Brush.ValueType.Vector4;
                        ColorMath.mask = currentBrush.VectorMask;//Vector mask is actually equivalent to color mask
                        currentBrush.DrawBrushValueInspector(enableValueFields, true);
                        if (clampMapValues)
                        {
                            currentBrush.vectorValue = Vector4Math.sharedHandler.Clamp01(currentBrush.vectorValue);
                        }
                    }
                    
                    displayMapTexture = EditorGUILayout.Toggle(displayMapTextureGUIContent, displayMapTexture);
                    clampMapValues = EditorGUILayout.Toggle(clampMapValuesGUIContent, clampMapValues);
                    break;

                case PROPS_TARGET:
                    DrawInstanceSetSelector();
                    
                    switch (currentBrush.mode)
                    {
                        case Brush.Mode.Set:
                            instanceValues.DoInstancePropertiesInspector();

                            if (GUI.changed && autoApplyValues) ApplyPropertiesToSelection(data.instanceSets[currentInstanceSetIndex]);
                            EditorGUILayout.BeginHorizontal();
                            GUI.enabled = !autoApplyValues;
                            bool applyToSelection = GUILayout.Button(applyGUIContent, EditorStyles.miniButton, miniButtonsWidthLayoutOption);
                            if (applyToSelection) ApplyPropertiesToSelection(data.instanceSets[currentInstanceSetIndex]);
                            GUI.enabled = true;
                            autoApplyValues = EditorGUILayout.ToggleLeft(autoApplyGUIContent, autoApplyValues);
                            EditorGUILayout.EndHorizontal();
                            
                            EditorGUILayout.BeginHorizontal();
                            bool deleteSelection = GUILayout.Button(deleteSelectionGUIContent, EditorStyles.miniButton, miniButtonsWidthLayoutOption);
                            if (deleteSelection) DeleteSelection(data.instanceSets[currentInstanceSetIndex]);
                            GUILayout.FlexibleSpace();
                            GUILayout.Label(new GUIContent(string.Format("Selection: {0}", selectionCount)), EditorStyles.miniLabel);
                            EditorGUILayout.EndHorizontal();

                            break;
                        case Brush.Mode.Add:
                            EditorGUILayout.LabelField(pacingGUIContent, GUI.skin.button);
                            currentBrush.currentValueType = Brush.ValueType.Float;
                            currentBrush.DrawBrushValueInspector(true, false);
                            if (currentBrush.floatValue < 0) currentBrush.floatValue = 0;

                            instanceValues.DoInstancePropertiesInspector();
                            break;
                        case Brush.Mode.Substract:
                            GUI.enabled = false;
                            EditorGUILayout.LabelField(GUIContent.none, GUI.skin.button);
                            GUI.enabled = true;
                            break;
                        default:
                            GUI.enabled = false;
                            EditorGUILayout.LabelField(GUIContent.none, GUI.skin.button);
                            GUI.enabled = true;
                            break;
                    }
                    break;
            }
            currentBrush.HandleBrushShortcuts();
        }

        if (GUI.changed) SceneView.RepaintAll();
    }

    readonly GUIContent[,] helpGUIContents =
    {
        { new GUIContent("Set Height"), new GUIContent("Increase Height"), new GUIContent("Reduce Height"), new GUIContent("Average Height"), new GUIContent("Smooth Height") },
        { new GUIContent("Set Texture Value"), new GUIContent("Add Texture Value"), new GUIContent("Substract Texture Value"), new GUIContent("Average Texture Value"), new GUIContent("Smooth Texture Value") },
        { new GUIContent("Edit Props (select with mouse, hold shift to add to selection, hold ctrl to remove from selection)"), new GUIContent("Add Props"), new GUIContent("Remove Props"), new GUIContent("No Action"), new GUIContent("No Action") }
    };

    private void DrawHelp()
    {
        int brushTargetIndex = Mathf.Clamp(brushTarget, 0, brushTargetGUIContents.Length - 1);
        int brushModeIndex = (int)currentBrush.mode;

        EditorGUILayout.HelpBox(helpGUIContents[brushTargetIndex, brushModeIndex]);
    }

    private void DrawInstanceSetSelector()
    {
        int nextInstanceSetIndex = IndexSelector(instanceSetGUIContent, currentInstanceSetIndex, data.instanceSets.Length);
        if (nextInstanceSetIndex != currentInstanceSetIndex)
        {
            InvalidateSelection();
        }
        currentInstanceSetIndex = nextInstanceSetIndex;
        if (currentInstanceSetIndex >= 0 && currentInstanceSetIndex < data.instanceSets.Length)
        {
            MapData.InstanceSet instanceSet = data.instanceSets[currentInstanceSetIndex];
            EditorGUILayout.LabelField(" ", instanceSet.GetInstanceSetName(currentInstanceSetIndex), EditorStyles.boldLabel);
        }
        else
        {
            EditorGUILayout.LabelField(string.Format("No instance set at index {0}", currentInstanceSetIndex), EditorStyles.boldLabel);
        }
    }
    

    private void DrawMapTextureSelector()
    {
        currentMapTextureIndex = IndexSelector(mapTextureGUIContent, currentMapTextureIndex, data.mapTextures.Length);
        if (currentMapTextureIndex >= 0 && currentMapTextureIndex < data.mapTextures.Length)
        {
            MapData.MapTexture mapTexture = data.mapTextures[currentMapTextureIndex];
            EditorGUILayout.LabelField(" ", mapTexture.GetTextureName(currentMapTextureIndex), EditorStyles.boldLabel);
        }
        else
        {
            EditorGUILayout.LabelField(string.Format("No texture at index {0}", currentInstanceSetIndex), EditorStyles.boldLabel);
        }
    }

    private int IndexSelector(GUIContent guiContent, int index, int length)
    {
        EditorGUILayout.BeginHorizontal();
        index = EditorGUILayout.IntField(guiContent, index);
        bool enabledGUI = GUI.enabled;
        GUI.enabled = index > 0;
        if (GUILayout.Button(minusGUIContent, EditorStyles.miniButtonLeft, plusMinusWidthLayoutOption)) index--;
        GUI.enabled = index < length - 1;
        if (GUILayout.Button(plusGUIContent, EditorStyles.miniButtonRight, plusMinusWidthLayoutOption)) index++;
        GUI.enabled = enabledGUI;
        EditorGUILayout.EndHorizontal();
        index = Mathf.Clamp(index, 0, length - 1);
        return index;
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

        EventType currentType = Event.current.type;
        eventsStopWatch.Reset();
        eventsStopWatch.Start();

        Matrix4x4 matrix = map.transform.localToWorldMatrix;

        Handles.matrix = matrix;
        Handles.color = Color.red;

        data = map.Data;
        if (editing && data != null)
        {
            //Debug.Log(Event.current.type);
            if (Event.current.type == EventType.Repaint)
            {
                repaintPeriod += (repaintStopWatch.ElapsedMilliseconds - repaintPeriod) * 0.5f;
                repaintStopWatch.Reset();
                repaintStopWatch.Start();

                RebuildPropMeshesAsync(false);
            }

            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
            {
                SceneView.RepaintAll();
                Repaint();
            }
            if (Event.current.type == EventType.Repaint)
            {
                Vector2 screenPoint = Event.current.mousePosition;
                Ray worldRay = HandleUtility.GUIPointToWorldRay(screenPoint);
                Matrix4x4 invMatrix = map.transform.worldToLocalMatrix;
                ray = new Ray(invMatrix.MultiplyPoint(worldRay.origin), invMatrix.MultiplyVector(worldRay.direction));

                HandleRaycast();

                DrawMapTexture(matrix);

                if (shouldApplyBrush)
                {// Don't apply brush unless there is need for it
                    if (rayHits) ApplyBrush();

                    shouldApplyBrush = false;
                }
            }
            if (brushTarget == PROPS_TARGET && currentBrush.mode == Brush.Mode.Set)
            {
                if (currentInstanceSetIndex >= 0 && currentInstanceSetIndex < data.instanceSets.Length)
                {
                    MapData.InstanceSet instanceSet = data.instanceSets[currentInstanceSetIndex];
                    SelectionOnSceneHandler(instanceSet);
                }
            }
            else
            {
                InvalidateSelection();
            }

            switch (currentBrush.CheckBrushEvent())
            {
                case BrushEvent.BrushDraw:
                    Mesh mesh = data.sharedTerrainMesh;

                    if (rayHits && mesh != null)
                    {
                        Matrix4x4 projMatrix = currentBrush.GetProjectionMatrix(intersection, matrix, sceneView.camera);

                        currentBrush.SetMaterial(projMatrix, brushTarget != PROPS_TARGET);

                        Graphics.DrawMeshNow(mesh, matrix, 0);
                    }
                    
                    //Debug.LogWarningFormat("Draw {0} {1} {2}", currentType, Event.current.mousePosition, Event.current.delta);
                    break;
                case BrushEvent.BrushPaintStart:
                    shouldApplyBrush = true;

                    lastInstancePlacing = new Vector3(float.MinValue, float.MinValue, float.MinValue);//Reset for prop placing mode
                    
                    switch (brushTarget)
                    {
                        case HEIGHT_TARGET:
                            objectsToRecord[0] = data; objectsToRecord[1] = data.HeightTexture;
                            Undo.RegisterCompleteObjectUndo(objectsToRecord, "Heights Changed");
                            break;
                        case TEXTURES_TARGET:
                            if (currentMapTextureIndex >= 0 && currentMapTextureIndex < data.mapTextures.Length)
                            {
                                MapData.MapTexture mapTexture = data.mapTextures[currentMapTextureIndex];
                                objectsToRecord[0] = data; objectsToRecord[1] = mapTexture.texture;
                                Undo.RegisterCompleteObjectUndo(objectsToRecord, "Map Texture Changed");
                            }
                            else
                            {
                                Debug.LogWarningFormat("No map texture at index {0}", currentMapTextureIndex);
                            }
                            break;
                        case PROPS_TARGET:
                            Undo.RegisterCompleteObjectUndo(data, "Props Changed");
                            break;
                    }

                    //Debug.LogWarningFormat("PaintStart {0} {1} {2}", currentType, Event.current.mousePosition, Event.current.delta);
                    break;
                case BrushEvent.BrushPaint:
                    // BrushApply moved to Repaint since Raycast is too expensive to be used on MouseDrag
                    shouldApplyBrush = true;
                    //Debug.LogWarningFormat("Paint {0} {1} {2}", currentType, Event.current.mousePosition, Event.current.delta);
                    break;
                case BrushEvent.BrushPaintEnd:
                    RebuildMapTerrain();
                    RebuildPropMeshesAsync(true);
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
                        int index0 = hitInfo.triangleIndex * 3;
                        Vector3 weights = hitInfo.barycentricCoordinate;

                        switch (brushTarget)
                        {
                            case HEIGHT_TARGET:
                                {
                                    float[] heights = data.Heights;
                                    int[] indices = data.Indices;
                                    FloatMath mathHandler = FloatMath.sharedHandler;
                                    float value = mathHandler.BarycentricInterpolation(
                                        heights[indices[index0]],
                                        heights[indices[index0 + 1]],
                                        heights[indices[index0 + 2]],
                                        weights);

                                    currentBrush.SetPeekValue(value);
                                }
                                break;
                            case TEXTURES_TARGET:
                                if (currentMapTextureIndex >= 0 && currentMapTextureIndex < data.mapTextures.Length)
                                {
                                    MapData.MapTexture mapTexture = data.mapTextures[currentMapTextureIndex];
                                    Color[] map = mapTexture.map;
                                    int[] indices = data.Indices;
                                    ColorMath mathHandler = ColorMath.sharedHandler;
                                    Color value = mathHandler.BarycentricInterpolation(
                                        map[indices[index0]],
                                        map[indices[index0 + 1]],
                                        map[indices[index0 + 2]],
                                        weights);
                                    if (colorBrushMode)
                                    {
                                        currentBrush.SetPeekValue(value);
                                    }
                                    else
                                    {
                                        currentBrush.SetPeekValue((Vector4) value);
                                    }
                                }
                                else
                                {
                                    Debug.LogWarningFormat("No map texture at index {0}", currentMapTextureIndex);
                                }
                                break;
                        }
                        currentBrush.AcceptPeekValue();
                        Repaint();
                    }
                    break;
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
            currentBrush.DrawBrushWindow();
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


    private void DrawMapTexture(Matrix4x4 matrix)
    {
        Mesh mesh = data.sharedTerrainMesh;

        if (mesh != null)
        {
            if (brushTarget == TEXTURES_TARGET && displayMapTexture)
            {
                if (currentMapTextureIndex >= 0 && currentMapTextureIndex < data.mapTextures.Length)
                {
                    MapData.MapTexture mapTexture = data.mapTextures[currentMapTextureIndex];

                    mapTextureMaterial.SetTexture(mainTexID, mapTexture.texture);
                    mapTextureMaterial.SetPass(0);
                    Graphics.DrawMeshNow(mesh, matrix, 0);
                }
            }
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
        if (data.Vertices == null) return;

        applyStopWatch.Reset();
        applyStopWatch.Start();
        int pointCount = data.Vertices.Length;
        switch (brushTarget)
        {
            case HEIGHT_TARGET:
                if (auxHeights == null || auxHeights.Length != pointCount) auxHeights = new float[pointCount];
                ApplyBrush<float>(data.Vertices, data.Heights, auxHeights, currentBrush.floatValue, FloatMath.sharedHandler);
                break;
            case TEXTURES_TARGET:
                if (currentMapTextureIndex >= 0 && currentMapTextureIndex < data.mapTextures.Length)
                {
                    MapData.MapTexture mapTexture = data.mapTextures[currentMapTextureIndex];
                    if (auxColors == null || auxColors.Length != pointCount) auxColors = new Color[pointCount];
                    if (colorBrushMode)
                    {
                        ApplyBrush<Color>(data.Vertices, mapTexture.map, auxColors, currentBrush.colorValue, ColorMath.sharedHandler);
                    }
                    else
                    {
                        ApplyBrush<Color>(data.Vertices, mapTexture.map, auxColors, currentBrush.vectorValue, ColorMath.sharedHandler);
                    }
                }
                else
                {
                    Debug.LogWarningFormat("No map texture at index {0}", currentMapTextureIndex);
                }
                break;
            case PROPS_TARGET:
                if (currentInstanceSetIndex >= 0 && currentInstanceSetIndex < data.instanceSets.Length)
                {
                    MapData.InstanceSet instanceSet = data.instanceSets[currentInstanceSetIndex];
                    ApplyPropsBrush(data.Vertices, instanceSet);
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

                data.WriteToTexture(data.Heights, data.HeightTexture);
                break;
            case TEXTURES_TARGET:
                if (currentMapTextureIndex == data.MeshColorMapIndex) data.UpdateMeshColor();
                
                if (currentMapTextureIndex >= 0 && currentMapTextureIndex < data.mapTextures.Length)
                {
                    MapData.MapTexture mapTexture = data.mapTextures[currentMapTextureIndex];
                    data.WriteToTexture(mapTexture.map, mapTexture.texture);
                }
                else
                {
                    Debug.LogWarningFormat("No map texture at index {0}", currentMapTextureIndex);
                }
                RebuildPropMeshesAsync(true);
                break;
            case PROPS_TARGET:
                RebuildPropMeshesAsync(true);
                break;
        }
        rebuildStopWatch.Stop();
        rebuildDuration += (rebuildStopWatch.ElapsedMilliseconds - rebuildDuration) * 0.5f;
        
        Repaint();
    }

    void ApplyBrush<T>(Vector3[] vertices, T[] srcArray, T[] auxArray, T value, IMathHandler<T> mathHandler) where T: struct
    {
        Matrix4x4 projMatrix = currentBrush.GetProjectionMatrix(intersection, map.transform.localToWorldMatrix, SceneView.currentDrawingSceneView.camera);

        int pointCount = vertices.Length;
        
        T avgValue = default(T);
        if (currentBrush.mode == Brush.Mode.Average)
        {
            T valueSum = default(T);
            float weightSum = 0;
            for(int index = 0; index < pointCount; ++index)
            {
                Vector3 vertex = vertices[index];
                float strength = currentBrush.GetStrength(projMatrix.MultiplyPoint(vertex));
                if (strength > 0)
                {
                    valueSum = mathHandler.WeightedSum(valueSum, srcArray[index], strength);
                    weightSum += strength;
                }
            }
            if (weightSum > 0.001f) avgValue = mathHandler.Product(valueSum, 1f / weightSum);
        }
        
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
                switch (currentBrush.mode)
                {
                    case Brush.Mode.Add:
                        for (int index = td.startIndex; index < td.endIndex; ++index)
                        {
                            Vector3 vertex = vertices[index];
                            float strength = currentBrush.GetStrength(projMatrix.MultiplyPoint(vertex));
                            if (strength > 0f) auxArray[index] = mathHandler.WeightedSum(srcArray[index], value, strength);
                            else auxArray[index] = srcArray[index];
                        }
                        break;
                    case Brush.Mode.Substract:
                        for (int index = td.startIndex; index < td.endIndex; ++index)
                        {
                            Vector3 vertex = vertices[index];
                            float strength = currentBrush.GetStrength(projMatrix.MultiplyPoint(vertex));
                            if (strength > 0f) auxArray[index] = mathHandler.WeightedSum(srcArray[index], value, -strength);
                            else auxArray[index] = srcArray[index];
                        }
                        break;
                    case Brush.Mode.Set:
                        for (int index = td.startIndex; index < td.endIndex; ++index)
                        {
                            Vector3 vertex = vertices[index];
                            float strength = currentBrush.GetStrength(projMatrix.MultiplyPoint(vertex));
                            if (strength > 0f) auxArray[index] = mathHandler.Blend(srcArray[index], value, strength);
                            else auxArray[index] = srcArray[index];
                        }
                        break;
                    case Brush.Mode.Average:
                        for (int index = td.startIndex; index < td.endIndex; ++index)
                        {
                            Vector3 vertex = vertices[index];
                            float strength = currentBrush.GetStrength(projMatrix.MultiplyPoint(vertex));
                            if (strength > 0f) auxArray[index] = mathHandler.Blend(srcArray[index], avgValue, strength);
                            else auxArray[index] = srcArray[index];
                        }
                        break;
                    case Brush.Mode.Smooth:
                        for (int index = td.startIndex; index < td.endIndex; ++index)
                        {
                            Vector3 vertex = vertices[index];
                            float strength = currentBrush.GetStrength(projMatrix.MultiplyPoint(vertex));
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

        if (clampMapValues)
        {
            for (int i = 0; i < pointCount; ++i) srcArray[i] = mathHandler.Clamp01(auxArray[i]);
        }
        else
        {
            Array.Copy(auxArray, srcArray, pointCount);//Parallel Copy not worth it
        }
    }


    
    Vector3 lastInstancePlacing;

    void ApplyPropsBrush(Vector3[] vertices, MapData.InstanceSet instanceSet)
    {
        Matrix4x4 projMatrix = currentBrush.GetProjectionMatrix(intersection, map.transform.localToWorldMatrix, SceneView.currentDrawingSceneView.camera);
        
        int instanceCount = instanceSet.Count;
        List<MapData.PropInstance> instances = instanceSet.Instances;
        switch (currentBrush.mode)
        {
            case Brush.Mode.Add:
                //instanceSet.EnsureCapacity(instanceSet.Count + 1);

                if (Vector3.Distance(lastInstancePlacing, intersection) > currentBrush.floatValue)
                {
                    lastInstancePlacing = intersection;

                    MapData.PropInstance instance = new MapData.PropInstance()
                    {
                        position = new Vector3(intersection.x, 0f, intersection.z),
                        alignment = 0,
                        rotation = UnityEngine.Random.value * 360f,
                        size = 1f,
                        tint = Color.white,
                        variantIndex = 0
                    };
                    Vector3 normal = data.SampleNormals(instance.position.x, instance.position.z);
                    instance = instanceValues.ApplyValues(instance, normal, 1f);
                    instances.Add(instance);
                    //instanceSet.Count = instanceCount = instanceCount + 1;
                }

                break;
            case Brush.Mode.Substract:
                //List<MapData.PropInstance> instances = instanceSet.Instances;
                for (int index = 0; index < instanceCount; ++index)
                {
                    MapData.PropInstance instance = instances[index];
                    Vector3 position = data.GetRealInstancePosition(instance.position);
                    Vector3 projOffset = projMatrix.MultiplyPoint(position);
                    float sqrDist = projOffset.sqrMagnitude;
                    if (sqrDist <= 1f) 
                    {
                        instance.variantIndex = -1;
                        instances[index] = instance;
                    }
                }
                instanceSet.RemoveMarked();
                break;
            case Brush.Mode.Set:
                ApplySelectionBrush(instanceSet);
                if (autoApplyValues) ApplyPropertiesToSelection(instanceSet);
                break;
            default:
                break;
        }
    }

    void RebuildMapTerrain()
    {
        data.RebuildParallel(8);
    }

    void RebuildPropMeshesSync()
    {
        Transform povTransform = map.POVTransform;
        if (povTransform == null && SceneView.currentDrawingSceneView != null) povTransform = SceneView.currentDrawingSceneView.camera.transform;
        Vector3 pov = povTransform != null ? map.transform.InverseTransformPoint(povTransform.position) : default(Vector3);
        Matrix4x4 localToWorld = map.transform.localToWorldMatrix;
        data.RefreshPropMeshes(pov, map.LODScale, localToWorld);
    }

    bool afterMeshUpdateRefreshPending = false;

    void RebuildPropMeshesAsync(bool forceDirtying)
    {
        if (forceDirtying) data.PropMeshesSetDirty();

        if (EditorApplication.isPlaying) return;//Let Map class do the rebuilding

        Transform povTransform = map.POVTransform;
        if (povTransform == null && SceneView.currentDrawingSceneView != null) povTransform = SceneView.currentDrawingSceneView.camera.transform;
        Vector3 pov = povTransform != null ? map.transform.InverseTransformPoint(povTransform.position) : default(Vector3);
        Matrix4x4 localToWorld = map.transform.localToWorldMatrix;
        data.RefreshPropMeshesAsync(pov, map.LODScale, localToWorld);

        if (data.PropMeshesRebuildOngoing())
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            //Extra repaint after "ready" to redraw with the new mesh
            afterMeshUpdateRefreshPending = true;
        }
        else if (afterMeshUpdateRefreshPending)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            afterMeshUpdateRefreshPending = false;
        }
    }

    //void DrawPropMeshes()
    //{
    //    if (EditorApplication.isPlaying) return;//Let Map class do the drawing

    //    Transform povTransform = map.POVTransform;
    //    if (povTransform == null && SceneView.currentDrawingSceneView != null) povTransform = SceneView.currentDrawingSceneView.camera.transform;
    //    Vector3 pov = povTransform != null ? map.transform.InverseTransformPoint(povTransform.position) : default(Vector3);
    //    Matrix4x4 localToWorld = map.transform.localToWorldMatrix;
    //    data.DrawProps(pov, map.LODScale, localToWorld);
    //}

    void SetPropsDirtyAndRepaintScene()
    {
        data.PropMeshesSetDirty();
        SceneView.RepaintAll();
    }

    #region Selection
    void SelectionOnSceneHandler(MapData.InstanceSet instanceSet)
    {
        int instanceCount = instanceSet.Count;
        List<MapData.PropInstance> instances = instanceSet.Instances;
        if (instanceSelection != null && instanceSelection.Length >= instanceCount)
        {
            Vector3 meanPosition = default(Vector3);
            for (int i = 0; i < instanceCount; ++i)
            {
                MapData.PropInstance inst = instances[i];
                Vector3 position = data.GetRealInstancePosition(inst.position);
                float handleSize = HandleUtility.GetHandleSize(position) * 0.05f;

                if (instanceSelection[i])
                {
                    meanPosition += inst.position;
                    if (Event.current.type == EventType.Repaint)
                    {
                        Handles.DotHandleCap(-1, position, Quaternion.identity, handleSize, EventType.Repaint);
                    }
                }
            }
            if (selectionCount > 0) meanPosition *= 1f / selectionCount;

            if (Tools.current == Tool.Move && selectionCount > 0)
            {
                Vector3 newPosition = Handles.PositionHandle(meanPosition, Quaternion.identity);

                if (newPosition != meanPosition)
                {
                    Undo.RegisterCompleteObjectUndo(data, "Props Move");

                    Vector3 deltaPosition = newPosition - meanPosition;
                    for (int index = 0; index < instanceCount; ++index)//No need for parallelization here
                    {
                        if (instanceSelection[index])
                        {
                            MapData.PropInstance instance = instances[index];
                            instance.position += deltaPosition;
                            instances[index] = instance;
                        }
                    }
                    
                    RebuildPropMeshesAsync(true);
                }
            }
        }
        if (Event.current.type == EventType.KeyDown)
        {
            if (Event.current.shift && Event.current.keyCode == KeyCode.A)
            {
                if (instanceSelection != null && Array.TrueForAll(instanceSelection, selected => selected))// All selected
                {
                    ResetSelection();
                }
                else
                {
                    SelectAll();
                }
                Repaint();
                Event.current.Use();
            }
            else if (Event.current.keyCode == KeyCode.Delete)
            {
                DeleteSelection(instanceSet);
                Repaint();
                Event.current.Use();
            }
        }
    }

    void InvalidateSelection()
    {
        instanceSelection = null;
        selectionCount = 0;
    }

    void ResetSelection()
    {
        if (instanceSelection == null) InitializeSelection();
        for (int i = 0; i < instanceSelection.Length; ++i) instanceSelection[i] = false;
        selectionCount = 0;
    }

    void SelectAll()
    {
        if (instanceSelection == null) InitializeSelection();
        for (int i = 0; i < instanceSelection.Length; ++i) instanceSelection[i] = true;
        selectionCount = instanceSelection.Length;
    }

    void InitializeSelection()
    {
        if (currentInstanceSetIndex >= 0 && currentInstanceSetIndex < data.instanceSets.Length)
        {
            MapData.InstanceSet instanceSet = data.instanceSets[currentInstanceSetIndex];

            int instanceCount = instanceSet.Count;
            instanceSelection = new bool[instanceCount];
            selectionCount = 0;
        }
        else
        {
            InvalidateSelection();
        }
    }

    bool[] instanceSelection = null;
    int selectionCount = 0;

    void ApplySelectionBrush(MapData.InstanceSet instanceSet)
    {
        Matrix4x4 projMatrix = currentBrush.GetProjectionMatrix(intersection, map.transform.localToWorldMatrix, SceneView.currentDrawingSceneView.camera);
        int instanceCount = instanceSet.Count;
        List<MapData.PropInstance> instances = instanceSet.Instances;

        bool shift = Event.current.shift;
        bool ctrl = Event.current.control;
        
        if (instanceSelection == null || instanceSelection.Length != instanceCount) InitializeSelection();
        if (instanceSelection == null) return;//Could not initialize
        
        int instancesPerThread = (instanceCount - 1) / threads + 1;
        for (int i = 0; i < threads; ++i)
        {
            ThreadData threadData = threadsData[i];
            threadData.Reset(i * instancesPerThread, Mathf.Min((i + 1) * instancesPerThread, instanceCount));
            
            ThreadPool.QueueUserWorkItem((d) =>
            {
                ThreadData td = (ThreadData)d;
                
                if (ctrl) { // Remove from seleciton
                    for (int index = td.startIndex; index < td.endIndex; ++index)
                    {
                        Vector3 vertex = data.GetRealInstancePosition(instances[index].position);
                        Vector3 projOffset = projMatrix.MultiplyPoint(vertex);
                        float sqrDist = projOffset.sqrMagnitude;
                        if (sqrDist <= 1f) instanceSelection[index] = false;
                    }
                }
                else if (shift) { // Add to selection
                    for (int index = td.startIndex; index < td.endIndex; ++index)
                    {
                        Vector3 vertex = data.GetRealInstancePosition(instances[index].position);
                        Vector3 projOffset = projMatrix.MultiplyPoint(vertex);
                        float sqrDist = projOffset.sqrMagnitude;
                        if (sqrDist <= 1f) instanceSelection[index] = true;
                    }
                }
                else { // Set selection
                    for (int index = td.startIndex; index < td.endIndex; ++index)
                    {
                        Vector3 vertex = data.GetRealInstancePosition(instances[index].position);
                        Vector3 projOffset = projMatrix.MultiplyPoint(vertex);
                        float sqrDist = projOffset.sqrMagnitude;
                        if (sqrDist <= 1f) instanceSelection[index] = true;
                        else instanceSelection[index] = false;
                    }
                }

                td.mre.Set();
            }, threadData);
        }
        foreach (var threadData in threadsData)
        {
            threadData.mre.WaitOne();
        }
        
        selectionCount = 0;
        for (int i = 0; i < instanceSelection.Length; ++i) if (instanceSelection[i]) selectionCount++;
    }

    void ApplyPropertiesToSelection(MapData.InstanceSet instanceSet)
    {
        int instanceCount = instanceSet.Count;
        List<MapData.PropInstance> instances = instanceSet.Instances;

        if (instanceSelection == null || instanceSelection.Length != instanceCount) InitializeSelection();
        if (instanceSelection == null) return;//Could not initialize

        for (int index = 0; index < instanceCount; ++index)
        {
            if (instanceSelection[index])
            {
                MapData.PropInstance instance = instances[index];
                Vector3 normal = data.SampleNormals(instance.position.x, instance.position.z);
                instance = instanceValues.ApplyValues(instance, normal, 1f);
                
                instances[index] = instance;
            }
        }

        SetPropsDirtyAndRepaintScene();//Doesn't call RefreshProps because it might not have the Scene POV
    }

    void DeleteSelection(MapData.InstanceSet instanceSet)
    {
        int instanceCount = instanceSet.Count;
        List<MapData.PropInstance> instances = instanceSet.Instances;

        if (instanceSelection == null || instanceSelection.Length != instanceCount) InitializeSelection();
        if (instanceSelection == null) return;//Could not initialize

        for (int index = 0; index < instanceCount; ++index)
        {
            MapData.PropInstance instance = instances[index];
            if (instanceSelection[index])
            {
                instance.variantIndex = -1;
                instances[index] = instance;
            }
        }

        instanceSet.RemoveMarked();
        InvalidateSelection();
        SetPropsDirtyAndRepaintScene();//Doesn't call RefreshProps because it might not have the Scene POV
    }

    #endregion
}