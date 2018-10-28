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

        // Draw label
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        // Don't make child fields be indented
        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        // Calculate rects
        var minRect = new Rect(position.x, position.y, position.width * 0.5f, position.height);
        var maxRect = new Rect(position.x + position.width * 0.5f, position.y, position.width * 0.5f, position.height);

        // Properties
        SerializedProperty minProperty = property.FindPropertyRelative("min");
        SerializedProperty maxProperty = property.FindPropertyRelative("max");

        // Draw fields - passs GUIContent.none to each so they are drawn without labels
        float labelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 30f;

        EditorGUI.PropertyField(minRect, minProperty, minGUIContent);
        if (minProperty.floatValue > maxProperty.floatValue) maxProperty.floatValue = minProperty.floatValue;
        EditorGUI.PropertyField(maxRect, maxProperty, maxGUIContent);
        if (maxProperty.floatValue < minProperty.floatValue) minProperty.floatValue = maxProperty.floatValue;

        EditorGUIUtility.labelWidth = labelWidth;

        // Set indent back to what it was
        EditorGUI.indentLevel = indent;

        EditorGUI.EndProperty();
    }
}