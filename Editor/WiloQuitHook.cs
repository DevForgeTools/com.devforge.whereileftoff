// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using UnityEditor;
using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Global quit hook for the Unity Editor. Intercepts the quit flow to optionally
    /// show the WILO exit prompt and let the user write/save a note before exiting.
    /// </summary>
    [InitializeOnLoad]
    public static class WiloQuitHook
    {
        /// <summary>
        /// Guards against re-entrancy when we decide to quit explicitly (after confirmation).
        /// </summary>
        private static bool _exitFlow = false;

        /// <summary>
        /// True while the exit prompt flow is active (a popup/window is shown to the user).
        /// </summary>
        public  static bool IsExitPromptOpen { get; private set; }
        
        /// <summary>
        /// Static initializer: subscribes to <see cref="EditorApplication.wantsToQuit"/>.
        /// </summary>
        static WiloQuitHook()
        {
            EditorApplication.wantsToQuit += OnWantsToQuit;
        }

        /// <summary>
        /// Quit callback. Returns <c>false</c> to cancel quitting and show the exit prompt
        /// when enabled in preferences; returns <c>true</c> to allow quitting.
        /// </summary>
        private static bool OnWantsToQuit()
        {
            if (Application.isBatchMode) return true;   // CI
            if (_exitFlow) return true;

            // Ignore during reimport/compilation/update cycles
            if (UnityEditor.EditorApplication.isCompiling) return true;
            if (UnityEditor.EditorApplication.isUpdating)  return true;

            if (!WiloPreferences.GetPromptOnExit())
                return true;

            IsExitPromptOpen = true;
            WiloNoteWindow.OpenExitMode();
            EditorApplication.Beep();
            return false;
        }


        /// <summary>
        /// Confirms the exit decision and closes the Editor process.
        /// Sets internal flags accordingly and calls <see cref="EditorApplication.Exit(int)"/>.
        /// </summary>
        public static void ConfirmExitAndQuit()
        {
            _exitFlow = true;
            IsExitPromptOpen = false;
            // Optional: save scenes/project here if appropriate before quitting.
            EditorApplication.Exit(0);
        }
        
        /// <summary>
        /// Cancels the exit prompt flow, restoring normal Editor operation.
        /// </summary>
        public static void CancelExitPrompt()
        {
            IsExitPromptOpen = false;
        }
    }
}
