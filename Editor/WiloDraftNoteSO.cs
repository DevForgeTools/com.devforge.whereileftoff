// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System.Collections.Generic;
using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// ScriptableObject that stores a draft note:
    /// title, message, and related asset GUIDs.
    /// </summary>
    public class WiloDraftNoteSO : ScriptableObject
    {
        /// <summary>Short note title.</summary>
        public string title;

        /// <summary>Free-form note content.</summary>
        public string message;

        /// <summary>
        /// Optional list of related asset GUIDs
        /// (e.g., scenes, prefabs, materials).
        /// </summary>
        public List<string> refGuids = new List<string>();

        /// <summary>
        /// Sets the draft title and message.
        /// </summary>
        /// <param name="newTitle">New title.</param>
        /// <param name="newMessage">New content.</param>
        public void Set(string newTitle, string newMessage)
        {
            title = newTitle;
            message = newMessage;
        }
    }
}