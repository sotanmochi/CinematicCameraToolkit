using CinematicCameraToolkit.AutoFraming;
using UnityEditor;
using UnityEngine;

namespace CinematicCameraToolkit.Timeline.Editor
{
    /// <summary>
    /// Adds a preset-to-values shortcut to the clip inspector.
    /// </summary>
    [CustomEditor(typeof(AutoFramingClip))]
    [CanEditMultipleObjects]
    public sealed class AutoFramingClipEditor : UnityEditor.Editor
    {
        private FramingSmoothingPreset _preset = FramingSmoothingPreset.Standard;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Smoothing Preset", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _preset = (FramingSmoothingPreset)EditorGUILayout.EnumPopup(_preset);
                if (GUILayout.Button("Copy To Clip", GUILayout.Width(110f))) CopyPresetToClip();
            }

            EditorGUILayout.HelpBox(
                "Overwrites this clip's smoothing values with the preset's. A preset is a starting " +
                "point only: the clip keeps values, so they stay editable and blendable afterwards.",
                MessageType.None);
        }

        private void CopyPresetToClip()
        {
            foreach (var clipTarget in targets)
            {
                if (clipTarget is not AutoFramingClip clip) continue;

                Undo.RecordObject(clip, "Copy Smoothing Preset");

                var data = clip.Data;
                data.ApplySmoothingPreset(_preset);
                clip.Data = data;

                EditorUtility.SetDirty(clip);
            }

            serializedObject.Update();
        }
    }
}
