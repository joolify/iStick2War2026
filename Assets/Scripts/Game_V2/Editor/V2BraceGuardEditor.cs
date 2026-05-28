using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace iStick2War_V2.Editor
{
    /*
     * V2BraceGuardEditor (brace mismatch guard for *_V2.cs)
     *
     * PURPOSE:
     * Catches common "extra } at file end" issues before they become confusing compile cascades.
     * Runs automatically before compilation and can also be triggered manually from the Tools menu.
     *
     * NOTE:
     * Lightweight lexical scan only (ignores braces inside comments/strings/chars).
     */
    [InitializeOnLoad]
    internal static class V2BraceGuardEditor
    {
        private const string MenuPath = "Tools/iStick2War/Validate *_V2 Brace Balance";

        static V2BraceGuardEditor()
        {
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
        }

        [MenuItem(MenuPath)]
        private static void ValidateMenu()
        {
            ValidateAndLogIssues(logSuccess: true);
        }

        private static void OnCompilationStarted(object _)
        {
            ValidateAndLogIssues(logSuccess: false);
        }

        private static void ValidateAndLogIssues(bool logSuccess)
        {
            string[] guids = AssetDatabase.FindAssets("t:Script");
            int checkedCount = 0;
            int issueCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path) ||
                    !path.EndsWith("_V2.cs") ||
                    path.Contains("/Editor/"))
                {
                    continue;
                }

                checkedCount++;
                if (!TryReadAllText(path, out string source))
                {
                    continue;
                }

                if (TryFindBraceIssue(source, out int line, out string reason))
                {
                    issueCount++;
                    Debug.LogError($"[V2BraceGuard] {path}({line},1): {reason}");
                }
            }

            if (logSuccess)
            {
                if (issueCount == 0)
                {
                    Debug.Log($"[V2BraceGuard] OK. Checked {checkedCount} *_V2.cs files.");
                }
                else
                {
                    Debug.LogWarning($"[V2BraceGuard] Found {issueCount} brace issue(s) across {checkedCount} *_V2.cs files.");
                }
            }
        }

        private static bool TryReadAllText(string assetPath, out string source)
        {
            source = string.Empty;
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            source = File.ReadAllText(fullPath);
            return true;
        }

        private static bool TryFindBraceIssue(string source, out int line, out string reason)
        {
            line = 1;
            reason = string.Empty;

            Stack<int> openings = new Stack<int>(64);
            bool inSingleLine = false;
            bool inMultiLine = false;
            bool inString = false;
            bool inChar = false;
            bool isVerbatimString = false;
            bool escaped = false;
            int currentLine = 1;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                char n = i + 1 < source.Length ? source[i + 1] : '\0';

                if (c == '\n')
                {
                    currentLine++;
                    inSingleLine = false;
                    escaped = false;
                    continue;
                }

                if (inSingleLine)
                {
                    continue;
                }

                if (inMultiLine)
                {
                    if (c == '*' && n == '/')
                    {
                        inMultiLine = false;
                        i++;
                    }

                    continue;
                }

                if (inString)
                {
                    if (isVerbatimString)
                    {
                        if (c == '"' && n == '"')
                        {
                            i++;
                            continue;
                        }

                        if (c == '"')
                        {
                            inString = false;
                            isVerbatimString = false;
                        }
                    }
                    else
                    {
                        if (!escaped && c == '"')
                        {
                            inString = false;
                        }

                        escaped = !escaped && c == '\\';
                    }

                    continue;
                }

                if (inChar)
                {
                    if (!escaped && c == '\'')
                    {
                        inChar = false;
                    }

                    escaped = !escaped && c == '\\';
                    continue;
                }

                if (c == '/' && n == '/')
                {
                    inSingleLine = true;
                    i++;
                    continue;
                }

                if (c == '/' && n == '*')
                {
                    inMultiLine = true;
                    i++;
                    continue;
                }

                if (c == '@' && n == '"')
                {
                    inString = true;
                    isVerbatimString = true;
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    escaped = false;
                    continue;
                }

                if (c == '\'')
                {
                    inChar = true;
                    escaped = false;
                    continue;
                }

                if (c == '{')
                {
                    openings.Push(currentLine);
                }
                else if (c == '}')
                {
                    if (openings.Count == 0)
                    {
                        line = currentLine;
                        reason = "CS1022-like pattern: extra closing brace '}' without matching '{'.";
                        return true;
                    }

                    openings.Pop();
                }
            }

            if (openings.Count > 0)
            {
                line = openings.Peek();
                reason = "Missing closing brace '}' for an opening '{'.";
                return true;
            }

            return false;
        }
    }
}
