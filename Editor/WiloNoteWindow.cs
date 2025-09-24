// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object; // DateTime
using UnityEditor.SceneManagement;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Main editor window to compose and save WILO notes.
    /// Provides title/message editing with a resizable text area, reference collection from the current selection,
    /// quick actions (clear/restore), and two operating modes: normal and exit-prompt flow.
    /// </summary>
    public class WiloNoteWindow : EditorWindow
    {
        /// <summary>
        /// Static constructor: hooks into assembly reload to persist the current draft to the session store.
        /// </summary>
        static WiloNoteWindow()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
        }

        /// <summary>
        /// Called right before domain reload; saves the current draft into <see cref="WiloDraftSessionStore"/>.
        /// </summary>
        static void OnBeforeReload()
        {
            if (_sdraft) WiloDraftSessionStore.Save(_sdraft);
        }
        
        /// <summary>
        /// How the window was opened:
        /// <list type="bullet">
        /// <item><description><see cref="Normal"/>: user opens it manually.</description></item>
        /// <item><description><see cref="Exit"/>: opened as part of the exit prompt flow.</description></item>
        /// </list>
        /// </summary>
        private enum OpenMode
        {
            Normal,
            Exit
        } // Normal = manual usage; Exit = quitting flow

        private OpenMode _mode = OpenMode.Normal;
        
        /// <summary>Deferred operation to run after repaint (used when confirmation prompts are needed).</summary>
        enum DeferredOp { None, Clear, Restore }
        DeferredOp _deferredOp;
        string _deferredMsg;

        // --- Window state (kept as-is; comments translated only) ---
        /// <summary>Dirty flag: indicates unsaved changes in the current draft.</summary>
        private bool _dirty = false;
        static System.WeakReference<WiloNoteWindow> _sLastDockedOwner;
        bool _isExitPopup;
        bool _closingByCode;

        /// <summary>Window title and Unity menu path.</summary>
        private const string WindowTitle = "Where I Left Off — Note";
        private const string MenuPath = "Tools/DevForge/Where I Left Off/Note";
        
        // Draft cache
        /// <summary>Flag to know if this was opened via menu.</summary>
        private static bool _sOpeningFromMenu = false;
        /// <summary>Shared draft ScriptableObject instance (lives for the session).</summary>
        private static WiloDraftNoteSO _sdraft;
        
        // UI sizes/flags
        /// <summary>Scroll position for the message area.</summary>
        private Vector2 _msgScroll;
        /// <summary>Current fixed height for the message area.</summary>
        private float _msgHeight = 120f;   // initial height
        /// <summary>Dragging grip flag for message area resize.</summary>
        private bool _isResizingMessage;
        /// <summary>Word-wrapped TextArea style (initialized lazily).</summary>
        private static GUIStyle _sWrapTextArea; 
        
        // References UI
        /// <summary>Temporary object picker (not persisted).</summary>
        private Object _refPick;
        /// <summary>Scroll position for the references list.</summary>
        private Vector2 _refsScroll;
        /// <summary>Pending dialog message for reference add summary (shown via delayCall).</summary>
        private string _pendingRefsDialog;
        /// <summary>Outer padding for the main layout.</summary>
        private const int SPadding = 12;
        private float _minWidth = 420f;
        bool _pendingMinWidthRecalc = false;
        
        // Cached rects and constants for layout/resize
        Rect _rMsgArea;      
        Rect _rBottomButtons;
        /// <summary>Extra margin below the TextArea reserved by the resize grip.</summary>
        const float kGripMargin = 12f;

        /// <summary>
        /// Common styles used by this window (created lazily).
        /// </summary>
        static class Styles
        {
            static GUIStyle _header, _titleField, _section;
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
            /// <summary>Section label style.</summary>
            public static GUIStyle Section => _section ??= new GUIStyle(EditorStyles.boldLabel);
        }
        
        /// <summary>
        /// Opens the note window in normal mode from the Unity menu.
        /// </summary>
        [MenuItem(MenuPath, priority = 2000)]
        public static void Open()
        {
            _sOpeningFromMenu = true;

            try
            {
                var w = GetWindow<WiloNoteWindow>(utility: false, title: WindowTitle, focus: true);
                w.titleContent = new GUIContent(WindowTitle);

                w._mode = OpenMode.Normal;
                w.minSize = new Vector2(420, 320);
                w._pendingMinWidthRecalc = true;
                w.Show();
                
            }
            finally
            {
                _sOpeningFromMenu = false;
            }
        }

        /// <summary>
        /// Called by the global quit hook to open the window in exit mode.
        /// Handles docked vs floating windows and displays an inline notification.
        /// </summary>
        public static void OpenExitMode()
        {
            if (HasOpenInstances<WiloNoteWindow>())
            {
                var open = GetWindow<WiloNoteWindow>();
                if (open.docked)
                {
                    _sLastDockedOwner = new System.WeakReference<WiloNoteWindow>(open);
                    
                    // Create a utility twin to show the exit prompt UI
                    var u = CreateInstance<WiloNoteWindow>();
                    u.titleContent = new GUIContent(WindowTitle);
                    u._isExitPopup = true;

                    u._mode = OpenMode.Exit;
                    u.minSize = new Vector2(420, 280);
                    u._pendingMinWidthRecalc = true;
                    u.ShowUtility();
                    u.Focus();
                    u.ShowNotification(new GUIContent(Strings.T("msg.exitPrompt")));
                }
                else
                {
                    // Reuse the existing floating window
                    open._mode = OpenMode.Exit;
                    open._pendingMinWidthRecalc = true;
                    open.Focus();
                    open.Repaint();
                    open.ShowNotification(new GUIContent(Strings.T("msg.exitPrompt")));
                }
                return;
            }

            // No window was open: create a utility window in Exit mode
            var w = CreateInstance<WiloNoteWindow>();
            w.titleContent = new GUIContent(WindowTitle);
            
            w._mode = OpenMode.Exit;
            w._pendingMinWidthRecalc = true;
            w.minSize = new Vector2(420, 280);
            w.ShowUtility();
            w.Focus();
            w.ShowNotification(new GUIContent(Strings.T("msg.exitPrompt")));
        }

        /// <summary>
        /// Unity lifecycle: ensures a draft exists, and restores any session-saved data.
        /// Also closes early if opened at startup in a disallowed context.
        /// </summary>
        private void OnEnable()
        {
            if (WiloStartup.JustStarted && !_sOpeningFromMenu && !docked && !WiloQuitHook.IsExitPromptOpen)
            {
                Close();
                return;
            }

            // 1) Ensure _sdraft always exists
            if (_sdraft == null)
            {
                _sdraft = CreateInstance<WiloDraftNoteSO>();
                _sdraft.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;

                // 2) Attempt restoring from SessionState
                if (WiloDraftSessionStore.TryLoad(out var t, out var m, out var refs))
                {
                    _sdraft.title   = t ?? string.Empty;
                    _sdraft.message = m ?? string.Empty;

                    _sdraft.refGuids.Clear();
                    if (refs != null) _sdraft.refGuids.AddRange(refs);

                    WiloDraftSessionStore.Clear();
                    _dirty = false;              
                    Repaint();
                }
            }
        }

        /// <summary>
        /// Main IMGUI loop: header, text area, references block, quick actions and bottom buttons.
        /// Applies deferred operations, maintains window height, and handles contextual menu.
        /// </summary>
        private void OnGUI()
        {
            if (_pendingMinWidthRecalc)
            {
                // Estamos dentro de OnGUI: ya podemos medir estilos y fijar minSize
                EnsureMinWidthForCurrentMode_IMGUI();
                _pendingMinWidthRecalc = false;
            }
            
            Shortcuts();

            using (new EditorGUILayout.VerticalScope(Styles.Pad))
            {
                EditorGUILayout.Space(8);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawHeader();
                }
                
                EditorGUILayout.Space(8);

                DrawTextArea();

                EditorGUILayout.Space(8);

                DrawSeparator();

                EditorGUILayout.Space(8);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawReferences();
                }
                
                EditorGUILayout.Space(8);

                DrawSeparator();

                EditorGUILayout.Space(8);

                DrawQuickActions();

                if (_mode == OpenMode.Exit)
                {
                    DrawExitButtons();
                }
                else
                {
                    DrawManualButtons();
                }
            }

            UpdateWindowHeight();
            DeferredCall();
            ContextualOptions();
            if (docked)
                _msgHeight = Mathf.Min(_msgHeight, MaxMessageHeightForDocked());
        }

        /// <summary>
        /// Keyboard shortcuts:
        /// <list type="bullet">
        /// <item><description>Cmd/Ctrl+S → Save (overwrite mode).</description></item>
        /// <item><description>Cmd/Ctrl+Enter → Save as new.</description></item>
        /// <item><description>Esc → Exit prompt cancel (in exit mode) or request window close (normal mode).</description></item>
        /// </list>
        /// </summary>
        void Shortcuts()
        {
            var e = Event.current;
            bool isCmd = Application.platform == RuntimePlatform.OSXEditor ? e.command : e.control;

            if (e.type == EventType.KeyDown && isCmd && e.keyCode == KeyCode.S)
            {
                if (SaveNote(false)) ShowNotification(new GUIContent(Strings.T("btn.save")));
                e.Use();
            }
            else if (e.type == EventType.KeyDown && isCmd && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                if (SaveNote(true)) ShowNotification(new GUIContent(Strings.T("btn.saveAsNew")));
                e.Use();
            }
            
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (_mode == OpenMode.Exit)
                {
                    CancelExit();
                    Event.current.Use();
                }
                else
                {
                    RequestCloseWindow();
                    e.Use();
                    return;
                }
            }
        }

        /// <summary>
        /// Draws the centered window header with dynamic height based on current width.
        /// </summary>
        void DrawHeader()
        {
            string header = "Where I Left Off — " + Strings.T("lbl.note");

            // calculate required height for current width
            float w = EditorGUIUtility.currentViewWidth;
            float h = Styles.HeaderCenter.CalcHeight(new GUIContent(header), w);

            var r = GUILayoutUtility.GetRect(0, h, GUILayout.ExpandWidth(true));
            GUI.Label(r, header, Styles.HeaderCenter);
        }

        /// <summary>
        /// Paints a thin horizontal separator (subtle).
        /// </summary>
        void DrawSeparator(float h = 1f)
        {
            var r = GUILayoutUtility.GetRect(1, h, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(r, new Color(1,1,1, 0.08f));
        }

        /// <summary>
        /// Renders the title input and the message TextArea with a fixed-height scroll
        /// and a draggable grip to resize the message area.
        /// </summary>
        private void DrawTextArea()
        {
            if (_sWrapTextArea == null)
            {
                _sWrapTextArea = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
                _sWrapTextArea.padding.right += (int)GUI.skin.verticalScrollbar.fixedWidth;
            }
            
            // 1) SerializedObject/Property on the SO (single source of truth)
            if (_sdraft == null)
            {
                // safety: recreate if something cleared it
                _sdraft = CreateInstance<WiloDraftNoteSO>();
                _sdraft.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            }
            var so       = new SerializedObject(_sdraft);
            var pTitle   = so.FindProperty("title");
            var pMessage = so.FindProperty("message");

            // Title
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(Strings.T("lbl.title"), Styles.Section);

            GUI.SetNextControlName("WiloTitle");
            var r = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));

            EditorGUI.BeginChangeCheck();
            string t = EditorGUI.TextField(r, GUIContent.none, pTitle.stringValue);
            bool changed = EditorGUI.EndChangeCheck();

            // Placeholder when empty and unfocused
            if (string.IsNullOrEmpty(pTitle.stringValue) &&
                GUI.GetNameOfFocusedControl() != "WiloTitle")
            {
                var hintRect = new Rect(r.x + 6f, r.y + 2f, r.width - 12f, r.height);
                var hintStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = new Color(1f, 1f, 1f, 0.35f) }
                };
                GUI.Label(hintRect, Strings.T("lbl.writeTitle"), hintStyle);
            }

            // Apply changes to the SO (with Undo) if any
            if (changed)
            {
                Undo.RecordObject(_sdraft, "Edit Title");
                pTitle.stringValue = t;
                so.ApplyModifiedProperties();
                _dirty = true;
            }

            // --- Message area (fixed height, wrapped, vertical scroll only) ---
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(Strings.T("lbl.message"), EditorStyles.boldLabel);

            _msgScroll = EditorGUILayout.BeginScrollView(
                _msgScroll,
                false,                   // no horizontal scroll
                false,                   // vertical appears as needed
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUIStyle.none,
                GUILayout.Height(_msgHeight)
            );

            EditorGUI.BeginChangeCheck();
            string newMsg = EditorGUILayout.TextArea(pMessage.stringValue,_sWrapTextArea, GUILayout.ExpandHeight(true));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_sdraft, "Edit Message");
                pMessage.stringValue = newMsg;
                so.ApplyModifiedProperties();
                _dirty   = true;
            }
            EditorGUILayout.EndScrollView();

            // Resize grip below the TextArea
            var gripRect = GUILayoutUtility.GetRect(0, 8, GUILayout.ExpandWidth(true));

            var gripColor = EditorGUIUtility.isProSkin ? new Color(1,1,1,0.08f) : new Color(0,0,0,0.12f);
            EditorGUI.DrawRect(gripRect, gripColor);
            EditorGUIUtility.AddCursorRect(gripRect, MouseCursor.ResizeVertical);

            var e = Event.current;
            if (e.type == EventType.MouseDown && gripRect.Contains(e.mousePosition))
            {
                _isResizingMessage = true;
                e.Use();
            }
            if (_isResizingMessage && e.type == EventType.MouseDrag)
            {
                _msgHeight = Mathf.Max(60f, _msgHeight + e.delta.y);

                // limit when docked
                if (docked)
                    _msgHeight = Mathf.Min(_msgHeight, MaxMessageHeightForDocked());

                Repaint();
            }
            if (e.type == EventType.MouseUp)
            {
                _isResizingMessage = false;
            }
        }

        /// <summary>
        /// Renders the references block:
        /// - Adds current selection (assets, scene/prefab sources, prefab stage root).
        /// - Clears references with confirmation.
        /// - Displays a scrollable list with ping/remove actions.
        /// </summary>
        private void DrawReferences()
        {
            // SerializedObject on the SO (same as title/message)
            if (_sdraft == null) {
                _sdraft = CreateInstance<WiloDraftNoteSO>();
                _sdraft.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            }
            var so = new SerializedObject(_sdraft);
            var pGuids = so.FindProperty("refGuids");
            EditorGUILayout.LabelField(Strings.T("lbl.references"), EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                // Add current selection
                if (GUILayout.Button(Strings.T("btn.addSelection"), GUILayout.ExpandWidth(true)))
                {
                    // Already existing GUIDs
                    var existing = new HashSet<string>();
                    for (int i = 0; i < pGuids.arraySize; i++)
                        existing.Add(pGuids.GetArrayElementAtIndex(i).stringValue);

                    int added = 0, skipped = 0;
                    
                    var scenesSeen  = new HashSet<string>();
                    var prefabsSeen = new HashSet<string>();
                    
                    void HandleSceneAndPrefab(GameObject go)
                    {
                        // (a) Scene asset
                        var scenePath = go.scene.path;
                        if (!string.IsNullOrEmpty(scenePath))
                        {
                            var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                            if (!scenesSeen.Contains(sceneGuid) && WiloUtilities.TryAddGuid(pGuids, existing, sceneGuid))
                            { added++; scenesSeen.Add(sceneGuid); }
                            else skipped++;
                        }

                        // (b) Prefab source (for instances)
                        if (PrefabUtility.IsPartOfPrefabInstance(go))
                        {
                            var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                            if (source != null && WiloUtilities.TryGetAssetGuid(source, out var prefabGuid))
                            {
                                if (!prefabsSeen.Contains(prefabGuid) && WiloUtilities.TryAddGuid(pGuids, existing, prefabGuid))
                                { added++; prefabsSeen.Add(prefabGuid); }
                                else skipped++;
                            }
                        }
                    }

                    // 1) Prefab Mode: add the stage prefab if selection belongs to the current stage
                    var stage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (stage != null)
                    {
                        bool selectionIsInStage = false;
                        var selGos = Selection.gameObjects;
                        for (int i = 0; i < selGos.Length; i++)
                        {
                            if (stage.IsPartOfPrefabContents(selGos[i]))
                            {
                                selectionIsInStage = true;
                                break;
                            }
                        }

                        if (selectionIsInStage)
                        {
                            var stageGuid = AssetDatabase.AssetPathToGUID(stage.assetPath);
                            if (WiloUtilities.TryAddGuid(pGuids, existing, stageGuid)) added++; else skipped++;
                        }
                    }

                    // 2) Project assets / scene objects / other resolvables
                    foreach (var obj in Selection.objects)
                    {
                        // Project asset
                        if (AssetDatabase.Contains(obj))
                        {
                            if (WiloUtilities.TryGetAssetGuid(obj, out var guid) && WiloUtilities.TryAddGuid(pGuids, existing, guid)) added++;
                            else skipped++;
                            continue;
                        }

                        // Scene object
                        if (obj is GameObject goObj)
                        {
                            HandleSceneAndPrefab(goObj);
                            continue;
                        }
                        if (obj is Component comp)
                        {
                            HandleSceneAndPrefab(comp.gameObject);
                            continue;
                        }

                        // Other types: attempt to resolve to an asset GUID
                        if (WiloUtilities.TryGetAssetGuid(obj, out var anyGuid) && WiloUtilities.TryAddGuid(pGuids, existing, anyGuid)) added++;
                        else skipped++;
                    }

                    // apply, mark dirty, show summary later
                    so.ApplyModifiedProperties();
                    _dirty = true;
                    Repaint();

                    if (skipped > 0)
                    {
                        _pendingRefsDialog = Strings.T("lbl.added") + added + "\n" + Strings.T("lbl.ignored") + skipped;
                    }
                }

                // Clear references (with confirmation)
                using (new EditorGUI.DisabledScope(pGuids.arraySize == 0))
                {
                    if (GUILayout.Button(Strings.T("btn.clearRefs"), GUILayout.ExpandWidth(true)))
                    {
                        if (EditorUtility.DisplayDialog(WindowTitle,
                                Strings.T("msg.confirmClearRefs"),
                                Strings.T("btn.yes"), Strings.T("btn.no")))
                        {
                            Undo.RecordObject(_sdraft, Strings.T("btn.clearRefs"));
                            pGuids.ClearArray();
                            so.ApplyModifiedProperties();
                            Repaint();
                            _dirty = true;
                        }
                    }
                }
            }

            // Render references list (up to 10 rows visible)
            int maxRows = 10;
            float rowH  = EditorGUIUtility.singleLineHeight + 6f;
            float listH = Mathf.Min(pGuids.arraySize, maxRows) * rowH + 4f;

            _refsScroll = EditorGUILayout.BeginScrollView(
                _refsScroll,
                false,   // no horizontal scroll
                false,   // vertical appears as needed
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUIStyle.none,
                GUILayout.Height(listH)
            );

            for (int i = 0; i < pGuids.arraySize; i++)
            {
                var guidProp = pGuids.GetArrayElementAtIndex(i);
                var guid = guidProp.stringValue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    // index + name with icon (from asset database)
                    var (label, _) = WiloAssetRefs.GetLabelAndIcon(guid);
                    label.text = $"{i + 1}. {label.text}";
                    EditorGUILayout.LabelField(label, GUILayout.Height(rowH));

                    if (GUILayout.Button(Strings.T("btn.ping"), GUILayout.Width(50)))
                    {
                        var obj = WiloAssetRefs.LoadByGuid(guid);
                        if (obj) EditorGUIUtility.PingObject(obj);
                    }
                    if (GUILayout.Button("X", GUILayout.Width(22)))
                    {
                        Undo.RecordObject(_sdraft, "Remove Reference");
                        pGuids.DeleteArrayElementAtIndex(i);
                        so.ApplyModifiedProperties();
                        _dirty = true;
                        break; // stop iterating after index removal
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            
            // Show deferred summary dialog (added/ignored) if needed
            if (!string.IsNullOrEmpty(_pendingRefsDialog))
            {
                string msg = _pendingRefsDialog;
                _pendingRefsDialog = null;
                EditorApplication.delayCall += () =>
                    EditorUtility.DisplayDialog(WindowTitle, msg, Strings.T("btn.ok"));
            }
        }

        /// <summary>
        /// Renders quick actions: Clear note and Restore last (current session).
        /// Uses deferred confirmation when the draft is dirty.
        /// </summary>
        private void DrawQuickActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // Clear
                if (GUILayout.Button(Strings.T("btn.clearNote"), GUILayout.ExpandWidth(true)))
                {
                    if (_dirty)
                    {
                        _deferredOp  = DeferredOp.Clear;
                        _deferredMsg = Strings.T("msg.confirmClearNoteDirty");
                    }
                    else
                    {
                        ClearDraft();
                    }
                }

                // Restore
                using (new EditorGUI.DisabledScope(!CanRestore()))
                {
                    if (GUILayout.Button(Strings.T("btn.restoreLast"), GUILayout.ExpandWidth(true)))
                    {
                        if (_dirty)
                        {
                            _deferredOp  = DeferredOp.Restore;
                            _deferredMsg = Strings.T("msg.confirmRestoreNoteDirty");
                        }
                        else
                        {
                            RestoreLast();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Executes deferred operations (clear/restore) after a user confirmation dialog.
        /// </summary>
        private void DeferredCall()
        {
            if (_deferredOp == DeferredOp.None || Event.current.type != EventType.Repaint)
                return;

            var op  = _deferredOp;
            var msg = _deferredMsg;
            _deferredOp  = DeferredOp.None;
            _deferredMsg = null;

            EditorApplication.delayCall += () =>
            {
                if (!EditorUtility.DisplayDialog(WindowTitle, msg, Strings.T("btn.yes"), Strings.T("btn.no")))
                    return;

                if (op == DeferredOp.Clear)   ClearDraft();
                else if (op == DeferredOp.Restore) RestoreLast();
            };
        }

        /// <summary>
        /// Context menu for the whole window: includes utilities like resetting message height
        /// and opening user preferences.
        /// </summary>
        private void ContextualOptions()
        {
            var e = Event.current;
            if (e.type == EventType.ContextClick)
            {
                var whole = new Rect(0, 0, position.width, position.height);
                if (whole.Contains(e.mousePosition))
                {
                    var menu = new GenericMenu();

                    menu.AddItem(new GUIContent(Strings.T("ctx.resetMsgSize")), false, () =>
                    {
                        _msgHeight = 120f;
                        Repaint();
                    });

                    // Preferences
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent(Strings.T("ctx.openPreferences")), false, () =>
                    {
                        SettingsService.OpenUserPreferences("Preferences/Dev Forge/Where I Left Off");
                    });

                    menu.ShowAsContext();
                    e.Use();
                }
            }
        }

        /// <summary>
        /// Bottom actions when in normal mode: Save / Save as New, with dialogs on success.
        /// </summary>
        private void DrawManualButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Strings.T("btn.save"), GUILayout.ExpandWidth(true)))
                {
                    if (SaveNote(false))
                        EditorUtility.DisplayDialog(WindowTitle, Strings.T("msg.saved"), Strings.T("btn.ok"));
                }

                if (GUILayout.Button(Strings.T("btn.saveAsNew"), GUILayout.ExpandWidth(true)))
                {
                    if (SaveNote(true))
                        EditorUtility.DisplayDialog(WindowTitle, Strings.T("msg.savedAsNew"), Strings.T("btn.ok"));
                }
            }
        }

        /// <summary>
        /// Exit-mode action row: Save/Save New and quit, Quit without saving, or Cancel exit.
        /// </summary>
        private void DrawExitButtons()
        {
            EditorGUILayout.HelpBox(
                Strings.T("msg.exitHelp"),
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Strings.T("btn.saveAndQuit"), GUILayout.ExpandWidth(true)))
                {
                    if (SaveNote(false))
                        WiloQuitHook.ConfirmExitAndQuit();
                }

                if (GUILayout.Button(Strings.T("btn.saveNewAndQuit"), GUILayout.ExpandWidth(true)))
                {
                    if (SaveNote(true))
                        WiloQuitHook.ConfirmExitAndQuit();
                }

                if (GUILayout.Button(Strings.T("btn.quitWithoutSaving"), GUILayout.ExpandWidth(true)))
                {
                    _dirty = false;
                    WiloQuitHook.ConfirmExitAndQuit();
                }
                
                if (GUILayout.Button(Strings.T("btn.cancel"), GUILayout.ExpandWidth(true)))
                {
                    CancelExit();
                }
            }
        }
        
        /// <summary>
        /// Persists the current draft as a note on disk.
        /// If <paramref name="saveAsNew"/> is false and the user preference allows overwrite,
        /// it replaces an existing note matching the normalized title and the chosen scope (by-session or by-day).
        /// Otherwise, it inserts a new entry at the top of the list.
        /// </summary>
        /// <param name="saveAsNew">True to always create a new entry; false to allow overwrite per user setting.</param>
        /// <remarks>
        /// Uses local date for the "same day" check and inserts the resulting note at index 0.
        /// After saving, <c>_dirty</c> becomes false.
        /// </remarks>
        private bool SaveNote(bool saveAsNew)
        {
            // 1) Confirm when title is empty
            if (string.IsNullOrWhiteSpace(_sdraft.title))
            {
                bool go = EditorUtility.DisplayDialog(
                    WindowTitle,
                    Strings.T("msg.titleEmptyConfirm"),
                    Strings.T("btn.save"), Strings.T("btn.cancel"));
                if (!go) return false; // user canceled
            }

            var file = WiloNotesStorage.LoadNotes();

            var note = new WiloNote
            {
                title     = _sdraft.title,
                message   = _sdraft.message,
                day       = DateTime.Now.ToString("yyyy-MM-dd"),
                utc       = DateTime.UtcNow.ToString("o"),
                sessionId = WiloUtilities.GetOrCreateSessionId(),
                refs      = new List<string>(_sdraft.refGuids),
            };

            var mode = WiloPreferences.GetOverwriteMode();

            string normNew          = WiloUtilities.NormalizeString(note.title);
            string today            = note.day;
            string currentSessionId = note.sessionId;

            bool allowOverwrite = mode != WiloPreferences.OverwriteMode.Dont
                                  && !saveAsNew
                                  && !string.IsNullOrEmpty(normNew);

            if (allowOverwrite)
            {
                int idx = file.notes.FindIndex(n =>
                    WiloUtilities.NormalizeString(n.title) == normNew &&
                    (
                        mode == WiloPreferences.OverwriteMode.BySession
                            ? n.sessionId == currentSessionId
                            : n.day == today
                    )
                );
                if (idx >= 0) file.notes.RemoveAt(idx);
            }

            file.notes.Insert(0, note);
            WiloNotesStorage.SaveNotes(file);
            _dirty = false;
            return true;
        }
        
        /// <summary>
        /// Clears the current draft (title, message, and references) and resets the dirty flag.
        /// </summary>
        private void ClearDraft()
        {
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;

            var so = new SerializedObject(_sdraft);
            so.FindProperty("title").stringValue   = string.Empty;
            so.FindProperty("message").stringValue = string.Empty;
            var pRefs = so.FindProperty("refGuids");
            pRefs.ClearArray();
            so.ApplyModifiedProperties();

            _dirty = false;
            Repaint();
        }

        /// <summary>
        /// Returns whether a previous note exists for the current session (to enable the Restore button).
        /// </summary>
        private bool CanRestore()
        {
            var file = WiloNotesStorage.LoadNotes();
            if (file?.notes == null || file.notes.Count == 0) return false;

            string sessId = WiloUtilities.GetOrCreateSessionId();
            for (int i = 0; i < file.notes.Count; i++)
                if (file.notes[i].sessionId == sessId)
                    return true;

            return false;
        }

        /// <summary>
        /// Restores title/message/references from the most recent note of the current session.
        /// </summary>
        private void RestoreLast()
        {
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;

            var file = WiloNotesStorage.LoadNotes();
            if (file?.notes == null || file.notes.Count == 0) return;

            string sessId = WiloUtilities.GetOrCreateSessionId();

            // Find most recent note for the current session
            WiloNote n = null;
            for (int i = 0; i < file.notes.Count; i++)
            {
                var candidate = file.notes[i];
                if (candidate.sessionId == sessId)
                {
                    n = candidate;
                    break;
                }
            }

            if (n == null) return;

            // Copy into the draft ScriptableObject
            var so     = new SerializedObject(_sdraft);
            var pTitle = so.FindProperty("title");
            var pMsg   = so.FindProperty("message");
            var pRefs  = so.FindProperty("refGuids");

            Undo.RecordObject(_sdraft, "Restore Last Note (This Session)");
            pTitle.stringValue = n.title   ?? string.Empty;
            pMsg.stringValue   = n.message ?? string.Empty;

            pRefs.ClearArray();
            if (n.refs != null)
            {
                for (int i = 0; i < n.refs.Count; i++)
                {
                    pRefs.InsertArrayElementAtIndex(i);
                    pRefs.GetArrayElementAtIndex(i).stringValue = n.refs[i];
                }
            }

            so.ApplyModifiedProperties();
            _dirty = false;
            Repaint();
        }

        /// <summary>
        /// Finds an index matching the current draft title according to the overwrite mode,
        /// used by alternative save flows (kept for compatibility).
        /// </summary>
        private int FindMatchingIndex(WiloNotesFile file)
        {
            var mode      = WiloPreferences.GetOverwriteMode();
            string norm   = WiloUtilities.NormalizeString(_sdraft?.title);
            string today  = DateTime.Now.ToString("yyyy-MM-dd");
            string sessId = WiloUtilities.GetOrCreateSessionId();

            for (int i = 0; i < file.notes.Count; i++)
            {
                var n = file.notes[i];
                if (!string.IsNullOrEmpty(norm) && WiloUtilities.NormalizeString(n.title) != norm) continue;

                switch (mode)
                {
                    case WiloPreferences.OverwriteMode.BySession:
                        if (n.sessionId == sessId) return i;
                        break;
                    case WiloPreferences.OverwriteMode.ByDay:
                        if (n.day == today) return i;
                        break;
                    case WiloPreferences.OverwriteMode.Dont:
                    default:
                        break;
                }
            }
            return -1;
        }

        /// <summary>
        /// Handles close requests:
        /// - Exit mode: cancel exit flow.
        /// - No changes: close immediately.
        /// - Dirty: show Save/Discard/Cancel dialog and act accordingly.
        /// </summary>
        void RequestCloseWindow()
        {
            if (_mode == OpenMode.Exit)
            {
                CancelExit();
                return;
            }

            if (!_dirty)
            {
                _closingByCode = true;
                Close();
                return;
            }

            int r = EditorUtility.DisplayDialogComplex(
                WindowTitle,
                Strings.T("btn.unsavedChanges"),
                Strings.T("btn.save"),     // 0
                Strings.T("btn.discard"),  // 1
                Strings.T("btn.cancel")    // 2
            );

            if (r == 0)
            {
                if (!SaveNote(false)) return;
            }
            else if (r == 1)
            {
                // Discard → clear draft before closing
                ClearDraft();
            }
            else if (r == 2)
            {
                return;
            }

            _closingByCode = true;
            Close();
        }

        /// <summary>
        /// Unity lifecycle: intercepts destruction and prompts to save/discard/cancel when dirty in normal mode.
        /// Reopens the window if the user cancels the close action.
        /// </summary>
        private void OnDestroy()
        {
            if (_closingByCode) return;
            
            if (_mode == OpenMode.Normal && _dirty)
            {
                int r = EditorUtility.DisplayDialogComplex(
                    WindowTitle,
                    Strings.T("btn.unsavedChanges"),
                    Strings.T("btn.save"),     // 0
                    Strings.T("btn.discard"),  // 1
                    Strings.T("btn.cancel")    // 2
                );

                if (r == 0)
                {
                    if (SaveNote(false))
                        _dirty = false;  
                }
                else if (r == 1)
                {
                    Undo.RecordObject(_sdraft, "Discard WILO Draft");
                    _sdraft.title = "";
                    _sdraft.message = "";
                    EditorUtility.SetDirty(_sdraft);
                }
                else if (r == 2)
                {
                    // Re-open to simulate canceling the close
                    EditorApplication.delayCall += Open;
                }
            }
        }

        /// <summary>
        /// Adjusts the window height to fit content exactly (floating) or clamps for docked layout.
        /// </summary>
        void UpdateWindowHeight()
        {
            if (docked) return;
            
            var probe = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                float contentHeight = Mathf.Ceil(probe.yMax);

                if (!docked)
                {
                    minSize = new Vector2(_minWidth, contentHeight);
                    maxSize = new Vector2(8192f, contentHeight);
                }
                else
                {
                    minSize = new Vector2(_minWidth, contentHeight);
                    maxSize = new Vector2(8192f, 8192f);
                }
            }
        }
        
        float ComputeExitButtonsRowMinWidth_IMGUI()
        {
            // Aquí sí es seguro usar GUI.skin / EditorStyles
            GUIStyle s = GUI.skin.button;
            float gap = 6f;
            float outer = SPadding * 2f;

            float w1 = s.CalcSize(new GUIContent(Strings.T("btn.saveAndQuit"))).x + s.margin.horizontal;
            float w2 = s.CalcSize(new GUIContent(Strings.T("btn.saveNewAndQuit"))).x + s.margin.horizontal;
            float w3 = s.CalcSize(new GUIContent(Strings.T("btn.quitWithoutSaving"))).x + s.margin.horizontal;
            float w4 = s.CalcSize(new GUIContent(Strings.T("btn.cancel"))).x + s.margin.horizontal;

            return Mathf.Ceil(outer + w1 + w2 + w3 + w4 + gap * 3f);
        }

        void EnsureMinWidthForCurrentMode_IMGUI()
        {
            const float baseMin = 420f;
            float m = (_mode == OpenMode.Exit)
                ? Mathf.Max(baseMin, ComputeExitButtonsRowMinWidth_IMGUI())
                : baseMin;

            _minWidth = m;
            var ms = minSize;
            ms.x = _minWidth;
            minSize = ms;
        }

        
        /// <summary>
        /// Cancels exit mode: restores the docked owner (if present) back to normal and closes utility popups.
        /// </summary>
        void CancelExit()
        {
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;

            if (_isExitPopup)
            {
                // Switch docked owner back to normal and notify
                if (_sLastDockedOwner != null &&
                    _sLastDockedOwner.TryGetTarget(out var owner) && owner)
                {
                    owner._mode = OpenMode.Normal;
                    owner.Repaint();
                    owner.ShowNotification(new GUIContent("Cierre cancelado"));
                }

                Close();
                return;
            }

            _mode = OpenMode.Normal;
            _pendingMinWidthRecalc = true;
            Repaint();
            ShowNotification(new GUIContent(Strings.T("msg.closeCancelled")));
            WiloQuitHook.CancelExitPrompt();
        }
        
        public static void NotifyLocaleChanged()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<WiloNoteWindow>())
            {
                w._pendingMinWidthRecalc = true;
                w.Repaint();
            }
        }

        /// <summary>
        /// Computes a conservative maximum height for the message area when the window is docked,
        /// taking into account padding, headers, reference list and buttons.
        /// </summary>
        float MaxMessageHeightForDocked()
        {
            const float outerVPad = SPadding + SPadding;
            float line     = EditorGUIUtility.singleLineHeight;
            float row      = line + 6f;
            float gripH    = 8f + 4f;
            float sep      = 6f;
            float buttons  = 24f;
            float qaRow    = 22f;

            float hdrH = Styles.HeaderCenter.CalcHeight(
                new GUIContent("Where I Left Off — "+ Strings.T("lbl.note")),
                position.width) + 4f;

            float titleBlock = line + 20f + 6f;

            int refsCount = _sdraft != null ? _sdraft.refGuids.Count : 0;
            int maxRows   = 10;
            float refsHdr = line + 6f;
            float refsBtns = 22f + 4f;
            float listH   = Mathf.Min(refsCount, maxRows) * row + 4f;

            float fixedBlocks =
                outerVPad +
                hdrH + sep +
                titleBlock + sep +
                /* message: label + area + grip → remove only label and grip here */
                line + 0f + gripH + sep +
                refsHdr + refsBtns + listH + sep +
                qaRow + sep +
                buttons + sep;

            float avail = position.height - fixedBlocks;
            return Mathf.Max(60f, avail);
        }
    }   
}
