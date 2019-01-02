using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(FloatRange))]
public class IngredientDrawer : PropertyDrawer
{
    static readonly GUIContent minGUIContent = new GUIContent("Min");
    static readonly GUIContent maxGUIContent = new GUIContent("Max");


    // Draw the property inside the given rect
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Using BeginProperty / EndProperty on the parent property means that
        // prefab override logic works on the entire property.
        EditorGUI.BeginProperty(position, label, property);

        // Save GUI attributes so they can be restored later
        int indent = EditorGUI.indentLevel;
        float labelWidth = EditorGUIUtility.labelWidth;

        // Draw label
        if (EditorGUIUtility.wideMode)
        {
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
        }
        else
        {
            EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth;
            EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            float offset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.indentLevel++;
            position = EditorGUI.IndentedRect(new Rect(position.x, position.y + offset, position.width, EditorGUIUtility.singleLineHeight));
        }

        // Don't make child fields be indented
        EditorGUI.indentLevel = 0;

        // Calculate rects
        Rect minRect = new Rect(position.x, position.y, position.width * 0.5f, position.height);
        Rect maxRect = new Rect(position.x + position.width * 0.5f, position.y, position.width * 0.5f, position.height);   
        
        // Properties
        SerializedProperty minProperty = property.FindPropertyRelative("min");
        SerializedProperty maxProperty = property.FindPropertyRelative("max");

        // Draw fields
        EditorGUIUtility.labelWidth = 30f;

        EditorGUI.PropertyField(minRect, minProperty, minGUIContent);
        if (minProperty.floatValue > maxProperty.floatValue) maxProperty.floatValue = minProperty.floatValue;
        EditorGUI.PropertyField(maxRect, maxProperty, maxGUIContent);
        if (maxProperty.floatValue < minProperty.floatValue) minProperty.floatValue = maxProperty.floatValue;
        
        // Set indent and labelWidth back to what they where
        EditorGUI.indentLevel = indent;
        EditorGUIUtility.labelWidth = labelWidth;

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (EditorGUIUtility.wideMode) return EditorGUIUtility.singleLineHeight;
        else return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
    }
}