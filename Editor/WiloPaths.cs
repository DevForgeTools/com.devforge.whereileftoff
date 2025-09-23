// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System.IO;
using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Centralized filesystem paths used by WILO for local persistence.
    /// Keeps paths consistent across the editor codebase.
    /// </summary>
    public static class WiloPaths
    {
        /// <summary>
        /// Absolute path to the Unity project root (parent folder of <c>Assets</c>).
        /// Example: <c>/path/to/MyProject</c>.
        /// </summary>
        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        /// <summary>
        /// Absolute path to WILO's working folder inside the Unity <c>Library</c>.
        /// Example: <c>{ProjectRoot}/Library/WhereILeftOff</c>.
        /// </summary>
        public static string NotesFolder => Path.Combine(ProjectRoot, "Library", "WhereILeftOff");

        /// <summary>
        /// Absolute path to the JSON file that stores the notes.
        /// Example: <c>{NotesFolder}/WhereILeftOffNotes.json</c>.
        /// </summary>
        public static string NotesFile   => Path.Combine(NotesFolder, "WhereILeftOffNotes.json");
    }
}