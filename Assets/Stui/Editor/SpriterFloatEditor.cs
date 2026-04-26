// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEngine;
using UnityEditor;

namespace Stui
{
    [CustomEditor(typeof(SpriterFloat))]
    public class SpriterFloatEditor : Editor
    {
        private GUIStyle _headerStyle;

        private static readonly string _headerLabel = "Spriter Float Variable";

        private static readonly string _componentSummary = $"Use the '{nameof(SpriterFloat.Value)}' property of " +
            "this component in your scripts to read this variable's current value.  The " +
            $"'{nameof(SpriterFloat.Value)}' property is animated so do not modify it at runtime.";

        private static readonly GUIContent _variableNameContent = new GUIContent(
            text: "Variable Name",
            tooltip: "The name of this float variable.  This is defined in the Spriter project.  To change this, " +
                "modify the Spriter project and reimport.");

        private static readonly GUIContent _defaultValueContent = new GUIContent(
            text: "Default Value",
            tooltip: "The variable's default value.  This is the value used when the variable isn't being explicitly " +
                "animated.  That is, the variable either a) has no keys for an entire animation, or b) has no keys " +
                "for the initial part of an animation.  The default value is defined in the Spriter project.  To " +
                "change it, modify the Spriter project and reimport.");

        private static readonly GUIContent _valueContent = new GUIContent(
            text: "Value",
            tooltip: "This is the variable's current value.  This is an animated property.  You are advised to " +
                "leave this unchanged in Unity as you will lose any changes when/if you reimport.  Use Spriter to " +
                "change this variable's animation curves and reimport.");

        private SerializedProperty _variableNameProperty;
        private SerializedProperty _defaultValueProperty;
        private SerializedProperty _valueProperty;

        protected virtual void OnEnable()
        {
            _variableNameProperty = serializedObject.FindProperty(nameof(SpriterFloat.VariableName));;
            _defaultValueProperty = serializedObject.FindProperty(nameof(SpriterFloat.DefaultValue));
            _valueProperty = serializedObject.FindProperty(nameof(SpriterFloat.Value));
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
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour(target as SpriterFloat), typeof(SpriterFloat), false);
                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(_variableNameProperty, _variableNameContent);
                EditorGUILayout.PropertyField(_defaultValueProperty, _defaultValueContent);
            }

            EditorGUILayout.PropertyField(_valueProperty, _valueContent);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
