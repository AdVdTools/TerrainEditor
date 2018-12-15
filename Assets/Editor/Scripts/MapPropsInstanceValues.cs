using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[System.Serializable]
public class MapPropsInstanceValues {

    //TODO keep diferent values for each brush mode, both here and in the Brush class!?!
    //TODO static class to handle instance inspector stuff
    public const int DISABLED_STATE = 0;
    public const int SINGLE_STATE = 1;
    public const int RANGE_STATE = 2;
    

    public int sizeState;
    public float minSize = 1f, maxSize = 1.2f;

    public int yOffsetState;
    public float minYOffset = 0f, maxYOffset = 0;

    public int rotationState;
    public float minRotation = 0f, maxRotation = 360f;
    
    public int alignmentState;
    public float minAlignment = 0.0f, maxAlignment = 1f;

    public int colorState;
    public Color singleColor = Color.white;
    public Gradient colorGradient = new Gradient();

    public int variantState;
    public int variantIndex = 0;
    public int variantCount = 1;
    
    private static readonly GUIContent sizeGUIContent = new GUIContent("Size");
    private static readonly GUIContent yOffsetGUIContent = new GUIContent("Y Offset");
    private static readonly GUIContent rotationGUIContent = new GUIContent("Rotation");
    private static readonly GUIContent alignmentGUIContent = new GUIContent("Normal Aligned");
    private static readonly GUIContent colorGUIContent = new GUIContent("Color");
    private static readonly GUIContent variantGUIContent = new GUIContent("Variant");

    private static readonly GUIContent singleGUIContent = new GUIContent("Single");
    private static readonly GUIContent rangeGUIContent = new GUIContent("Range");
    private static readonly GUIContent disabledGUIContent = new GUIContent("Disabled");

    private static readonly GUIContent randomGUIContent = new GUIContent("Random");

    private static readonly GUIContent valueGUIContent = new GUIContent("Value");
    private static readonly GUIContent valuesGUIContent = new GUIContent("Values");
    private static readonly GUIContent minGUIContent = new GUIContent("Min");
    private static readonly GUIContent maxGUIContent = new GUIContent("Max");

    public void DoInstancePropertiesInspector()
    {
        //Scale
        EditorGUILayout.LabelField(sizeGUIContent, EditorStyles.boldLabel);
        InstancePropertyInspector(ref sizeState, ref minSize, ref maxSize, 0f, float.MaxValue);

        //Y Offset
        EditorGUILayout.LabelField(yOffsetGUIContent, EditorStyles.boldLabel);
        InstancePropertyInspector(ref yOffsetState, ref minYOffset, ref maxYOffset, float.MinValue, float.MaxValue);
        
        //Rotation
        EditorGUILayout.LabelField(rotationGUIContent, EditorStyles.boldLabel);
        InstancePropertyInspector(ref rotationState, ref minRotation, ref maxRotation, -360f, 360);
        
        //Vertical vs Normal aligned
        EditorGUILayout.LabelField(alignmentGUIContent, EditorStyles.boldLabel);
        InstancePropertyInspector(ref alignmentState, ref minAlignment, ref maxAlignment, 0f, 1f);
        //if (alignmentState != DISABLED_STATE) {
        //    Vector3Inspector(directionGUIContent, ref alignDirection);//  alignDirecton = EditorGUILayout.Vector3Field(directionGUIContent, alignDirection);
        //    alignDirection = alignDirection.normalized;
        //}

        //Color gradient
        EditorGUILayout.LabelField(colorGUIContent, EditorStyles.boldLabel);
        SpecialColorInspector(ref colorState, ref singleColor, ref colorGradient);
        
        //Variant
        EditorGUILayout.LabelField(variantGUIContent, EditorStyles.boldLabel);
        SpecialVariantInspector(ref variantState, ref variantIndex);
    }

