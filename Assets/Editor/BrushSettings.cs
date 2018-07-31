using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

[System.Serializable]
public class Brush
{
    public enum Mode { Add, Substract, Set, Smooth }
    public enum Type { Sharp, Smooth }//TODO more?
    public enum Projection { Sphere, Vertical, View }//TODO merge view and perspective?
                                                     //TODO math vs curve brush?
    public Mode mode = Mode.Add;
    public float amount = 1f;
    public float opacity = 1f;
    public float radius = 1f;
    public Type type = Type.Smooth;
    public Projection projection = Projection.Sphere;

    public static readonly AnimationCurve smoothCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    public float GetStrength(Vector3 projectedOffset)
    {
        float distance = Vector3.Magnitude(projectedOffset);
        if (distance > 1f) return 0f;
        switch (type)
        {
            case Type.Sharp:
                return opacity;
            case Type.Smooth:
                return smoothCurve.Evaluate(1f - distance) * opacity;
            default:
                return opacity;
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
            //    Transform camTransform = camera.transform;

            //    Vector3 camPosition = mapTransform.InverseTransformPoint(camTransform.position);
            //    Vector3 lookDirection = mapTransform.InverseTransformVector(camTransform.forward);// center - camPosition;
            //    Matrix4x4 cm = Matrix4x4.TRS(center, Quaternion.FromToRotation(Vector3.forward, lookDirection), new Vector3(radius, radius, radius)).inverse;

            //    //Quaternion mr = mapTransform.worldToLocalMatrix.rotation;
            //    //Matrix4x4 cm = Matrix4x4.TRS(center, mr * camTransform.rotation, new Vector3(radius, radius, radius)).inverse;
            //    cm.SetRow(2, Vector4.zero);
            //    return cm;
            //case Projection.Perspective:
                Vector3 ld = mapTransform.InverseTransformVector(camera.transform.forward);// center - camPosition;

                //Matrix4x4 pm = camera.projectionMatrix.inverse * camera.worldToCameraMatrix * mapTransform.localToWorldMatrix;// * Matrix4x4.TRS(center, Quaternion.FromToRotation(Vector3.forward, ld), new Vector3(radius, radius, radius)).inverse;
                Matrix4x4 pm = camera.transform.worldToLocalMatrix * mapTransform.localToWorldMatrix;// * Matrix4x4.TRS(center, Quaternion.FromToRotation(Vector3.forward, ld), new Vector3(radius, radius, radius)).inverse;
                pm = camera.projectionMatrix.inverse * pm;
                pm = Matrix4x4.Translate(-pm.MultiplyPoint(center)) * pm;
                pm.SetRow(1, pm.GetRow(1) * camera.aspect);
                pm.SetRow(2, Vector4.zero);
                pm.SetRow(3, pm.GetRow(3) * (camera.orthographic ? radius * 50f : radius / 50f));
                return pm;//TODO keep in mind map transform!
            default:
                return Matrix4x4.identity;
        }
    }
}

public class BrushSettings : ScriptableObject {

    public const string settingsPath = "Assets/Editor/BrushSettings.asset";

    private static BrushSettings instance;
    public static BrushSettings Instance {
        get {
            if (instance == null)
            {
                instance = AssetDatabase.LoadAssetAtPath<BrushSettings>(settingsPath);
                if (instance == null)
                {
                    instance = CreateInstance<BrushSettings>();

                    if (!Directory.Exists(settingsPath)) Directory.CreateDirectory(Directory.GetParent(settingsPath).ToString());
                    AssetDatabase.CreateAsset(instance, settingsPath);
                    AssetDatabase.SaveAssets();
                }
                currentBrush = instance.savedBrushes.Length > 0 ? instance.savedBrushes[0] : new Brush(); ;
            }
            return instance;
        }
    }

    private static Rect windowPosition = new Rect(10f, 20f, 0f, 0f);
    private static bool editBrush;
    public static Brush currentBrush = new Brush();

    [HideInInspector] [SerializeField] Brush[] savedBrushes = new Brush[] { new Brush() };

    public static void DrawBrushWindow()
    {
        windowPosition = GUILayout.Window(GUIUtility.GetControlID(new GUIContent("BrushWindow"), FocusType.Passive), windowPosition, DrawBrushWindow, new GUIContent("Brush"));
    }
    private static void DrawBrushWindow(int id)
    {
        EditorGUIUtility.labelWidth = 120f;
        Color normalColor = EditorStyles.label.normal.textColor;
        EditorStyles.label.normal.textColor = GUI.skin.label.normal.textColor;//Gets window greyish font color

        //TODO foldout label color, change size when folded?
        if (editBrush = EditorGUILayout.Foldout(editBrush, new GUIContent("Edit Brush")))
        {
            currentBrush.mode = (Brush.Mode)EditorGUILayout.EnumPopup(new GUIContent("Brush Mode"), currentBrush.mode);
            currentBrush.amount = EditorGUILayout.FloatField(new GUIContent("Amount"), currentBrush.amount);
            currentBrush.opacity = EditorGUILayout.Slider(new GUIContent("Opacity"), currentBrush.opacity, 0f, 1f);
            currentBrush.radius = EditorGUILayout.FloatField(new GUIContent("Radius"), currentBrush.radius);
            currentBrush.type = (Brush.Type)EditorGUILayout.EnumPopup(new GUIContent("Brush Type"), currentBrush.type);
            currentBrush.projection = (Brush.Projection)EditorGUILayout.EnumPopup(new GUIContent("Projection"), currentBrush.projection);
        }

        EditorStyles.label.normal.textColor = normalColor;
        GUI.BringWindowToFront(id);
        GUI.DragWindow();
    }

}
