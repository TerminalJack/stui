// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Stui.EntityInfo;
using Stui.Importing;
using Stui.Prefabs;
using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using Stui;
using Unity.VisualScripting;

public class ScmlFileInspectorWindow : EditorWindow
{
    private GUIStyle _invalidFieldStyle;
    private GUIStyle _refreshingTreeFieldStyle;

    private TreeViewState _entityAnimationTreeViewState;
    private EntityAnimationTreeView _entityAnimationTree;
    private Vector2 _windowScrollView;

    private bool _isRunning;
    private bool _showCancelButton;
    private bool _userCanceled;

    private string _scmlInputPath;
    private string _lastLoadedScmlInputPath;
    private bool _isRefreshingEntityAnimationTree = false;
    private bool _showEntityAnimationTree = true;
    private bool _showUtilityInformation = false;
    private bool _logStatusMessages = true;

    [SerializeField] private List<ScmlInspectorProjectTask> _earlyPerProjectTasks = new List<ScmlInspectorProjectTask>();
    [SerializeField] private List<ScmlInspectorProjectTask> _latePerProjectTasks = new List<ScmlInspectorProjectTask>();

    [SerializeField] private List<ScmlInspectorEntityTask> _earlyPerEntityTasks = new List<ScmlInspectorEntityTask>();
    [SerializeField] private List<ScmlInspectorEntityTask> _postSpriterEntityInfoPerEntityTasks = new List<ScmlInspectorEntityTask>();
    [SerializeField] private List<ScmlInspectorEntityTask> _latePerEntityTasks = new List<ScmlInspectorEntityTask>();

    [SerializeField] private List<ScmlInspectorAnimationTask> _perAnimationTasks = new List<ScmlInspectorAnimationTask>();

    private SerializedProperty _earlyPerProjectTasksProp;
    private SerializedProperty _latePerProjectTasksProp;
    private SerializedProperty _earlyPerEntityTasksProp;
    private SerializedProperty _latePerEntityTasksProp;
    private SerializedProperty _postSpriterEntityInfoPerEntityTasksProp;
    private SerializedProperty _perAnimationTasksProp;

    private bool _preprocessWithSpriterEntityInfo = true;

    private SerializedObject _soThis;

    [MenuItem("Assets/SCML File Inspector...", false, 110)]
    private static void ResizeSpriterProjectMenuItem()
    {
        if (Selection.objects.Length > 0)
        {
            var obj = Selection.objects[0];
            string path = AssetDatabase.GetAssetPath(obj);

            var window = GetWindow<ScmlFileInspectorWindow>();
            window.SetScmlInputPath(path);
        }
    }

    [MenuItem("Assets/SCML File Inspector...", true)]
    private static bool ResizeSpriterProjectMenuItem_Validate()
    {
        if (Selection.objects.Length != 1)
        {
            return false;
        }

        string path = AssetDatabase.GetAssetPath(Selection.activeObject);

        return path.EndsWith(".scml", StringComparison.OrdinalIgnoreCase) && !path.Contains("autosave");
    }

    [MenuItem("Window/SCML File Inspector...")]
    private static void ShowWindow()
    {
        GetWindow<ScmlFileInspectorWindow>();
    }

    public void SetScmlInputPath(string _scmlPath)
    {
        _scmlInputPath = _scmlPath;
        RefreshEntityAnimationTree(_scmlInputPath);
    }

    void OnEnable()
    {
        titleContent = new GUIContent("SCML File Inspector");
        minSize = new Vector2(400, 350);

        _entityAnimationTreeViewState ??= new TreeViewState();
        _entityAnimationTree ??= new EntityAnimationTreeView(_entityAnimationTreeViewState);

        _soThis = new SerializedObject(this);

        _earlyPerProjectTasksProp = _soThis.FindProperty(nameof(_earlyPerProjectTasks));
        _latePerProjectTasksProp = _soThis.FindProperty(nameof(_latePerProjectTasks));
        _earlyPerEntityTasksProp = _soThis.FindProperty(nameof(_earlyPerEntityTasks));
        _latePerEntityTasksProp = _soThis.FindProperty(nameof(_latePerEntityTasks));
        _postSpriterEntityInfoPerEntityTasksProp = _soThis.FindProperty(nameof(_postSpriterEntityInfoPerEntityTasks));
        _perAnimationTasksProp = _soThis.FindProperty(nameof(_perAnimationTasks));
    }

