// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Session-scoped (non-persistent) storage for the WILO draft note.
    /// Uses <see cref="SessionState"/> to save/load data for the lifetime of the editor session.
    /// </summary>
    static class WiloDraftSessionStore
    {
        /// <summary>
        /// Project-unique key (derived from <see cref="Application.dataPath"/>).
        /// Ensures session storage is isolated across different projects.
        /// </summary>
        static string Key => $"WILO_DRAFT_{Application.dataPath.GetHashCode()}";

        /// <summary>
        /// Serializable DTO used to persist draft fields.
        /// </summary>
        [System.Serializable]
        class Dto
        {
            /// <summary>Draft title.</summary>
            public string title;

            /// <summary>Draft content.</summary>
            public string message;

            /// <summary>List of related asset GUIDs.</summary>
            public List<string> refs;
        }

        /// <summary>
        /// Saves the contents of the provided <see cref="WiloDraftNoteSO"/> into the current session.
        /// </summary>
        /// <param name="so">Instance containing the draft data.</param>
        public static void Save(WiloDraftNoteSO so)
        {
            if (!so) return;
            var dto = new Dto {
                title   = so.title ?? "",
                message = so.message ?? "",
                refs    = new List<string>(so.refGuids ?? new List<string>()),
            };
            var json = EditorJsonUtility.ToJson(dto);
            SessionState.SetString(Key, json);
        }
        
        /// <summary>
        /// Saves raw values into the session without requiring a <see cref="WiloDraftNoteSO"/>.
        /// Creates a temporary ScriptableObject to reuse <see cref="Save(WiloDraftNoteSO)"/>.
        /// </summary>
        /// <param name="title">Title to save (can be null).</param>
        /// <param name="message">Message to save (can be null).</param>
        /// <param name="refs">Reference GUIDs (can be null).</param>
        public static void SaveRaw(string title, string message, List<string> refs)
        {
            var tmp = ScriptableObject.CreateInstance<WiloDraftNoteSO>();
            tmp.title    = title ?? "";
            tmp.message  = message ?? "";
            tmp.refGuids = new List<string>(refs ?? new List<string>());
            Save(tmp);
            Object.DestroyImmediate(tmp);
        }

        /// <summary>
        /// Attempts to load the stored draft from the current session.
        /// </summary>
        /// <param name="title">Loaded title (empty if none).</param>
        /// <param name="message">Loaded message (empty if none).</param>
        /// <param name="refs">Loaded GUID list (empty list if none).</param>
        /// <returns><c>true</c> if session data existed; otherwise <c>false</c>.</returns>
        public static bool TryLoad(out string title, out string message, out List<string> refs)
        {
            title = ""; message = ""; refs = null;
            var json = SessionState.GetString(Key, null);
            if (string.IsNullOrEmpty(json)) return false;

            var dto = new Dto();
            EditorJsonUtility.FromJsonOverwrite(json, dto);
            title = dto.title ?? "";
            message = dto.message ?? "";
            refs = dto.refs ?? new List<string>();
            return true;
        }

        /// <summary>
        /// Clears the draft data from the current session storage.
        /// </summary>
        public static void Clear() => SessionState.EraseString(Key);
    }
}
