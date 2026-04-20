// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEditor;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Xml;
using System.IO;

public class ResizeSpriterProject : EditorWindow
{
    public string InputPath = "";
    public string OutputPath = "";
    public float NewScale = 0.3f;

    private bool _conversionComplete;
    GUIStyle _invalidFieldStyle;

    private static readonly float minScale = 0.05f;
    private static readonly float maxScale = 0.95f;

    [MenuItem("Assets/Resize Spriter Project...", false, 100)]
    private static void ResizeSpriterProjectMenuItem()
    {
        if (Selection.objects.Length > 0)
        {
            var obj = Selection.objects[0];
            string path = AssetDatabase.GetAssetPath(obj);

            var window = GetWindow<ResizeSpriterProject>();
            window.InputPath = path;
        }
    }

    [MenuItem("Assets/Resize Spriter Project...", true)]
    private static bool ResizeSpriterProjectMenuItem_Validate()
    {
        if (Selection.objects.Length != 1)
        {
            return false;
        }

        string path = AssetDatabase.GetAssetPath(Selection.activeObject);

        return path.EndsWith(".scml", StringComparison.OrdinalIgnoreCase) && !path.Contains("autosave");
    }

    [MenuItem("Window/Resize Spriter Project...")]
    private static void ShowWindow()
    {
        GetWindow<ResizeSpriterProject>();
    }

    void OnEnable()
    {
        titleContent = new GUIContent("Resize Spriter Project");
        minSize = new Vector2(375, 290);
    }

    void InitStyles()
    {
        if (_invalidFieldStyle != null)
        {
            return;
        }

        _invalidFieldStyle = new GUIStyle(GUI.skin.label);
        _invalidFieldStyle.fontStyle = FontStyle.Italic;
        _invalidFieldStyle.normal.textColor = EditorGUIUtility.isProSkin
            ? new Color(0.8f, 0f, 0f, 1f)
            : new Color(0.5f, 0.1f, 0.1f, 1f);
    }

    void OnGUI()
    {
        InitStyles();

        EditorGUILayout.Space(8);

        EditorGUILayout.HelpBox("Resize Spriter Project.  This utility will copy the input Spriter project's .scml " +
            "file and all of the project's image files into another folder and resize them with the scale specified by " +
            "'New Scale.'  You are strongly advised to select an empty output folder.  (Creating it, if necessary.)",
            MessageType.Info, wide: true);

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Input File (.scml)  (The Spriter project to resize)");

        EditorGUILayout.BeginHorizontal();

        GUI.SetNextControlName("InputPath");
        InputPath = EditorGUILayout.TextField(InputPath);

        if (GUILayout.Button("…", GUILayout.Width(20), GUILayout.Height(18)))
        {
            if (GUI.GetNameOfFocusedControl() == "InputPath")
            {
                GUI.FocusControl(null);
            }

            InputPath = EditorUtility.OpenFilePanel(
                title: "Select Spriter Input File",
                directory: Application.dataPath,
                extension: "scml"
            );

            if (InputPath.StartsWith(Application.dataPath))
            {
                InputPath = "Assets" + InputPath.Substring(Application.dataPath.Length);
            }
        }

        EditorGUILayout.EndHorizontal();

        bool isInputPathValid = !string.IsNullOrEmpty(InputPath) && File.Exists(InputPath);

        if (!isInputPathValid)
        {
            EditorGUILayout.LabelField("* This field is invalid.", _invalidFieldStyle);
        }
        else
        {
            EditorGUILayout.Space(10);
        }

        EditorGUILayout.LabelField("Output File and Folder  (The newly created project)");

        EditorGUILayout.BeginHorizontal();

        GUI.SetNextControlName("OutputPath");
        OutputPath = EditorGUILayout.TextField(OutputPath);

        if (GUILayout.Button("…", GUILayout.Width(20), GUILayout.Height(18)))
        {
            if (GUI.GetNameOfFocusedControl() == "OutputPath")
            {
                GUI.FocusControl(null);
            }

            OutputPath = EditorUtility.SaveFilePanel(
                title: "Save as Spriter Project (scml & images)",
                directory: Application.dataPath,
                defaultName: "",
                extension: "scml"
            );

            if (OutputPath.StartsWith(Application.dataPath))
            {
                OutputPath = "Assets" + OutputPath.Substring(Application.dataPath.Length);

                var outputDirectory = Path.GetDirectoryName(OutputPath);

                AssetDatabase.ImportAsset(outputDirectory, ImportAssetOptions.Default); // In case the user created it.
            }
        }

        EditorGUILayout.EndHorizontal();

        bool isOutputDirectoryValid =
            !string.IsNullOrEmpty(OutputPath) &&
            Directory.Exists(Path.GetDirectoryName(OutputPath)) &&
            InputPath != OutputPath;

        bool willOverwriteExisting = isOutputDirectoryValid && File.Exists(OutputPath);

        if (!isOutputDirectoryValid)
        {
            EditorGUILayout.LabelField("* This field is invalid.", _invalidFieldStyle);
        }
        else if (willOverwriteExisting)
        {
            EditorGUILayout.LabelField("* This file will be overwritten.", _invalidFieldStyle);
        }
        else
        {
            EditorGUILayout.Space(10);
        }

        EditorGUILayout.LabelField("The Output Project's New Scale");

        NewScale = EditorGUILayout.Slider(NewScale, minScale, maxScale);

        EditorGUILayout.Space(16);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (_conversionComplete)
        {
            if (GUILayout.Button("Dismiss", GUILayout.Width(100), GUILayout.Height(24)))
            {
                Close();
            }
        }
        else
        {
            if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(24)))
            {
                Close();
            }

