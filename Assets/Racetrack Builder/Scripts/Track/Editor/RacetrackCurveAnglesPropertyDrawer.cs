using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RacetrackCurveAnglesAttribute))]
public class RacetrackCurveAnglesPropertyDrawer : ButtonSliderPropertyDrawerBase
{
    private PresetValueButton[] XAngleButtons;
    private PresetValueButton[] YAngleButtons;
    private PresetValueButton[] ZAngleButtons;

    private float lineHeight;
    private bool rebuildCurve;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        LoadAssets();

        if (property.propertyType != SerializedPropertyType.Vector3)
        {
            EditorGUI.LabelField(position, label.text, "[RacetrackCurveAngles] must be applied to a Vector3");
            return;
        }

        Vector3? angles = !property.hasMultipleDifferentValues ? (Vector3?)property.vector3Value : null;

        lineHeight = base.GetPropertyHeight(property, label);
        position.height = lineHeight;
        position.y += LineSpacing;

        rebuildCurve = false;

        float? yAngle = EditAngle(ref position, angles?.y, 90.0f, "Turn (Y)", YAngleButtons);
        float? xAngle = EditAngle(ref position, angles?.x, 180.0f, "Gradient (X)", XAngleButtons);
        float? zAngle = EditAngle(ref position, angles?.z, 90.0f, "Bank (Z)", ZAngleButtons);

        if (xAngle != angles?.x || yAngle != angles?.y || zAngle != angles?.z)
        {
            // Update curve objects individually, so that we can set just the angle for the changed axis
            var curves = property.serializedObject.targetObjects.OfType<RacetrackCurve>().ToList();
            using (var undo = new ScopedUndo("Set curve angle"))
            {
                foreach (var curve in curves) 
                {
                    var obj = new SerializedObject(curve);
                    var objProp = obj.FindProperty(property.name);
                    var newAngles = objProp.vector3Value;
                    if (xAngle != null) newAngles.x = xAngle.Value;
                    if (yAngle != null) newAngles.y = yAngle.Value;
                    if (zAngle != null) newAngles.z = zAngle.Value;
                    objProp.vector3Value = newAngles;
                    obj.ApplyModifiedProperties();
                    curve.Track.InvalidatePath();
                }                
            }
            
            //var newAngles = property.vector3Value;
            //if (xAngle != null) newAngles.x = xAngle.Value;
            //if (yAngle != null) newAngles.y = yAngle.Value;
            //if (zAngle != null) newAngles.z = zAngle.Value;
            //property.vector3Value = newAngles;
        }

        if (rebuildCurve)
        {
            // Find racetrack
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

    private float? EditAngle(ref Rect position, float? value, float range, string prompt, PresetValueButton[] buttons)
    {
        EditorGUI.LabelField(position, prompt);
        float? newY = DrawAngleButtons(position, value, buttons);
        if (value != newY && newY != null)
        {
            value = newY;
            rebuildCurve = true;
        }
        position.y += ButtonHeight;
        float sliderValue = EditorGUI.Slider(position, new GUIContent(" "), value ?? 0.0f, -range, range);
        if (sliderValue != value && (value != null || sliderValue != 0.0f))
            value = sliderValue;
        position.y += lineHeight + LineSpacing;

        return value;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int rowCount = 3; // property.serializedObject.targetObject is RacetrackCurve ? 3 : 2;
        return (base.GetPropertyHeight(property, label) + ButtonHeight + LineSpacing) * rowCount;
    }

    protected override void InternalLoadAssets()
    {
        YAngleButtons = new[] {
            new PresetValueButton("YAngN90", -90.0f),
            new PresetValueButton("YAngN45", -45.0f),
            new PresetValueButton("YAngN30", -30.0f),
            new PresetValueButton("YAng0", 0.0f),
            new PresetValueButton("YAng30", 30.0f),
            new PresetValueButton("YAng45", 45.0f),
            new PresetValueButton("YAng90", 90.0f)
        };

        XAngleButtons = new[]
        {
            new PresetValueButton("Grad45", -45.0f),
            new PresetValueButton("Grad30", -30.0f),
            new PresetValueButton("Grad15", -15.0f),
            new PresetValueButton("Grad0", 0.0f),
            new PresetValueButton("GradN15", 15.0f),
            new PresetValueButton("GradN30", 30.0f),
            new PresetValueButton("GradN45", 45.0f)
        };

        ZAngleButtons = new[]
        {
            new PresetValueButton("Grad45", 45.0f),
            new PresetValueButton("Grad30", 30.0f),
            new PresetValueButton("Grad15", 15.0f),
            new PresetValueButton("Grad0", 0.0f),
            new PresetValueButton("GradN15", -15.0f),
            new PresetValueButton("GradN30", -30.0f),
            new PresetValueButton("GradN45", -45.0f)
        };
    }
}
