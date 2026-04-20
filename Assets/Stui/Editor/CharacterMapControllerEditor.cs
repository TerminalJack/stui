// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

#if !UNITY_2021_1_OR_NEWER
using UnityEditor.Experimental.SceneManagement;
#endif

namespace Spriter2UnityDX
{
    [CustomEditor(typeof(CharacterMapController))]
    public class CharacterMapControllerEditor : Editor
    {
        private static readonly GUIContent _activeMapNamesContent = new GUIContent(
            text: "Active Maps",
            tooltip: "The active character maps.  Add, remove, and rearrange the names from Available Maps to " +
            "this list.  Updates should be automatic but the 'Apply Active Maps' button can be used to ensure " +
            "that all changes take effect.  (As well as validate all of the names in the list.)");

        private static readonly GUIContent _baseMapContent = new GUIContent(
            text: "Base Map",
            tooltip: "Base Map is this prefab's default character mapping.  You are advised to leave this " +
            "unmodified since any changes will likely cause future imports to fail.");

        private static readonly GUIContent _availableMapsContent = new GUIContent(
            text: "Available Maps",
            tooltip: "These are the character maps that are available for this prefab.  Add/remove their name(s) " +
            "to Active Maps via the +/- buttons.");

        private static readonly GUIContent _applyActiveMapsButtonContent = new GUIContent(
            text: "Apply Active Maps",
            tooltip: "If you feel that the Active Maps haven't been applied then click this to manually apply them " +
            "as well as validate the names in the list.  Any invalid names will be logged to the console.");

        private SerializedProperty _activeMapNamesProperty;
        private SerializedProperty _baseMapProperty;
        private SerializedProperty _availableMapsProperty;

        private bool _showAvailableMaps;
        private string _availableMapsSearchString = "";
        private ReorderableList _availableMapsList;
        private List<int> _availableMapsIndexMap = new List<int>();

        private bool _showActiveMapNames;
        private string _activeMapsSearchString = "";
        private ReorderableList _activeMapNamesList;
        private List<int> _activeMapsIndexMap = new List<int>();

        private CharacterMapController _characterMapController;

        private void OnEnable()
        {
            _characterMapController = target as CharacterMapController;

            _activeMapNamesProperty = serializedObject.FindProperty("activeMapNames");
            _baseMapProperty = serializedObject.FindProperty("baseMap");
            _availableMapsProperty = serializedObject.FindProperty("availableMaps");

            _activeMapNamesList = new ReorderableList(
                serializedObject,
                _activeMapNamesProperty,
                draggable: true,
                displayHeader: false,
                displayAddButton: true,
                displayRemoveButton: true
            );

            _activeMapNamesList.headerHeight = 0;

            _activeMapNamesList.drawElementCallback = OnActiveMapNamesDrawElement;
            _activeMapNamesList.onReorderCallback = OnActiveMapNamesReorder;

            _availableMapsList = new ReorderableList(
                serializedObject,
                _availableMapsProperty,
                draggable: true,
                displayHeader: false,
                displayAddButton: true,
                displayRemoveButton: true
            );

            _availableMapsList.headerHeight = 0;

            _availableMapsList.elementHeightCallback = OnAvailableMapsElementHeight;
            _availableMapsList.drawElementCallback = OnAvailableMapsDrawElement;
        }

        private void OnActiveMapNamesReorder(ReorderableList _)
        {
#if !UNITY_2020_1_OR_NEWER
            EditorApplication.delayCall += () => RefreshCharacterMapController(logWarnings: false);
#endif
        }

        private void OnActiveMapNamesDrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            rect.y += 2; // small vertical padding

            SerializedProperty element = _activeMapNamesProperty.GetArrayElementAtIndex(index);