    private void InstancePropertyInspector(ref int propertyState, ref float minValue, ref float maxValue, float minLimit, float maxLimit)
    {
        float labelWidth = EditorGUIUtility.labelWidth;
        EditorGUILayout.BeginHorizontal();
        bool boolState = propertyState != DISABLED_STATE;
        GUIContent guiContent = boolState ? (propertyState == SINGLE_STATE ? singleGUIContent : rangeGUIContent) : disabledGUIContent;
        bool newState = GUILayout.Toggle(boolState, guiContent, EditorStyles.miniButton, GUILayout.Width(labelWidth * 0.5f));
        if (boolState != newState)
        {
            propertyState = (propertyState + 1) % 3;
        }
        EditorGUIUtility.labelWidth = 40f;
        switch (propertyState)
        {
            case SINGLE_STATE:
                minValue = EditorGUILayout.FloatField(valueGUIContent, minValue);
                minValue = Mathf.Clamp(minValue, minLimit, maxLimit);
                break;
            case RANGE_STATE:
                minValue = EditorGUILayout.FloatField(minGUIContent, minValue);
                if (maxValue < minValue) maxValue = minValue;
                maxValue = EditorGUILayout.FloatField(maxGUIContent, maxValue);
                if (minValue > maxValue) minValue = maxValue;
                minValue = Mathf.Clamp(minValue, minLimit, maxLimit);
                maxValue = Mathf.Clamp(maxValue, minLimit, maxLimit);
                break;
            default:
                bool guiEnabled = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.LabelField(GUIContent.none, EditorStyles.textField);
                GUI.enabled = guiEnabled;
                break;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUIUtility.labelWidth = labelWidth;
    }

    //TODO rename color-tint?
    private void SpecialColorInspector(ref int colorState, ref Color singleColor, ref Gradient colorGradient)
    {
        float labelWidth = EditorGUIUtility.labelWidth;
        EditorGUILayout.BeginHorizontal();
        bool boolState = colorState != DISABLED_STATE;
        GUIContent guiContent = boolState ? (colorState == SINGLE_STATE ? singleGUIContent : rangeGUIContent) : disabledGUIContent;
        bool newState = GUILayout.Toggle(boolState, guiContent, EditorStyles.miniButton, GUILayout.Width(labelWidth * 0.5f));
        if (boolState != newState)
        {
            colorState = (colorState + 1) % 3;
        }
        EditorGUIUtility.labelWidth = 40f;
        switch (colorState)
        {
            case SINGLE_STATE:
                singleColor = EditorGUILayout.ColorField(valueGUIContent, singleColor);
                break;
            case RANGE_STATE:
                colorGradient = GradientField(valuesGUIContent, colorGradient);
                break;
            default:
                bool guiEnabled = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.LabelField(GUIContent.none, EditorStyles.textField);
                GUI.enabled = guiEnabled;
                break;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUIUtility.labelWidth = labelWidth;
    }

    private static System.Reflection.MethodInfo GradientFieldMethod;
    private static Gradient GradientField(GUIContent content, Gradient gradient)
    {
        if (GradientFieldMethod == null)
        {
            var methods = typeof(EditorGUILayout).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            //System.Array.ForEach(methods, (m) => {
            //    if (m.Name == "GradientField") {
            //        string parameters = string.Join(", ", System.Array.ConvertAll(m.GetParameters(), (param) => param.Name + " " + param.ParameterType));
            //        Debug.Log(m.ReturnType + " " + m.Name + " (" + parameters + ")");
            //    }
            //});
            GradientFieldMethod = System.Array.Find(methods, (m) => {
                if (m.Name == "GradientField")
                {
                    var p = m.GetParameters();
                    return (p.Length >= 2 && p[0].ParameterType == typeof(GUIContent) && p[1].ParameterType == typeof(Gradient));
                }
                else return false;
            });//TODO get third? check logs
        }
        if (GradientFieldMethod != null)
        {
            //EditorGUILayout.HelpBox(string.Join(", ", System.Array.ConvertAll(GradientFieldMethod.GetParameters(), (param) => param.Name +" "+param.ParameterType)), MessageType.Info);
            gradient = GradientFieldMethod.Invoke(null, new object[] { content, gradient, new GUILayoutOption[0] }) as Gradient;//TODO this signature might not exist! check in editorGui instead of editorguilayout
        }
        else
        {
            EditorGUILayout.HelpBox("GradientField method not found", MessageType.Error);
        }
        return gradient;
    }

    //private void Vector3Inspector(GUIContent content, ref Vector3 value)
    //{
    //    float labelWidth = EditorGUIUtility.labelWidth;
    //    EditorGUILayout.BeginHorizontal();

    //    EditorGUILayout.LabelField(content, GUILayout.Width(labelWidth * 0.5f));

    //    EditorGUIUtility.labelWidth = 40f;

    //    value = EditorGUILayout.Vector3Field(GUIContent.none, value);

    //    EditorGUILayout.EndHorizontal();
    //    EditorGUIUtility.labelWidth = labelWidth;
    //}

    private void SpecialVariantInspector(ref int variantState, ref int variantIndex)
    {
        float labelWidth = EditorGUIUtility.labelWidth;
        EditorGUILayout.BeginHorizontal();
        bool boolState = variantState != DISABLED_STATE;
        GUIContent guiContent = boolState ? (variantState == SINGLE_STATE ? singleGUIContent : randomGUIContent) : disabledGUIContent;
        bool newState = GUILayout.Toggle(boolState, guiContent, EditorStyles.miniButton, GUILayout.Width(labelWidth * 0.5f));
        if (boolState != newState)
        {
            variantState = (variantState + 1) % 3;
        }
        EditorGUIUtility.labelWidth = 40f;
        switch (variantState)
        {
            case SINGLE_STATE:
                variantIndex = EditorGUILayout.IntField(valueGUIContent, variantIndex);
                variantIndex = Mathf.Max(0, variantIndex);
                break;
            case RANGE_STATE:
                variantCount = EditorGUILayout.IntField(rangeGUIContent, variantCount);
                variantCount = Mathf.Max(1, variantCount);
                break;
            default:
                bool guiEnabled = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.LabelField(GUIContent.none, EditorStyles.textField);
                GUI.enabled = guiEnabled;
                break;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUIUtility.labelWidth = labelWidth;
    }


    //TODO fixes
    public MapData.PropInstance ApplyValues(MapData.PropInstance instance, Vector3 normal, float strength)
    {
        if (sizeState != DISABLED_STATE)
        {
            float targetSize = minSize;
            if (sizeState == RANGE_STATE)
            {
                targetSize += (maxSize - minSize) * Random.value;
            }
            instance.size += (targetSize - instance.size) * strength;
        }


        if (yOffsetState != DISABLED_STATE)
        {
            float targetYOffset = minYOffset;
            if (yOffsetState == RANGE_STATE)
            {
                targetYOffset += (maxYOffset - minYOffset) * Random.value;
            }
            instance.position.y += (targetYOffset - instance.position.y) * strength;
        }


        if (rotationState != DISABLED_STATE)
        {
            float targetRotation = minRotation;
            if (rotationState == RANGE_STATE)
            {
                targetRotation += (maxRotation - minRotation) * Random.value;
            }
            instance.rotation += (targetRotation - instance.rotation) * strength;
        }


        if (alignmentState != DISABLED_STATE)//TODO rename to normal aligned? //TODO do this on mesh build?
        {
            float targetAlign = minAlignment;
            if (alignmentState == RANGE_STATE)
            {
                targetAlign += (maxAlignment - minAlignment) * Random.value;
            }
            instance.alignment += (targetAlign - instance.alignment) * strength;
        }

        if (colorState != DISABLED_STATE)
        {
            Color targetColor = singleColor;
            if (colorState == RANGE_STATE)
            {
                targetColor += colorGradient.Evaluate(Random.value);
            }
            instance.tint += (targetColor - instance.tint) * strength;
        }

        if (variantState != DISABLED_STATE)
        {
            int targetIndex = variantIndex;
            if (variantState == RANGE_STATE)// Random
            {
                targetIndex = Random.Range(0, variantCount);
            }
            instance.variantIndex = targetIndex;
        }

        return instance;
    }
}
