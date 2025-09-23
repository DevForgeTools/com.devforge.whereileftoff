// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System.IO;
using System.Security;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Simple in-Editor README viewer. Loads the preferred README (ES/EN),
    /// converts a minimal subset of Markdown to Unity rich text, and renders it in a scrollable window.
    /// </summary>
    internal class DocsViewerWindow : EditorWindow
    {
        Vector2 _scroll;
        string _richText;
        string _rawMd;
        string _path;
        
        private const int SPadding = 12;
        
        /// <summary>
        /// Common styles used by this window (created lazily).
        /// </summary>
        static class Styles
        {
            static GUIStyle _headerCenter;

            /// <summary>Centered bold header for the top title.</summary>
            public static GUIStyle HeaderCenter => _headerCenter ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 20,
                fontStyle = FontStyle.Bold,
                wordWrap  = true,
                padding   = new RectOffset(4,4,6,6),
            };
            
            static GUIStyle _pad;
            /// <summary>Outer padding container.</summary>
            public static GUIStyle Pad => _pad ??= new GUIStyle { padding = new RectOffset(SPadding,SPadding,SPadding,SPadding) };
            
        }

        /// <summary>
        /// Opens the documentation viewer window and attempts to load the README.
        /// </summary>
        [MenuItem("Tools/DevForge/Where I Left Off/Open Documentation (In-Editor)", priority = 3001)]
        static void ShowWindow()
        {
            var w = GetWindow<DocsViewerWindow>(Strings.T("docs.windowTitle"));
            w.minSize = new Vector2(640, 480);
            w.LoadReadme();
            w.Show();
        }

        /// <summary>
        /// Ensures the README content is loaded when the window is enabled.
        /// </summary>
        void OnEnable() => LoadReadme();

        /// <summary>
        /// Locates and loads the preferred README file and prepares its rich-text rendering.
        /// </summary>
        void LoadReadme()
        {
            _path = Docs.GetPreferredReadmePath();
            if (!string.IsNullOrEmpty(_path) && File.Exists(_path))
            {
                _rawMd = File.ReadAllText(_path);
                _richText = MdToRichText(_rawMd);
            }
            else
            {
                _rawMd = string.Empty;
                _richText = $"<b>{Strings.T("docs.noReadmeFound.title")}</b>\n\n{Strings.T("docs.noReadmeFound.body")}";
            }
        }

        /// <summary>
        /// Draws the toolbar (open externally / reload) and the converted README body.
        /// </summary>
        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(Strings.T("docs.openExternally"), EditorStyles.toolbarButton))
                {
                    Docs.OpenPreferredReadmeExternally();
                }

                if (GUILayout.Button(Strings.T("docs.reload"), EditorStyles.toolbarButton))
                    LoadReadme();
            }

            using (new EditorGUILayout.VerticalScope(Styles.Pad))
            {
                GUILayout.Space(8);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawHeader();
                }

                GUILayout.Space(8);
                
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var style = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
                    using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
                    {
                        _scroll = scroll.scrollPosition;
                        GUILayout.Label(_richText, style, GUILayout.ExpandHeight(true));
                    }
                }
            }
        }
        
        private void DrawHeader()
        {
            string header = Strings.T("docs.windowTitle");

            // calculate required height for current width
            float w = EditorGUIUtility.currentViewWidth;
            float h = Styles.HeaderCenter.CalcHeight(new GUIContent(header), w);

            var r = GUILayoutUtility.GetRect(0, h, GUILayout.ExpandWidth(true));
            GUI.Label(r, header, Styles.HeaderCenter);
        }

        /// <summary>
        /// Very small Markdown-to-RichText converter for headers, emphasis, lists, quotes, code blocks and inline code.
        /// </summary>
        /// <param name="md">Raw Markdown text.</param>
        /// <returns>Unity rich-text content suitable for a label.</returns>
        static string MdToRichText(string md)
        {
            if (string.IsNullOrEmpty(md)) return string.Empty;

            // Normalize line breaks
            var t = md.Replace("\r\n", "\n");

            // Fenced code blocks ``` ```
            t = Regex.Replace(t, "```([\\s\\S]*?)```", m =>
            {
                var code = m.Groups[1].Value.TrimEnd();
                code = SecurityElement.Escape(code);
                return $"\n<size=11><b>{Strings.T("docs.codeBlockLabel")}</b></size>\n<color=#C0C0C0><i>{code}</i></color>\n";
            });

            // Headers
            t = Regex.Replace(t, "^###\\s+(.+)$", m => $"<size=14><b>{m.Groups[1].Value}</b></size>", RegexOptions.Multiline);
            t = Regex.Replace(t, "^##\\s+(.+)$",  m => $"<size=16><b>{m.Groups[1].Value}</b></size>", RegexOptions.Multiline);
            t = Regex.Replace(t, "^#\\s+(.+)$",   m => $"<size=18><b>{m.Groups[1].Value}</b></size>", RegexOptions.Multiline);

            // Bold / Italic
            t = Regex.Replace(t, "\\*\\*(.+?)\\*\\*", "<b>$1</b>");
            t = Regex.Replace(t, "\\*(.+?)\\*", "<i>$1</i>");

            // Links → show URL text (labels can't open links directly)
            t = Regex.Replace(t, "\\[(.+?)\\]\\((https?://.+?)\\)", "<b>$1</b> <color=#4EA1FF><i>$2</i></color>");

            // Lists
            t = Regex.Replace(t, "^(?:- |\\* )(.+)$", "• $1", RegexOptions.Multiline);

            // Blockquotes
            t = Regex.Replace(t, "^>\\s?(.+)$", "<i><color=#AAAAAA>$1</color></i>", RegexOptions.Multiline);

            // Inline code
            t = Regex.Replace(t, "`([^`]+)`", "<i><color=#C0C0C0>$1</color></i>");

            // Compact extra spacing
            t = Regex.Replace(t, "\n{3,}", "\n\n");

            return t;
        }
    }
}