            var fieldRect = new Rect(rect.x, rect.y, rect.width - 34f, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(
                fieldRect,
                element,
                GUIContent.none // Don't show the element #.
            );

            // '-' Button on the right
            var removeButtonRect = new Rect(
                fieldRect.x + fieldRect.width + 4f,
                rect.y,
                24f,
                EditorGUIUtility.singleLineHeight - 1f
            );

            Color oldBG = GUI.backgroundColor;

            GUI.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.75f, 0f, 0f)     // dark red
                : new Color(1f, 0.75f, 0.75f); // light red

            if (GUI.Button(removeButtonRect, "-"))
            {
                Event.current.Use();

                var mapName = element.stringValue;

                EditorApplication.delayCall += () =>
                {
                    if (_characterMapController.Remove(mapName))
                    {
                        RefreshCharacterMapController();
                    }
                };
            }

            GUI.backgroundColor = oldBG;
        }

        private float OnAvailableMapsElementHeight(int index)
        {
            var element = _availableMapsProperty.GetArrayElementAtIndex(index);

            // Base line + spacing
            float height = EditorGUIUtility.singleLineHeight + 4;

            // If its foldout is open, add the full height of all child properties
            if (element.isExpanded)
            {
                height = EditorGUI.GetPropertyHeight(element, true) + 4;
            }

            return height;
        }

        private void OnAvailableMapsDrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            rect.y += 1;

            var element = _availableMapsProperty.GetArrayElementAtIndex(index);

            // Foldout field
            var fieldRect = new Rect(rect.x + 10f, rect.y, rect.width - 34f, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(fieldRect, element);

            // +/- Button on the right
            var addRemoveButtonRect = new Rect(
                fieldRect.x + fieldRect.width + 4f,
                rect.y + 1f,
                24f,
                EditorGUIUtility.singleLineHeight - 1f
            );

            var mapName = _characterMapController.availableMaps[index].name;
            bool isActiveMapName = _characterMapController.activeMapNames.Contains(mapName);

            Color oldBG = GUI.backgroundColor;

            if (EditorGUIUtility.isProSkin)
            {
                GUI.backgroundColor = isActiveMapName
                    ? new Color(0.75f, 0f, 0f)     // red
                    : new Color(0.2f, 0.8f, 0.2f); // green
            }
            else
            {
                GUI.backgroundColor = isActiveMapName
                    ? new Color(1f, 0.75f, 0.75f) // red
                    : new Color(0.7f, 1f, 0.7f);  // green
            }

            if (GUI.Button(addRemoveButtonRect, isActiveMapName ? "-" : "+"))
            {
                Event.current.Use();

                EditorApplication.delayCall += () =>
                {
                    if (isActiveMapName)
                    {
                        _characterMapController.Remove(mapName);
                    }
                    else
                    {
                        _characterMapController.Add(mapName);
                    }

                    RefreshCharacterMapController();
                };
            }

            GUI.backgroundColor = oldBG;

            // If expanded, draw all of the children
            if (element.isExpanded)
            {
                var childRect = new Rect(
                    fieldRect.x,
                    fieldRect.y,
                    fieldRect.width + addRemoveButtonRect.width + 2f,
                    EditorGUI.GetPropertyHeight(element, true) - EditorGUIUtility.singleLineHeight);

                EditorGUI.PropertyField(childRect, element, GUIContent.none, includeChildren: true);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour(_characterMapController),
                typeof(CharacterMapController), false);
            EditorGUILayout.Space();
            GUI.enabled = true;

            _showActiveMapNames = EditorGUILayout.Foldout(
                _showActiveMapNames,
                _activeMapNamesContent,
                toggleOnLabelClick: true);

            if (_showActiveMapNames)
            {
                ShowActiveMapNames();
            }

            if (GUILayout.Button(_applyActiveMapsButtonContent))
            {
                RefreshCharacterMapController();
            }

            _showAvailableMaps = true; // Force the list to stay open since some versions of Unity will close it unexpectedly.

            _showAvailableMaps = EditorGUILayout.Foldout(
                _showAvailableMaps,
                _availableMapsContent,
                toggleOnLabelClick: true);

            if (_showAvailableMaps)
            {
                ShowAvailableMapNames();
            }

            EditorGUILayout.PropertyField(_baseMapProperty, _baseMapContent);

            serializedObject.ApplyModifiedProperties();
        }

