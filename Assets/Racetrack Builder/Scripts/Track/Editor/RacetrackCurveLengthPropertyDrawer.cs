using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RacetrackCurveLengthAttribute))]
public class RacetrackCurveLengthPropertyDrawer : ButtonSliderPropertyDrawerBase
{
    private PresetValueButton[] LengthButtons;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        LoadAssets();

        if (property.propertyType != SerializedPropertyType.Float)
        {
            EditorGUI.LabelField(position, label.text, "[RacetrackCurveLength] must be applied to a float ");
            return;
        }

        float? length = property.hasMultipleDifferentValues ? null : (float?)property.floatValue;

        float lineHeight = base.GetPropertyHeight(property, label);
        position.height = lineHeight;
        position.y += LineSpacing;

        bool rebuildCurve = false;

        EditorGUI.LabelField(position, "Length");
        float? newLength = DrawAngleButtons(position, length, LengthButtons);
        if (length != newLength)
        {
            length = newLength;
            rebuildCurve = true;
        }
        position.y += ButtonHeight;
        float sliderLength = EditorGUI.Slider(position, new GUIContent(" "), length ?? property.floatValue, 1.0f, 250.0f);
        if (sliderLength != length && (length != null || sliderLength != property.floatValue))
            length = sliderLength;
        position.y += lineHeight + LineSpacing;

        if (length != null)
            property.floatValue = length.Value;

        if (rebuildCurve)
        {
            Racetrack track = null;
            if (property.serializedObject.targetObject is RacetrackCurve)
            {
                var curve = (RacetrackCurve)property.serializedObject.targetObject;
                track = curve.Track;
            }
            else if (property.serializedObject.targetObject is Racetrack)
            {
                track = (Racetrack)property.serializedObject.targetObject;
            }

            // Flag track as needing update
            if (track != null)
                track.IsUpdateRequired = true;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return base.GetPropertyHeight(property, label) + ButtonHeight + LineSpacing;
    }

    protected override void InternalLoadAssets()
    {
        LengthButtons = new[]
        {
            new PresetValueButton(10),
            new PresetValueButton(20),
            new PresetValueButton(30),
            new PresetValueButton(50),
            new PresetValueButton(75),
            new PresetValueButton(100)
        };
    }
}
