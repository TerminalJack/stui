// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using System;
using UnityEditor;
using UnityEngine;
using System.Xml.Serialization;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Stui.Importing;
using Stui.Prefabs;
using Stui.EntityInfo;

namespace Stui
{
    public class SpriterProjectLogger
    {
        [MenuItem("Assets/Log Spriter Project Info. to Console", false, 105)]
        private static void LogSpriterProjectToConsoleMenuItem()
        {
            if (Selection.objects.Length > 0)
            {
                var obj = Selection.objects[0];
                string path = AssetDatabase.GetAssetPath(obj);

                LogToConsole(path);
            }
        }

        [MenuItem("Assets/Log Spriter Project Info. to Console", true)]
        private static bool LogSpriterProjectToConsoleMenuItem_Validate()
        {
            if (Selection.objects.Length != 1)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);

            return path.EndsWith(".scml", StringComparison.OrdinalIgnoreCase) && !path.Contains("autosave");
        }

        public static void LogToConsole(string scmlInputPath)
        {
            try
            {
                Debug.Log($"Logging Spriter project file '{scmlInputPath}' to console...");

                ScmlObject scmlObject = Deserialize(scmlInputPath);

                var scmlFileLoggerTask = new ScmlFileLoggerTask();

                scmlFileLoggerTask.BeginTask(DoProcessFile(scmlObject, scmlInputPath, scmlFileLoggerTask));
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

        private static IEnumerator DoProcessFile(ScmlObject scmlObject, string scmlInputPath, IBuildTaskContext loggingTaskCtx)
        {
            var fileLogger = new SpriterProjectFileLogger
            {
                ScmlObject = scmlObject,
                ScmlPath = scmlInputPath,
                LoggingTaskCtx = loggingTaskCtx
            };

            loggingTaskCtx.InputFileName = Path.GetFileName(scmlInputPath);

            var loggingProcess =
                IteratorUtils.SafeEnumerable(
                    () => fileLogger.Process(),
                    ex =>
                    {
                        Debug.LogException(ex);
                    });

            while (loggingProcess.MoveNext())
            {
                yield return loggingProcess.Current;
            }
        }

        private class SpriterProjectFileLogger
        {
            public ScmlObject ScmlObject { get; set; }
            public string ScmlPath { get; set; }
            public IBuildTaskContext LoggingTaskCtx { get; set; }

            public IEnumerator Process()
            {
                var spriterProjDirectory = Path.GetDirectoryName(ScmlPath);

                yield return $"{LoggingTaskCtx.MessagePrefix}: Checking image and sound file existence...";

                // We need to load fileInfo up for SpriterEntityInfo.
                var folders = new Dictionary<int, IDictionary<int, Sprite>>();
                var fileInfo = new Dictionary<int, IDictionary<int, Importing.File>>();

                foreach (var folder in ScmlObject.folders)
                {
                    var files = folders[folder.id] = new Dictionary<int, Sprite>();
                    var fi = fileInfo[folder.id] = new Dictionary<int, Importing.File>();

                    foreach (var file in folder.files)
                    {
                        var path = string.Format("{0}/{1}", spriterProjDirectory, file.name);

                        if (file.objectType == ObjectType.sprite)
                        {
                            files[file.id] = PrefabBuilder.GetSpriteAtPath(path, logWarning: false);
                            fi[file.id] = file;

                            string resultStr = files[file.id] != null ? "ok" : "NOT FOUND";
                            yield return $"{LoggingTaskCtx.MessagePrefix}: Checking sprite at {path}...  {resultStr}";
                        }
                        else if (file.objectType == ObjectType.sound)
                        {
                            string resultStr = System.IO.File.Exists(path) ? "ok" : "NOT FOUND";
                            yield return $"{LoggingTaskCtx.MessagePrefix}: Checking sound file at {path}...  {resultStr}";
                        }
                    }
                }

                LoggingTaskCtx.InputFileName = "";
                LoggingTaskCtx.AnimationName = "";

                foreach (var entity in ScmlObject.entities)
                {
                    LoggingTaskCtx.EntityName = entity.name;

                    yield return "==========================================================";
                    yield return $"Logging entity '{entity.name}'";
                    yield return "==========================================================";

                    SpriterEntityInfo entityInfo = new SpriterEntityInfo()
                    {
                        loggingEnabled = true
                    };

                    var entityInfoProcess = entityInfo.Process(spriterProjDirectory, ScmlObject, entity, fileInfo, LoggingTaskCtx);

                    while (entityInfoProcess.MoveNext())
                    {
                        yield return entityInfoProcess.Current;
                    }
                }
            }
        }

        private class ScmlFileLoggerTask : IBuildTaskContext
        {
            private List<string> _importedPrefabs = new List<string>();
            private IEnumerator _task;

            public bool IsCanceled => false;
            public string InputFileName { get; set; }
            public string EntityName { get; set; }
            public string AnimationName { get; set; }
            public List<string> ImportedPrefabs => _importedPrefabs;

            public void Cancel() {}

            private static readonly double _maxTaskTimePerFrame_s = 0.013;

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
                        break; // Wait for next frame.
                    }
                }
            }

            private void HandleTaskCompletion()
            {
                EditorApplication.update -= OnEditorUpdate;
                _task = null;

                Status("Spriter project file logger task complete.");
            }

            private void Status(string message)
            {
                Debug.Log(message);
            }
        }
    }
}