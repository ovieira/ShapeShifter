﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using Miniclip.ShapeShifter.Utils;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Miniclip.ShapeShifter {

    public partial class ShapeShifter {

        private static Editor externalConfigurationEditor;
        private int selectedExternalAsset;
        private bool showExternalSkinner = true;

        private string DetermineRecommendedPath() => GitUtils.RepositoryPath;

        private void DrawSkinnedExternalAssetSection(string relativePath) {
            GUIStyle boxStyle = GUI.skin.GetStyle("Box");
            
            using (new GUILayout.HorizontalScope(boxStyle)) {
                foreach (string game in Configuration.GameNames) {
                    string key = this.GenerateKeyFromRelativePath(relativePath);
                    string assetPath = Path.Combine(
                        SkinsFolder.FullName,
                        game,
                        ExternalAssetsFolder,
                        key,
                        Path.GetFileName(relativePath)
                    );

                    this.GenerateAssetPreview(key, assetPath);
                    this.DrawAssetPreview(key, game, assetPath);
                }
            }

            Color oldColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.red;
                
            if (GUILayout.Button("Remove skins")) {
                this.RemoveExternalSkins(relativePath);
            }

            GUI.backgroundColor = oldColor;
        }

        private string GenerateKeyFromRelativePath(string relativePath) {
            return WebUtility.UrlEncode(relativePath).Replace(".", "{dot}");
        }

        private static string GenerateRelativePathFromKey(string key) {
            return WebUtility.UrlDecode(key).Replace("{dot}", ".");
        }

        // since Path.GetRelativePath doesn't seem to be available
        private string GetRelativeURIPath(string absolutePath, string relativeTo) {
            if (! relativeTo.EndsWith("/")) {
                relativeTo += "/";  
            }

            Uri assetPathIdentifier = new Uri(absolutePath);
            Uri relativeToPathIdentifier = new Uri(relativeTo);
            return relativeToPathIdentifier.MakeRelativeUri(assetPathIdentifier).ToString();
        }
        
        private void OnExternalAssetSkinnerGUI() {
            this.showExternalSkinner = EditorGUILayout.Foldout(
                this.showExternalSkinner,
                "External Asset Skinner"
            );

            if (! this.showExternalSkinner) {
                return;
            }

            GUIStyle boxStyle = GUI.skin.GetStyle("Box");
            GUIStyle buttonStyle = GUI.skin.GetStyle("Button");

            using (new GUILayout.VerticalScope(boxStyle)) {
                int count = Configuration.SkinnedExternalAssetPaths.Count;
                
                if (count > 0) {
                    this.selectedExternalAsset = GUILayout.SelectionGrid(
                        this.selectedExternalAsset,
                        Configuration.SkinnedExternalAssetPaths.ToArray(),
                        2,
                        buttonStyle
                    );

                    if (this.selectedExternalAsset >= 0 && this.selectedExternalAsset < count) {
                        string relativePath = Configuration.SkinnedExternalAssetPaths[this.selectedExternalAsset];
                        this.DrawSkinnedExternalAssetSection(relativePath);
                    }
                }

                if (GUILayout.Button("Skin external file")) {
                    this.SkinExternalFile();
                }
            }
        }

        private string PickFile(string recommendedPath) {
            string assetPath = EditorUtility.OpenFilePanel(
                "Pick a file, any file!",
                recommendedPath,
                string.Empty
            );

            if (string.IsNullOrEmpty(assetPath)) {
                return null;
            }
            
            if (! assetPath.Contains(recommendedPath) && ! EditorUtility.DisplayDialog (
                "Shape Shifter",
                $"The chosen asset is outside of the recommended path ({recommendedPath}). Are you sure?",
                "Yeah, go for it!",
                "Hmm... not sure, let me check!"
            )) {
                return null;
            }

            return assetPath;
        }

        private void RemoveExternalSkins(string relativePath) {
            string key = this.GenerateKeyFromRelativePath(relativePath);
            
            foreach (string game in Configuration.GameNames) {
                dirtyAssets.Remove(key);
                previewPerAsset.Remove(key);
            
                string assetFolder = Path.Combine(
                    SkinsFolder.FullName,
                    game,
                    ExternalAssetsFolder,
                    key
                );
            
                Directory.Delete(assetFolder, true);                
            }

            Configuration.SkinnedExternalAssetPaths.Remove(relativePath);
        }

        private void SkinExternalFile() {
            string recommendedPath = this.DetermineRecommendedPath();
            string absoluteAssetPath = this.PickFile(recommendedPath);

            this.SkinExternalFile(absoluteAssetPath);
        }

        private void SkinExternalFile(string absoluteAssetPath, Dictionary<string, string> overridesPerGame = null) {
            if (absoluteAssetPath == null) {
                return;
            }

            string relativeAssetPath = this.GetRelativeURIPath(absoluteAssetPath, Application.dataPath);
            
            if (Configuration.SkinnedExternalAssetPaths.Contains(relativeAssetPath)) {
                EditorUtility.DisplayDialog(
                    "Shape Shifter",
                    $"Could not skin: {relativeAssetPath}. It was already skinned.",
                    "Oops!"
                );
                
                return;
            }
            
            Configuration.SkinnedExternalAssetPaths.Add(relativeAssetPath);
            
            // even though it's an "external" file, it still might be a Unity file (ex: ProjectSettings), so it's
            // still important to make sure any pending changes are saved before generating copies
            SavePendingChanges();

            string origin = absoluteAssetPath;
            string key = this.GenerateKeyFromRelativePath(relativeAssetPath);
            
            foreach (string game in Configuration.GameNames) {
                string assetFolder = Path.Combine(
                    SkinsFolder.FullName,
                    game, 
                    ExternalAssetsFolder,
                    key
                );
                
                if (!Directory.Exists(assetFolder)) {
                    Directory.CreateDirectory(assetFolder);
                }

                string target = Path.Combine(assetFolder, Path.GetFileName(origin));

                if (overridesPerGame != null && overridesPerGame.ContainsKey(game)) {
                    origin = overridesPerGame[game];
                }
                
                IOUtils.CopyFile(origin, target);
            }
        }
    }
}