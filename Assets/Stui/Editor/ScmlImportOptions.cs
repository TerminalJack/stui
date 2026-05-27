// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEditor;
using UnityEngine;

namespace Stui.Importing
{
    public class ScmlImportOptionsWindow : EditorWindow
    {
        public System.Action OnImport;
        private Vector2 _windowScrollView;

        private static readonly string _pixelsPerUnitTooltip =
            "Pixels Per Unit: The images will have their PPU import setting set to this value.  PPU is " +
            "the number of pixels of width/height in the sprite image that correspond to one distance unit " +
            "in world space.  You can typically leave this at its default value of 100.";

        private static readonly string _directSpriteSwappingTooltip =
            "Direct Sprite Swapping: With direct sprite swapping enabled, sprites will be keyed directly as " +
            "opposed to indirectly via the Texture Controller component.  See the documentation for the Texture " +
            "Controller component for more information.";

        private static readonly string _createCharacterMapsTooltip =
            "Create Character Maps: If this is enabled then entities with character maps defined will have a " +
            "Character Map Controller added to the prefab.  Direct Sprite Swapping must be disabled to use this " +
            "feature.";

        private static readonly string _animateBoneScalesTooltip =
            "Animate Bone Scales: If your Spriter project uses this feature then enabling this option will " +
            "create the necessary components and animation curves to support it.  Otherwise, bone scales will be " +
            "baked-in for each keyframe.  This is more performant but could result in animations that do not " +
            "match Spriter's playback.  Note that if a Spriter project doesn't use animated bone scales then " +
            "enabling this option will have no effect on performance.";

        private static readonly string _animationImportStyleTooltip =
            "Animation Import Style: Where to store animation clips.  They can be stored in the prefab or in a " +
            "folder named '{prefabName}_Anims'  Note that the import will run faster when storing the animations " +
			"in a folder.";

        private static readonly GUIContent _pixelsPerUnitContent = new GUIContent(
            text: "Pixels Per Unit",
            tooltip: _pixelsPerUnitTooltip);

        private static readonly GUIContent _directSpriteSwappingContent = new GUIContent(
            text: "Direct Sprite Swapping",
            tooltip: _directSpriteSwappingTooltip);

        private static readonly GUIContent _createCharacterMapsContent = new GUIContent(
            text: "Create Character Maps",
            tooltip: _createCharacterMapsTooltip);

        private static readonly GUIContent _animateBoneScalesContent = new GUIContent(
            text: "Animate Bone Scales",
            tooltip: _animateBoneScalesTooltip);

        private static readonly GUIContent _animationImportStyleContent = new GUIContent(
            text: "Animation Import Style",
            tooltip: _animationImportStyleTooltip);

        void OnEnable()
        {
            titleContent = new GUIContent("Spriter Import Options");
            minSize = new Vector2(400, 270);
        }

        void OnGUI()
        {
            _windowScrollView = EditorGUILayout.BeginScrollView(_windowScrollView);

            EditorGUILayout.Space();

            GUI.SetNextControlName("PPU");
            ScmlImportOptions.options.pixelsPerUnit =
                EditorGUILayout.FloatField(_pixelsPerUnitContent, ScmlImportOptions.options.pixelsPerUnit);

            GUI.SetNextControlName("Swapping");
            ScmlImportOptions.options.directSpriteSwapping =
                EditorGUILayout.Toggle(_directSpriteSwappingContent, ScmlImportOptions.options.directSpriteSwapping);

            GUI.enabled = !ScmlImportOptions.options.directSpriteSwapping;

            ScmlImportOptions.options.createCharacterMaps = GUI.enabled
                ? ScmlImportOptions.options.createCharacterMaps
                : false;

            GUI.SetNextControlName("CharMaps");
            ScmlImportOptions.options.createCharacterMaps =
                EditorGUILayout.Toggle(_createCharacterMapsContent, ScmlImportOptions.options.createCharacterMaps);

            GUI.SetNextControlName("BoneScales");
            ScmlImportOptions.options.boneScaleAnimationEnabled =
                EditorGUILayout.Toggle(_animateBoneScalesContent, ScmlImportOptions.options.boneScaleAnimationEnabled);

            GUI.enabled = true;

            GUI.SetNextControlName("ImportStyle");
            ScmlImportOptions.options.importOption =
                (ScmlImportOptions.AnimationImportOption)EditorGUILayout.EnumPopup(
                    _animationImportStyleContent,
                    ScmlImportOptions.options.importOption);

            EditorGUILayout.Space(8);

            string helpMsg = "Choose your desired settings then click 'Import' to proceed.  Click 'Cancel' to " +
                "close this window without importing.";

            switch (GUI.GetNameOfFocusedControl())
            {
                case "PPU":
                    helpMsg = _pixelsPerUnitTooltip;
                    break;

                case "Swapping":
                    helpMsg = _directSpriteSwappingTooltip;
                    break;

                case "CharMaps":
                    helpMsg = _createCharacterMapsTooltip;
                    break;

                case "BoneScales":
                    helpMsg = _animateBoneScalesTooltip;
                    break;

                case "ImportStyle":
                    helpMsg = _animationImportStyleTooltip;
                    break;

                default:
                    break;
            }

            EditorGUILayout.HelpBox(helpMsg, MessageType.Info, wide: true);

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

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
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(16);

            EditorGUILayout.EndScrollView();
        }
    }

    public class ScmlImportOptions
    {
        public static ScmlImportOptions options = null;

	    public enum AnimationImportOption : byte { NestedInPrefab, SeparateFolder }

        public float pixelsPerUnit = 100f;
        public bool directSpriteSwapping = false;
        public bool createCharacterMaps = true; // directSpriteSwapping must be false to support character maps.
        public bool boneScaleAnimationEnabled = false;
		public AnimationImportOption importOption;
    }
}