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

    public const int DISABLED_CURSOR = 0;
    public const int AIM_CURSOR = 1;
    public const int MATCH_CURSOR = 2;

    public int scaleState;
    public float minScale = 0.1f, maxScale = 1f;

    public int rotationState;
    public float minRotation = 0f, maxRotation = 360f;

    private bool rotationCursor = false;
    private int rotationCursorState;
    private Vector3 rotationCursorPosition;
    private Vector3 rotationCursorDirection = Vector3.forward;
    private readonly static Color rotationColor = new Color(0.5f, 0.5f, 0.9f);

    public int alignmentState;
    public float minAlignment = 0.0f, maxAlignment = 1f;

    private bool alignCursor = false;
    private int alignCursorState;
    private Vector3 alignCursorPosition;
    private Vector3 alignCursorDirection = Vector3.up;
    private readonly static Color alignColor = new Color(0.5f, 0.9f, 0.5f);

    private static readonly GUIContent scaleGUIContent = new GUIContent("Scale");
    private static readonly GUIContent rotationGUIContent = new GUIContent("Rotation");
    private static readonly GUIContent alignmentGUIContent = new GUIContent("Alignment (direction...normal)");
    private static readonly GUIContent directionGUIContent = new GUIContent("Direction");

    private static readonly GUIContent singleGUIContent = new GUIContent("Single");
    private static readonly GUIContent rangeGUIContent = new GUIContent("Range");
    private static readonly GUIContent disabledGUIContent = new GUIContent("Disabled");
    private static readonly GUIContent aimGUIContent = new GUIContent("Aim");
    private static readonly GUIContent matchGUIContent = new GUIContent("Match");
    private static readonly GUIContent useCursorGUIContent = new GUIContent("Use Cursor");

    private static readonly GUIContent valueGUIContent = new GUIContent("Value");
    private static readonly GUIContent minGUIContent = new GUIContent("Min");
    private static readonly GUIContent maxGUIContent = new GUIContent("Max");

    public void DoInstancePropertiesInspector()
    {
        //Scale
        EditorGUILayout.LabelField(scaleGUIContent, EditorStyles.boldLabel);
        InstancePropertyInspector(ref scaleState, ref minScale, ref maxScale, 0f, float.MaxValue);

        //Rotation
        EditorGUILayout.LabelField(rotationGUIContent, EditorStyles.boldLabel);
        rotationCursor = EditorGUILayout.Toggle(useCursorGUIContent, rotationCursor);
        if (rotationCursor) CursorInspector(ref rotationCursorState, ref rotationCursorPosition, ref rotationCursorDirection, rotationColor);
        else InstancePropertyInspector(ref rotationState, ref minRotation, ref maxRotation, -360f, 360);
        

        //Vertical vs Normal aligned
        EditorGUILayout.LabelField(alignmentGUIContent, EditorStyles.boldLabel);
        alignCursor = EditorGUILayout.Toggle(useCursorGUIContent, alignCursor);
        if (alignCursor) CursorInspector(ref alignCursorState, ref alignCursorPosition, ref alignCursorDirection, alignColor);
        //else Vector3Inspector(directionGUIContent, ref alignDirection);//  alignDirecton = EditorGUILayout.Vector3Field(directionGUIContent, alignDirection);
        InstancePropertyInspector(ref alignmentState, ref minAlignment, ref maxAlignment, 0f, 1f);
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
                if (maxScale < minScale) maxScale = minScale;
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

    private void CursorInspector(ref int cursorState, ref Vector3 position, ref Vector3 direction, Color color)
    {
        float labelWidth = EditorGUIUtility.labelWidth;
        EditorGUILayout.BeginHorizontal();
        bool boolState = cursorState != DISABLED_CURSOR;
        GUIContent guiContent = boolState ? (cursorState == AIM_CURSOR ? aimGUIContent : matchGUIContent) : disabledGUIContent;
        bool newState = GUILayout.Toggle(boolState, guiContent, EditorStyles.miniButton, GUILayout.Width(labelWidth * 0.5f));
        if (boolState != newState)
        {
            cursorState = (cursorState + 1) % 3;
        }
        EditorGUIUtility.labelWidth = 40f;
        switch (cursorState)
        {
            case AIM_CURSOR:
                position = EditorGUILayout.Vector3Field(GUIContent.none, position);
                break;
            case MATCH_CURSOR:
                direction = EditorGUILayout.Vector3Field(GUIContent.none, direction);
                direction = direction.normalized;
                break;
            default:
                bool guiEnabled = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.LabelField(new GUIContent(""), EditorStyles.textField);
                GUI.enabled = guiEnabled;
                break;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUIUtility.labelWidth = labelWidth;
    }

    private void Vector3Inspector(GUIContent content, ref Vector3 value)
    {
        float labelWidth = EditorGUIUtility.labelWidth;
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(content, GUILayout.Width(labelWidth * 0.5f));
        
        EditorGUIUtility.labelWidth = 40f;

        value = EditorGUILayout.Vector3Field(GUIContent.none, value);
                
        EditorGUILayout.EndHorizontal();
        EditorGUIUtility.labelWidth = labelWidth;
    }

    //TODO dont draw brush if cursor is being edited? return if busy
    public bool DoCursorHandles()
    {
        if (rotationCursor) DoCursorHandle(ref rotationCursorPosition, ref rotationCursorDirection, rotationColor);
        if (alignCursor) DoCursorHandle(ref alignCursorPosition, ref alignCursorDirection, alignColor);
        if (currentCursorId != -1)
        {
            if (Event.current.type == EventType.Layout)
            {//This will allow clicks to be eaten
                HandleUtility.AddDefaultControl(currentCursorId);
            }

            if (Event.current.type == EventType.MouseUp && Event.current.button == 0) currentCursorId = -1;
            else return true;
        }
        return false;
    }
    
    //TODO place/aim cursor with raycast
    int currentCursorId;
    Quaternion deltaRotation = Quaternion.identity;
    private void DoCursorHandle(ref Vector3 position, ref Vector3 direction, Color cursorColor)
    {
        int controlId = GUIUtility.GetControlID(GUIContent.none, FocusType.Passive);
        Handles.color = cursorColor;

        float handleSize = HandleUtility.GetHandleSize(position);
        Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, direction);
        Handles.ArrowHandleCap(0, position, deltaRotation * rotation, handleSize * 0.8f, Event.current.type);
        bool click = Handles.Button(position, Quaternion.identity, handleSize * 0.05f, handleSize * 0.05f, Handles.DotHandleCap);

        if (controlId != currentCursorId)
        {
            if (click) currentCursorId = controlId;
        }
        else
        {
            switch (Tools.current)
            {
                case Tool.Move:
                    position = Handles.DoPositionHandle(position, Quaternion.identity);
                    break;
                case Tool.Rotate:
                    bool mouseUp = Event.current.type == EventType.MouseUp;
                    deltaRotation = Handles.RotationHandle(deltaRotation, position);
                    
                    if (mouseUp)
                    {
                        direction = deltaRotation * direction;
                        direction = direction.normalized;

                        deltaRotation = Quaternion.identity;
                    }
                    break;
                default:
                    //Handles.Button(position, Quaternion.identity, handleSize * 0.05f, handleSize * 0.05f, Handles.DotHandleCap);
                    break;
            }
        }
    }


    //TODO fixes
    public MapData.PropInstance ApplyValues(MapData.PropInstance instance, float strength)
    {
        if (scaleState != DISABLED_STATE)
        {
            float targetScale = minScale;//TODO rename to size or rename size
            if (scaleState == RANGE_STATE)
            {
                targetScale += (maxScale - minScale) * Random.value;
            }
            instance.size += (targetScale - instance.size) * strength;
        }


        if (alignmentState != DISABLED_STATE)//TODO rename to normal aligned? //TODO do this on mesh build?
        {
            float targetAlign = minAlignment;
            if (alignmentState == RANGE_STATE)
            {
                targetAlign += (maxAlignment - minAlignment) * Random.value;
            }
            Vector3 alignDirection = Vector3.up;
            if (alignCursor)//TODO much to decide about this shit
            {
                if (alignCursorState != DISABLED_CURSOR)
                {
                    if (alignCursorState == AIM_CURSOR)
                    {
                        alignDirection = (alignCursorPosition - instance.position).normalized;
                    }
                    else
                    {
                        alignDirection = alignCursorDirection;
                    }
                }
            }
            Vector3 targetDirection = alignDirection;// Vector3.Slerp(alignDirection, normal, targetAlign);

            instance.direction += (targetDirection - instance.direction) * strength;
        }
        

        if (rotationCursor)//TODO offset rotation from cursor?
        {
            if (rotationCursorState != DISABLED_CURSOR)
            {
                Vector3 aimDirection;
                if (rotationCursorState == AIM_CURSOR)
                {
                    aimDirection = rotationCursorPosition - instance.position;
                }
                else
                {
                    aimDirection = rotationCursorDirection;
                }
                float targetRotation = Vector3.Angle(
                    Vector3.Cross(instance.direction, Vector3.forward),
                    Vector3.Cross(instance.direction, aimDirection)
                );
                instance.rotation += (targetRotation - instance.rotation) * strength;
            }
        }
        else
        {
            if (rotationState != DISABLED_STATE)
            {
                float targetRotation = minRotation;//TODO rename to size or rename size
                if (scaleState == RANGE_STATE)
                {
                    targetRotation += (maxRotation - minRotation) * Random.value;
                }
                instance.rotation += (targetRotation - instance.rotation) * strength;
            }
        }

        return instance;
    }
}
