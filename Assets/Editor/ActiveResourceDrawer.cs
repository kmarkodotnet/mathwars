using MathHorde.Infrastructure.Configuration;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ActiveResourceAttribute))]
public sealed class ActiveResourceDrawer : PropertyDrawer
{
    private static readonly GUIContent[] Labels =
    {
        new GUIContent("Health"), new GUIContent("Shield"),
        new GUIContent("EnemyArmor"), new GUIContent("Energy")
    };
    private static readonly int[] Values = { 1, 2, 3, 4 };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        int value = EditorGUI.IntPopup(position, label, property.intValue, Labels, Values);
        // A legacy zero is left invalid until explicitly edited; no silent import migration.
        if (EditorGUI.EndChangeCheck()) property.intValue = value;
        EditorGUI.EndProperty();
    }
}
