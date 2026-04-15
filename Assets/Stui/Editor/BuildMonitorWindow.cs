using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Spriter2UnityDX.Importing
{
    public interface IBuildTaskContext
    {
        string InputFileName { get; set; }
        string EntityName { get; set; }
        string AnimationName { get; set; }

        string MessagePrefix { get; }

        List<string> ImportedPrefabs { get; }

        bool IsCanceled { get; }

        public void Cancel();
    }

    public class BuildMonitorWindow : EditorWindow, IBuildTaskContext
    {
        private List<string> _logs = new List<string>();
        private Vector2 _scroll;
        private bool _showCancelButton;
        private bool _isCanceled;
        private bool _isRunning;
        private List<string> _importedPrefabs = new List<string>();
        private IEnumerator _task;

        public bool IsCanceled => _isCanceled;
        public string InputFileName { get; set; }
        public string EntityName { get; set; }
        public string AnimationName { get; set; }
        public List<string> ImportedPrefabs => _importedPrefabs;

        private static readonly double _maxTaskTimePerFrame_s = 0.013;

        public void Cancel() => _isCanceled = true;

        public string MessagePrefix
        {
            get
            {
                List<string> prefixStrings = new List<string>();

                if (!string.IsNullOrEmpty(InputFileName)) { prefixStrings.Add($"scml file: '{InputFileName}'"); }
                if (!string.IsNullOrEmpty(EntityName)) { prefixStrings.Add($"entity: '{EntityName}'"); }
                if (!string.IsNullOrEmpty(AnimationName)) { prefixStrings.Add($"animation: '{AnimationName}'"); }

                return string.Join(", ", prefixStrings);
            }
        }

        public static void Open()
        {
            GetWindow<BuildMonitorWindow>().Show();
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Spriter Import Status");
            minSize = new Vector2(400, 400);
        }

        void OnDestroy()
        {
            if (_isRunning)
            {   // User clicked the windows 'X' to close it during an import.
                _isCanceled = true;
                Debug.LogWarning("Spriter2UnityDX: The Spriter Import Status window has been closed during an " +
                    "import without properly canceling the import.  The generated files will likely be unusable.");
            }
        }

        void OnGUI()
        {
            GUILayout.Space(8);
            GUILayout.Label("Import Status:", EditorStyles.boldLabel);
            GUILayout.Space(4);

            Rect outerRect = EditorGUILayout.GetControlRect(
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true)
            );

            // Pick a contrasting background for light vs. dark skin
            Color bg = EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.18f, 0.18f)
                : new Color(0.85f, 0.85f, 0.85f);

            EditorGUI.DrawRect(outerRect, bg);

            float rowHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            int totalCount = _logs.Count;

            // Content rect: full width minus scrollbar, height = totalCount * rowHeight
            float contentWidth  = outerRect.width - GUI.skin.verticalScrollbar.fixedWidth;
            float contentHeight = totalCount * rowHeight;
            Rect contentRect    = new Rect(0, 0, contentWidth, contentHeight);

            // Begin low-level scroll view in that reserved space
            _scroll = GUI.BeginScrollView(
                outerRect,     // viewport
                _scroll,       // current scroll offset
                contentRect,   // virtual content size
                false,         // no horiz scrollbar
                true           // vertical scrollbar
            );

            // Determine how many lines to show (when running, cap to last 100)
            int maxLinesToShow = _isRunning ? 100 : totalCount;
            int startIdx = _isRunning
                ? Mathf.Max(0, totalCount - maxLinesToShow)
                : 0;

            // Calculate visible range
            int firstVisible = Mathf.FloorToInt(_scroll.y / rowHeight);
            firstVisible = Mathf.Clamp(firstVisible, startIdx, totalCount - 1);

            int visibleRows = Mathf.CeilToInt(outerRect.height / rowHeight) + 2;
            int lastVisible = Mathf.Min(totalCount, firstVisible + visibleRows);

            // Draw only the slice of labels that fit
            for (int i = firstVisible; i >= 0 && i < _logs.Count && i < lastVisible; ++i)
            {
                Rect rowRect = new Rect(
                    0,
                    i * rowHeight,
                    contentWidth,
                    rowHeight
                );
                GUI.Label(rowRect, _logs[i]);
            }

            GUI.EndScrollView();

            GUILayout.Space(8);

            GUILayout.BeginHorizontal();

            if (_isRunning)
            {
                if (!_showCancelButton)
                {
                    Rect helpRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight * 3);
                    EditorGUI.HelpBox(helpRect, "Warning: Canceling the import may leave the output files in an unusable " +
                        "state.  It may be necessary to delete the output files before they can be regenerated.  Click " +
                        "this message to reveal the 'Cancel Import' button.",
                        MessageType.Warning); // , wide: false);

                    if (GUI.Button(helpRect, GUIContent.none, GUIStyle.none))
                    {
                        _showCancelButton = true;
                    }
                }
                else
                {
                    GUILayout.FlexibleSpace(); // Pushes button to right.

                    if (GUILayout.Button("Cancel Import", GUILayout.Width(100), GUILayout.Height(24)))
                    {
                        _isCanceled = true;
                    }

                    GUILayout.FlexibleSpace(); // Causes button to be centered.
                }
            }
            else
            {
                GUILayout.FlexibleSpace(); // Pushes button to right.

                if (GUILayout.Button("Dismiss", GUILayout.Width(100), GUILayout.Height(24)))
                {
                    Close();
                }

                GUILayout.FlexibleSpace(); // Causes button to be centered.
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8);
        }

        public void BeginBuild(IEnumerator buildTask)
        {
            _logs.Clear();
            _importedPrefabs.Clear();
            _isCanceled = false;
            _showCancelButton = false;
            _isRunning = true;

            _task = buildTask;

            EditorApplication.update += OnEditorUpdate;
        }

        void OnEditorUpdate()
        {
            if (_task == null)
            {
                EditorApplication.update -= OnEditorUpdate;
                return;
            }

            double startTime = EditorApplication.timeSinceStartup;

            while (EditorApplication.timeSinceStartup - startTime < _maxTaskTimePerFrame_s)
            {
                if (!_task.MoveNext())
                {
                    HandleTaskCompletion();
                    break;
                }
                else if (_task.Current is string msg && !string.IsNullOrEmpty(msg))
                {
                    Status(msg);
                }
            }

            _scroll.y = float.MaxValue;
            Repaint();
        }

        private void HandleTaskCompletion()
        {
            EditorApplication.update -= OnEditorUpdate;
            _task = null;
            _isRunning = false;

            if (_isCanceled)
            {
                Status("Import canceled.");
            }
            else
            {
                Status("Import complete.  The following prefabs were imported:");

                foreach (var prefabPath in _importedPrefabs)
                {
                    Status($"    '{prefabPath}'");
                }
            }
        }

        private void Status(string message)
        {
            _logs.Add(message);
        }
    }
}