            GUILayout.FlexibleSpace();

            GUI.enabled =
                !string.IsNullOrEmpty(InputPath) &&
                !string.IsNullOrEmpty(OutputPath) &&
                File.Exists(InputPath) &&
                Directory.Exists(Path.GetDirectoryName(OutputPath)) &&
                InputPath != OutputPath;

            if (GUILayout.Button("Create", GUILayout.Width(100), GUILayout.Height(24)))
            {
                CreateResizedSpriterProject();
                _conversionComplete = true;
            }

            GUI.enabled = true;
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    void CreateResizedSpriterProject()
    {
        string lastFileHandled = "";
        int lastFileWidth = 0;
        int lastFileHeight = 0;

        var inputDirectory = Path.GetDirectoryName(InputPath);
        var outputDirectory = Path.GetDirectoryName(OutputPath);

        void ImageFileHandler(string file, ImageResizer imageResizer)
        {
            string inputFullPath = $"{inputDirectory}/{file}";
            string outputFullPath = $"{outputDirectory}/{file}";

            Debug.Log($"Resizing '{inputFullPath}' and writing to '{outputFullPath}' with a scale of {NewScale}");

            // Make sure the output directory exists.  Create it if necessary.
            string outputFileDirectory = Path.GetDirectoryName(outputFullPath);

            if (!Directory.Exists(outputFileDirectory))
            {
                Directory.CreateDirectory(outputFileDirectory);
            }

            imageResizer.ResizeImage(inputFullPath, outputFullPath, NewScale, ref lastFileWidth, ref lastFileHeight);

            lastFileHandled = file;
        }

        string GetFileWidthAttribValue(string valueStr, string file)
        {
            if (file != lastFileHandled)
            {
                Debug.LogError("GetFileWidthAttribValue(): The 'last file handled' isn't correct.  This is a programming error.");
            }

            return lastFileWidth.ToString();
        }

        string GetFileHeightAttribValue(string valueStr, string file)
        {
            if (file != lastFileHandled)
            {
                Debug.LogError("GetFileHeightAttribValue(): The 'last file handled' isn't correct.  This is a programming error.");
            }

            return lastFileHeight.ToString();
        }

        string ScaleDoubleValue(string valueStr) => (float.Parse(valueStr) * NewScale).ToString("0.######");

        string ScaleIntValue(string valueStr) => Mathf.RoundToInt(float.Parse(valueStr) * NewScale).ToString();

        var replacementsByElement = new Dictionary<string, Dictionary<string, Func<string, string, string>>>
        {
            ["file"] = new Dictionary<string, Func<string, string, string>>
            {
                ["width"] = (oldValue, file) => GetFileWidthAttribValue(oldValue, file),
                ["height"] = (oldValue, file) => GetFileHeightAttribValue(oldValue, file)
            },
            ["obj_info"] = new Dictionary<string, Func<string, string, string>>
            {
                ["w"] = (oldValue, file) => ScaleDoubleValue(oldValue),
                ["h"] = (oldValue, file) => ScaleDoubleValue(oldValue)
            },
            ["bone"] = new Dictionary<string, Func<string, string, string>>
            {
                ["x"] = (oldValue, file) => ScaleDoubleValue(oldValue),
                ["y"] = (oldValue, file) => ScaleDoubleValue(oldValue)
            },
            ["object"] = new Dictionary<string, Func<string, string, string>>
            {
                ["x"] = (oldValue, file) => ScaleDoubleValue(oldValue),
                ["y"] = (oldValue, file) => ScaleDoubleValue(oldValue)
            },
            ["gline"] = new Dictionary<string, Func<string, string, string>>
            {
                ["pos"] = (oldValue, file) => ScaleIntValue(oldValue)
            }
        };

        AssetDatabase.StartAssetEditing();

        using (var imageResizer = new ImageResizer())
        UpdateSpriterFileAttributes( // And resize images.
            inPath: InputPath,
            outPath: OutputPath,
            fileHandler: (file) => ImageFileHandler(file, imageResizer),
            predicate: (elem, attr, val, file) => replacementsByElement.TryGetValue(elem, out var attrs) && attrs.ContainsKey(attr),
            modifier: (elem, attr, oldVal, file) => replacementsByElement[elem][attr](oldVal, file)
        );

        AssetDatabase.StopAssetEditing();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
    }

