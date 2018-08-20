﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;

public enum BrushEvent { None, BrushDraw, BrushPaintStart, BrushPaint, BrushPaintEnd, BrushChanged, ValuePick }

[System.Serializable]
public class Brush
{
    private const int MODES = 5;
    public enum Mode { Set, Add, Substract, Average, Smooth }//TODO include picker? or move to separate variable
    public enum Projection { Sphere, Vertical, View }//TODO merge view and perspective?

    public Mode mode = Mode.Add;
    public float opacity = 1f;
    public float size = 5f;
    public Projection projection = Projection.Sphere;

    private const string brushTexturesPath = "Assets/Editor/BrushTextures";
    private Texture2D[] brushTextures;
    private GUIContent[] brushTextureGUIContents;

    public Brush()
    {
        if (Directory.Exists(brushTexturesPath))
        {
            string[] textureFiles = Directory.GetFiles(brushTexturesPath, "*.png");
            brushTextures = System.Array.ConvertAll(textureFiles, (path) => EditorGUIUtility.Load(path) as Texture2D);
            brushTextureGUIContents = System.Array.ConvertAll(textureFiles, (path) => new GUIContent(Path.GetFileNameWithoutExtension(path)));
            currentBrushTextureIndex = System.Array.FindIndex(brushTextureGUIContents, (c) => c.text.Contains("Smooth"));
            if (currentBrushTextureIndex >= 0) SetBrushTexture(currentTexture);
            else currentBrushTextureIndex = 0;
        }
        else
        {
            brushTextures = new Texture2D[0];
            brushTextureGUIContents = new GUIContent[0];
        }
    }
    
    private int currentBrushTextureIndex;
    public Texture2D currentTexture {
        get {
            if (currentBrushTextureIndex < 0 || currentBrushTextureIndex >= brushTextures.Length) return null;
            else return brushTextures[currentBrushTextureIndex];
        }
    }
    private Color[] currentBrushTexturePixels;
    private int currentBrushTextureWidth, currentBrushTextureHeight;
    private void SetBrushTexture(Texture2D texture)
    {
        if (texture == null)
        {
            currentBrushTexturePixels = null;
            currentBrushTextureWidth = 0;
            currentBrushTextureHeight = 0;
        }
        else
        {
            //TODO use native array?//Texture.GetRawPixels
            currentBrushTexturePixels = texture.GetPixels();
            currentBrushTextureWidth = texture.width;
            currentBrushTextureHeight = texture.height;
        }
    }
    
    public float GetStrength(Vector3 projectedOffset)
    {
        Texture2D t = currentTexture;
        if (t == null) return opacity;
        float zScale = 1f / Mathf.Sqrt(1 - projectedOffset.z * projectedOffset.z);
        if (float.IsInfinity(zScale) || float.IsNaN(zScale)) return 0f;
        Vector2 coords = new Vector2(projectedOffset.x, projectedOffset.y) * zScale;
        //if (Mathf.Abs(coords.x) > 1f || Mathf.Abs(coords.y) > 1f) return 0f;
        //TODO should invert Y?
        return SampleCurrentTextureAlpha(coords * 0.5f + new Vector2(0.5f, 0.5f)) * opacity;
    }

    private float SampleCurrentTextureAlpha(Vector2 coords)
    {
        coords.x *= currentBrushTextureWidth;
        coords.y *= currentBrushTextureHeight;
        int x = Mathf.FloorToInt(coords.x);
        if (x < 0 || x >= currentBrushTextureWidth - 1) return 0f;
        int y = Mathf.FloorToInt(coords.y);
        if (y < 0 || y >= currentBrushTextureHeight - 1) return 0f;
        int index = x + y * currentBrushTextureWidth;
        float a00 = currentBrushTexturePixels[index].a;
        //TODO remove interpolation for speed! (avoid substraction in previus ifs too)
        float a01 = currentBrushTexturePixels[index + 1].a;
        float a10 = currentBrushTexturePixels[index + currentBrushTextureWidth].a;
        float a11 = currentBrushTexturePixels[index + currentBrushTextureWidth + 1].a;
        float u0 = coords.x - x, u1 = 1 - u0;
        float v0 = coords.y - y, v1 = 1 - v0;
        return a00 * u1 * v1 + a01 * u0 * v1 + a10 * u1 * v0 + a11 * u0 * v0;
    }

