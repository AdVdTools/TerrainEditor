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

    [System.Serializable]
    public class Brush {
        public enum Op { Add, Substract, Set, Average, Smooth }
        public enum Projection { Sphere, Vertical, View, Perspective }//TODO merge view and perspective?
        //TODO math vs curve brush?
        public Op operation = Op.Add;
        public float amount = 1f;
        public float opacity = 1f;
        public float radius = 1f;
        public AnimationCurve curve = AnimationCurve.Constant(0f, 1f, 1f);
        public Projection projection = Projection.Sphere;

        public float GetStrength(Vector3 projectedOffset)
        {
            float distance = Vector3.Magnitude(projectedOffset);
            if (distance > 1f) return 0f;
            float curveValue = curve.Evaluate(1f - distance);
            return curveValue * opacity;//?? use strength in op math?
        }

        public float Math(float value, float multiplier)
        {
            switch (operation)
            {
                case Op.Add:
                    return value + amount * multiplier;
                case Op.Substract:
                    return value - amount * multiplier;
                case Op.Set:
                    return value + (amount - value) * multiplier;
                default:
                    return value;
            }
        }

        public Matrix4x4 GetProjectionMatrix(Vector3 center, Transform mapTransform, Camera camera)
        {
            switch (projection)
            {
                case Projection.Sphere:
                    return Matrix4x4.TRS(center, Quaternion.identity, new Vector3(radius, radius, radius)).inverse;
                case Projection.Vertical:
                    Matrix4x4 vm = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(radius, radius, radius)).inverse;
                    vm.SetRow(1, Vector4.zero);
                    return vm;
                case Projection.View:
                    Transform camTransform = camera.transform;

                    Vector3 camPosition = mapTransform.InverseTransformPoint(camTransform.position);
                    Vector3 lookDirection = mapTransform.InverseTransformVector(camTransform.forward);// center - camPosition;
                    Matrix4x4 cm = Matrix4x4.TRS(center, Quaternion.FromToRotation(Vector3.forward, lookDirection), new Vector3(radius, radius, radius)).inverse;

                    //Quaternion mr = mapTransform.worldToLocalMatrix.rotation;
                    //Matrix4x4 cm = Matrix4x4.TRS(center, mr * camTransform.rotation, new Vector3(radius, radius, radius)).inverse;
                    cm.SetRow(2, Vector4.zero);
                    return cm;
                case Projection.Perspective:
                    Vector3 ld = mapTransform.InverseTransformVector(camera.transform.forward);// center - camPosition;

                    //Matrix4x4 pm = camera.projectionMatrix.inverse * camera.worldToCameraMatrix * mapTransform.localToWorldMatrix;// * Matrix4x4.TRS(center, Quaternion.FromToRotation(Vector3.forward, ld), new Vector3(radius, radius, radius)).inverse;
                    Matrix4x4 pm = camera.transform.worldToLocalMatrix * mapTransform.localToWorldMatrix;// * Matrix4x4.TRS(center, Quaternion.FromToRotation(Vector3.forward, ld), new Vector3(radius, radius, radius)).inverse;
                    pm = camera.projectionMatrix.inverse * pm;
                    pm =  Matrix4x4.Translate(-pm.MultiplyPoint(center)) * pm;
                    pm.SetRow(1, pm.GetRow(1) * camera.aspect);
                    pm.SetRow(2, Vector4.zero);
                    pm.SetRow(3, pm.GetRow(3) * (camera.orthographic ? radius * 50f : radius / 50f));
                    return pm;//TODO keep in mind map transform!
                default:
                    return Matrix4x4.identity;
            }
        }
    }

    [SerializeField] Brush currentBrush = new Brush();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        editing = GUILayout.Toggle(editing, new GUIContent("Edit"), EditorStyles.miniButton);
        Tools.hidden = editing;

        currentBrush.operation = (Brush.Op)EditorGUILayout.EnumPopup(new GUIContent("Operation"), currentBrush.operation);
        currentBrush.amount = EditorGUILayout.FloatField(new GUIContent("Amount"), currentBrush.amount);
        currentBrush.opacity = EditorGUILayout.FloatField(new GUIContent("Opacity"), currentBrush.opacity);
        currentBrush.radius = EditorGUILayout.FloatField(new GUIContent("Radius"), currentBrush.radius);
        currentBrush.curve = EditorGUILayout.CurveField(new GUIContent("Curve"), currentBrush.curve);
        currentBrush.projection = (Brush.Projection)EditorGUILayout.EnumPopup(new GUIContent("Projection"), currentBrush.projection);
        
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

                
                Matrix4x4 projMatrix = currentBrush.GetProjectionMatrix(intersection, map.transform, SceneView.currentDrawingSceneView.camera);

                brushProjectorMaterial.SetMatrix(projMatrixID, projMatrix);
                brushProjectorMaterial.SetPass(0);
                Graphics.DrawMeshNow(data.sharedMesh, Handles.matrix, 0);
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
            
            Handles.BeginGUI();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(40f));
            EditorGUILayout.LabelField(string.Format("{0} ms", raycastDuration), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Format("{0} ms", rebuildDuration), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Format("{0} ms", repaintPeriod), EditorStyles.label);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Proj " + currentBrush.GetProjectionMatrix(intersection, map.transform, SceneView.currentDrawingSceneView.camera) * new Vector4(intersection.x, intersection.y, intersection.z, 1f)));
            EditorGUILayout.EndVertical();
            Handles.EndGUI();
        }
    }

    void ApplyBrush()
    {
        //Undo.RecordObject(mapData, "Map Changed");//TODO records per MouseUp?

        Matrix4x4 projMatrix = currentBrush.GetProjectionMatrix(intersection, map.transform, SceneView.currentDrawingSceneView.camera);
        float[] heights = data.Heights;
        data.ForEachVertex((index, vertex) =>
        {
            float strength = currentBrush.GetStrength(projMatrix.MultiplyPoint(vertex));
            if (strength > 0)
            {
                //TODO handle data serialization/dirtying somehow and trigger rebuild mesh
                heights[index] = currentBrush.Math(heights[index], strength);
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