    void OnDestroy()
    {
        if (_isRunning)
        {   // User clicked the windows 'X' to close it while the inspection task is running.  This doesn't seem to
            // cause a problem but warn the user anyway.
            _userCanceled = true;
            Debug.LogWarning("ScmlFileInspectorWindow: The SCML File Inspector window has been closed while the " +
                "inspection task was running.  Please use the 'Cancel Inspection' button.");
        }
    }

    void InitStyles()
    {
        if (_invalidFieldStyle == null)
        {
            _invalidFieldStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Italic
            };

            _invalidFieldStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? new Color(0.84f, 0.36f, 0.36f, 1f)
                : new Color(0.5f, 0.1f, 0.1f, 1f);
        }

        if (_refreshingTreeFieldStyle == null)
        {
            _refreshingTreeFieldStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Italic
            };

            _refreshingTreeFieldStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? new Color(0.29f, 0.64f, 1f, 1f)
                : new Color(0.12f, 0.35f, 0.59f, 1f);

        }
    }

    void OnGUI()
    {
        InitStyles();

        _soThis.Update();

        _windowScrollView = EditorGUILayout.BeginScrollView(_windowScrollView);

        EditorGUILayout.Space(8);

        _showUtilityInformation = EditorGUILayout.Foldout(
            _showUtilityInformation,
            _showUtilityInformation
                ? "SCML File Inspector.  Click here to hide the help box."
                : "SCML File Inspector.  Click here for more information.",
            toggleOnLabelClick: true);

        if (_showUtilityInformation)
        {
            EditorGUILayout.HelpBox(
                "SCML File Inspector.  Use this development utility to gain insight into a Spriter project's .scml file " +
                "contents.\n\n" +
                "The utility uses the Dumpify package to show Spriter project file information in a structured table " +
                "format.  You may also use the utility as a kind of sandbox by making localized changes to the " +
                "in-memory SCML data model (see the ScmlSupport.cs file) and then dumping the results.\n\n" +
                "Check the console for this utility's output.  You will likely need to enable the console's 'Use " +
                "Monospace Font' option.  This option is available in Unity versions 2021 and later.  For earlier " +
                "versions of Unity you can open the log file in an external editor such as VS Code.  Unlike Unity, VS " +
                "Code supports simple syntax highlighting of the log file.",
                MessageType.Info, wide: true);
        }

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Input File (.scml)  (The Spriter project to inspect.)");

        EditorGUILayout.BeginHorizontal();

        bool inputPathChanged = false;

        EditorGUI.BeginChangeCheck();

        GUI.SetNextControlName("InputPath");
        string newPath = EditorGUILayout.TextField(_scmlInputPath);

        if (EditorGUI.EndChangeCheck())
        {
            // User typed or pasted something new
            _scmlInputPath = newPath;
            inputPathChanged = true;
        }

        if (GUILayout.Button("…", GUILayout.Width(20), GUILayout.Height(18)))
        {
            if (GUI.GetNameOfFocusedControl() == "InputPath")
            {
                GUI.FocusControl(null);
            }

            string selectedPath = EditorUtility.OpenFilePanel(
                title: "Select Spriter Input File",
                directory: Application.dataPath,
                extension: "scml"
            );

            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    selectedPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }

                if (selectedPath != _scmlInputPath)
                {
                    _scmlInputPath = selectedPath;
                    inputPathChanged = true;
                }
            }
        }

        EditorGUILayout.EndHorizontal();

        bool isInputPathValid = !string.IsNullOrEmpty(_scmlInputPath) && System.IO.File.Exists(_scmlInputPath);

        if (!isInputPathValid)
        {
            EditorGUILayout.LabelField("* The Input File field is invalid.", _invalidFieldStyle);

            if (GUI.GetNameOfFocusedControl() != "InputPath")
            {   // Don't clear the tree while the user is still editing the path.
                _entityAnimationTree.Clear();
            }
        }
        else if (_isRefreshingEntityAnimationTree)
        {
            EditorGUILayout.LabelField("Refreshing the entities and animations treeview...", _refreshingTreeFieldStyle);
        }
        else
        {
            bool doRefresh = _entityAnimationTree.IsEmpty() || (inputPathChanged && _lastLoadedScmlInputPath != _scmlInputPath);

            if (doRefresh)
            {
                RefreshEntityAnimationTree(_scmlInputPath);
            }
        }

        EditorGUILayout.Space(4);

        _showEntityAnimationTree = EditorGUILayout.Foldout(
            _showEntityAnimationTree,
            "Entities and/or animations to inspect",
            toggleOnLabelClick: true);

        if (_showEntityAnimationTree)
        {
            EditorGUILayout.HelpBox(
                "Right-click within the tree for selection/expansion/checking options.  The tree supports multi-selection.",
                MessageType.Info, wide: true);

            if (_entityAnimationTree == null || !_entityAnimationTree.IsEmpty())
            {
                float height = _entityAnimationTree.totalHeight;
                Rect rect = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));
                _entityAnimationTree.OnGUI(rect);
            }
            else
            {
                EditorGUILayout.LabelField("Please specify a valid Input File.", _invalidFieldStyle);
            }
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Step 1: Per project tasks");
        EditorGUILayout.PropertyField(_earlyPerProjectTasksProp);

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Step 2: Per entity tasks");
        EditorGUILayout.PropertyField(_earlyPerEntityTasksProp);

        EditorGUILayout.Space(4);

        _preprocessWithSpriterEntityInfo = EditorGUILayout.ToggleLeft("Step 3: Preprocess with SpriterEntityInfo",
            _preprocessWithSpriterEntityInfo);

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Step 4: Per entity tasks");
        EditorGUILayout.PropertyField(_postSpriterEntityInfoPerEntityTasksProp);

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Step 5: Per animation tasks");
        EditorGUILayout.PropertyField(_perAnimationTasksProp);

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Step 6: Per entity tasks");
        EditorGUILayout.PropertyField(_latePerEntityTasksProp);

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Step 7: Per project tasks");
        EditorGUILayout.PropertyField(_latePerProjectTasksProp);

        EditorGUILayout.Space(4);

        _logStatusMessages = GUILayout.Toggle(_logStatusMessages, "Log Status Messages");

        EditorGUILayout.Space(16);

        EditorGUILayout.BeginVertical();
        GUILayout.FlexibleSpace();

        EditorGUILayout.BeginHorizontal();

        if (_isRunning)
        {
            if (!_showCancelButton)
            {
                Rect helpRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight * 2);
                EditorGUI.HelpBox(helpRect, "Warning: Dumpify can take a long time to create its output.  Click " +
                        "this message to reveal the 'Cancel Inspection' button.",
                    MessageType.Warning); // , wide: false);

                if (GUI.Button(helpRect, GUIContent.none, GUIStyle.none))
                {
                    _showCancelButton = true;
                }
            }
            else
            {
                GUILayout.FlexibleSpace(); // Pushes button to right.

                if (GUILayout.Button("Cancel Inspection", GUILayout.Width(160), GUILayout.Height(24)))
                {
                    _userCanceled = true;
                }

                GUILayout.FlexibleSpace(); // Causes button to be centered.
            }
        }
        else
        {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Dismiss", GUILayout.Width(100), GUILayout.Height(24)))
            {
                EditorApplication.delayCall += () => Close();
            }

            GUILayout.FlexibleSpace();

            bool shouldDisableInspectButton =
                !isInputPathValid ||
                _isRefreshingEntityAnimationTree ||
                _entityAnimationTree.IsEmpty();

            using (new EditorGUI.DisabledScope(shouldDisableInspectButton))
            {
                if (GUILayout.Button("Inspect", GUILayout.Width(100), GUILayout.Height(24)))
                {
                    DoInspection();
                }
            }

            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(16);

        EditorGUILayout.EndScrollView();

        _soThis.ApplyModifiedProperties();
    }

    private void Delay(float seconds, Action callback)
    {
        double start = EditorApplication.timeSinceStartup;

        void Update()
        {
            if (EditorApplication.timeSinceStartup - start >= seconds)
            {
                EditorApplication.update -= Update;
                callback?.Invoke();
            }
        }

        EditorApplication.update += Update;
    }

    private void RefreshEntityAnimationTree(string scmlFilePath)
    {
        _isRefreshingEntityAnimationTree = true;

        // Let the UI repaint before refreshing.
        Delay(0.05f, () =>
        {
            try
            {
                ScmlObject scmlObject = Deserialize(scmlFilePath);

                _entityAnimationTree.LoadFromSpriterProject(scmlObject);

                _lastLoadedScmlInputPath = scmlFilePath;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            _isRefreshingEntityAnimationTree = false;
            Repaint();
        });
    }

    private void DoInspection()
    {
        try
        {
            ScmlObject scmlObject = Deserialize(_scmlInputPath);

            var scmlFileInspectorTask = new ScmlFileInspectorTask
            {
                LogStatusMessages = _logStatusMessages
            };

            scmlFileInspectorTask.BeginTask(DoProcessFile(scmlObject, scmlFileInspectorTask));
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private static ScmlObject Deserialize(string inputScmlPath)
    {
        var serializer = new XmlSerializer(typeof(ScmlObject));
        using (var reader = new StreamReader(inputScmlPath))
        {
            return (ScmlObject)serializer.Deserialize(reader);
        }
    }

    private IEnumerator DoProcessFile(ScmlObject scmlObject, IBuildTaskContext inspectionCtx)
    {
        _userCanceled = false;
        _isRunning = true;
        _showCancelButton = false;

        var fileInspector = new SpriterProjectFileInspector
        {
            SelectedEntityNames = _entityAnimationTree.GetCheckedEntityNames(),
            SelectedAnimationInfo = _entityAnimationTree.GetCheckedAnimationInfo(),
            ScmlObject = scmlObject,
            ScmlPath = _scmlInputPath,
            PreprocessWithSpriterEntityInfo = _preprocessWithSpriterEntityInfo,
            InspectionCtx = inspectionCtx,
            EarlyPerProjectTasks = _earlyPerProjectTasks,
            LatePerProjectTasks = _latePerProjectTasks,
            EarlyPerEntityTasks = _earlyPerEntityTasks,
            PostSpriterEntityInfoPerEntityTasks = _postSpriterEntityInfoPerEntityTasks,
            LatePerEntityTasks = _latePerEntityTasks,
            PerAnimationTasks = _perAnimationTasks
        };

        inspectionCtx.InputFileName = Path.GetFileName(_scmlInputPath);
        yield return $"Processing Spriter project file '{_scmlInputPath}'";

        var inspectionProcess =
            IteratorUtils.SafeEnumerable(
                () => fileInspector.Process(),
                ex =>
                {
                    Debug.LogException(ex);
                });

        while (inspectionProcess.MoveNext())
        {
            yield return inspectionProcess.Current;

            if (_userCanceled)
            {
                inspectionCtx.Cancel();
            }
        }

        _isRunning = false;

        if (!this.IsDestroyed())
        {
            Repaint();
        }
    }

    private class SpriterProjectFileInspector
    {
        public List<string> SelectedEntityNames = new List<string>();
        public Dictionary<string, List<string>> SelectedAnimationInfo = new Dictionary<string, List<string>>();

        public ScmlObject ScmlObject { get; set; }
        public string ScmlPath { get; set; }
        public bool PreprocessWithSpriterEntityInfo { get; set; }
        public IBuildTaskContext InspectionCtx { get; set; }

        public List<ScmlInspectorProjectTask> EarlyPerProjectTasks = new List<ScmlInspectorProjectTask>();
        public List<ScmlInspectorProjectTask> LatePerProjectTasks = new List<ScmlInspectorProjectTask>();

        public List<ScmlInspectorEntityTask> EarlyPerEntityTasks = new List<ScmlInspectorEntityTask>();
        public List<ScmlInspectorEntityTask> PostSpriterEntityInfoPerEntityTasks = new List<ScmlInspectorEntityTask>();
        public List<ScmlInspectorEntityTask> LatePerEntityTasks = new List<ScmlInspectorEntityTask>();

        public List<ScmlInspectorAnimationTask> PerAnimationTasks = new List<ScmlInspectorAnimationTask>();

        public IEnumerator Process()
        {
            if (EarlyPerProjectTasks.Count > 0)
            {
                if (InspectionCtx.IsCanceled) { yield break; }
                yield return $"Running early per project inspection tasks for '{ScmlPath}'";

                foreach (var earlyProjectTask in EarlyPerProjectTasks)
                {
                    if (earlyProjectTask != null)
                    {
                        if (InspectionCtx.IsCanceled) { yield break; }

                        var task = earlyProjectTask.ProcessProject(ScmlObject, InspectionCtx);

                        while (task.MoveNext())
                        {
                            yield return task.Current;
                        }

                        if (InspectionCtx.IsCanceled) { yield break; }
                    }
                }
            }

            var spriterProjDirectory = Path.GetDirectoryName(ScmlPath);

            var folders = new Dictionary<int, IDictionary<int, Sprite>>();
            var fileInfo = new Dictionary<int, IDictionary<int, Stui.Importing.File>>();

            // Process the folder info only when SpriterEntityInfo is preprocessing the Spriter project.
            if (PreprocessWithSpriterEntityInfo)
            {
                foreach (var folder in ScmlObject.folders)
                {
                    var files = folders[folder.id] = new Dictionary<int, Sprite>();
                    var fi = fileInfo[folder.id] = new Dictionary<int, Stui.Importing.File>();

                    foreach (var file in folder.files)
                    {
                        if (file.objectType == ObjectType.sprite)
                        {
                            var path = string.Format("{0}/{1}", spriterProjDirectory, file.name);

                            if (InspectionCtx.IsCanceled) { yield break; }
                            yield return $"{InspectionCtx.MessagePrefix}: Getting sprite at {path}";

                            files[file.id] = PrefabBuilder.GetSpriteAtPath(path);
                            fi[file.id] = file;
                        }
                    }
                }
            }

            foreach (var entity in ScmlObject.entities)
            {
                if (!SelectedEntityNames.Concat(SelectedAnimationInfo.Keys).Contains(entity.name))
                {   // This entity is not selected and has no animations that are selected.
                    continue;
                }

                InspectionCtx.EntityName = entity.name;
                InspectionCtx.AnimationName = "";

                if (InspectionCtx.IsCanceled) { yield break; }
                yield return $"{InspectionCtx.MessagePrefix}: Processing entity '{entity.name}'";

                if (EarlyPerEntityTasks.Count > 0)
                {
                    if (InspectionCtx.IsCanceled) { yield break; }
                    yield return $"Running early per entity inspection tasks for '{entity.name}'";

                    foreach (var earlyEntityTask in EarlyPerEntityTasks)
                    {
                        if (earlyEntityTask != null)
                        {
                            if (InspectionCtx.IsCanceled) { yield break; }

                            var task = earlyEntityTask.ProcessEntity(ScmlObject, entityInfo: null, entity, InspectionCtx);

                            while (task.MoveNext())
                            {
                                yield return task.Current;
                            }

                            if (InspectionCtx.IsCanceled) { yield break; }
                        }
                    }
                }

                SpriterEntityInfo entityInfo = null;

                if (PreprocessWithSpriterEntityInfo)
                {
                    entityInfo = new SpriterEntityInfo();
                    var entityInfoProcess = entityInfo.Process(spriterProjDirectory, ScmlObject, entity, fileInfo, InspectionCtx);

                    while (entityInfoProcess.MoveNext())
                    {
                        yield return entityInfoProcess.Current;
                    }

                    if (InspectionCtx.IsCanceled) { yield break; }
                }

                if (PostSpriterEntityInfoPerEntityTasks.Count > 0)
                {
                    if (InspectionCtx.IsCanceled) { yield break; }
                    yield return $"Running post SpriterEntityInfo per entity inspection tasks for '{entity.name}'";

                    foreach (var postSpriterEntityInfoPerEntityTask in PostSpriterEntityInfoPerEntityTasks)
                    {
                        if (postSpriterEntityInfoPerEntityTask != null)
                        {
                            if (InspectionCtx.IsCanceled) { yield break; }

                            var task = postSpriterEntityInfoPerEntityTask.ProcessEntity(ScmlObject, entityInfo, entity, InspectionCtx);

                            while (task.MoveNext())
                            {
                                yield return task.Current;
                            }

                            if (InspectionCtx.IsCanceled) { yield break; }
                        }
                    }
                }

                if (SelectedAnimationInfo.ContainsKey(entity.name))
                {   // One or more of this entity's animations are selected.
                    var selectedAnimations = SelectedAnimationInfo[entity.name];

                    foreach (var animation in entity.animations)
                    {
                        if (selectedAnimations.Contains(animation.name))
                        {   // This animation is selected.
                            InspectionCtx.AnimationName = animation.name;

                            if (InspectionCtx.IsCanceled) { yield break; }
                            yield return $"{InspectionCtx.MessagePrefix}: Inspecting animation '{animation.name}'";

                            if (PerAnimationTasks.Count > 0)
                            {
                                if (InspectionCtx.IsCanceled) { yield break; }
                                yield return $"Running per animation inspection tasks for '{animation.name}'";

                                foreach (var perAnimationTask in PerAnimationTasks)
                                {
                                    if (perAnimationTask != null)
                                    {
                                        if (InspectionCtx.IsCanceled) { yield break; }

                                        var task = perAnimationTask.ProcessAnimation(ScmlObject, entityInfo, entity, animation, InspectionCtx);

                                        while (task.MoveNext())
                                        {
                                            yield return task.Current;
                                        }

                                        if (InspectionCtx.IsCanceled) { yield break; }
                                    }
                                }
                            }
                        }
                    }
                }

                if (SelectedEntityNames.Contains(entity.name))
                {
                    if (LatePerEntityTasks.Count > 0)
                    {
                        if (InspectionCtx.IsCanceled) { yield break; }
                        yield return $"Running late per entity inspection tasks for '{entity.name}'";

                        foreach (var lateEntityTask in LatePerEntityTasks)
                        {
                            if (lateEntityTask != null)
                            {
                                if (InspectionCtx.IsCanceled) { yield break; }

                                var task = lateEntityTask.ProcessEntity(ScmlObject, entityInfo, entity, InspectionCtx);

                                while (task.MoveNext())
                                {
                                    yield return task.Current;
                                }

                                if (InspectionCtx.IsCanceled) { yield break; }
                            }
                        }
                    }
                }
            }

            InspectionCtx.EntityName = "";
            InspectionCtx.AnimationName = "";

            if (LatePerProjectTasks.Count > 0)
            {
                if (InspectionCtx.IsCanceled) { yield break; }
                yield return $"Running late per project inspection tasks for '{ScmlPath}'";

                foreach (var lateProjectTask in LatePerProjectTasks)
                {
                    if (lateProjectTask != null)
                    {
                        if (InspectionCtx.IsCanceled) { yield break; }

                        var task = lateProjectTask.ProcessProject(ScmlObject, InspectionCtx);

                        while (task.MoveNext())
                        {
                            yield return task.Current;
                        }

                        if (InspectionCtx.IsCanceled) { yield break; }
                    }
                }
            }
        }
    }

    private class ScmlFileInspectorTask : IBuildTaskContext
    {
        private bool _isCanceled;
        private List<string> _importedPrefabs = new List<string>();
        private IEnumerator _task;

        public bool IsCanceled => _isCanceled;
        public string InputFileName { get; set; }
        public string EntityName { get; set; }
        public string AnimationName { get; set; }
        public List<string> ImportedPrefabs => _importedPrefabs;

        public bool LogStatusMessages = true;

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

        public void BeginTask(IEnumerator buildTask)
        {
            _importedPrefabs.Clear();
            _isCanceled = false;

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
                else if (_task.Current == null)
                {
                    break; // Wait for next frame.  Used when running Dumpify in a background thread.
                }
            }
        }

        private void HandleTaskCompletion()
        {
            EditorApplication.update -= OnEditorUpdate;
            _task = null;

            if (_isCanceled)
            {
                Status("Scml file inspector task canceled.");
            }
            else
            {
                Status("Scml file inspector task complete.");
            }
        }

        private void Status(string message)
        {
            if (LogStatusMessages)
            {
                Debug.Log(message);
            }
        }
    }

    private class EntityAnimationTreeView : TreeView
    {
        ScmlObject _scmlObject;

        public EntityAnimationTreeView(TreeViewState state) : base(state)
        {
        }

        public void LoadFromSpriterProject(ScmlObject scmlObject)
        {
            _scmlObject = scmlObject;

            SetExpanded(new List<int>()); // Collapse everything.
            Reload(); // This will call BuildRoot().
        }

        public void Clear()
        {
            LoadFromSpriterProject(null);
        }

        public bool IsEmpty()
        {
            return
                rootItem == null ||
                rootItem.children == null ||
                rootItem.children.Count == 0;
        }

        public List<string> GetCheckedEntityNames()
        {
            List<string> result = new List<string>();

            foreach (var item in rootItem.children)
            {
                if (item is EntityAnimationCheckboxItem entity && entity.depth == 0 && entity.isChecked)
                {
                    result.Add(entity.displayName);
                }
            }

            return result;
        }

        public Dictionary<string, List<string>> GetCheckedAnimationInfo()
        {
            Dictionary<string, List<string>> result = new Dictionary<string, List<string>>();

            foreach (var item in rootItem.children)
            {
                if (item is EntityAnimationCheckboxItem entity && entity.depth == 0)
                {
                    foreach (var animation in entity.children)
                    {
                        if (animation is EntityAnimationCheckboxItem checkbox && checkbox.isChecked)
                        {
                            if (!result.ContainsKey(entity.displayName))
                            {
                                result[entity.displayName] = new List<string>();
                            }

                            result[entity.displayName].Add(animation.displayName);
                        }
                    }
                }
            }

            return result;
        }

        protected override TreeViewItem BuildRoot()
        {
            // Root must have depth = -1
            var newRootItem = new TreeViewItem
            {
                id = 0,
                depth = -1,
                displayName = "Root",
                children = new List<TreeViewItem>()
            };

            if (_scmlObject == null)
            {
                return newRootItem;
            }

            int nextId = 1;

            foreach (var entity in _scmlObject.entities)
            {
                var entityItem = new EntityAnimationCheckboxItem(nextId++, 0, entity.name)
                {
                    children = new List<TreeViewItem>()
                };

                foreach (var animation in entity.animations)
                {
                    entityItem.AddChild(new EntityAnimationCheckboxItem(nextId++, 1, animation.name));
                }

                newRootItem.AddChild(entityItem);
            }

            SetupDepthsFromParentsAndChildren(newRootItem);

            return newRootItem;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 1 &&
                args.rowRect.Contains(Event.current.mousePosition))
            {
                ShowContextMenu(args.item.id);
                Event.current.Use();
            }

            var item = (EntityAnimationCheckboxItem)args.item;

            // Checkbox rect
            Rect toggleRect = args.rowRect;
            toggleRect.x += GetContentIndent(item);
            toggleRect.width = 18;

            item.isChecked = EditorGUI.Toggle(toggleRect, item.isChecked);

            // Label rect (shifted right of checkbox)
            Rect labelRect = args.rowRect;
            labelRect.x = toggleRect.xMax + 4;   // 4px padding

            EditorGUI.LabelField(labelRect, item.displayName);
        }

        protected override void ContextClickedItem(int id)
        {
            ShowContextMenu(id);
        }

        protected override void ContextClicked()
        {
            ShowContextMenu(null);
        }

        private void ShowContextMenu(int? clickedId)
        {
            GenericMenu menu = new GenericMenu();

            menu.AddItem(new GUIContent("Check All"), false, () => CheckAll());
            menu.AddItem(new GUIContent("Check All Entities"), false, () => CheckAllEntities());
            menu.AddItem(new GUIContent("Check All Animations"), false, () => CheckAllAnimations());

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Uncheck All"), false, () => UncheckAll());
            menu.AddItem(new GUIContent("Uncheck All Entities"), false, () => UncheckAllEntities());
            menu.AddItem(new GUIContent("Uncheck All Animations"), false, () => UncheckAllAnimations());

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Expand All"), false, () => ExpandAll());
            menu.AddItem(new GUIContent("Collapse All"), false, () => CollapseAll());

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Select All"), false, () => SelectAll());
            menu.AddItem(new GUIContent("Select None"), false, () => SetSelection(new List<int>()));

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Check Selected"), false, () => CheckSelected(true));
            menu.AddItem(new GUIContent("Uncheck Selected"), false, () => CheckSelected(false));

            menu.ShowAsContext();
        }

        private void SelectAll()
        {
            List<int> ids = new List<int>();
            GetAllItemIds(rootItem, ids);
            SetSelection(ids);
        }

        private void GetAllItemIds(TreeViewItem item, List<int> list)
        {
            if (item.id != 0) // skip root
            {
                list.Add(item.id);
            }

            if (item.hasChildren)
            {
                foreach (var child in item.children)
                {
                    GetAllItemIds(child, list);
                }
            }
        }

        private void CheckSelected(bool value)
        {
            foreach (int id in GetSelection())
            {
                if (FindItem(id, rootItem) is EntityAnimationCheckboxItem checkbox)
                {
                    checkbox.isChecked = value;
                }
            }

            Repaint();
        }

        private void CheckAll() => DoCheckOrUncheckAll(isChecked: true);
        private void CheckAllEntities() => DoCheckOrUncheckAllEntities(isChecked: true);
        private void CheckAllAnimations() => DoCheckOrUncheckAllAnimations(isChecked: true);

        private void UncheckAll() => DoCheckOrUncheckAll(isChecked: false);
        private void UncheckAllEntities() => DoCheckOrUncheckAllEntities(isChecked: false);
        private void UncheckAllAnimations() => DoCheckOrUncheckAllAnimations(isChecked: false);

        private void DoCheckOrUncheckAll(bool isChecked)
        {
            CheckRecursive(rootItem, isChecked);
            Repaint();
        }

        private void DoCheckOrUncheckAllEntities(bool isChecked)
        {
            foreach (var item in rootItem.children)
            {
                if (item is EntityAnimationCheckboxItem entity && entity.depth == 0)
                {
                    entity.isChecked = isChecked;
                }
            }

            Repaint();
        }

        private void DoCheckOrUncheckAllAnimations(bool isChecked)
        {
            foreach (var item in rootItem.children)
            {
                if (item is EntityAnimationCheckboxItem entity && entity.depth == 0 && item.hasChildren)
                {
                    foreach (var child in item.children)
                    {
                        CheckRecursive(child, isChecked);
                    }
                }
            }

            Repaint();

        }

        private void CheckRecursive(TreeViewItem item, bool isChecked)
        {
            if (item is EntityAnimationCheckboxItem checkbox && item.id != 0)
            {
                checkbox.isChecked = isChecked;
            }

            if (item.hasChildren)
            {
                foreach (var child in item.children)
                {
                    CheckRecursive(child, isChecked);
                }
            }
        }
    }

    private class EntityAnimationCheckboxItem : TreeViewItem
    {
        public bool isChecked;

        public EntityAnimationCheckboxItem(int id, int depth, string name) : base(id, depth, name)
        {
        }
    }
}
