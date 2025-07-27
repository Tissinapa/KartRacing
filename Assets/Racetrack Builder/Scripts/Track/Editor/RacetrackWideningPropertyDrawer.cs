using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RacetrackWidening))]
public class RacetrackWideningPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
        var leftRect = new Rect(position.x, position.y, position.width / 2, position.height);
        var rightRect = new Rect(position.x + position.width / 2, position.y, position.width / 2, position.height);
        EditorGUI.PropertyField(leftRect, property.FindPropertyRelative("Left"), GUIContent.none);
        EditorGUI.PropertyField(rightRect, property.FindPropertyRelative("Right"), GUIContent.none);
        EditorGUI.EndProperty();
    }
}
