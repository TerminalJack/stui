// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEditor;
using UnityEngine;
using System.Collections;

namespace Spriter2UnityDX.Importing
{
    public class ScmlImportOptionsWindow : EditorWindow
    {
        public System.Action OnImport;

        void OnEnable()
        {
            titleContent = new GUIContent("Spriter Import Options");
            minSize = new Vector2(400, 330);
        }

        void OnGUI()
        {
            EditorGUILayout.Space();

            ScmlImportOptions.options.pixelsPerUnit =
                EditorGUILayout.FloatField("Pixels Per Unit", ScmlImportOptions.options.pixelsPerUnit);

            ScmlImportOptions.options.directSpriteSwapping =
                EditorGUILayout.Toggle("Direct Sprite Swapping", ScmlImportOptions.options.directSpriteSwapping);

            GUI.enabled = !ScmlImportOptions.options.directSpriteSwapping;

            ScmlImportOptions.options.createCharacterMaps = GUI.enabled
                                                          ? ScmlImportOptions.options.createCharacterMaps
                                                          : false;

            ScmlImportOptions.options.createCharacterMaps =
                EditorGUILayout.Toggle("Create Character Maps", ScmlImportOptions.options.createCharacterMaps);

            GUI.enabled = true;

            ScmlImportOptions.options.importOption =
                (ScmlImportOptions.AnimationImportOption)EditorGUILayout.EnumPopup("Animation Import Style", ScmlImportOptions.options.importOption);

            EditorGUILayout.Space(8);

            EditorGUILayout.HelpBox(
                "Pixels Per Unit: The images will have their PPU import setting set to this value.  PPU is " +
                "the number of pixels of width/height in the sprite image that correspond to one distance unit " +
                "in world space.  You can typically leave this at its default value of 100.\n\n" +
                "Direct Sprite Swapping: With direct sprite swapping enabed, sprites will be keyed directly as " +
                "opposed to indirectly via the Texture Controller component.  See the documentation for the Texture " +
                "Controller component for more information.\n\n" +
                "Create Character Maps: If this is enabled then entities with character maps defined will have a " +
                "Character Map Controller added to the prefab.  Direct Sprite Swapping must be disabled to use this " +
                "feature.\n\n" +
                "Animation Import Style: Where to store animation clips.",
                MessageType.Info, wide: true);

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(24)))
            {
                Close();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Import", GUILayout.Width(100), GUILayout.Height(24)))
            {
                Close();
                OnImport();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }
    }

    public class ScmlImportOptions
    {
        public static ScmlImportOptions options = null;

	    public enum AnimationImportOption : byte { NestedInPrefab, SeparateFolder }

        public float pixelsPerUnit = 100f;
        public bool directSpriteSwapping = false;
        public bool createCharacterMaps = true; // directSpriteSwapping must be false to support character maps.
		public AnimationImportOption importOption;
    }
}