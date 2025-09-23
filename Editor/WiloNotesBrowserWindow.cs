// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// In-Editor browser window for WILO notes. Provides search, grouping, sorting,
    /// paging, quick status toggles (read / re-read), edit/duplicate/delete, and
    /// asset reference utilities (ping, select, reveal in Finder/Explorer).
    /// </summary>
    public class WiloNotesBrowserWindow : EditorWindow
    {
        /// <summary>Window title.</summary>
        const string WindowTitle = "Where I Left Off — Notes Browser";
        /// <summary>Prefix for EditorPrefs keys.</summary>
        const string PrefPrefix  = "Wilo/Browser/";

        /// <summary>
        /// Opens the notes browser window from the Unity menu.
        /// </summary>
        [MenuItem("Tools/DevForge/Where I Left Off/Browser", priority = 2002)]
        public static void Open()
        {
            var w = CreateInstance<WiloNotesBrowserWindow>();
            w.titleContent = new GUIContent(WindowTitle);
            w.minSize = new Vector2(560, 400);
            w.Refresh();
            w.Show();
            w.Focus();
        }

        // ===== State =====
        /// <summary>Main scroll position.</summary>
        Vector2 _scroll;
        /// <summary>Free-form search query.</summary>
        [SerializeField] string _search = "";
        /// <summary>When true, shows only titles (no message/snippet).</summary>
        [SerializeField] bool _titlesOnly = false; // "Titles only" mode
        
        string[] GroupOptions  => new[] { Strings.T("browser.group.none"), Strings.T("browser.group.day"), Strings.T("browser.group.session") };
        string[] SortOptions   => new[] { Strings.T("browser.sort.dateDesc"), Strings.T("browser.sort.dateAsc"), Strings.T("browser.sort.titleAsc"), Strings.T("browser.sort.titleDesc") };
        string[] StateOptions  => new[] { Strings.T("browser.filter.all"), Strings.T("browser.filter.unread"), Strings.T("browser.filter.reread") };

        /// <summary>Computed minimum width required by the toolbar.</summary>
        float _toolbarMinWidth;
        /// <summary>Lock to avoid recomputing minimum size multiple times.</summary>
        bool  _minSizeLocked;
        /// <summary>Control name for the search TextField (to receive focus programmatically).</summary>
        const string SearchControlName = "WILO_BROWSER_SEARCH";
        /// <summary>Request focus on the search field next repaint.</summary>
        bool _wantFocusSearch;               // request focus to the search field
        /// <summary>Request blur of the active control next repaint (e.g., after ESC).</summary>
        bool _wantBlur;                      // remove focus from active control

        // data
        /// <summary>All notes loaded from storage.</summary>
        List<WiloNote> _all = new();
        /// <summary>Visible notes after filtering/sorting/paging.</summary>
        List<WiloNote> _visible = new();

        // per-note expansion state (key = utc)
        /// <summary>Expansion state map per note, indexed by <c>utc</c> (or fallback id).</summary>
        readonly Dictionary<string, bool> _expanded = new();

        // per-note editing state
        /// <summary>Editing state map per note (key = <c>utc</c>).</summary>
        readonly Dictionary<string, bool> _editing = new();
        /// <summary>Draft ScriptableObject per editing note (key = <c>utc</c>).</summary>
        readonly Dictionary<string, WiloDraftNoteSO> _editSO = new();
        /// <summary>Cache of labels and loaded objects for reference GUIDs.</summary>
        readonly Dictionary<string, (GUIContent label, UnityEngine.Object obj)> _refCache =
            new Dictionary<string, (GUIContent, UnityEngine.Object)>();

        // human-friendly per-day session index
        /// <summary>Readable session index per session id: maps to (day, sequential index).</summary>
        readonly Dictionary<string, (string day, int idx)> _sessionIndex = new();

        // styles
        /// <summary>Cached GUI styles used by the window.</summary>
        static GUIStyle _h1, _wrap, _rowTitle, _rowBg, _snippetStyle, _badgeMini, _miniRight;
        
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
        
        // right fixed column (date·time) and reserved width for ref badge
        /// <summary>Fixed width for date/time right column.</summary>
        const float kDateColWidth = 170f;
        /// <summary>Reserved width for the reference count badge.</summary>
        const float kRefBadgeWidth = 34f;

        /// <summary>Grouping modes available for the list.</summary>
        enum GroupMode { None, Day, Session }
        /// <summary>Sorting modes.</summary>
        enum SortMode { DateDesc, DateAsc, TitleAsc, TitleDesc }
        /// <summary>Read status filter.</summary>
        enum StateFilter { All, Unread, ReRead }

        /// <summary>Current sorting mode.</summary>
        [SerializeField] private SortMode _sortMode = SortMode.DateDesc;
        /// <summary>Current read-status filter.</summary>
        [SerializeField] private StateFilter _stateFilter = StateFilter.All;
        /// <summary>Current grouping mode.</summary>
        [SerializeField] private GroupMode _groupMode = GroupMode.None;

        // paging
        /// <summary>Page size (number of rows per page).</summary>
        const int PageSize = 100;
        /// <summary>Current page index (0-based).</summary>
        int _page = 0;
        /// <summary>Total number of pages.</summary>
        int _pages = 1;

        /// <summary>
        /// Initializes styles and preferences when the window is enabled.
        /// </summary>
        void OnEnable()
        {
            _h1 = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true, padding = new RectOffset(4,4,8,8) };
            _wrap = new GUIStyle(EditorStyles.label) { wordWrap = true };
            _rowTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, wordWrap = false, clipping = TextClipping.Clip };
            _rowBg = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(8,8,8,8) };
            _snippetStyle = new GUIStyle(EditorStyles.label) { wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.UpperLeft };
            _badgeMini = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            _miniRight = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight }; // NEW

            LoadPrefs();
            Refresh();
        }

        /// <summary>
        /// Saves preferences when the window is disabled.
        /// </summary>
        void OnDisable() => SavePrefs();

        // ===== Data =====

        /// <summary>
        /// Reloads notes from storage, rebuilds indices and visible list, then repaints.
        /// </summary>
        void Refresh()
        {
            var file = WiloNotesStorage.LoadNotes();
            _all = file?.notes != null ? new List<WiloNote>(file.notes) : new List<WiloNote>();
            _all = _all.OrderByDescending(n => n.utc).ToList();

            foreach (var n in _all)
            {
                if (string.IsNullOrEmpty(n.utc)) continue;
                if (!_expanded.ContainsKey(n.utc)) _expanded[n.utc] = false;
            }

            BuildSessionIndex();
            RebuildVisible();
            Repaint();
        }

        /// <summary>
        /// Builds a per-day session index, enumerating sessions by first timestamp within each day.
        /// </summary>
        void BuildSessionIndex()
        {
            _sessionIndex.Clear();
            foreach (var dayGroup in _all.GroupBy(n => n.day ?? "—"))
            {
                var sessions = dayGroup
                    .GroupBy(n => n.sessionId ?? "—")
                    .Select(g => new {
                        sessionId = g.Key,
                        day = dayGroup.Key,
                        firstTime = g
                            .Where(n => !string.IsNullOrEmpty(n.utc) && DateTimeOffset.TryParse(n.utc, out _))
                            .Select(n => DateTimeOffset.Parse(n.utc)).DefaultIfEmpty(DateTimeOffset.MinValue).Min()
                    })
                    .OrderBy(s => s.firstTime)
                    .ToList();

                for (int i = 0; i < sessions.Count; i++)
                {
                    var s = sessions[i];
                    _sessionIndex[s.sessionId] = (s.day, i + 1);
                }
            }
        }

        /// <summary>
        /// Recomputes the visible list applying read-state filter, search, sorting and paging.
        /// </summary>
        void RebuildVisible()
        {
            IEnumerable<WiloNote> q = _all;

            // state filter
            if (_stateFilter == StateFilter.Unread) q = q.Where(n => !WiloUtilities.IsRead(n));
            if (_stateFilter == StateFilter.ReRead) q = q.Where(n => WiloUtilities.IsReRead(n));

            // search
            if (!string.IsNullOrWhiteSpace(_search))
            {
                var s = _search.Trim();
                bool Match(string t) => !string.IsNullOrEmpty(t) && t.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
                q = q.Where(n =>
                {
                    if (Match(n.title) || Match(n.message)) return true;
                    if (n.refs != null)
                    {
                        foreach (var g in n.refs)
                        {
                            if (Match(g)) return true;
                            var (label, _) = ResolveRef(g);
                            if (Match(label.text)) return true;
                        }
                    }
                    return false;
                });
            }

            // sort
            q = _sortMode switch
            {
                SortMode.DateAsc   => q.OrderBy(n => n.utc),
                SortMode.TitleAsc  => q.OrderBy(n => n.title, StringComparer.OrdinalIgnoreCase).ThenByDescending(n => n.utc),
                SortMode.TitleDesc => q.OrderByDescending(n => n.title, StringComparer.OrdinalIgnoreCase).ThenByDescending(n => n.utc),
                _                  => q.OrderByDescending(n => n.utc),
            };

            _visible = q.ToList();

            _pages = Mathf.Max(1, Mathf.CeilToInt(_visible.Count / (float)PageSize));
            _page = Mathf.Clamp(_page, 0, _pages - 1);
        }

        // ===== UI =====

        /// <summary>
        /// Main GUI loop. Handles shortcuts, toolbar, list, and bottom bar.
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

                // apply focus/blur requests at a stable time (during Repaint)
                if (_wantBlur && Event.current.type == EventType.Repaint)
                {
                    GUI.FocusControl(null);
                    _wantBlur = false;
                }

                if (_wantFocusSearch && Event.current.type == EventType.Repaint)
                {
                    // Also force focus to the window, just in case
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
                    _scroll = EditorGUILayout.BeginScrollView(_scroll, false, false, GUIStyle.none,
                        GUI.skin.verticalScrollbar, GUIStyle.none);

                    GUILayout.Space(8);
                    if (_visible.Count == 0)
                    {
                        var r = GUILayoutUtility.GetRect(1, 160, GUILayout.ExpandWidth(true));
                        using (new GUI.GroupScope(r))
                        {
                        }
                    }
                    else
                    {
                        RenderList();
                    }

                    EditorGUILayout.EndScrollView();
                }

                BottomBar();
            }

            HandleWindowContextMenu();
        }


        private void DrawHeader()
        {
            string header = Strings.T("browser.title");

            // calculate required height for current width
            float w = EditorGUIUtility.currentViewWidth;
            float h = Styles.HeaderCenter.CalcHeight(new GUIContent(header), w);

            var r = GUILayoutUtility.GetRect(0, h, GUILayout.ExpandWidth(true));
            GUI.Label(r, header, Styles.HeaderCenter);
        }

        // --- keyboard shortcuts

        /// <summary>
        /// Handles keyboard shortcuts: Ctrl/Cmd+F focuses search, ESC clears it.
        /// </summary>
        void HandleShortcuts(Event e)
        {
            if (e == null || e.type != EventType.KeyDown) return;

            var mods = e.modifiers;

            // Ctrl/Cmd + F => focus search (even if another TextField is active)
            if ((mods & (EventModifiers.Control | EventModifiers.Command)) != 0 && e.keyCode == KeyCode.F)
            {
                _wantFocusSearch = true;     // will apply during Repaint
                e.Use();
                Repaint();
                return;
            }

            // ESC => clear search (if any) and blur active control
            if (e.keyCode == KeyCode.Escape)
            {
                if (!string.IsNullOrEmpty(_search))
                {
                    _search = "";
                    _page = 0;
                    RebuildVisible();
                    _wantBlur = true;        // remove focus from active control
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

        /// <summary>
        /// Draws the top toolbar (group, sort, filter, search, and quick actions).
        /// </summary>
        void Toolbar()
        {
            // ---- Row 1 ----
            using (new EditorGUILayout.HorizontalScope())
            {
                var newGroup = (GroupMode)EditorGUILayout.Popup((int)_groupMode, GroupOptions, GUILayout.Width(180));
                if (newGroup != _groupMode) { _groupMode = newGroup; _page = 0; RebuildVisible(); SavePrefs(); }

                var newSort  = (SortMode)EditorGUILayout.Popup((int)_sortMode,  SortOptions,  GUILayout.Width(120));
                if (newSort != _sortMode) { _sortMode = newSort; _page = 0; RebuildVisible(); SavePrefs(); }

                var newFilter= (StateFilter)EditorGUILayout.Popup((int)_stateFilter, StateOptions, GUILayout.Width(110));
                if (newFilter != _stateFilter) { _stateFilter = newFilter; _page = 0; RebuildVisible(); SavePrefs(); }

                GUILayout.Space(6);

                // Search
                EditorGUI.BeginChangeCheck();
                GUI.SetNextControlName(SearchControlName);
                _search = EditorGUILayout.TextField(
                    _search,
                    GUILayout.MinWidth(200),
                    GUILayout.MaxWidth(320),
                    GUILayout.ExpandWidth(true)
                );
                if (EditorGUI.EndChangeCheck()) { _page = 0; RebuildVisible(); SavePrefs(); }

                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(24)))
                {
                    if (!string.IsNullOrEmpty(_search))
                    {
                        _search = "";
                        _page = 0;
                        RebuildVisible();
                        _wantBlur = true;
                        SavePrefs();
                        Repaint();
                    }
                }
            }

            // ---- Row 2 ----
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                // Titles-only mode
                bool newTitlesOnly = GUILayout.Toggle(_titlesOnly,
                    new GUIContent(Strings.T("browser.titlesOnly")), EditorStyles.miniButton, GUILayout.Width(100));
                if (newTitlesOnly != _titlesOnly)
                {
                    _titlesOnly = newTitlesOnly;
                    SavePrefs();
                    Repaint();
                }

                if (GUILayout.Button(Strings.T("browser.expandVisible"), EditorStyles.miniButton, GUILayout.Width(130)))
                    ForEachVisible(n => _expanded[n.utc] = true);

                if (GUILayout.Button(Strings.T("browser.collapseVisible"), EditorStyles.miniButton, GUILayout.Width(130)))
                    ForEachVisible(n => _expanded[n.utc] = false);

                if (GUILayout.Button(Strings.T("browser.markVisibleRead"), EditorStyles.miniButton, GUILayout.Width(200)))
                    WiloUtilities.SetReadBulk(CurrentPageNotes(), true);

                if (GUILayout.Button(Strings.T("browser.refresh"), GUILayout.Width(90)))
                    Refresh();
            }

            GUILayout.Space(4);
        }

        /// <summary>
        /// Draws the bottom bar with pagination and count info.
        /// </summary>
        void BottomBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int total = _visible.Count;
                if (_pages > 1)
                {
                    GUILayout.Label(string.Format(Strings.T("browser.pageXofY"), _page + 1, _pages, total), EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();

                    GUI.enabled = _page > 0;
                    if (GUILayout.Button(Strings.T("browser.prev"), GUILayout.Width(100))) { _page = Mathf.Max(0, _page - 1); }
                    GUI.enabled = _page < _pages - 1;
                    if (GUILayout.Button(Strings.T("browser.next"), GUILayout.Width(100))) { _page = Mathf.Min(_pages - 1, _page + 1); }
                    GUI.enabled = true;
                }
                else
                {
                    GUILayout.Label(string.Format(Strings.T("browser.totalNotes"), total), EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                }

                if (GUILayout.Button(Strings.T("browser.close"), GUILayout.Width(90))) 
                    Close();
            }
        }

        /// <summary>
        /// Renders the current notes list, with or without grouping.
        /// </summary>
        void RenderList()
        {
            if (_groupMode == GroupMode.None)
            {
                foreach (var n in CurrentPageNotes())
                {
                    DrawNoteRow(n);
                    DrawSeparator();
                }
                return;
            }

            IEnumerable<IGrouping<string, WiloNote>> groups =
                _groupMode == GroupMode.Day
                    ? CurrentPageNotes().GroupBy(n => n.day ?? "—")
                    : CurrentPageNotes().GroupBy(n => n.sessionId ?? "—");

            foreach (var g in groups)
            {
                bool fold = SessionState.GetBool(GroupKey(g.Key), true);
                fold = EditorGUILayout.BeginFoldoutHeaderGroup(fold, GroupHeader(g));
                SessionState.SetBool(GroupKey(g.Key), fold);

                if (fold)
                {
                    foreach (var n in g)
                    {
                        DrawNoteRow(n);
                        DrawSeparator();
                    }
                }

                EditorGUILayout.EndFoldoutHeaderGroup();
                GUILayout.Space(4);
            }
        }

        /// <summary>
        /// Builds a unique key for a foldout group.
        /// </summary>
        string GroupKey(string id) => $"WILO_BROWSER_GROUP_{_groupMode}_{id}";

        /// <summary>
        /// Creates the header text for a group, including time range and counters.
        /// </summary>
        string GroupHeader(IGrouping<string, WiloNote> g)
        {
            DateTimeOffset? min = null, max = null;
            int refs = 0;
            foreach (var n in g)
            {
                if (!string.IsNullOrEmpty(n.utc) && DateTimeOffset.TryParse(n.utc, out var dto))
                {
                    if (min == null || dto < min) min = dto;
                    if (max == null || dto > max) max = dto;
                }
                refs += n.refs?.Count ?? 0;
            }
            string range = (min != null && max != null) ? $"{min.Value.ToLocalTime():HH:mm} ➝ {max.Value.ToLocalTime():HH:mm}" : "—";

            if (_groupMode == GroupMode.Day)
            {
                string day = HumanDay(g.Key);
                return string.Format(Strings.T("browser.group.header.day"), day, range, g.Count(), refs);
            }
            else
            {
                var first = g.FirstOrDefault();
                string sid = first?.sessionId ?? "—";
                string day = first?.day ?? "—";
                int idx = 0;
                if (!string.IsNullOrEmpty(sid) && _sessionIndex.TryGetValue(sid, out var info))
                {
                    day = info.day ?? day; idx = info.idx;
                }
                string dayPretty = HumanDay(day);
                return string.Format(Strings.T("browser.group.header.session"), dayPretty, idx, range, g.Count(), refs);
            }
        }

        // ===== Row =====

        /// <summary>
        /// Draws a single note row (header, snippet or body, actions, context menu).
        /// Handles expand/collapse, edit mode, references, and status tags.
        /// </summary>
        void DrawNoteRow(WiloNote n)
        {
            string id = n.utc ?? n.title ?? Guid.NewGuid().ToString();
            bool isOpen = _expanded.TryGetValue(id, out var v) && v;
            bool inEdit = _editing.TryGetValue(id, out var ed) && ed;

            var pad = _titlesOnly ? 6 : 8;
            _rowBg.padding = new RectOffset(pad, pad, pad, pad);

            using (new EditorGUILayout.VerticalScope(_rowBg))
            {
                // Header (title + badges + date/time + refs)
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Title: single line, clipped, expands to available width
                    GUILayout.Label(n.title ?? Strings.T("popup.noTitle"), _rowTitle, GUILayout.ExpandWidth(true));

                    // Tags
                    if (WiloUtilities.IsReRead(n))
                    {
                        var old = GUI.color; GUI.color = new Color(1f, .78f, .25f, 1f);
                        GUILayout.Label(Strings.T("popup.badge.reread"), _badgeMini);
                        GUI.color = old;
                        GUILayout.Space(10);
                    }
                    if (!WiloUtilities.IsRead(n))
                    {
                        var old = GUI.color; GUI.color = new Color(1f, .4f, .4f, 1f);
                        GUILayout.Label(Strings.T("popup.badge.new"), _badgeMini);
                        GUI.color = old;
                        GUILayout.Space(10);
                    }

                    // Date · Time (fixed right column)
                    var (dayText, timeText) = DayAndTime(n);
                    GUILayout.Label($"{dayText} · {timeText}", _miniRight, GUILayout.Width(kDateColWidth));

                    // Refs badge (fixed column; reserve space when none)
                    int refCount = n.refs?.Count ?? 0;
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

                // Snippet if NOT titles-only and collapsed
                if (!isOpen && !_titlesOnly)
                {
                    float available = position.width - 32f;
                    var snippet = GetSnippet(n.message, available);
                    GUILayout.Label(snippet, _snippetStyle, GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
                else if (isOpen)
                {
                    if (!WiloUtilities.IsRead(n)) WiloUtilities.SetRead(n, true);

                    if (!inEdit)
                    {
                        if (!string.IsNullOrEmpty(n.message))
                        {
                            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                            {
                                GUILayout.Label(n.message, _wrap, GUILayout.ExpandWidth(true));
                            }
                        }

                        int rc = n.refs?.Count ?? 0;
                        if (rc > 0) GUILayout.Space(_titlesOnly ? 4 : 6);
                        if (rc > 0) EditorGUILayout.LabelField(Strings.T("popup.references"), EditorStyles.boldLabel);

                        if (rc > 0)
                        {
                            for (int i = 0; i < n.refs.Count; i++)
                            {
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    var guid = n.refs[i];
                                    var (label, obj) = ResolveRef(guid);
                                    EditorGUILayout.LabelField($"{i + 1}. {label.text}", GUILayout.ExpandWidth(true));

                                    if (GUILayout.Button(Strings.T("popup.btn.ping"), GUILayout.Width(50)) && obj) EditorGUIUtility.PingObject(obj);
                                    if (GUILayout.Button(Strings.T("popup.btn.select"), GUILayout.Width(90)) && obj) Selection.activeObject = obj;
                                    if (GUILayout.Button(RevealButtonLabel(), GUILayout.Width(130)) && obj)
                                    {
                                        var path = AssetDatabase.GetAssetPath(obj);
                                        if (!string.IsNullOrEmpty(path)) EditorUtility.RevealInFinder(path);
                                    }
                                }
                            }
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(Strings.T("browser.btn.edit"), GUILayout.Width(80)))
                                BeginEdit(n);

                            if (GUILayout.Button(Strings.T("browser.btn.duplicate"), GUILayout.Width(100)))
                                DuplicateNote(n);

                            if (GUILayout.Button(Strings.T("browser.btn.delete"), GUILayout.Width(90)))
                                DeleteNote(n);

                            GUILayout.FlexibleSpace();
                            var (dayText2, timeText2) = DayAndTime(n);
                            GUILayout.Label($"{dayText2}  •  {timeText2}", EditorStyles.miniLabel);
                        }
                    }
                    else
                    {
                        var so = _editSO[n.utc];
                        if (!so) { CancelEdit(n); return; }

                        var sobj = new SerializedObject(so);

                        // Title
                        sobj.Update();
                        var pTitle = sobj.FindProperty("title");
                        EditorGUI.BeginChangeCheck();
                        Undo.RecordObject(so, "Editar título (Browser)");
                        EditorGUILayout.PropertyField(pTitle, new GUIContent(Strings.T("lbl.title")));
                        if (EditorGUI.EndChangeCheck()) sobj.ApplyModifiedProperties();

                        // Message
                        var pMsg = sobj.FindProperty("message");
                        EditorGUILayout.LabelField(Strings.T("lbl.message"), EditorStyles.boldLabel);
                        EditorGUI.BeginChangeCheck();
                        Undo.RecordObject(so, "Editar mensaje (Browser)");
                        pMsg.stringValue = EditorGUILayout.TextArea(
                            pMsg.stringValue,
                            GUILayout.MinHeight(_titlesOnly ? 60 : 100),
                            GUILayout.ExpandWidth(true)
                        );
                        if (EditorGUI.EndChangeCheck()) sobj.ApplyModifiedProperties();

                        // References
                        int rc = so.refGuids?.Count ?? 0;
                        if (rc > 0) GUILayout.Space(_titlesOnly ? 4 : 6);
                        if (rc > 0) EditorGUILayout.LabelField(Strings.T("popup.references"), EditorStyles.boldLabel);

                        if (rc > 0)
                        {
                            for (int i = 0; i < so.refGuids.Count; i++)
                            {
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    var guid = so.refGuids[i];
                                    var (label, obj) = ResolveRef(guid);
                                    EditorGUILayout.LabelField($"{i + 1}. {label.text}", GUILayout.ExpandWidth(true));

                                    if (GUILayout.Button(Strings.T("popup.btn.ping"), GUILayout.Width(50)) && obj) EditorGUIUtility.PingObject(obj);
                                    if (GUILayout.Button(Strings.T("popup.btn.select"), GUILayout.Width(90)) && obj) Selection.activeObject = obj;
                                    if (GUILayout.Button(RevealButtonLabel(), GUILayout.Width(130)) && obj)
                                    {
                                        var path = AssetDatabase.GetAssetPath(obj);
                                        if (!string.IsNullOrEmpty(path)) EditorUtility.RevealInFinder(path);
                                    }

                                    if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(22)))
                                    {
                                        Undo.RecordObject(so, "Quitar referencia (Browser)");
                                        so.refGuids.RemoveAt(i);
                                        sobj.ApplyModifiedProperties();
                                        Repaint();
                                        break;
                                    }
                                }
                            }
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("+ " + Strings.T("btn.addSelection"), GUILayout.Width(160)))
                            {
                                Undo.RecordObject(so, "Añadir referencias (Browser)");
                                var set = new HashSet<string>(so.refGuids ?? new List<string>());
                                int added = 0;
                                foreach (var g in WiloAssetRefs.GetGuidsFromCurrentSelection())
                                {
                                    if (set.Add(g)) added++;
                                }
                                so.refGuids = set.ToList();
                                sobj.ApplyModifiedProperties();
                                if (added > 0) Repaint();
                            }
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(Strings.T("btn.save"), GUILayout.Width(100))) 
                                SaveEdit(n);

                            if (GUILayout.Button(Strings.T("btn.cancel"), GUILayout.Width(100)))
                                CancelEdit(n);

                            GUILayout.FlexibleSpace();
                            var (dayText3, timeText3) = DayAndTime(n);
                            GUILayout.Label($"{dayText3}  •  {timeText3}", EditorStyles.miniLabel);
                        }
                    }
                }

                // Click to expand/collapse the whole row area
                var last = GUILayoutUtility.GetLastRect();
                var clickRect = new Rect(0, yMin, position.width - 16f, last.yMax - yMin);
                var e = Event.current;
                EditorGUIUtility.AddCursorRect(clickRect, MouseCursor.Link);

                if (e.type == EventType.MouseDown && clickRect.Contains(e.mousePosition) && e.button == 0)
                {
                    _expanded[id] = !isOpen;
                    e.Use();
                    GUI.FocusControl(null);
                }

                // Context menu
                if (e.type == EventType.ContextClick && clickRect.Contains(e.mousePosition))
                {
                    var menu = new GenericMenu();
                    if (WiloUtilities.IsRead(n))
                        menu.AddItem(new GUIContent(Strings.T("popup.ctx.markUnread")), false, () =>
                        {
                            WiloUtilities.SetRead(n, false);
                            Repaint();
                        });
                    else
                        menu.AddItem(new GUIContent(Strings.T("popup.ctx.markRead")), false, () =>
                        {
                            WiloUtilities.SetRead(n, true);
                            Repaint();
                        });

                    if (WiloUtilities.IsReRead(n))
                        menu.AddItem(new GUIContent(Strings.T("popup.ctx.unmarkReRead")), false, () =>
                        {
                            WiloUtilities.SetReRead(n, false);
                            Repaint();
                        });
                    else
                        menu.AddItem(new GUIContent(Strings.T("popup.ctx.markReRead")), false, () =>
                        {
                            WiloUtilities.SetReRead(n, true);
                            Repaint();
                        });

                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent(Strings.T("browser.btn.duplicate")), false, () => { DuplicateNote(n); });
                    menu.AddItem(new GUIContent(Strings.T("browser.btn.delete")), false, () => { DeleteNote(n); });
                    menu.ShowAsContext();
                    e.Use();
                }
            }
        }

        // ===== Actions =====

        /// <summary>Creates a duplicate of the given note and inserts it at the top.</summary>
        void DuplicateNote(WiloNote n)
        {
            if (n == null) return;
            var file = WiloNotesStorage.LoadNotes();
            if (file?.notes == null) return;

            var copy = new WiloNote
            {
                title = n.title,
                message = n.message,
                day = DateTime.Now.ToString("yyyy-MM-dd"),
                utc = DateTime.UtcNow.ToString("o"),
                sessionId = WiloUtilities.GetOrCreateSessionId(),
                refs = n.refs != null ? new List<string>(n.refs) : new List<string>()
            };

            file.notes.Insert(0, copy);
            WiloNotesStorage.SaveNotes(file);
            Refresh();
        }

        /// <summary>Deletes the given note after confirmation.</summary>
        void DeleteNote(WiloNote n)
        {
            if (n == null) return;
            if (!EditorUtility.DisplayDialog(
                    Strings.T("browser.confirm.delete.title"),
                    Strings.T("browser.confirm.delete.msg"),
                    Strings.T("browser.confirm.delete.ok"),
                    Strings.T("browser.confirm.delete.cancel")))
                return;


            var file = WiloNotesStorage.LoadNotes();
            if (file?.notes == null) return;

            int idx = file.notes.FindIndex(x => x.utc == n.utc);
            if (idx >= 0)
            {
                file.notes.RemoveAt(idx);
                WiloNotesStorage.SaveNotes(file);
            }
            Refresh();
        }

        /// <summary>Begins edit mode for the given note, creating a temporary draft SO.</summary>
        void BeginEdit(WiloNote n)
        {
            if (n == null || string.IsNullOrEmpty(n.utc)) return;
            if (_editSO.TryGetValue(n.utc, out var so) && so != null)
            {
                _editing[n.utc] = true; return;
            }

            var draft = ScriptableObject.CreateInstance<WiloDraftNoteSO>();
            draft.hideFlags = HideFlags.DontSave;
            draft.title = n.title ?? "";
            draft.message = n.message ?? "";
            draft.refGuids = (n.refs != null) ? new List<string>(n.refs) : new List<string>();
            _editSO[n.utc] = draft;
            _editing[n.utc] = true;
        }

        /// <summary>Cancels edit mode for the given note and disposes its temporary draft.</summary>
        void CancelEdit(WiloNote n)
        {
            if (n == null || string.IsNullOrEmpty(n.utc)) return;
            if (_editSO.TryGetValue(n.utc, out var so) && so != null) DestroyImmediate(so);
            _editSO.Remove(n.utc);
            _editing[n.utc] = false;
            Repaint();
        }

        /// <summary>Saves edits from the temporary draft back into the note and persists to disk.</summary>
        void SaveEdit(WiloNote n)
        {
            if (n == null || string.IsNullOrEmpty(n.utc)) return;
            if (!_editSO.TryGetValue(n.utc, out var so) || so == null) return;

            var file = WiloNotesStorage.LoadNotes();
            if (file?.notes == null) return;

            int idx = file.notes.FindIndex(x => x.utc == n.utc);
            if (idx < 0) return;

            n.title = so.title ?? "";
            n.message = so.message ?? "";
            n.refs = (so.refGuids != null) ? new List<string>(so.refGuids) : new List<string>();
            file.notes[idx] = n;

            WiloNotesStorage.SaveNotes(file);

            CancelEdit(n);
            Refresh();
        }

        // ===== Utils =====

        /// <summary>Returns the notes for the current page (or all if under <see cref="PageSize"/>).</summary>
        IEnumerable<WiloNote> CurrentPageNotes()
        {
            if (_visible.Count <= PageSize) return _visible;
            int start = _page * PageSize;
            int count = Mathf.Min(PageSize, _visible.Count - start);
            return _visible.GetRange(start, count);
        }

        /// <summary>Applies an action to each visible note in the current page and repaints.</summary>
        void ForEachVisible(Action<WiloNote> act)
        {
            foreach (var n in CurrentPageNotes()) act(n);
            Repaint();
        }

        /// <summary>
        /// Builds a one-line snippet from the first line of text, trimmed and width-constrained.
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

        /// <summary>Resolves a reference GUID to a label (name + icon) and the loaded Unity object.</summary>
        (GUIContent, UnityEngine.Object) ResolveRef(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return (new GUIContent("(vacío)"), null);
            if (_refCache.TryGetValue(guid, out var cached)) return cached;

            var c = WiloAssetRefs.GetLabelAndIcon(guid); // (label, icon)
            var obj = WiloAssetRefs.LoadByGuid(guid);
            var res = (c.Item1, obj);
            _refCache[guid] = res;
            return res;
        }

        /// <summary>Draws a thin horizontal separator.</summary>
        void DrawSeparator(float h = 1f)
        {
            var r = GUILayoutUtility.GetRect(1, h, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                var c = EditorGUIUtility.isProSkin ? new Color(1,1,1,0.08f) : new Color(0,0,0,0.08f);
                EditorGUI.DrawRect(r, c);
            }
        }

        /// <summary>Returns the platform-appropriate label for the "reveal in file browser" button.</summary>
        static string RevealButtonLabel()
        {
#if UNITY_EDITOR_OSX
    return Strings.T("popup.btn.reveal.macos");
#else
            return Strings.T("popup.btn.reveal.win");
#endif
        }

        /// <summary>Calculates the minimum width needed to avoid breaking the toolbar layout.</summary>
        float CalcToolbarMinWidth()
        {
            float left = 180f + 6f + 120f + 6f + 110f + 6f;
            float searchMin = 200f + 24f;
            float edges = 40f;
            float line1 = left + searchMin + edges;

            var btn = GUI.skin.button;
            float B(string s) => GUI.skin.button.CalcSize(new GUIContent(s)).x + 12f;
            float line2 = B(Strings.T("browser.titlesOnly")) + 6f
                                                             + B(Strings.T("browser.expandVisible")) + 6f
                                                             + B(Strings.T("browser.collapseVisible")) + 6f
                                                             + B(Strings.T("browser.markVisibleRead")) + 6f
                                                             + B(Strings.T("browser.refresh")) + edges;


            return Mathf.Ceil(Mathf.Max(line1, line2));
        }

        // ---- Date/Time helpers ----

        /// <summary>Converts a raw day string into a friendly label (or returns it as-is).</summary>
        string HumanDay(string dayRaw)
        {
            if (DateTime.TryParse(dayRaw, out var d))
                return d.ToString("dd MMM yyyy");
            return dayRaw ?? "—";
        }

        /// <summary>Returns localized day and time strings for the given note.</summary>
        (string day, string time) DayAndTime(WiloNote n)
        {
            string day = HumanDay(n.day ?? "—");
            string time = "—:—";
            if (!string.IsNullOrEmpty(n.utc) && DateTimeOffset.TryParse(n.utc, out var dto))
                time = dto.ToLocalTime().ToString("HH:mm");
            return (day, time);
        }

        // ---- Persistence ----

        /// <summary>Loads UI preferences from EditorPrefs.</summary>
        void LoadPrefs()
        {
            _sortMode   = (SortMode)EditorPrefs.GetInt(PrefPrefix + "Sort", (int)SortMode.DateDesc);
            _stateFilter= (StateFilter)EditorPrefs.GetInt(PrefPrefix + "State", (int)StateFilter.All);
            _groupMode  = (GroupMode)EditorPrefs.GetInt(PrefPrefix + "Group", (int)GroupMode.None);
            _search     = EditorPrefs.GetString(PrefPrefix + "Search", "");
            _titlesOnly = EditorPrefs.GetBool(PrefPrefix + "TitlesOnly", false);
        }

        /// <summary>Saves UI preferences to EditorPrefs.</summary>
        void SavePrefs()
        {
            EditorPrefs.SetInt(PrefPrefix + "Sort", (int)_sortMode);
            EditorPrefs.SetInt(PrefPrefix + "State", (int)_stateFilter);
            EditorPrefs.SetInt(PrefPrefix + "Group", (int)_groupMode);
            EditorPrefs.SetString(PrefPrefix + "Search", _search ?? "");
            EditorPrefs.SetBool(PrefPrefix + "TitlesOnly", _titlesOnly);
        }
    }
}
