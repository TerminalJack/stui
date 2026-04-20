#nullable enable

// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEngine;
using Dumpify;
using Spriter2UnityDX.Importing;
using System.Collections;
using System.Threading;
using System;
using System.Runtime.CompilerServices;

namespace Spriter2UnityDX.Extensions
{
    public static class UnityDumpifyExtensions
    {
        public static void DumpToUnityConsole<T>(this T obj, bool? expandLines = null, string? linePrefix = null,
            string? label = null, int? maxDepth = null, IRenderer? renderer = null, bool? useDescriptors = null,
            ColorConfig? colors = null, MembersConfig? members = null, TypeNamingConfig? typeNames = null,
            TableConfig? tableConfig = null, OutputConfig? outputConfig = null,
            TypeRenderingConfig? typeRenderingConfig = null, TruncationConfig? truncationConfig = null,
            [CallerArgumentExpression("obj")] string? autoLabel = null)
        {
            var dumpLines = obj.DumpText(label, maxDepth, renderer, useDescriptors, colors, members, typeNames,
                tableConfig, outputConfig, typeRenderingConfig, truncationConfig, autoLabel);

            bool shouldExpandLines = expandLines ?? true;

            if (shouldExpandLines)
            {
                string prefix = linePrefix ?? "";

                foreach (var line in dumpLines.Split("\n"))
                {
                    Debug.Log(prefix + line); // Outputs a formatted table to the Unity Console
                }
            }
            else
            {
                Debug.Log(dumpLines);
            }
        }

        public static IEnumerator DumpToUnityConsoleRoutine<T>(this T obj, IBuildTaskContext ctx,
            bool? expandLines = null, string? linePrefix = null,
            string? label = null, int? maxDepth = null, IRenderer? renderer = null, bool? useDescriptors = null,
            ColorConfig? colors = null, MembersConfig? members = null, TypeNamingConfig? typeNames = null,
            TableConfig? tableConfig = null, OutputConfig? outputConfig = null,
            TypeRenderingConfig? typeRenderingConfig = null, TruncationConfig? truncationConfig = null,
            [CallerArgumentExpression("obj")] string? autoLabel = null)
        {
            Exception? dumpException = null;
            string dumpLines = "";

            var thread = new Thread(() =>
            {
                try
                {
                    dumpLines = obj.DumpText(label, maxDepth, renderer, useDescriptors, colors, members, typeNames,
                        tableConfig, outputConfig, typeRenderingConfig, truncationConfig, autoLabel);
                }
                catch (ThreadAbortException)
                {
                    // Swallow — expected during cancellation
                }
                catch (Exception ex)
                {
                    dumpException = ex;
                }
            });

            thread.IsBackground = true;
            thread.Start();

            while (thread.IsAlive)
            {
                if (ctx.IsCanceled)
                {
                    try
                    {
                        thread?.Abort();
                    }
                    catch { }

                    yield break;
                }

                yield return null; // Wait until next frame.
            }

            if (dumpException == null)
            {
                bool shouldExpandLines = expandLines ?? true;

                if (shouldExpandLines)
                {
                    int lineCount = 0;
                    string prefix = linePrefix ?? "";

                    foreach (var line in dumpLines.Split("\n"))
                    {
                        Debug.Log(prefix + line); // Outputs a formatted table to the Unity Console

                        if (++lineCount > 100)
                        {
                            yield return ""; // Don't log as a message.  Don't (unconditionally) wait for next frame.
                            lineCount = 0;
                        }

                        if (ctx.IsCanceled) { yield break; }
                    }
                }
                else
                {
                    Debug.Log(dumpLines);
                }
            }
            else
            {
                Debug.LogException(dumpException);
            }
        }
    }
}