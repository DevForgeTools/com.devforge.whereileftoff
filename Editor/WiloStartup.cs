// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System;
using UnityEditor;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Editor startup hook for WILO.
    /// Runs once per Editor process to set a "just started" flag and optionally open the startup popup.
    /// </summary>
    [InitializeOnLoad]
    static class WiloStartup
    {
        /// <summary>Session-state key: indicates the current Editor process has already run startup logic.</summary>
        private const string StartedKey = "WILO_EditorProcessStarted";
        /// <summary>Session-state key: indicates the Editor has just started.</summary>
        private const string JustStartedKey = "WILO_JustStarted";

        /// <summary>
        /// True only on the first domain update after the Editor process launches; false afterwards.
        /// </summary>
        public static bool JustStarted { get; private set; }

        /// <summary>
        /// Static constructor: ensures one-time initialization per Editor process and schedules first-update callbacks.
        /// </summary>
        static WiloStartup()
        {
            if (!SessionState.GetBool(StartedKey, false))
            {
                SessionState.SetBool(StartedKey, true);
                JustStarted = true;

                // Open the popup on first Update if the preference is enabled.
                EditorApplication.update += ShowStartupPopupOnce;

                // Clear the "just started" flag on the next Update.
                EditorApplication.update += Clear;
            }
            else
            {
                JustStarted = false;
            }
        }
        
        /// <summary>
        /// One-shot callback executed on the first Editor update:
        /// opens the "Last Session" popup depending on user preferences.
        /// </summary>
        static void ShowStartupPopupOnce()
        {
            EditorApplication.update -= ShowStartupPopupOnce;
            try
            {
                if (WiloPreferences.GetShowStartupPopup())
                {
                    // Keep as-is: opens the "Last Session" range.
                    var mode = WiloPreferences.GetShowStartupPopup();
                    WiloNotesPopupWindow.Open(NotesPopupMode.LastSession);
                }
            }
            catch (Exception)
            {
                // Swallow: do not break Editor startup if something fails here.
            }
        }

        /// <summary>
        /// One-shot callback to clear the "JustStarted" session flag after the first update.
        /// </summary>
        private static void Clear()
        {
            EditorApplication.update -= Clear;
            SessionState.SetBool(JustStartedKey, false);
        }
    }
}
