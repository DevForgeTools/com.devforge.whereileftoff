// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Popup mode options for the notes window.
    /// </summary>
    public enum NotesPopupMode
    {
        CurrentSession = 0,
        CurrentDay     = 1,
        LastSession    = 2,
        LastDay        = 3,
    }

    /// <summary>
    /// Compact popup window that shows recent WILO notes.
    /// Supports quick switching of ranges (current/last session or day),
    /// search, expand/collapse, read/re-read toggles, and opening referenced assets.
    /// </summary>
    public class WiloNotesPopupWindow : EditorWindow
    {
        /// <summary>Window title text.</summary>
        const string WindowTitleKey = "Where I Left Off - Last Notes";
        /// <summary>Control name for the search field (for programmatic focus).</summary>
        const string SearchControlName = "WILO_NOTES_SEARCH";

        // ----- State -----

        /// <summary>Current active mode (range selector).</summary>
        NotesPopupMode _mode = NotesPopupMode.LastSession;
        /// <summary>Main scroll position.</summary>
        Vector2 _scroll;
        /// <summary>Current search query.</summary>
        string _search = "";

        /// <summary>When true, renders titles only (hides body), replacing the older “Compact” concept.</summary>
        bool _titlesOnly = false;

        /// <summary>Currently visible notes after filtering and search.</summary>
        List<WiloNote> _visible = new();
        /// <summary>Notes for the active mode before search filtering.</summary>
        List<WiloNote> _filtered = new();
        /// <summary>Per-note expansion state keyed by note identifier.</summary>
        readonly Dictionary<string, bool> _expanded = new();

        /// <summary>Human-readable label describing the current range.</summary>
        string _rangeLabel = "";
        /// <summary>Whether the chosen mode actually has data available.</summary>
        bool _hasDataForMode = false;

        /// <summary>UI labels for the toolbar mode selector (Spanish by design).</summary>
        string[] ModeLabels => new[]
        {
            Strings.T("popup.mode.currentSession"),
            Strings.T("popup.mode.currentDay"),
            Strings.T("popup.mode.lastSession"),
            Strings.T("popup.mode.lastDay"),
        };
        /// <summary>Cached minimum width required for the toolbar to avoid layout breakage.</summary>
        float _toolbarMinWidth;
        /// <summary>Prevents recomputing min size multiple times.</summary>
        bool _minSizeLocked;

        // Shortcuts
        /// <summary>Request to focus the search field on next repaint (Ctrl/Cmd+F).</summary>
        bool _wantFocusSearch;
        /// <summary>Request to blur the active control on next repaint (e.g., after ESC).</summary>
        bool _wantBlur;

        // Styles
        /// <summary>Cached GUI styles used by the window.</summary>
        static GUIStyle _titleCenter, _subHeader, _wrapLabel, _rowTitle, _rowBg, _snippetStyle, _miniRight, _badgeMini;
        
        /// <summary>Outer padding for the main layout.</summary>
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
        
        // Right fixed column (date·time) and reserved space for reference badge
        /// <summary>Fixed width for the right-side date/time column.</summary>
        const float kDateColWidth = 170f;
        /// <summary>Reserved width for the reference-count badge (space is kept even when 0).</summary>
        const float kRefBadgeWidth = 34f;

        /// <summary>
        /// Opens the popup from the menu, using the startup preference to decide Last Session or Last Day.
        /// </summary>
        [MenuItem("Tools/DevForge/Where I Left Off/Last Notes", priority = 2001)]
        public static void OpenMenu()
        {
            var w = CreateInstance<WiloNotesPopupWindow>();
            w.titleContent = new GUIContent(Strings.T(WindowTitleKey));
            w.minSize = new Vector2(560, 360);

            // Startup mode preference (Last Day vs Last Session)
            var pref = WiloPreferences.GetStartupPopupRange();
            w._mode = pref == WiloPreferences.StartupPopupRange.LastDay ? NotesPopupMode.LastDay : NotesPopupMode.LastSession;

            w.Refresh();
            w.ShowUtility();
            w.Focus();
        }

        /// <summary>
        /// Opens the popup window programmatically with a given mode.
        /// </summary>
        public static void Open(NotesPopupMode mode = NotesPopupMode.LastSession)
        {
            var w = CreateInstance<WiloNotesPopupWindow>();
            w.titleContent = new GUIContent(Strings.T(WindowTitleKey));
            w.minSize = new Vector2(560, 360);
            w._mode = mode;
            w.Refresh();
            w.ShowUtility();
            w.Focus();
        }

        /// <summary>
        /// Initializes GUI styles and loads initial data when the window is enabled.
        /// </summary>
        void OnEnable()
        {
            _titleCenter = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 20,
                fontStyle = FontStyle.Bold,
                wordWrap  = true,
                padding   = new RectOffset(4,4,6,6),
            };
            _subHeader = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = true,
                padding   = new RectOffset(0,0,0,6),
            };
            _wrapLabel = new GUIStyle(EditorStyles.label) { wordWrap = true };
            _rowTitle  = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, wordWrap = false, clipping = TextClipping.Clip };
            _rowBg     = new GUIStyle(EditorStyles.helpBox)   { padding = new RectOffset(8,8,8,8) };

            _snippetStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = false,
                clipping = TextClipping.Clip,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(0,0,0,0),
                margin  = new RectOffset(0,0,0,0)
            };

            _miniRight = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            _badgeMini = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };

            Refresh();
        }

        // ---------- Data ----------

        /// <summary>
        /// Loads notes, filters by current mode, prepares expansion state, builds the range label,
        /// and updates the visible list.
        /// </summary>
        void Refresh()
        {
            var file = WiloNotesStorage.LoadNotes();
            var all = file?.notes ?? new List<WiloNote>();
            _filtered = Filter(all, _mode, out _hasDataForMode);

            foreach (var n in _filtered)
            {
                var k = NoteKey(n);
                if (!_expanded.ContainsKey(k)) _expanded[k] = false;
            }

            _rangeLabel = BuildRangeLabel(_filtered, _mode, _hasDataForMode);
            RebuildVisible();
            Repaint();
        }

        /// <summary>
        /// Returns a stable note key (prefers <c>utc</c>, falls back to <c>title</c> or a GUID).
        /// </summary>
        static string NoteKey(WiloNote n)
            => n.utc ?? n.title ?? Guid.NewGuid().ToString();

        /// <summary>
        /// Filters a note list according to the selected popup mode, returning a sorted list
        /// and whether data exists for that mode.
        /// </summary>
        static List<WiloNote> Filter(List<WiloNote> notes, NotesPopupMode mode, out bool hasData)
        {
            hasData = false;
            if (notes == null || notes.Count == 0) return new List<WiloNote>();

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string currentSession = SafeGetCurrentSessionId();

            IEnumerable<WiloNote> q = Enumerable.Empty<WiloNote>();

            switch (mode)
            {
                case NotesPopupMode.CurrentDay:
                    q = notes.Where(n => n.day == today);
                    break;

                case NotesPopupMode.CurrentSession:
                    if (!string.IsNullOrEmpty(currentSession))
                        q = notes.Where(n => n.sessionId == currentSession);
                    break;

                case NotesPopupMode.LastDay:
                {
                    var pastDays = notes
                        .Select(n => n.day)
                        .Where(d => !string.IsNullOrEmpty(d) && d != today)
                        .Distinct()
                        .OrderBy(d => d)
                        .ToList();
                    hasData = pastDays.Count > 0;
                    var lastDay = hasData ? pastDays.Last() : null;
                    q = notes.Where(n => n.day == lastDay);
                    break;
                }

                case NotesPopupMode.LastSession:
                default:
                {
                    var groups = notes
                        .Where(n => !string.IsNullOrEmpty(n.sessionId) && n.sessionId != currentSession)
                        .GroupBy(n => n.sessionId)
                        .Select(g => new { Session = g.Key, MaxUtc = g.Max(x => x.utc ?? string.Empty) })
                        .OrderBy(x => x.MaxUtc)
                        .ToList();
                    hasData = groups.Count > 0;
                    var targetSession = hasData ? groups.Last().Session : null;
                    q = notes.Where(n => n.sessionId == targetSession);
                    break;
                }
            }

            var list = q.OrderByDescending(n => n.utc).ToList();
            if (mode == NotesPopupMode.CurrentDay || mode == NotesPopupMode.CurrentSession)
                hasData = list.Count > 0;
            return list;
        }

        /// <summary>
        /// Safe helper to get/create the current session id, swallowing errors.
        /// </summary>
        static string SafeGetCurrentSessionId()
        {
            try { return WiloUtilities.GetOrCreateSessionId(); }
            catch { return null; }
        }

        /// <summary>
        /// Builds the range label shown under the title according to the mode and data available.
        /// </summary>
        static string BuildRangeLabel(List<WiloNote> notes, NotesPopupMode mode, bool hasData)
        {
            if (mode == NotesPopupMode.CurrentDay)
            {
                var day = DateTime.Now.ToString("yyyy-MM-dd");
                return string.Format(Strings.T("popup.range.currentDay"), day);
            }

            if (mode == NotesPopupMode.LastDay)
            {
                if (!hasData) return Strings.T("popup.range.lastDay.noData");
                var day = notes.Count > 0 ? (notes[0].day ?? "—") : "—";
                return string.Format(Strings.T("popup.range.lastDay"), day);
            }

            // CurrentSession / LastSession
            var prefix = mode == NotesPopupMode.CurrentSession
                ? Strings.T("popup.range.session.prefix.current")
                : Strings.T("popup.range.session.prefix.last");

            if (!hasData)
                return string.Format(Strings.T("popup.range.session.noData"), prefix);

            DateTimeOffset? min = null, max = null;
            foreach (var n in notes)
            {
                if (string.IsNullOrEmpty(n.utc)) continue;
                if (DateTimeOffset.TryParse(n.utc, out var dto))
                {
                    if (min == null || dto < min) min = dto;
                    if (max == null || dto > max) max = dto;
                }
            }

            if (min != null && max != null)
            {
                var a = min.Value.ToLocalTime();
                var b = max.Value.ToLocalTime();
                var sameDay = a.Date == b.Date;
                var dayStr = sameDay ? a.ToString("yyyy-MM-dd") : $"{a:yyyy-MM-dd} ➝ {b:yyyy-MM-dd}";
                return string.Format(Strings.T("popup.range.session.window"),
                    prefix, a.ToString("HH:mm"), b.ToString("HH:mm"), dayStr);
            }

            return string.Format(Strings.T("popup.range.session.noData"), prefix);
        }



        // ---------- UI ----------

        /// <summary>
        /// Main GUI loop: handles shortcuts, sizing, header, toolbar, list and bottom bar.
        /// </summary>
        void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Pad))
            {
                HandleShortcuts(Event.current);

                if (!_minSizeLocked && Event.current.type == EventType.Layout)
                {
                    _toolbarMinWidth = CalcToolbarMinWidth();
                    minSize = new Vector2(Mathf.Max(minSize.x, _toolbarMinWidth), minSize.y);
                    _minSizeLocked = true;
                }

                // Apply focus/blur requests during Repaint
                if (_wantBlur && Event.current.type == EventType.Repaint)
                {
                    GUI.FocusControl(null);
                    _wantBlur = false;
                }

                if (_wantFocusSearch && Event.current.type == EventType.Repaint)
                {
                    Focus();
                    EditorGUI.FocusTextInControl(SearchControlName);
                    _wantFocusSearch = false;
                }

                GUILayout.Space(8);
                
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawHeader();
                }
                
                GUILayout.Space(8);

                Toolbar();

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    _scroll = EditorGUILayout.BeginScrollView(
                        _scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);

                    if (_filtered.Count == 0 || _visible.Count == 0)
                    {
                        var min = GUILayoutUtility.GetRect(1, 120, GUILayout.ExpandWidth(true));
                        using (new GUI.GroupScope(min))
                        {
                        }
                    }
                    else
                    {
                        GUILayout.Space(8);
                        foreach (var n in _visible)
                        {
                            DrawNoteRow(n);
                            DrawSeparator();
                        }
                    }

                    EditorGUILayout.EndScrollView();
                }

                BottomBar();
            }

            HandleWindowContextMenu();
        }

        /// <summary>
        /// Handles keyboard shortcuts: Ctrl/Cmd+F focuses search; ESC clears it and blurs active control.
        /// </summary>
        void HandleShortcuts(Event e)
        {
            if (e == null || e.type != EventType.KeyDown) return;

            var mods = e.modifiers;

            // Ctrl/Cmd + F => focus search
            if ((mods & (EventModifiers.Control | EventModifiers.Command)) != 0 && e.keyCode == KeyCode.F)
            {
                _wantFocusSearch = true;
                e.Use();
                Repaint();
                return;
            }

            // ESC => clear search and blur
            if (e.keyCode == KeyCode.Escape)
            {
                if (!string.IsNullOrEmpty(_search))
                {
                    _search = "";
                    RebuildVisible();
                    _wantBlur = true;
                    e.Use();
                    Repaint();
                }
            }
        }
        
        /// <summary>
        /// Handles right click contextual menu to open the preferences
        /// </summary>
        void HandleWindowContextMenu()
        {
            var e = Event.current;
            if (e.type != EventType.ContextClick) return;   // ← solo ContextClick

            var whole = new Rect(0, 0, position.width, position.height);
            if (!whole.Contains(e.mousePosition)) return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(Strings.T("ctx.prefs")), false, WiloUtilities.OpenWiloPreferences);
            menu.DropDown(new Rect(e.mousePosition, Vector2.zero));
            e.Use();
        }


        /// <summary>Renders the window header (title + range label).</summary>
        void DrawHeader()
        {
            string header = Strings.T("popup.title");

            // calculate required height for current width
            float w = EditorGUIUtility.currentViewWidth;
            float h = Styles.HeaderCenter.CalcHeight(new GUIContent(header), w);

            var r = GUILayoutUtility.GetRect(0, h, GUILayout.ExpandWidth(true));
            GUI.Label(r, header, Styles.HeaderCenter);
        }

        /// <summary>
        /// Draws the top toolbar: mode selector, search field, and quick actions (titles-only, expand/collapse, mark read, refresh).
        /// </summary>
        void Toolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int newIndex = GUILayout.Toolbar((int)_mode, ModeLabels);
                if (newIndex != (int)_mode)
                {
                    _mode = (NotesPopupMode)newIndex;
                    Refresh();
                }

                GUILayout.Space(6);

                // Search field
                EditorGUI.BeginChangeCheck();
                GUI.SetNextControlName(SearchControlName);
                var newSearch = EditorGUILayout.TextField(_search, GUILayout.MinWidth(200), GUILayout.MaxWidth(320), GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck())
                {
                    _search = newSearch;
                    RebuildVisible();
                }

                // Clear search button
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(24)))
                {
                    if (!string.IsNullOrEmpty(_search))
                    {
                        _search = "";
                        GUI.FocusControl(null);
                        RebuildVisible();
                        Repaint();
                    }
                }

                GUILayout.FlexibleSpace();
            }

            // Second row: actions
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                // Titles-only toggle
                bool newTitlesOnly = GUILayout.Toggle(
                    _titlesOnly,
                    new GUIContent(Strings.T("popup.titlesOnly")),
                    EditorStyles.miniButton,
                    GUILayout.Width(100)
                );
                if (newTitlesOnly != _titlesOnly)
                {
                    _titlesOnly = newTitlesOnly;
                    Repaint();
                }

                if (GUILayout.Button(Strings.T("popup.expandAll"), EditorStyles.miniButton, GUILayout.Width(110)))
                    foreach (var n in _visible) _expanded[NoteKey(n)] = true;

                if (GUILayout.Button(Strings.T("popup.collapseAll"), EditorStyles.miniButton, GUILayout.Width(110)))
                    foreach (var n in _visible) _expanded[NoteKey(n)] = false;

                if (GUILayout.Button(Strings.T("popup.markVisibleRead"), EditorStyles.miniButton, GUILayout.Width(200)))
                    WiloUtilities.SetReadBulk(_visible, true);

                if (GUILayout.Button(Strings.T("popup.refresh"), GUILayout.Width(90)))
                    Refresh();
            }
        }

        /// <summary>
        /// Draws a single note row (header, snippet/body, references, contextual actions).
        /// Handles expand/collapse, read state, and per-row context menu.
        /// </summary>
        void DrawNoteRow(WiloNote n)
        {
            string key = NoteKey(n);
            bool isOpen = _expanded.TryGetValue(key, out var v) && v;
            bool isNew = !WiloUtilities.IsRead(n);
            bool isReRead = WiloUtilities.IsReRead(n);
            int refCount = n.refs?.Count ?? 0;

            var pad = _titlesOnly ? 6 : 8;
            _rowBg.padding = new RectOffset(pad, pad, pad, pad);

            using (new EditorGUILayout.VerticalScope(_rowBg))
            {
                // Header line
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Title: single line, trimmed, clipped, expands to fill available space
                    var title = string.IsNullOrWhiteSpace(n.title) ? Strings.T("popup.noTitle") : n.title.Trim();
                    GUILayout.Label(title, _rowTitle, GUILayout.ExpandWidth(true));

                    // Tags (re-read / new)
                    if (isReRead)
                    {
                        var old = GUI.color; GUI.color = new Color(1f, .78f, .25f, 1f);
                        GUILayout.Label(Strings.T("popup.badge.reread"), _badgeMini);
                        GUI.color = old;
                        GUILayout.Space(10);
                    }
                    if (isNew)
                    {
                        var old = GUI.color; GUI.color = new Color(1f, .4f, .4f, 1f);
                        GUILayout.Label(Strings.T("popup.badge.new"), _badgeMini);
                        GUI.color = old;
                        GUILayout.Space(10);
                    }

                    // Right fixed column: date · time
                    var (dayText, timeText) = DayAndTime(n);
                    GUILayout.Label($"{dayText} · {timeText}", _miniRight, GUILayout.Width(kDateColWidth));

                    // Reference badge (fixed width; keeps space even when 0)
                    if (refCount > 0)
                    {
                        var old = GUI.color; 
                        GUI.color = EditorGUIUtility.isProSkin ? new Color(0.5f, 0.85f, 0.5f) : new Color(0.2f, 0.6f, 0.2f);
                        GUILayout.Label($"● {refCount}", _badgeMini, GUILayout.Width(kRefBadgeWidth));
                        GUI.color = old;
                    }
                    else
                    {
                        GUILayout.Space(kRefBadgeWidth);
                    }
                }

                var firstRect = GUILayoutUtility.GetLastRect();
                float yMin = firstRect.yMin;

                // Snippet (collapsed) or full message (expanded)
                if (!isOpen && !_titlesOnly)
                {
                    float available = position.width - 32f;
                    var snippet = GetSnippet(n.message, available);
                    GUILayout.Label(snippet, _snippetStyle,
                        GUILayout.ExpandWidth(true),
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
                else if (isOpen)
                {
                    GUILayout.Label(n.message ?? "", EditorStyles.wordWrappedLabel, GUILayout.ExpandWidth(true));

                    if (refCount > 0)
                    {
                        EditorGUILayout.Space(_titlesOnly ? 4 : 6);
                        EditorGUILayout.LabelField(Strings.T("popup.references"), EditorStyles.boldLabel);

                        for (int i = 0; i < n.refs.Count; i++)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                var guid = n.refs[i];
                                var (label, _) = WiloAssetRefs.GetLabelAndIcon(guid);
                                EditorGUILayout.LabelField($"{i + 1}. {label.text}", GUILayout.ExpandWidth(true));

                                if (GUILayout.Button(Strings.T("popup.btn.ping"), GUILayout.Width(50)))
                                {
                                    var obj = WiloAssetRefs.LoadByGuid(guid);
                                    if (obj) EditorGUIUtility.PingObject(obj);
                                }
                                if (GUILayout.Button(Strings.T("popup.btn.select"), GUILayout.Width(90)))
                                {
                                    var obj = WiloAssetRefs.LoadByGuid(guid);
                                    if (obj) Selection.activeObject = obj;
                                }
                                if (GUILayout.Button(RevealButtonLabel(), GUILayout.Width(130)))
                                {
                                    var obj = WiloAssetRefs.LoadByGuid(guid);
                                    if (obj)
                                    {
                                        var path = AssetDatabase.GetAssetPath(obj);
                                        if (!string.IsNullOrEmpty(path))
                                            EditorUtility.RevealInFinder(path);
                                    }
                                }
                            }
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        var (d2, t2) = DayAndTime(n);
                        GUILayout.Label($"{d2}  •  {t2}", EditorStyles.miniLabel);
                    }
                }

                // Click to toggle expansion + context menu
                var last = GUILayoutUtility.GetLastRect();
                float safeRightPadding = 16f;
                var clickRect = new Rect(0, yMin, position.width - safeRightPadding, last.yMax - yMin);
                var e = Event.current;
                EditorGUIUtility.AddCursorRect(clickRect, MouseCursor.Link);

                if (e.type == EventType.MouseDown && clickRect.Contains(e.mousePosition) && e.button == 0)
                {
                    bool willOpen = !isOpen;
                    _expanded[key] = willOpen;
                    if (willOpen) WiloUtilities.SetRead(n, true);
                    e.Use();
                    GUI.FocusControl(null);
                }

                if (e.type == EventType.ContextClick && clickRect.Contains(e.mousePosition))
                {
                    var menu = new GenericMenu();

                    if (WiloUtilities.IsRead(n))
                        menu.AddItem(new GUIContent(Strings.T("popup.ctx.markUnread")), false, () => { WiloUtilities.SetRead(n, false); Repaint(); });
                    else
                        menu.AddItem(new GUIContent(Strings.T("popup.ctx.markRead")), false, () => { WiloUtilities.SetRead(n, true); Repaint(); });

                    if (WiloUtilities.IsReRead(n))
                        menu.AddItem(new GUIContent(Strings.T("popup.ctx.unmarkReRead")), false, () => { WiloUtilities.SetReRead(n, false); Repaint(); });
                    else
                        menu.AddItem(new GUIContent(Strings.T("popup.ctx.markReRead")), false, () => { WiloUtilities.SetReRead(n, true); Repaint(); });

                    menu.ShowAsContext();
                    e.Use();
                }
            }
        }

        /// <summary>
        /// Returns the platform-appropriate label for the “reveal in file browser” action.
        /// </summary>
        static string RevealButtonLabel()
        {
#if UNITY_EDITOR_OSX
            return Strings.T("popup.btn.reveal.macos");
#else
            return Strings.T("popup.btn.reveal.win");
#endif
        }

        /// <summary>Draws the bottom bar with a close button.</summary>
        void BottomBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(Strings.T("popup.btn.close"), GUILayout.Width(80))) Close();
            }
        }

        /// <summary>
        /// Builds a single-line snippet from the first line of text, trimmed and width-constrained.
        /// </summary>
        static string GetSnippet(string s, float availableWidthPx, int maxFallbackChars = 160)
        {
            if (string.IsNullOrEmpty(s)) return "—";
            int nl = s.IndexOf('\n');
            if (nl >= 0) s = s.Substring(0, nl);
            s = s.Replace("\r", "").Trim();
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            if (availableWidthPx <= 0f)
                availableWidthPx = EditorGUIUtility.currentViewWidth > 0 ? EditorGUIUtility.currentViewWidth : 800f;
            float budgetPx = Mathf.Max(180f, availableWidthPx * 0.5f);
            int maxByWidth = Mathf.Max(24, Mathf.FloorToInt(budgetPx / 8f));
            int limit = Mathf.Min(maxByWidth, maxFallbackChars);
            return s.Length <= limit ? s : s.Substring(0, Mathf.Max(0, limit - 1)) + "…";
        }

        /// <summary>Draws a thin horizontal separator line.</summary>
        void DrawSeparator(float h = 1f)
        {
            var r = GUILayoutUtility.GetRect(1, h, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                var c = EditorGUIUtility.isProSkin ? new Color(1,1,1,0.08f) : new Color(0,0,0,0.08f);
                EditorGUI.DrawRect(r, c);
            }
        }

        /// <summary>Converts a raw day string into a human-friendly label, or returns it as-is.</summary>
        static string HumanDay(string dayRaw)
        {
            if (DateTime.TryParse(dayRaw, out var d))
                return d.ToString("dd MMM yyyy");
            return dayRaw ?? "—";
        }

        /// <summary>Returns localized day and time strings for a note.</summary>
        static (string day, string time) DayAndTime(WiloNote n)
        {
            string day = HumanDay(n.day ?? "—");
            string time = "—:—";
            if (!string.IsNullOrEmpty(n.utc) && DateTimeOffset.TryParse(n.utc, out var dto))
                time = dto.ToLocalTime().ToString("HH:mm");
            return (day, time);
        }

        /// <summary>
        /// Case-insensitive “contains” matching against title, message and resolved ref labels.
        /// </summary>
        static bool NoteMatchesQuery(WiloNote n, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            var q = query.Trim();
            if (q.Length == 0) return true;

            bool Match(string s) => !string.IsNullOrEmpty(s) &&
                                    s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

            if (Match(n.title) || Match(n.message)) return true;

            if (n.refs != null)
            {
                for (int i = 0; i < n.refs.Count; i++)
                {
                    var guid = n.refs[i];
                    if (Match(guid)) return true;
                    var (label, _) = WiloAssetRefs.GetLabelAndIcon(guid);
                    if (Match(label.text)) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Rebuilds the visible list from the current filtered set and search query.
        /// Also ensures expansion state is initialized for all visible notes.
        /// </summary>
        void RebuildVisible()
        {
            _visible = (_filtered ?? new List<WiloNote>())
                .Where(n => NoteMatchesQuery(n, _search))
                .OrderByDescending(n => n.utc)
                .ToList();

            foreach (var n in _visible)
            {
                var k = NoteKey(n);
                if (!_expanded.ContainsKey(k)) _expanded[k] = false;
            }
        }

        /// <summary>
        /// Computes a conservative minimum width required to keep toolbar layout stable across two rows.
        /// </summary>
        float CalcToolbarMinWidth()
        {
            var style = GUI.skin.button;
            float buttons = 0f;
            foreach (var s in ModeLabels)
                buttons += style.CalcSize(new GUIContent(s)).x + 12f;

            const float spaceBetween = 6f;
            const float searchMin    = 200f;
            const float clearBtn     = 24f;
            const float edges        = 40f;

            // Include the second row width (with “Titles only” and actions)
            float line1 = buttons + spaceBetween + searchMin + clearBtn + edges;

            float line2 = style.CalcSize(new GUIContent(Strings.T("popup.titlesOnly"))).x + 12f
                + 6f + style.CalcSize(new GUIContent(Strings.T("popup.expandAll"))).x + 12f
                + 6f + style.CalcSize(new GUIContent(Strings.T("popup.collapseAll"))).x + 12f
                + 6f + style.CalcSize(new GUIContent(Strings.T("popup.markVisibleRead"))).x + 12f
                + 6f + style.CalcSize(new GUIContent(Strings.T("popup.refresh"))).x + 12f
                + edges;

            return Mathf.Ceil(Mathf.Max(line1, line2));
        }
    }
}
