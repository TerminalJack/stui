// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEngine;
using UnityEditor;

namespace Stui
{
    [CustomEditor(typeof(SpriterTag))]
    public class SpriterTagEditor : Editor
    {
        private GUIStyle _headerStyle;

        private static readonly string _headerLabel = "Spriter Tag";

        private static readonly string _componentSummary = $"Use the '{nameof(SpriterTag.IsActive)}' property of this " +
            $"component in your scripts to read this tag's current value.  The '{nameof(SpriterTag.IsActive)}' " +
            "property is animated so do not modify it at runtime.";

        private static readonly GUIContent _tagNameContent = new GUIContent(
            text: "Tag Name",
            tooltip: "The name of this tag.  This is defined in the Spriter project.  To change this, modify the " +
                "Spriter project and reimport.");

        private static readonly GUIContent _isActiveContent = new GUIContent(
            text: "Is Active",
            tooltip: "This is the tag's current value.  A value of true means the tag is active.  This is an " +
                "animated property.  You are advised to leave this unchanged in Unity as you will lose any changes " +
                "when/if you reimport.  Use Spriter to change this tag's animation curves and reimport.");

        private SerializedProperty _tagNameProperty;
        private SerializedProperty _isActiveProperty;

        protected virtual void OnEnable()
        {
            _tagNameProperty = serializedObject.FindProperty(nameof(SpriterTag.TagName));;
            _isActiveProperty = serializedObject.FindProperty(nameof(SpriterTag.IsActive));
        }

        private void InitStyles()
        {
            _headerStyle = new GUIStyle(EditorStyles.largeLabel);
            _headerStyle.richText = true;
            _headerStyle.normal.textColor = GetHeaderTextColor(EditorGUIUtility.isProSkin);
        }

        private Color GetHeaderTextColor(bool IsDarkMode)
        {
            return IsDarkMode ? Color.HSVToRGB(0.58f, 0.85f, 0.95f) : Color.HSVToRGB(0.63f, 1f, 1f);
        }

        public override void OnInspectorGUI()
        {
            InitStyles();

            serializedObject.Update();

            GUILayout.Label($"<b><i>{_headerLabel}</i></b>", _headerStyle);
            GUILayout.Space(5);

            EditorGUILayout.HelpBox(_componentSummary,  MessageType.Info);
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour(target as SpriterTag), typeof(SpriterTag), false);
                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(_tagNameProperty, _tagNameContent);
            }

            EditorGUILayout.PropertyField(_isActiveProperty, _isActiveContent);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
