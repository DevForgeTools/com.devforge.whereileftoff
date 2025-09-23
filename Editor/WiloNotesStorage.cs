// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Persistence utilities for WILO notes.
    /// Handles folder creation, JSON save/load, and basic retrieval helpers.
    /// </summary>
    public class WiloNotesStorage
    {
        /// <summary>
        /// Ensures the notes folder exists on disk.
        /// </summary>
        public static void EnsureNotesFolder()
        {
            Directory.CreateDirectory(WiloPaths.NotesFolder);
        }
        
        /// <summary>
        /// Serializes and saves the given notes file as JSON at the configured path.
        /// </summary>
        /// <param name="file">Notes container to save.</param>
        public static void SaveNotes(WiloNotesFile file)
        {
            EnsureNotesFolder();
            var json = JsonUtility.ToJson(file, prettyPrint: true);
            File.WriteAllText(WiloPaths.NotesFile, json);
        }
        
        /// <summary>
        /// Loads the notes file from disk (if present). Returns an empty container if missing or invalid.
        /// </summary>
        /// <returns>A deserialized <see cref="WiloNotesFile"/> or an empty one.</returns>
        public static WiloNotesFile LoadNotes()
        {
            // Placeholder comment to compile (kept intentionally)
            EnsureNotesFolder();
            
            if(!File.Exists(WiloPaths.NotesFile))
                return new WiloNotesFile();
            
            var json = File.ReadAllText(WiloPaths.NotesFile);
            var data = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<WiloNotesFile>(json);
            
            return data ?? new WiloNotesFile();;
        }
        
        /// <summary>
        /// Returns the last note (stub placeholder). Update with actual retrieval if needed.
        /// </summary>
        /// <returns>A new <see cref="WiloNote"/> instance.</returns>
        public static WiloNote LoadLastNote()
        {
            return new WiloNote();
        }
    }
}
