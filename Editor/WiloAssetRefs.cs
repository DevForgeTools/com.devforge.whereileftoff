// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Helpers to work with Unity assets via GUIDs:
    /// - Resolve GUIDs for assets and scene-related objects.
    /// - Load assets by GUID.
    /// - Get a readable label and icon for display.
    /// - Collect GUIDs from the current selection (Project + Scene/PrefabStage context).
    /// </summary>
    internal static class WiloAssetRefs
    {
        /// <summary>
        /// Tries to obtain the asset GUID for the given <paramref name="obj"/>.
        /// Returns <c>true</c> if the object is an asset (not a scene object) and a non-empty GUID was resolved.
        /// </summary>
        public static bool TryGetGuid(Object obj, out string guid)
        {
            guid = null;
            if (!obj) return false;

            // Reject scene objects (only assets have an asset path).
            var assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath)) return false;

            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out _)
                   && !string.IsNullOrEmpty(guid);
        }

        /// <summary>
        /// Loads an asset by its GUID. Returns <c>null</c> if not found.
        /// </summary>
        public static Object LoadByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAssetAtPath<Object>(path);
        }

        /// <summary>
        /// Builds a <see cref="GUIContent"/> label and resolves the asset icon for a given GUID.
        /// </summary>
        public static (GUIContent label, Texture icon) GetLabelAndIcon(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = string.IsNullOrEmpty(path) ? "(missing)" : System.IO.Path.GetFileName(path);
            var icon = string.IsNullOrEmpty(path) ? null : AssetDatabase.GetCachedIcon(path);
            return (new GUIContent(name, icon), icon);
        }

        /// <summary>
        /// Returns all distinct GUIDs represented by the current selection:
        /// - Direct Project assets (by GUID).
        /// - For selected scene objects: the scene asset, the original prefab (if instance),
        ///   and, when in Prefab Stage, the stage prefab asset.
        /// </summary>
        public static List<string> GetGuidsFromCurrentSelection()
        {
            var result = new HashSet<string>();
            var objs = Selection.objects;
            if (objs == null || objs.Length == 0) return new List<string>();

            foreach (var obj in objs)
            {
                if (!obj) continue;

                // 1) Project asset → direct GUID
                var assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var assetGuid, out _) &&
                        !string.IsNullOrEmpty(assetGuid))
                    {
                        result.Add(assetGuid);
                    }
                    continue;
                }

                // 2) Scene object → scene GUID + prefab source (if instance) + prefab stage asset (if applicable)
                if (obj is GameObject go)
                {
                    // 2.a) Scene asset
                    var scenePath = go.scene.path;
                    if (!string.IsNullOrEmpty(scenePath))
                    {
                        var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                        if (!string.IsNullOrEmpty(sceneGuid)) result.Add(sceneGuid);
                    }

                    // 2.b) Prefab source
                    if (PrefabUtility.IsPartOfPrefabInstance(go))
                    {
                        var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
                        if (src)
                        {
                            var srcPath = AssetDatabase.GetAssetPath(src);
                            if (!string.IsNullOrEmpty(srcPath))
                            {
                                var srcGuid = AssetDatabase.AssetPathToGUID(srcPath);
                                if (!string.IsNullOrEmpty(srcGuid)) result.Add(srcGuid);
                            }
                        }
                    }

#if UNITY_2018_3_OR_NEWER
                    // 2.c) Prefab Stage root asset
                    var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                    if (stage != null && go.scene.IsValid() && go.scene == stage.scene)
                    {
                        var stageGuid = AssetDatabase.AssetPathToGUID(stage.assetPath);
                        if (!string.IsNullOrEmpty(stageGuid)) result.Add(stageGuid);
                    }
#endif
                }
            }

            return result.ToList();
        }
    }
}
