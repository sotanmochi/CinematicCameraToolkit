using System.Collections.Generic;
using UnityEditor;

namespace CinematicCameraToolkit.Editor
{
    public static class SerializedPropertyUtility
    {
        /// <summary>
        /// Enumerates a property's immediate children, skipping the ones hidden from the Inspector.
        /// Use it to draw a nested struct's fields where the struct itself would otherwise render as a foldout.
        /// </summary>
        public static IEnumerable<SerializedProperty> VisibleChildren(SerializedProperty property)
        {
            var iterator = property.Copy();
            var end = property.GetEndProperty();
            var enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                yield return iterator.Copy();
            }
        }
    }
}