    public Matrix4x4 GetProjectionMatrix(Vector3 center, Transform mapTransform, Camera camera)
    {
        switch (projection)
        {
            case Projection.Sphere:
                Matrix4x4 sm = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(size, size, size)).inverse;
                Vector4 r1 = sm.GetRow(1); sm.SetRow(1, sm.GetRow(2)); sm.SetRow(2, r1);// Swap Z and Y
                return sm;
            case Projection.Vertical:
                Matrix4x4 vm = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(size, size, size)).inverse;
                vm.SetRow(1, vm.GetRow(2));// Move Z to Y
                vm.SetRow(2, Vector4.zero);// Remove Z
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
                pm.SetRow(3, pm.GetRow(3) * (camera.orthographic ? size * 50f : size / 50f));
                return pm;//TODO keep in mind map transform!
            default:
                return Matrix4x4.identity;
        }
    }
    

    private Rect windowPosition = new Rect(10f, 20f, 0f, 0f);
    private bool editBrush;
    public static Brush currentBrush = new Brush();

    private bool brushChanged;

    private bool dragging;

    public BrushEvent CheckBrushEvent()
    {
        int controlId = GUIUtility.GetControlID(brushEditorGUIContent, FocusType.Passive);
        EventType type = Event.current.type;
        bool leftClick = Event.current.button == 0;
        bool focused = EditorWindow.focusedWindow == SceneView.currentDrawingSceneView;

        if (pickingValue)
        {
            if (type == EventType.Repaint)
            {
                //TODO
            }
            else if (type == EventType.Layout)
            {//This will allow clicks to be eaten
                HandleUtility.AddDefaultControl(controlId);
            }
            else if (leftClick && (type == EventType.MouseDown || type == EventType.MouseDrag))
            {
                Event.current.Use();
                return BrushEvent.ValuePick;
            }
            if (type == EventType.MouseUp) {
                pickingValue = !pickingValue;
                Event.current.Use();
            }
        }
        else
        {
            if (type == EventType.Repaint && focused)
            {
                return BrushEvent.BrushDraw;
            }
            else if (type == EventType.Layout)
            {//This will allow clicks to be eaten
                HandleUtility.AddDefaultControl(controlId);
            }
            else if (type == EventType.MouseDown && focused && leftClick)
            {
                dragging = true;
                Event.current.Use();
                return BrushEvent.BrushPaintStart;
            }
            else if (type == EventType.MouseDrag && focused && dragging && leftClick)
            {
                Event.current.Use();
                return BrushEvent.BrushPaint;
            }
            else if (type == EventType.MouseUp && leftClick)
            {
                dragging = false;
                Event.current.Use();
                return BrushEvent.BrushPaintEnd;
            }
            else
            {
                return HandleBrushShortcuts();
            }
        }
        return BrushEvent.None;
    }

    public void SetPeekValue(float v)
    {
        pickingFloatValue = v;
    }
    public void SetPeekValue(Vector4 v)
    {
        pickingVectorValue = v;
    }
    public void SetPeekValue(Color v)
    {
        pickingColorValue = v;
    }

    public void AcceptPeekValue()
    {
        switch (currentValueType)
        {
            case ValueType.Float:
                floatValue = pickingFloatValue;
                break;
            case ValueType.Color:
                colorValue = pickingColorValue;
                break;
            default:
                vectorValue = pickingVectorValue;
                break;
        }
    }

    public BrushEvent HandleBrushShortcuts()
    {
        EventType type = Event.current.type;
        if (type == EventType.ScrollWheel && Event.current.control)
        {
            currentBrush.size -= Event.current.delta.y * 0.5f;
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
        else if (type == EventType.KeyUp && Event.current.keyCode == KeyCode.C)
        {
            pickingValue = true;
            Event.current.Use();
        }
        else if (type == EventType.MouseDrag || type == EventType.MouseMove)
        {
            brushChanged = false;
        }
        return BrushEvent.None;
    }

    private static readonly GUIContent brushGUIContent = new GUIContent("Brush");
    private static readonly GUIContent brushWindowGUIContent = new GUIContent("BrushWindow");
    private static readonly GUIContent editBrushGUIContent = new GUIContent("Edit Brush");
    private static readonly GUIContent brushEditorGUIContent = new GUIContent("BrushEditor");

    private static readonly GUIContent brushModeGUIContent = new GUIContent("Brush Mode", "Ctrl+[Shift+]Tab");
    private static readonly GUIContent opacityGUIContent = new GUIContent("Opacity", "Alt+ScrollWheel");
    private static readonly GUIContent sizeGUIContent = new GUIContent("Size", "Ctrl+ScrollWheel");
    private static readonly GUIContent brushTexGUIContent = new GUIContent("Brush Tex");
    private static readonly GUIContent projectionGUIContent = new GUIContent("Projection");


    public void DrawBrushWindow()
    {
        GUI.skin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene);
        windowPosition = GUILayout.Window(GUIUtility.GetControlID(brushWindowGUIContent, FocusType.Passive), windowPosition, currentBrush.DrawBrushWindow, brushGUIContent);
    }
    private void DrawBrushWindow(int id)
    {
        EditorGUIUtility.labelWidth = 80f;
        Color normalColor = EditorStyles.label.normal.textColor;
        EditorStyles.label.normal.textColor = GUI.skin.label.normal.textColor;//Gets window greyish font color
        
        editBrush = GUILayout.Toggle(editBrush, editBrushGUIContent, EditorStyles.miniButton);
        if (editBrush || brushChanged)
        {
            mode = (Brush.Mode)EditorGUILayout.EnumPopup(brushModeGUIContent, mode);
            opacity = EditorGUILayout.FloatField(opacityGUIContent, opacity * 100f) * 0.01f;
            size = EditorGUILayout.FloatField(sizeGUIContent, size);
            GUI.changed = false;
            currentBrushTextureIndex = EditorGUILayout.Popup(brushTexGUIContent, currentBrushTextureIndex, brushTextureGUIContents);
            if (GUI.changed) SetBrushTexture(currentTexture);
            projection = (Brush.Projection)EditorGUILayout.EnumPopup(projectionGUIContent, projection);

            opacity = Mathf.Clamp01(opacity);
            size = Mathf.Max(size, 0.01f);
        }
        else
        {
            windowPosition.size = new Vector2(100f, 40f);//Min size
        }

        EditorStyles.label.normal.textColor = normalColor;
        GUI.BringWindowToFront(id);
        GUI.DragWindow();
    }
    
    public enum ValueType { Float, Vector2, Vector3, Vector4, Color }
    public ValueType currentValueType;

    public float floatValue = 1f;
    public Vector4 vectorValue;
    public Color colorValue = Color.white;
    private bool mask0 = true, mask1 = true, mask2 = true, mask3 = true;

    public Vector4 VectorMask {
        get { return new Vector4(mask0 ? 1f : 0f, mask1 ? 1f : 0f, mask2 ? 1f : 0f, mask3 ? 1f : 0f); }
    }

    public Color ColorMask
    {
        get { return new Color(mask0 ? 1f : 0f, mask1 ? 1f : 0f, mask2 ? 1f : 0f, mask3 ? 1f : 0f); }
    }

    private bool pickingValue;

    private float pickingFloatValue;
    private Vector4 pickingVectorValue;
    private Color pickingColorValue;

    readonly GUIContent valueGUIContent = new GUIContent("Value");
    readonly GUIContent maskGUIContent = new GUIContent("Mask");
    readonly GUIContent pickValueButtonContent = new GUIContent("Pick Value", "C");

    readonly GUIContent RContent = new GUIContent("R");
    readonly GUIContent GContent = new GUIContent("G");
    readonly GUIContent BContent = new GUIContent("B");
    readonly GUIContent AContent = new GUIContent("A");

    readonly GUIContent XContent = new GUIContent("X");
    readonly GUIContent YContent = new GUIContent("Y");
    readonly GUIContent ZContent = new GUIContent("Z");
    readonly GUIContent WContent = new GUIContent("W");

    public void DrawBrushValueInspector()//TODO out values
    {
        //TODO check value mode (color vs float, vs Vector?)
        bool enableValueFields = Brush.currentBrush.mode != Brush.Mode.Average && Brush.currentBrush.mode != Brush.Mode.Smooth;
        switch (currentValueType)
        {
            case ValueType.Float:
                GUI.enabled = enableValueFields;
                floatValue = EditorGUILayout.FloatField(valueGUIContent, floatValue);
                break;
            case ValueType.Vector2:
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(maskGUIContent);
                mask0 = GUILayout.Toggle(mask0, XContent, EditorStyles.miniButtonLeft);
                mask1 = GUILayout.Toggle(mask1, YContent, EditorStyles.miniButtonRight);
                EditorGUILayout.EndHorizontal();

                GUI.enabled = enableValueFields;
                vectorValue = EditorGUILayout.Vector2Field(valueGUIContent, vectorValue);
                break;
            case ValueType.Vector3:
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(maskGUIContent);
                mask0 = GUILayout.Toggle(mask0, XContent, EditorStyles.miniButtonLeft);
                mask1 = GUILayout.Toggle(mask1, YContent, EditorStyles.miniButtonMid);
                mask2 = GUILayout.Toggle(mask2, ZContent, EditorStyles.miniButtonRight);
                EditorGUILayout.EndHorizontal();

                GUI.enabled = enableValueFields;
                vectorValue = EditorGUILayout.Vector3Field(valueGUIContent, vectorValue);
                break;
            case ValueType.Vector4:
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(maskGUIContent);
                mask0 = GUILayout.Toggle(mask0, XContent, EditorStyles.miniButtonLeft);
                mask1 = GUILayout.Toggle(mask1, YContent, EditorStyles.miniButtonMid);
                mask2 = GUILayout.Toggle(mask2, ZContent, EditorStyles.miniButtonMid);
                mask3 = GUILayout.Toggle(mask3, WContent, EditorStyles.miniButtonRight);
                EditorGUILayout.EndHorizontal();

                GUI.enabled = enableValueFields;
                vectorValue = EditorGUILayout.Vector4Field(valueGUIContent, vectorValue);
                break;
            case ValueType.Color:
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(maskGUIContent);
                mask0 = GUILayout.Toggle(mask0, RContent, EditorStyles.miniButtonLeft);
                mask1 = GUILayout.Toggle(mask1, GContent, EditorStyles.miniButtonMid);
                mask2 = GUILayout.Toggle(mask2, BContent, EditorStyles.miniButtonMid);
                mask3 = GUILayout.Toggle(mask3, AContent, EditorStyles.miniButtonRight);
                EditorGUILayout.EndHorizontal();
                
                GUI.enabled = enableValueFields;
                colorValue = EditorGUILayout.ColorField(valueGUIContent, colorValue);
                break;
        }
        GUI.enabled = true;
        pickingValue = GUILayout.Toggle(pickingValue, pickValueButtonContent, EditorStyles.miniButton, GUILayout.Width(80f));
    }

}