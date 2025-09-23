// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Centralized user preferences for WILO (stored in <see cref="EditorPrefs"/> with a per-project key).
    /// Exposes getters/setters and a Unity <see cref="SettingsProvider"/> to edit options in Preferences.
    /// </summary>
    public static class WiloPreferences
    {
        /// <summary>
        /// Overwrite policy when saving a note via "Save" (not "Save as New").
        /// </summary>
        public enum OverwriteMode
        {
            /// <summary>Never overwrite. "Save" always creates a new entry.</summary>
            Dont = 0,
            /// <summary>Overwrite when the normalized title matches and it's the same session.</summary>
            BySession = 1,
            /// <summary>Overwrite when the normalized title matches and it's the same day.</summary>
            ByDay = 2
        }

        /// <summary>
        /// Default range used by the startup popup window.
        /// </summary>
        public enum StartupPopupRange
        {
            /// <summary>Show notes from the last session.</summary>
            LastSession = 0,
            /// <summary>Show notes from the last day.</summary>
            LastDay     = 1
        }

        /// <summary>
        /// Forced UI language for the package (Editor only).
        /// </summary>
        public enum ForcedLanguage { English = 0, Spanish = 1 }

        // Keys stored in EditorPrefs (project-scoped via ProjectKey())
        const string kOverwriteModeKey = "OverwriteMode";
        const string kPromptOnExitKey  = "PromptOnExit";
        const string kShowStartupKey   = "ShowStartupPopup";
        const string kStartupRangeKey  = "StartupPopupRange";
        const string KeyUserAlias      = "WILO_UserAlias";
        const string kForcedLangKey    = "ForcedLanguage";

        // -------- Persistence --------

        /// <summary>Returns the current overwrite mode (defaults to <see cref="OverwriteMode.ByDay"/>).</summary>
        public static OverwriteMode GetOverwriteMode()
        {
            int def = (int)OverwriteMode.ByDay;
            return (OverwriteMode)EditorPrefs.GetInt(ProjectKey(kOverwriteModeKey), def);
        }

        /// <summary>Sets the overwrite mode preference.</summary>
        public static void SetOverwriteMode(OverwriteMode m)
            => EditorPrefs.SetInt(ProjectKey(kOverwriteModeKey), (int)m);

        /// <summary>Returns whether to prompt for a note when quitting the Editor (default: true).</summary>
        public static bool GetPromptOnExit()
            => EditorPrefs.GetBool(ProjectKey(kPromptOnExitKey), true);

        /// <summary>Sets whether to prompt for a note when quitting the Editor.</summary>
        public static void SetPromptOnExit(bool v)
            => EditorPrefs.SetBool(ProjectKey(kPromptOnExitKey), v);

        /// <summary>Returns whether to show the startup popup (default: true).</summary>
        public static bool GetShowStartupPopup()
            => EditorPrefs.GetBool(ProjectKey(kShowStartupKey), true);

        /// <summary>Sets whether to show the startup popup.</summary>
        public static void SetShowStartupPopup(bool v)
            => EditorPrefs.SetBool(ProjectKey(kShowStartupKey), v);

        /// <summary>Returns the default range used by the startup popup (default: Last Session).</summary>
        public static StartupPopupRange GetStartupPopupRange()
            => (StartupPopupRange)EditorPrefs.GetInt(ProjectKey(kStartupRangeKey), (int)StartupPopupRange.LastSession);

        /// <summary>Sets the default range used by the startup popup.</summary>
        public static void SetStartupPopupRange(StartupPopupRange r)
            => EditorPrefs.SetInt(ProjectKey(kStartupRangeKey), (int)r);

        /// <summary>Returns the forced language setting (English/Spanish). Clamps legacy values.</summary>
        public static ForcedLanguage GetForcedLanguage()
        {
            // Legacy compatibility: previously we stored 0=System, 1=English, 2=Spanish.
            int raw = EditorPrefs.GetInt(ProjectKey(kForcedLangKey), (int)ForcedLanguage.English);
            if (raw < 0 || raw > 1) raw = (int)ForcedLanguage.English; // clamp legacy "System" to English
            return (ForcedLanguage)raw;
        }

        /// <summary>Sets the forced language for the package UI (Editor only).</summary>
        public static void SetForcedLanguage(ForcedLanguage v)
            => EditorPrefs.SetInt(ProjectKey(kForcedLangKey), (int)v);

        /// <summary>
        /// Optional per-user alias. If set, read-state files will use <c>read_state.&lt;alias&gt;.json</c>.
        /// </summary>
        public static string UserAlias
        {
            get => EditorPrefs.GetString(KeyUserAlias, string.Empty);
            set => EditorPrefs.SetString(KeyUserAlias, value ?? "");
        }

        /// <summary>
        /// Builds a project-scoped key by prefixing a local key with a stable per-project hash.
        /// </summary>
        static string ProjectKey(string localKey)
        {
            int pid = Application.dataPath.GetHashCode();
            return $"WILO_{pid}_{localKey}";
        }

        // ------- UI Helpers -------

        /// <summary>
        /// Draws a labeled popup with a fixed label width, returning the selected index.
        /// </summary>
        static int LabeledPopup(string label, string[] options, int selected, float labelWidth = 190f)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, EditorStyles.label, GUILayout.Width(labelWidth));
                return EditorGUILayout.Popup(selected, options, GUILayout.ExpandWidth(true));
            }
        }

        /// <summary>
        /// Registers the "Where I Left Off" Preferences page under
        /// <c>Preferences/Dev Forge/Where I Left Off</c>.
        /// </summary>
        [SettingsProvider]
        public static SettingsProvider CreatePreferencesGUI()
        {
            var provider = new SettingsProvider("Preferences/Dev Forge/Where I Left Off", SettingsScope.User)
            {
                label = "Where I Left Off",
                guiHandler = _ =>
                {
                    // ----- Language -----
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField(Strings.T("prefs.section.language"), EditorStyles.boldLabel);

                    var forced = GetForcedLanguage();

                    // Two options only: English / Spanish
                    string[] langOptions = {
                        Strings.T("prefs.lang.english"),
                        Strings.T("prefs.lang.spanish")
                    };
                    EditorGUI.BeginChangeCheck();
                    int forcedIndex = (int)forced;
                    forcedIndex = LabeledPopup(Strings.T("prefs.lang.label"),
                        langOptions, forcedIndex, 190f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        forced = (ForcedLanguage)forcedIndex;
                        SetForcedLanguage(forced);
                        Locale.ClearCache();
                        Strings.Reload();
                    }

                    // ----- Startup -----
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField(Strings.T("prefs.section.startup"), EditorStyles.boldLabel);

                    bool showStartup = GetShowStartupPopup();
                    EditorGUI.BeginChangeCheck();
                    showStartup = EditorGUILayout.ToggleLeft(
                        new GUIContent(
                            Strings.T("prefs.startup.showPopup.label"),
                            Strings.T("prefs.startup.showPopup.tip")),
                        showStartup);
                    if (EditorGUI.EndChangeCheck())
                        SetShowStartupPopup(showStartup);

                    using (new EditorGUI.DisabledScope(!showStartup))
                    {
                        string[] rangeOptions = {
                            Strings.T("prefs.startup.range.option.lastSession"),
                            Strings.T("prefs.startup.range.option.lastDay")
                        };
                        int sel = (int)GetStartupPopupRange();
                        EditorGUI.BeginChangeCheck();
                        sel = LabeledPopup(
                            Strings.T("prefs.startup.range.label"),
                            rangeOptions, sel, 200f);
                        if (EditorGUI.EndChangeCheck())
                            SetStartupPopupRange((StartupPopupRange)sel);
                    }

                    // ----- Exit behavior -----
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField(Strings.T("prefs.section.onExit"), EditorStyles.boldLabel);

                    bool promptOnExit = GetPromptOnExit();
                    EditorGUI.BeginChangeCheck();
                    promptOnExit = EditorGUILayout.ToggleLeft(
                        new GUIContent(
                            Strings.T("prefs.exit.showOnQuit.label"),
                            Strings.T("prefs.exit.showOnQuit.tip")),
                        promptOnExit);
                    if (EditorGUI.EndChangeCheck())
                        SetPromptOnExit(promptOnExit);

                    // ----- Save behavior -----
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField(Strings.T("prefs.section.save"), EditorStyles.boldLabel);

                    var mode = GetOverwriteMode();
                    string[] overwriteOptions = {
                        Strings.T("prefs.save.overwrite.opt.dont"),
                        Strings.T("prefs.save.overwrite.opt.bySession"),
                        Strings.T("prefs.save.overwrite.opt.byDay"),
                    };
                    EditorGUI.BeginChangeCheck();
                    int modeIdx = (int)mode;
                    modeIdx = LabeledPopup(
                        Strings.T("prefs.save.overwrite.label"),
                        overwriteOptions, modeIdx, 190f);
                    if (EditorGUI.EndChangeCheck())
                        SetOverwriteMode((OverwriteMode)modeIdx);

                    EditorGUILayout.Space(6);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        string help = modeIdx switch
                        {
                            (int)OverwriteMode.Dont      => Strings.T("prefs.save.help.mode.dont"),
                            (int)OverwriteMode.BySession => Strings.T("prefs.save.help.mode.bySession"),
                            (int)OverwriteMode.ByDay     => Strings.T("prefs.save.help.mode.byDay"),
                            _ => ""
                        };
                        EditorGUILayout.LabelField(help, EditorStyles.wordWrappedLabel);
                    }

                    // ----- User alias -----
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField(Strings.T("prefs.section.user"), EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    string alias = UserAlias;
                    alias = EditorGUILayout.TextField(
                        new GUIContent(
                            Strings.T("prefs.user.alias.label"),
                            Strings.T("prefs.user.alias.tip")),
                        alias);
                    if (EditorGUI.EndChangeCheck())
                        UserAlias = alias;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(Strings.T("prefs.user.openFolder.btn"), GUILayout.Width(180)))
                        {
                            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                            var baseDir = Directory.Exists(Path.Combine(projectRoot ?? "", "UserSettings"))
                                ? Path.Combine(projectRoot ?? "", "UserSettings", "WILO")
                                : Path.Combine(projectRoot ?? "", "Library", "WILO");
                            if (!Directory.Exists(baseDir))
                                Directory.CreateDirectory(baseDir);
                            EditorUtility.RevealInFinder(baseDir);
                        }
                    }
                }

            };

            // Search keywords for the Preferences page
            provider.keywords = new HashSet<string>(new[]
            {
                "WILO","Where I Left Off","Startup","Arranque","Overwrite","Sobrescribir","Notas","Dev Forge"
            });

            return provider;
        }
    }
}
