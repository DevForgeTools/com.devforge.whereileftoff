// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Utility helpers for WILO:
    /// - Session-scoped ID management
    /// - String normalization
    /// - Asset GUID retrieval and SerializedProperty helpers
    /// - Project-scoped key names
    /// - Thin wrappers around the per-user read-state store
    /// </summary>
    public class WiloUtilities
    {
        /// <summary>
        /// SessionState key for the current Editor session identifier.
        /// </summary>
        private const string SessionKey = "WILO_SessionId";
        
        /// <summary>
        /// Gets the current session id for the Editor process, creating and caching a new one if missing.
        /// </summary>
        /// <returns>Stable GUID (32 hex chars) for the current Editor session.</returns>
        public static string GetOrCreateSessionId()
        {
            var id = SessionState.GetString(SessionKey, "");
            
            if (!string.IsNullOrEmpty(id)) return id;
            
            id = Guid.NewGuid().ToString("N");
            SessionState.SetString(SessionKey, id);
            return id;
        }
        
        /// <summary>
        /// Normalizes a string for comparisons: trims and converts to lower-invariant.
        /// Returns empty string when input is null/whitespace.
        /// </summary>
        public static string NormalizeString(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            return s.Trim().ToLowerInvariant();
        }
        
        /// <summary>
        /// Attempts to get the GUID for any project asset.
        /// </summary>
        /// <param name="obj">Unity object to resolve.</param>
        /// <param name="guid">Resolved asset GUID (if any).</param>
        /// <returns><c>true</c> if a non-empty GUID was resolved; otherwise <c>false</c>.</returns>
        public static bool TryGetAssetGuid(UnityEngine.Object obj, out string guid)
        {
            guid = null;
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) return false;
            guid = AssetDatabase.AssetPathToGUID(path);
            return !string.IsNullOrEmpty(guid);
        }

        /// <summary>
        /// Adds a GUID to a <see cref="SerializedProperty"/> array (of strings) without duplicates.
        /// </summary>
        /// <param name="pGuids">Serialized array property (string[]).</param>
        /// <param name="existing">HashSet with the current GUIDs for fast lookups.</param>
        /// <param name="guid">GUID to insert.</param>
        /// <returns><c>true</c> if the GUID was added; otherwise <c>false</c> (null/empty or already present).</returns>
        public static bool TryAddGuid(SerializedProperty pGuids, HashSet<string> existing, string guid)
        {
            if (string.IsNullOrEmpty(guid) || existing.Contains(guid)) return false;
            int i = pGuids.arraySize;
            pGuids.InsertArrayElementAtIndex(i);
            pGuids.GetArrayElementAtIndex(i).stringValue = guid;
            existing.Add(guid);
            return true;
        }
        
        /// <summary>
        /// Builds a project-scoped key for <see cref="EditorPrefs"/> using a stable per-project hash.
        /// </summary>
        /// <param name="localKey">Local suffix/key.</param>
        /// <returns>Unique key for the current project.</returns>
        public static string ProjectKey(string localKey)
        {
            int pid = Application.dataPath.GetHashCode();
            return $"WILO_{pid}_{localKey}";
        }
        
        // === WILO: per-user read state (wraps WiloReadStateStore) ===

        /// <summary>
        /// Returns whether the given note is marked as read for the current user+project.
        /// </summary>
        public static bool IsRead(WiloNote n)
        {
            return n != null && WiloReadStateStore.IsRead(n.utc);
        }

        /// <summary>
        /// Sets or clears the "read" flag for the given note (current user+project).
        /// </summary>
        public static void SetRead(WiloNote n, bool v)
        {
            if (n == null) return;
            WiloReadStateStore.SetRead(n.utc, v);
        }

        /// <summary>
        /// Bulk setter for the "read" flag on a collection of notes (current user+project).
        /// </summary>
        public static void SetReadBulk(IEnumerable<WiloNote> notes, bool v)
        {
            if (notes == null) return;
            var utcs = new List<string>();
            foreach (var n in notes) if (n != null && !string.IsNullOrEmpty(n.utc)) utcs.Add(n.utc);
            WiloReadStateStore.SetReadBulk(utcs, v);
        }

        /// <summary>
        /// Returns whether the given note is marked as "re-read" (to revisit) for the current user+project.
        /// </summary>
        public static bool IsReRead(WiloNote n)
        {
            return n != null && WiloReadStateStore.IsReRead(n.utc);
        }

        /// <summary>
        /// Sets or clears the "re-read" flag for the given note (current user+project).
        /// </summary>
        public static void SetReRead(WiloNote n, bool v)
        {
            if (n == null) return;
            WiloReadStateStore.SetReRead(n.utc, v);
        }

        /// <summary>
        /// Shortcut to preferences
        /// </summary>
        public static void OpenWiloPreferences()
        {
            SettingsService.OpenUserPreferences("Preferences/Dev Forge/Where I Left Off");
        }
        
    }
}
