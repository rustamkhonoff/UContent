#if UNITY_EDITOR

using System;
using UContent.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace UContent.Editor
{
    public sealed class ContentDiagnosticsWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("Tools/UContent/Diagnostics")]
        private static void Open()
        {
            GetWindow<ContentDiagnosticsWindow>("UContent");
        }

        private void OnEnable()
        {
            ContentDiagnostics.Changed += Repaint;
        }

        private void OnDisable()
        {
            ContentDiagnostics.Changed -= Repaint;
        }

        private void Update()
        {
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            var entries = ContentDiagnostics.GetEntries();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Open objects: {entries.Count}", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var entry in entries)
                DrawEntry(entry);

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            ContentDiagnostics.Enabled = EditorGUILayout.Toggle("Enabled", ContentDiagnostics.Enabled);
            ContentDiagnostics.CaptureStackTrace = EditorGUILayout.Toggle("Capture Stack Trace", ContentDiagnostics.CaptureStackTrace);
        }

        private static void DrawEntry(ContentDebugEntry entry)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(entry.Type.ToString(), GUILayout.Width(80));
            EditorGUILayout.LabelField(entry.Name ?? "-", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(entry.Key))
                EditorGUILayout.LabelField("Key", entry.Key);

            var age = DateTime.UtcNow - entry.CreatedAtUtc;
            EditorGUILayout.LabelField("Age", $"{age.TotalSeconds:F1}s");

            if (!string.IsNullOrEmpty(entry.StackTrace))
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.TextArea(entry.StackTrace);
            }

            EditorGUILayout.EndVertical();
        }
    }
}

#endif