        private void ShowActiveMapNames()
        {
            EditorGUI.indentLevel++;

            _activeMapsSearchString = EditorGUILayout.TextField("Search", _activeMapsSearchString);

            UpdateActiveMapsIndexMap();

            if (string.IsNullOrEmpty(_activeMapsSearchString))
            {
                EditorGUILayout.Space(1);

                UpdateActiveMapsIndexMap();

                EditorGUI.BeginChangeCheck();

                _activeMapNamesList.DoLayoutList();

                if (EditorGUI.EndChangeCheck())
                {   // Something changed: add, remove, reorder, or edit strings
                    EditorApplication.delayCall += () => RefreshCharacterMapController(logWarnings: false);
                }
            }
            else
            {
                ShowFilteredActiveMapNames();
            }

            EditorGUI.indentLevel--;
        }

        private void ShowFilteredActiveMapNames()
        {
            float elementHeight = _activeMapNamesList.elementHeight;

            foreach (int realIndex in _activeMapsIndexMap)
            {
                string itemName = _activeMapNamesProperty
                    .GetArrayElementAtIndex(realIndex)
                    .stringValue;

                // Reserve a rect exactly the same height as a ReorderableList row
                Rect rowRect = EditorGUILayout.GetControlRect(
                    false,
                    elementHeight
                );

                rowRect.x += 6;
                rowRect.y += 6;
                rowRect.width -= 47;
                rowRect.height -= 3;

                // Draw a selectable label that fills that rect
                EditorGUI.SelectableLabel(
                    rowRect,
                    itemName,
                    EditorStyles.textField
                );

                // '-' Button on the right
                var removeButtonRect = new Rect(
                    rowRect.x + rowRect.width + 4f,
                    rowRect.y,
                    24f,
                    rowRect.height - 1
                );

                Color oldBG = GUI.backgroundColor;

                GUI.backgroundColor = EditorGUIUtility.isProSkin
                    ? new Color(0.75f, 0f, 0f)     // dark red
                    : new Color(1f, 0.75f, 0.75f); // light red

                if (GUI.Button(removeButtonRect, "-"))
                {
                    Event.current.Use();

                    EditorApplication.delayCall += () =>
                    {
                        if (_characterMapController.Remove(itemName))
                        {
                            RefreshCharacterMapController();
                        }
                    };
                }

                GUI.backgroundColor = oldBG;
            }

            if (_activeMapsIndexMap.Count == 0)
            {
                EditorGUILayout.HelpBox("No matching items.", MessageType.Info);
            }
            else
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox($"Showing {_activeMapsIndexMap.Count} out of {_activeMapNamesList.count} items", MessageType.Info);
            }
        }

        private void ShowAvailableMapNames()
        {
            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();

            _availableMapsSearchString = EditorGUILayout.TextField("Search", _availableMapsSearchString);

            if (EditorGUI.EndChangeCheck())
            {
                UpdateAvailableMapsIndexMap();
            }

            if (string.IsNullOrEmpty(_availableMapsSearchString))
            {
                EditorGUILayout.Space(2);

                UpdateAvailableMapsIndexMap();

                EditorGUI.BeginChangeCheck();

                _availableMapsList.DoLayoutList();

                if (EditorGUI.EndChangeCheck())
                {   // Something changed: add, remove, reorder, or edit strings
                    EditorApplication.delayCall += () => RefreshCharacterMapController(logWarnings: false);
                }
            }
            else
            {
                ShowFilteredAvailableMapNames();
            }

            EditorGUI.indentLevel--;
        }

        private void ShowFilteredAvailableMapNames()
        {
            float elementHeight = _availableMapsList.elementHeight;

            foreach (int realIndex in _availableMapsIndexMap)
            {
                var mapName = _characterMapController.availableMaps[realIndex].name;

                Rect rowRect = EditorGUILayout.GetControlRect(
                    false,
                    elementHeight + 1
                );

                rowRect.x += 14;
                rowRect.y += 6;
                rowRect.width -= 45;
                rowRect.height -= 3;

                EditorGUI.SelectableLabel(
                    rowRect,
                    mapName,
                    EditorStyles.textField
                );

                // +/- Button on the right
                var addRemoveButtonRect = new Rect(
                    rowRect.x + rowRect.width + 4f,
                    rowRect.y + 1,
                    24f,
                    rowRect.height - 2
                );

                bool isActiveMapName = _characterMapController.activeMapNames.Contains(mapName);

                Color oldBG = GUI.backgroundColor;

                if (EditorGUIUtility.isProSkin)
                {
                    GUI.backgroundColor = isActiveMapName
                        ? new Color(0.75f, 0f, 0f)     // red
                        : new Color(0.2f, 0.8f, 0.2f); // green
                }
                else
                {
                    GUI.backgroundColor = isActiveMapName
                        ? new Color(1f, 0.75f, 0.75f) // red
                        : new Color(0.7f, 1f, 0.7f);  // green
                }

                if (GUI.Button(addRemoveButtonRect, isActiveMapName ? "-" : "+"))
                {
                    Event.current.Use();
                    GUI.FocusControl(null); // Make sure the search field losses focus.  (Fixes an issue.)

                    EditorApplication.delayCall += () =>
                    {
                        if (isActiveMapName)
                        {
                            _characterMapController.Remove(mapName);
                        }
                        else
                        {
                            _characterMapController.Add(mapName);
                        }

                        RefreshCharacterMapController();
                    };
                }

                GUI.backgroundColor = oldBG;
            }

            if (_availableMapsIndexMap.Count == 0)
            {
                EditorGUILayout.HelpBox("No matching items.", MessageType.Info);
            }
            else
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox($"Showing {_availableMapsIndexMap.Count} out of {_availableMapsList.count} items", MessageType.Info);
            }
        }

        private void UpdateActiveMapsIndexMap()
        {
            _activeMapsIndexMap.Clear();

            for (int i = 0; i < _activeMapNamesProperty.arraySize; i++)
            {
                string name = _activeMapNamesProperty.GetArrayElementAtIndex(i).stringValue;

                if (string.IsNullOrEmpty(_activeMapsSearchString) ||
                    name.IndexOf(_activeMapsSearchString, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _activeMapsIndexMap.Add(i);
                }
            }
        }

        private void UpdateAvailableMapsIndexMap()
        {
            _availableMapsIndexMap.Clear();

            for (int i = 0; i < _availableMapsProperty.arraySize; i++)
            {
                var name = _characterMapController.availableMaps[i].name;

                if (string.IsNullOrEmpty(_availableMapsSearchString) ||
                    name.IndexOf(_availableMapsSearchString, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _availableMapsIndexMap.Add(i);
                }
            }
        }

        private void RefreshCharacterMapController(bool logWarnings = true)
        {
            _characterMapController.Refresh(logWarnings);

            EditorUtility.SetDirty(_characterMapController);
            PrefabUtility.RecordPrefabInstancePropertyModifications(_characterMapController);

            foreach (var textureController in _characterMapController.GetComponentsInChildren<TextureController>())
            {
                EditorUtility.SetDirty(textureController);
                PrefabUtility.RecordPrefabInstancePropertyModifications(textureController);
            }

            foreach (var spriteRenderer in _characterMapController.GetComponentsInChildren<SpriteRenderer>())
            {
                EditorUtility.SetDirty(spriteRenderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(spriteRenderer);
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                EditorSceneManager.MarkSceneDirty(stage.scene);
            }

            AssetDatabase.SaveAssets();
        }
    }
}