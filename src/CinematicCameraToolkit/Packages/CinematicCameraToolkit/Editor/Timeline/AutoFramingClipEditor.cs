using CinematicCameraToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace CinematicCameraToolkit.Timeline.Editor
{
    /// <summary>
    /// Lifts the clip's data struct out of its foldout and adds a preset-to-values shortcut
    /// above the smoothing values it overwrites.
    /// </summary>
    [CustomEditor(typeof(AutoFramingClip))]
    [CanEditMultipleObjects]
    public sealed class AutoFramingClipEditor : UnityEditor.Editor
    {
        private FramingSmoothingPreset _preset = FramingSmoothingPreset.Standard;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var copyRequested = false;
            foreach (var field in SerializedPropertyUtility.VisibleChildren(serializedObject.FindProperty("_data")))
            {
                switch (field.name)
                {
                    case nameof(AutoFramingClipData.MarginLeft):
                        DrawHeader("Margins");
                        break;
                    case nameof(AutoFramingClipData.HorizontalAlignment):
                        DrawHeader("Alignment");
                        break;
                    case nameof(AutoFramingClipData.Smoothing):
                        DrawHeader("Smoothing");
                        copyRequested = DrawPresetRow();
                        break;
                }

                EditorGUILayout.PropertyField(field, true);
            }

            serializedObject.ApplyModifiedProperties();

            // Applied after the write-back: the preset edits the targets directly, so the values
            // this pass collected would otherwise overwrite it again.
            if (copyRequested) CopyPresetToClip();
        }

        private static void DrawHeader(string label)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        private bool DrawPresetRow()
        {
            bool copyRequested;
            using (new EditorGUILayout.HorizontalScope())
            {
                _preset = (FramingSmoothingPreset)EditorGUILayout.EnumPopup("Preset", _preset);
                copyRequested = GUILayout.Button("Copy To Clip", GUILayout.Width(110f));
            }

            EditorGUILayout.HelpBox(
                "Overwrites this clip's smoothing values with the preset's. A preset is a starting " +
                "point only: the clip keeps values, so they stay editable and blendable afterwards.",
                MessageType.None);

            return copyRequested;
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
