using UnityEditor;
using UnityEngine;

namespace CinematicCameraToolkit.Editor
{
    /// <summary>
    /// Draws the struct's fields inline. As a nested field it would otherwise render as a foldout,
    /// burying the values one click deep under the header that introduces them.
    /// </summary>
    [CustomPropertyDrawer(typeof(FramingSmoothingSettings))]
    public sealed class FramingSmoothingSettingsDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var y = position.y;

            foreach (var field in SerializedPropertyUtility.VisibleChildren(property))
            {
                var height = EditorGUI.GetPropertyHeight(field, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), field, true);
                y += height + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = 0f;
            var count = 0;

            foreach (var field in SerializedPropertyUtility.VisibleChildren(property))
            {
                height += EditorGUI.GetPropertyHeight(field, true);
                count++;
            }

            return height + Mathf.Max(0, count - 1) * EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
