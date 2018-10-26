using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PropDitherPattern))]
public class PropDitherPatternEditor : Editor {

    PropDitherPattern targetPDP;

    Material drawMaterial;
    Mesh circleMesh;

    private void OnEnable()
    {
        targetPDP = target as PropDitherPattern;
        
        drawMaterial = new Material(Shader.Find("Hidden/AdVd/GridShader"));
        drawMaterial.hideFlags = HideFlags.HideAndDontSave;

        circleMesh = BuildCircleMesh();

        CameraInit();
        ResetPreview();

        Undo.undoRedoPerformed += OnUndoRedo;
    }

    Mesh BuildCircleMesh()
    {
        Mesh mesh = new Mesh();
        mesh.hideFlags = HideFlags.HideAndDontSave;

        int vertexCount = 24;
        Vector3[] vertices = new Vector3[vertexCount];
        for (int i = 0; i < vertexCount; ++i)
        {
            float angle = i * 2 * Mathf.PI / vertexCount;
            vertices[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        int[] lineIndices = new int[vertexCount + 1];
        for (int i = 0; i < vertexCount; ++i) lineIndices[i] = i;//Last index remains 0
        int[] areaIndices = new int[(vertexCount - 2) * 3];
        int indexIndex = 0;
        for (int i = 2; i < vertexCount; ++i)
        {
            areaIndices[indexIndex++] = 0;
            areaIndices[indexIndex++] = i;
            areaIndices[indexIndex++] = i - 1;
        }

        mesh.subMeshCount = 2;
        mesh.vertices = vertices;
        mesh.SetIndices(lineIndices, MeshTopology.LineStrip, 0);
        mesh.SetIndices(areaIndices, MeshTopology.Triangles, 1);
        return mesh;
    }

    private void OnDisable()
    {
        if (previewCamera != null) DestroyImmediate(previewCamera.gameObject, true);
        
        if (drawMaterial != null) DestroyImmediate(drawMaterial, false);
        if (circleMesh != null) DestroyImmediate(circleMesh, false);

        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    private void OnUndoRedo()
    {
        ResetPreview();
    }


    private int threshold = -1;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Rect previewRect = GUILayoutUtility.GetAspectRect(1f);

        if (GUILayout.Button("Generate"))
        {
            GenerateInstances();
        }
        threshold = EditorGUILayout.IntSlider(new GUIContent("Threshold"), threshold, -1, targetPDP.amount);
        
        if (GUILayout.Button(simulating ? "Stop Simulation" : "Simulate"))
        {
            simulating = !simulating;
            if (simulating)
            {
                EditorApplication.delayCall -= FixedSimulate;
                EditorApplication.delayCall += FixedSimulate;
            }
            else
            {
                EditorApplication.delayCall -= FixedSimulate;
            }
        }
        timeScale = EditorGUILayout.Slider("Time Scale", timeScale, 0f, 10f);

        if (GUILayout.Button("Calculate"))
        {
            Instance.Order(instances);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply"))
        {
            ApplyPreview();
        }
        if (GUILayout.Button("Reset Preview"))
        {
            ResetPreview();
        }
        EditorGUILayout.EndHorizontal();


        DrawTexture(previewRect);
    }

    static bool simulating = false;
    static float timeScale = 1f;
    void FixedSimulate()
    {
        int steps = (int) (timeScale * 5);// 0.2 * 5 = 10fps
        for (int i = 0; i < steps; ++i) Simulate(0.02f);
        
        Repaint();
        if (simulating) EditorApplication.delayCall += FixedSimulate;
    }

    #region Simulation

    public struct Instance
    {
        public Vector2 pos;
        public float r;

        public int order;

        public Instance(float x, float y, float r = 1)
        {
            this.pos = new Vector2(x, y);
            this.r = r;

            vel = default(Vector2);
            acc = default(Vector2);
            order = 0;
        }

        public Vector2 vel;
        public Vector2 acc;

        const float intersectionSolveTime = 0.05f;
        const float springK = 0.05f;
        const float springC = 0.5f;

        public static void HandleInteraction(ref Instance inst1, ref Instance inst2)
        {
            Vector2 delta = inst2.pos - inst1.pos;

            delta = Wrap(delta);
            float rSum = inst1.r + inst2.r;

            float d2 = delta.sqrMagnitude;
            float d = Mathf.Sqrt(d2);

            Vector2 deltaV = inst2.vel - inst1.vel;

            Vector2 n = delta / d;
            float sep = d - rSum;
            if (sep > 0)
            {
                // Assuming mass 1 for all instances
                // a comes from the force 1 applies to 2
                Vector2 acc = -sep * n * springK - deltaV * springC;

                inst1.acc -= acc * 0.5f;// 2 to 1
                inst2.acc += acc * 0.5f;// 1 to 2
            }
            else
            {
                float targetDesp = -sep * 0.5f;
                float halfAcc = targetDesp / (intersectionSolveTime * intersectionSolveTime);// dx / (t*t) = acc / 2 

                inst1.acc -= halfAcc * n;
                inst2.acc += halfAcc * n;
                // Cancel approach velocity
                float dotNDeltaV = Vector2.Dot(n, deltaV);
                if (dotNDeltaV < 0)
                {
                    float velCancel = -dotNDeltaV * 0.5f;
                    inst1.vel -= velCancel * n;
                    inst2.vel += velCancel * n;
                }
            }
        }

        public void Simulate(float step)
        {
            Vector2 deltaV = step * acc;

            pos += step * (vel + deltaV * 0.5f);

            vel += deltaV;

            acc = default(Vector2);

            pos = Wrap(pos);
        }

        public static Vector2 Wrap(Vector2 p)
        {
            float min = -0.5f * PropDitherPattern.CellSize, max = 0.5f * PropDitherPattern.CellSize;

            if (p.x > max) p.x -= PropDitherPattern.CellSize;
            else if (p.x <= min) p.x += PropDitherPattern.CellSize;
            if (p.y > max) p.y -= PropDitherPattern.CellSize;
            else if (p.y <= min) p.y += PropDitherPattern.CellSize;

            return p;
        }

        public static void Order(Instance[] instances)
        {
            int count = instances.Length;
            instances[0].order = 1;
            for (int i = 1; i < count; ++i) instances[i].order = 0;
            int order = 2;
            while (order <= count)
            {
                float best = float.MaxValue;
                int bestIndex = 0;
                for (int i = 1; i < count; ++i)
                {
                    if (instances[i].order > 0) continue;
                    float meanInvDist = 0;
                    for (int j = 0; j < count; ++j)
                    {
                        if (instances[j].order <= 0) continue;
                        Instance i1 = instances[i], i2 = instances[j];

                        Vector2 delta = i2.pos - i1.pos;
                        delta = Wrap(delta);

                        meanInvDist += 1 / delta.magnitude;
                    }
                    meanInvDist /= order;
                    if (meanInvDist < best)
                    {
                        best = meanInvDist;
                        bestIndex = i;
                    }
                }
                instances[bestIndex].order = order;

                order++;
            }
        }
    }

    private Instance[] instances;

    public void GenerateInstances()
    {
        instances = new Instance[targetPDP.amount];

        for (int i = 0; i < instances.Length; ++i)
        {
            float x = (Random.value - 0.5f) * PropDitherPattern.CellSize;
            float y = (Random.value - 0.5f) * PropDitherPattern.CellSize;
            float r = targetPDP.minR + Random.value * (targetPDP.maxR - targetPDP.minR);

            instances[i] = new Instance(x, y, r);
            instances[i].vel += new Vector2(1f, 0.5f);
        }
    }

    public void Simulate(float step)
    {
        if (instances == null) return;
        //Interactions
        for (int i = 0; i < instances.Length; ++i)
        {
            for (int j = i + 1; j < instances.Length; ++j)
            {
                Instance.HandleInteraction(ref instances[i], ref instances[j]);
            }
        }

        for (int i = 0; i < instances.Length; ++i)
        {
            instances[i].Simulate(step);
        }
    }
    #endregion
    
    public void DrawInstances()
    {
        if (instances == null) return;

        drawMaterial.color = new Color(1f, 0.5f, 1f);
        drawMaterial.SetPass(0);

        for (int i = 0; i < instances.Length; ++i)
        {
            Instance ins = instances[i];

            Matrix4x4 matrix = Matrix4x4.TRS(ins.pos, Quaternion.identity, new Vector3(ins.r, ins.r, ins.r));
            Graphics.DrawMeshNow(circleMesh, matrix * baseMatrix, 0);
        }
        
        for (int i = 0; i < instances.Length; ++i)
        {
            Instance ins = instances[i];

            Matrix4x4 matrix = Matrix4x4.TRS(ins.pos, Quaternion.identity, new Vector3(ins.r, ins.r, ins.r));

            float value;
            if (threshold < 0) value = (float)ins.order / instances.Length;
            else value = threshold >= ins.order ? 1f : 0f;
            drawMaterial.color = new Color(1f, 0.5f, 1f, value);
            drawMaterial.SetPass(1);

            Graphics.DrawMeshNow(circleMesh, matrix * baseMatrix, 1);
        }
    }

    Camera previewCamera;
    void CameraInit() {
        if (previewCamera != null) return;
        GameObject cameraObject = new GameObject("__PropDitherPattern_PreviewCamera", typeof(Camera));
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.SetActive(false);

        previewCamera = cameraObject.GetComponent<Camera>();

        previewCamera.orthographic = true;
        previewCamera.aspect = 1f;
        previewCamera.orthographicSize = PropDitherPattern.CellSize * 0.5f;
        previewCamera.backgroundColor = new Color(0, 0, 0);
        previewCamera.cullingMask = 0;
        previewCamera.clearFlags = CameraClearFlags.Depth;
    }

    Matrix4x4 baseMatrix = Matrix4x4.TRS(Vector3.forward, Quaternion.identity, Vector3.one);
    
    private void DrawTexture(Rect previewRect)
    {
        if (previewCamera == null)
        {
            previewCamera = new GameObject("__PropDItherPattern_PreviewCamera").AddComponent<Camera>();
            previewCamera.hideFlags = HideFlags.HideAndDontSave;
        }
        
        EditorGUI.DrawRect(previewRect, Color.black);
        Handles.SetCamera(previewRect, previewCamera);
          
        DrawInstances();

        //Handles.DrawCamera(previewRect, previewCamera,
        //                    DrawCameraMode.Normal);//Other than normal draws light/cam gizmos
                           
    }
    
    void ApplyPreview()
    {
        Undo.RecordObject(targetPDP, "Pattern Preview Applied");
        System.Array.Resize(ref targetPDP.elements, instances.Length);
        for (int i = 0; i < instances.Length; ++i)
        {
            int order = i + 1;
            Instance instance = System.Array.Find(instances, (ins) => ins.order == order);

            targetPDP.elements[i] = new PropDitherPattern.PatternElement()
            {
                pos = instance.pos,
                r = instance.r,

                rand0 = Random.value,
                rand1 = Random.value,
                rand2 = Random.value,
                rand3 = Random.value
            };
        }
    }

    void ResetPreview()
    {
        if (targetPDP.elements == null) return;
        int count = targetPDP.elements.Length;
        System.Array.Resize(ref instances, count);
        for (int i = 0; i < count; ++i)
        {
            int order = i + 1;

            PropDitherPattern.PatternElement element = targetPDP.elements[i];

            instances[i] = new Instance(element.pos.x, element.pos.y, element.r)
            {
                order = order
            };
        }
    }
}
