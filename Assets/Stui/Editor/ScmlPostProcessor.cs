// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

//This project is open source. Anyone can use any part of this code however they wish
//Feel free to use this code in your own projects, or expand on this code
//If you have any improvements to the code itself, please visit
//https://github.com/Dharengo/Spriter2UnityDX and share your suggestions by creating a fork
//-Dengar/Dharengo

using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Xml.Serialization;
using System.Collections;
using System.Collections.Generic;

namespace Spriter2UnityDX.PostProcessing
{
    using Importing;
    using Prefabs;

    // Detects when a .scml file has been imported, then begins the process to create the prefab
    public class ScmlPostProcessor : AssetPostprocessor
    {
        // Called after an import, detects if imported files end in .scml
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            var filesToProcess = new List<string>();
            bool optionsNeedUpdated = false;

            foreach (var path in importedAssets)
            {
                if (path.EndsWith(".scml") && !path.Contains("autosave"))
                {
                    filesToProcess.Add(path);
                    optionsNeedUpdated = true;
                }
            }

            if (filesToProcess.Count > 0)
            {
                if (optionsNeedUpdated || ScmlImportOptions.options == null)
                {
                    ScmlImportOptionsWindow optionsWindow = EditorWindow.GetWindow<ScmlImportOptionsWindow>();
                    ScmlImportOptions.options = new ScmlImportOptions();
                    optionsWindow.OnImport += () => ProcessFiles(filesToProcess);
                }
                else
                {
                    ProcessFiles(filesToProcess);
                }
            }
        }

        private static void ProcessFiles(IList<string> paths)
        {
            var buildMonitorWindow = EditorWindow.GetWindow<BuildMonitorWindow>();
            buildMonitorWindow.BeginBuild(DoProcessFiles(paths, buildMonitorWindow));

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }

        private static IEnumerator DoProcessFiles(IList<string> paths, BuildMonitorWindow buildMonitorWindow)
        {
            var info = new ScmlProcessingInfo();
            var builder = new PrefabBuilder(info);

            foreach (var path in paths)
            {
                buildMonitorWindow.InputFileName = Path.GetFileName(path);
                yield return $"Importing {path}";

                yield return $"Deserializing {path}";

                var buildProcess = builder.Build(Deserialize(path), path, buildMonitorWindow);

                while (buildProcess.MoveNext())
                {
                    yield return buildProcess.Current;
                }

                if (buildMonitorWindow.IsCanceled)
                {
                    yield break;
                }
            }
        }

        private static ScmlObject Deserialize(string path)
        {
            var serializer = new XmlSerializer(typeof(ScmlObject));
            using (var reader = new StreamReader(path))
            {
                return (ScmlObject)serializer.Deserialize(reader);
            }
        }
    }
}

namespace Spriter2UnityDX
{
    public class ScmlProcessingInfo
    {
        public List<GameObject> NewPrefabs { get; set; }
        public List<GameObject> ModifiedPrefabs { get; set; }
        public List<AnimationClip> NewAnims { get; set; }
        public List<AnimationClip> ModifiedAnims { get; set; }
        public List<AnimatorController> NewControllers { get; set; }
        public List<AnimatorController> ModifiedControllers { get; set; }

        public ScmlProcessingInfo()
        {
            NewPrefabs = new List<GameObject>();
            ModifiedPrefabs = new List<GameObject>();
            NewAnims = new List<AnimationClip>();
            ModifiedAnims = new List<AnimationClip>();
            NewControllers = new List<AnimatorController>();
            ModifiedControllers = new List<AnimatorController>();
        }
    }
}
