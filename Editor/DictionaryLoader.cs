// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// JSON table model for localized strings loaded from Resources.
    /// Expected schema: { "entries": [ { "key": "...", "value": "..." }, ... ] }.
    /// </summary>
    [System.Serializable]
    internal class StringTable
    {
        /// <summary>List of key-value entries parsed from the JSON file.</summary>
        public List<Entry> entries;

        /// <summary>Single key-value pair.</summary>
        [System.Serializable]
        public class Entry
        {
            public string key;
            public string value;
        }
    }

    /// <summary>
    /// String dictionary loader and accessor for editor UI localization.
    /// Chooses the dictionary based on <see cref="Locale.IsSpanish"/> and provides a simple T() lookup.
    /// </summary>
    internal static class Strings
    {
        static Dictionary<string, string> _map;

        /// <summary>
        /// Loads the localized string table from Resources at editor domain load.
        /// It looks for a <see cref="TextAsset"/> named "wilo_dictionary.es" or "wilo_dictionary.en".
        /// </summary>
        [InitializeOnLoadMethod]
        static void Load()
        {
            var baseName = Locale.IsSpanish ? "wilo_dictionary.es" : "wilo_dictionary.en";
            var text = Resources.Load<TextAsset>(baseName);

            _map = new Dictionary<string, string>(128);

            if (text == null)
            {
                Debug.LogWarning($"[WILO] Resources/{baseName} not found (ensure a TextAsset in a 'Resources' folder, name without extension).");
                return;
            }

            var table = JsonUtility.FromJson<StringTable>(text.text);
            if (table?.entries == null) return;

            foreach (var e in table.entries)
            {
                if (!string.IsNullOrEmpty(e.key))
                    _map[e.key] = e.value ?? string.Empty;
            }
        }

        /// <summary>
        /// Forces a reload of the current dictionary from disk (useful after editing the JSON).
        /// </summary>
        public static void Reload()
        {
            _map = null;
            Load();
        }

        /// <summary>
        /// Translates the given <paramref name="key"/> using the loaded dictionary.
        /// Returns <paramref name="fallback"/> if provided, otherwise returns the key itself when not found.
        /// </summary>
        /// <param name="key">Lookup key, e.g. "btn.save".</param>
        /// <param name="fallback">Optional text to use if the key is missing or empty.</param>
        public static string T(string key, string fallback = null)
        {
            // Lazy load in case domain reload was disabled or Load() didn't run.
            if (_map == null) Load();

            if (_map != null && _map.TryGetValue(key, out var val) && !string.IsNullOrEmpty(val))
                return val;

            return fallback ?? key;
        }
    }
}
