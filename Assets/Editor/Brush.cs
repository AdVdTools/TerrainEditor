using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public enum BrushEvent { None, BrushDraw, BrushPaint, BrushPaintEnd, BrushChanged }

[System.Serializable]
public class Brush
{
    private const int MODES = 5;
    public enum Mode { Add, Substract, Set, Average, Smooth }
    public enum Type { Sharp, Smooth }//TODO more?
    public enum Projection { Sphere, Vertical, View }//TODO merge view and perspective?
                                                     //TODO math vs curve brush?

    public Mode mode = Mode.Add;
    public float amount = 1f;
    public float opacity = 1f;
    public float radius = 5f;
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
                //Vector3 ld = mapTransform.InverseTransformVector(camera.transform.forward);// center - camPosition;

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
    

    private static Rect windowPosition = new Rect(10f, 20f, 0f, 0f);
    private static bool editBrush;
    public static Brush currentBrush = new Brush();

    private static bool brushChanged;

    public static BrushEvent CheckBrushEvent()
    {
        int controlId = GUIUtility.GetControlID(brushEditorGUIContent, FocusType.Passive);
        EventType type = Event.current.type;
        bool leftClick = Event.current.button == 0;

        if (Event.current.type == EventType.Repaint)
        {
            return BrushEvent.BrushDraw;
        }
        else if (type == EventType.Layout)
        {//This will allow clicks to be eaten
            HandleUtility.AddDefaultControl(controlId);
        }
        else if ((type == EventType.MouseDown || type == EventType.MouseDrag) && leftClick)
        {
            Event.current.Use();
            return BrushEvent.BrushPaint;
        }
        else if (type == EventType.MouseUp && leftClick)
        {
            Event.current.Use();
            return BrushEvent.BrushPaintEnd;
        }
        else {
            if (type == EventType.ScrollWheel && Event.current.control)
            {
                currentBrush.radius -= Event.current.delta.y * 0.5f;
                Event.current.Use();
                brushChanged = true;
                return BrushEvent.BrushChanged;
            }
            else if (type == EventType.ScrollWheel && Event.current.alt)
            {
                currentBrush.opacity -= Event.current.delta.y * 0.0625f;
                currentBrush.opacity = Mathf.Round(currentBrush.opacity * 100f) * 0.01f;
                Event.current.Use();
                brushChanged = true;
                return BrushEvent.BrushChanged;
            }
            else if (type == EventType.KeyUp && Event.current.keyCode == KeyCode.Tab && Event.current.control)
            {
                currentBrush.mode = (Mode)(((int)currentBrush.mode + MODES + (Event.current.shift ? -1 : +1)) % MODES);
                Event.current.Use();
                brushChanged = true;
                return BrushEvent.BrushChanged;
            }
            else if (type == EventType.MouseDrag || type == EventType.MouseMove)
            {
                brushChanged = false;
            }
        }
        return BrushEvent.None;
    }

    private static readonly GUIContent brushGUIContent = new GUIContent("Brush");
    private static readonly GUIContent brushWindowGUIContent = new GUIContent("BrushWindow");
    private static readonly GUIContent editBrushGUIContent = new GUIContent("Edit Brush");
    private static readonly GUIContent brushEditorGUIContent = new GUIContent("BrushEditor");

    private static readonly GUIContent brushModeGUIContent = new GUIContent("Brush Mode");
    private static readonly GUIContent amountGUIContent = new GUIContent("Amount");
    private static readonly GUIContent opacityGUIContent = new GUIContent("Opacity");
    private static readonly GUIContent radiusGUIContent = new GUIContent("Radius");
    private static readonly GUIContent brushTypeGUIContent = new GUIContent("Brush Type");
    private static readonly GUIContent projectionGUIContent = new GUIContent("Projection");


    public static void DrawBrushWindow()
    {
        GUI.skin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene);
        windowPosition = GUILayout.Window(GUIUtility.GetControlID(brushWindowGUIContent, FocusType.Passive), windowPosition, DrawBrushWindow, brushGUIContent);
    }
    private static void DrawBrushWindow(int id)
    {
        EditorGUIUtility.labelWidth = 80f;
        Color normalColor = EditorStyles.label.normal.textColor;
        EditorStyles.label.normal.textColor = GUI.skin.label.normal.textColor;//Gets window greyish font color
        
        editBrush = GUILayout.Toggle(editBrush, editBrushGUIContent, EditorStyles.miniButton);
        if (editBrush || brushChanged)
        {
            currentBrush.mode = (Brush.Mode)EditorGUILayout.EnumPopup(brushModeGUIContent, currentBrush.mode);
            GUI.enabled = currentBrush.mode != Mode.Average && currentBrush.mode != Mode.Smooth;
            currentBrush.amount = EditorGUILayout.FloatField(amountGUIContent, currentBrush.amount);
            GUI.enabled = true;
            currentBrush.opacity = EditorGUILayout.FloatField(opacityGUIContent, currentBrush.opacity * 100f) * 0.01f;
            currentBrush.radius = EditorGUILayout.FloatField(radiusGUIContent, currentBrush.radius);
            currentBrush.type = (Brush.Type)EditorGUILayout.EnumPopup(brushTypeGUIContent, currentBrush.type);
            currentBrush.projection = (Brush.Projection)EditorGUILayout.EnumPopup(projectionGUIContent, currentBrush.projection);

            currentBrush.opacity = Mathf.Clamp01(currentBrush.opacity);
            currentBrush.radius = Mathf.Max(currentBrush.radius, 0.01f);
        }
        else
        {
            windowPosition.size = new Vector2(100f, 40f);//Min size
        }

        EditorStyles.label.normal.textColor = normalColor;
        GUI.BringWindowToFront(id);
        GUI.DragWindow();
    }

}