    private static void UpdateSpriterFileAttributes(
        string inPath,
        string outPath,
        Action<string> fileHandler,
        Func<string, string, string, string, bool> predicate,
        Func<string, string, string, string, string> modifier)
    {
        var readerSettings = new XmlReaderSettings
        {
            IgnoreWhitespace = false,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false
        };

        var writerSettings = new XmlWriterSettings
        {
            Indent = false,
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };

        using (var reader = XmlReader.Create(inPath, readerSettings))
        using (var writer = XmlWriter.Create(outPath, writerSettings))
        {
            writer.WriteStartDocument(true);

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:

                        string elementName = reader.LocalName;
                        string currentFileName = elementName == "file" ? reader.GetAttribute("name") : null;

                        if (!string.IsNullOrEmpty(currentFileName))
                        {
                            fileHandler(currentFileName);
                        }

                        // write the <tag>
                        writer.WriteStartElement(
                            reader.Prefix,
                            reader.LocalName,
                            reader.NamespaceURI);

                        // copy/patch each attribute
                        if (reader.HasAttributes)
                        {
                            reader.MoveToFirstAttribute();

                            do
                            {
                                var attribName = reader.Name;
                                var attribValue = reader.Value;

                                if (predicate(elementName, attribName, attribValue, currentFileName))
                                {
                                    attribValue = modifier(elementName, attribName, attribValue, currentFileName);
                                }

                                writer.WriteAttributeString(
                                    reader.Prefix,
                                    reader.LocalName,
                                    reader.NamespaceURI,
                                    attribValue);
                            }
                            while (reader.MoveToNextAttribute());

                            reader.MoveToElement();
                        }

                        // automatically close empty elements
                        if (reader.IsEmptyElement)
                        {
                            writer.WriteEndElement();
                        }

                        break;

                    case XmlNodeType.EndElement:
                        writer.WriteFullEndElement();
                        break;

                    case XmlNodeType.Text:
                        writer.WriteString(reader.Value);
                        break;

                    case XmlNodeType.CDATA:
                        writer.WriteCData(reader.Value);
                        break;

                    case XmlNodeType.ProcessingInstruction:
                        writer.WriteProcessingInstruction(
                            reader.Name,
                            reader.Value);
                        break;

                    case XmlNodeType.Comment:
                        writer.WriteComment(reader.Value);
                        break;

                    case XmlNodeType.DocumentType:
                        writer.WriteDocType(
                            reader.Name,
                            reader.GetAttribute("PUBLIC"),
                            reader.GetAttribute("SYSTEM"),
                            reader.Value);
                        break;

                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        // Preserve indent/newlines
                        writer.WriteWhitespace(reader.Value);
                        break;

                    default:
                        // writer.WriteNode(reader, false);
                        break;
                }
            }
        }
    }

    private sealed class ImageResizer : IDisposable
    {
        private readonly Material _bleedMat;
        private readonly Material _blurMat;
        private readonly Material _bicubicMat;

        private bool _disposed;

        public ImageResizer()
        {
            _bleedMat = new Material(Shader.Find("Hidden/BleedTransparent"));
            _blurMat = new Material(Shader.Find("Hidden/SeparableGaussianBlur"));
            _bicubicMat = new Material(Shader.Find("Hidden/BicubicResize"));
        }

        public bool ResizeImage(string inputPath, string outputPath, float scale,
            ref int newWidth, ref int newHeight)
        {
            if (!File.Exists(inputPath))
            {
                Debug.LogError($"ResizeImage: Input file not found: {inputPath}");
                return false;
            }

            Texture2D source = LoadTexture(inputPath);
            if (source == null)
            {
                Debug.LogError("ResizeImage: Failed to load texture.");
                return false;
            }

            source.wrapMode = TextureWrapMode.Clamp;
            source.wrapModeU = TextureWrapMode.Clamp;
            source.wrapModeV = TextureWrapMode.Clamp;

            newWidth = Mathf.FloorToInt(source.width * scale + 0.5f);
            newHeight = Mathf.FloorToInt(source.height * scale + 0.5f);

            Texture2D resized = ProgressiveResize(source, newWidth, newHeight);

            SaveTexture(resized, outputPath);

            DestroyImmediate(source);
            DestroyImmediate(resized);

            return true;
        }

        private Texture2D ProgressiveResize(Texture2D src, int finalW, int finalH)
        {
            Texture2D current = src;

            current.wrapMode = TextureWrapMode.Clamp;
            current.wrapModeU = TextureWrapMode.Clamp;
            current.wrapModeV = TextureWrapMode.Clamp;

            // Half only while next‐half is still at least twice the final size
            while (current.width / 2 >= finalW * 2 || current.height / 2 >= finalH * 2)
            {
                int w = current.width / 2;
                int h = current.height / 2;

                RenderTexture bleedRt = RenderTexture.GetTemporary(current.width, current.height, 0, RenderTextureFormat.Default);
                Graphics.Blit(current, bleedRt, _bleedMat);

                var next = PreBlurAndResize(bleedRt, w, h);

                RenderTexture.ReleaseTemporary(bleedRt);

                if (current != src)
                {
                    DestroyImmediate(current);
                }

                current = next;

                current.wrapMode = TextureWrapMode.Clamp;
                current.wrapModeU = TextureWrapMode.Clamp;
                current.wrapModeV = TextureWrapMode.Clamp;
            }

            // Single final step
            if (current.width != finalW || current.height != finalH)
            {
                RenderTexture bleedRt = RenderTexture.GetTemporary(current.width, current.height, 0, RenderTextureFormat.Default);
                Graphics.Blit(current, bleedRt, _bleedMat);

                var last = PreBlurAndResize(bleedRt, finalW, finalH);

                RenderTexture.ReleaseTemporary(bleedRt);

                if (current != src)
                {
                    DestroyImmediate(current);
                }

                current = last;
            }

            return current;
        }

        private static Texture2D LoadTexture(string path)
        {
            byte[] data = File.ReadAllBytes(path);

            // Force sRGB interpretation regardless of project color space
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: false);
            tex.LoadImage(data, markNonReadable: false);

            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            return tex;
        }

        private Texture2D PreBlurAndResize(RenderTexture source, int outW, int outH)
        {
            // Prepare descriptors
            var desc = new RenderTextureDescriptor(source.width, source.height,
                RenderTextureFormat.ARGB32, 0)
            {
                sRGB = true,
                useMipMap = false,
                autoGenerateMips = false
            };

            // When creating RTs:
            desc.colorFormat = RenderTextureFormat.ARGB32;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;

            // Create two temp RTs for blur
            var rtH = RenderTexture.GetTemporary(desc);
            rtH.wrapMode = TextureWrapMode.Clamp;
            var rtV = RenderTexture.GetTemporary(desc);
            rtV.wrapMode = TextureWrapMode.Clamp;

            // Shader expects _Direction = (1,0) or (0,1)
            _blurMat.SetVector("_Direction", Vector2.right);
            Graphics.Blit(source, rtH, _blurMat);

            _blurMat.SetVector("_Direction", Vector2.up);
            Graphics.Blit(rtH, rtV, _blurMat);

            // Now bicubic-resize the blurred RT into final RT
            desc.width = outW;
            desc.height = outH;
            var rtFinal = RenderTexture.GetTemporary(desc);

            Graphics.Blit(rtV, rtFinal, _bicubicMat);

            RenderTexture.active = rtFinal;
            var result = new Texture2D(outW, outH, TextureFormat.RGBA32, mipChain: false, linear: false);
            result.ReadPixels(new Rect(0, 0, outW, outH), 0, 0);
            result.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            // Cleanup
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rtH);
            RenderTexture.ReleaseTemporary(rtV);
            RenderTexture.ReleaseTemporary(rtFinal);

            return result;
        }

        private static void SaveTexture(Texture2D tex, string outputPath)
        {
            byte[] pngData = tex.EncodeToPNG();
            File.WriteAllBytes(outputPath, pngData);
            AssetDatabase.Refresh();
        }

        #region IDisposable Support
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            DestroyImmediate(_bleedMat);
            DestroyImmediate(_blurMat);
            DestroyImmediate(_bicubicMat);

            _disposed = true;

            GC.SuppressFinalize(this);
        }

        ~ImageResizer()
        {
            Dispose();
        }
        #endregion

    }
}
