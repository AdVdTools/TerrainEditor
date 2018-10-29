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

    public int variantState;
    public int variantIndex = 0;
    public int variantCount = 1;
    
    private static readonly GUIContent sizeGUIContent = new GUIContent("Size");
    private static readonly GUIContent yOffsetGUIContent = new GUIContent("Y Offset");
    private static readonly GUIContent rotationGUIContent = new GUIContent("Rotation");
    private static readonly GUIContent alignmentGUIContent = new GUIContent("Normal Aligned");
    private static readonly GUIContent variantGUIContent = new GUIContent("Variant");

    private static readonly GUIContent singleGUIContent = new GUIContent("Single");
    private static readonly GUIContent rangeGUIContent = new GUIContent("Range");
    private static readonly GUIContent disabledGUIContent = new GUIContent("Disabled");

    private static readonly GUIContent randomGUIContent = new GUIContent("Random");

    private static readonly GUIContent valueGUIContent = new GUIContent("Value");
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
