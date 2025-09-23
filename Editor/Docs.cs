// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Documentation utilities: resolves the package root and the preferred README path.
    /// </summary>
    internal static class Docs
    {
        const string ReadmeENmd = "README.en.md";
        const string ReadmeEN   = "README.en";
        const string ReadmeESmd = "README.es.md";
        const string ReadmeES   = "README.es";

        /// <summary>Resolves the package root for this assembly (absolute path).</summary>
        internal static string GetPackageRoot()
        {
            var p = PackageInfo.FindForAssembly(typeof(Docs).Assembly);
            return (p != null && !string.IsNullOrEmpty(p.resolvedPath))
                ? p.resolvedPath.Replace('\\', '/')
                : null;
        }

        /// <summary>
        /// Returns the absolute path to the preferred README according to <see cref="Locale.IsSpanish"/>.
        /// Tries ES first when Spanish; EN first otherwise.
        /// </summary>
        internal static string GetPreferredReadmePath()
        {
            var root = GetPackageRoot();
            if (string.IsNullOrEmpty(root)) return null;

            var candidates = Locale.IsSpanish
                ? new[] { ReadmeESmd, ReadmeES, ReadmeENmd, ReadmeEN }
                : new[] { ReadmeENmd, ReadmeEN, ReadmeESmd, ReadmeES };

            foreach (var f in candidates)
            {
                var p = Path.Combine(root, f);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        /// <summary>
        /// Opens the preferred README in the OS default application (optional helper).
        /// </summary>
        internal static void OpenPreferredReadmeExternally()
        {
            var path = GetPreferredReadmePath();
            if (!string.IsNullOrEmpty(path)) EditorUtility.OpenWithDefaultApp(path);
            else EditorUtility.DisplayDialog("Documentation", "No README found in the package.", "OK");
        }
    }
}
