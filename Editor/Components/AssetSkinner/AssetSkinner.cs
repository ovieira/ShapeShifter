﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Miniclip.ShapeShifter.Utils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Miniclip.ShapeShifter {
   
    public static class AssetSkinner {
        
        
        private static void OnSelectionChange() {
            SharedInfo.DirtyAssets.Clear();
            SharedInfo.CachedPreviewPerAssetDict.Clear();
            ShapeShifter.ClearAllWatchedPaths();
        }

        public static void RemoveSkins(string assetPath) {
            foreach (string game in ShapeShifterConfiguration.Instance.GameNames) {
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                string key = ShapeShifterUtils.GenerateUniqueAssetSkinKey(game, guid);
                SharedInfo.DirtyAssets.Remove(key);
                SharedInfo.CachedPreviewPerAssetDict.Remove(key);
                
                string assetFolder = Path.Combine(
                    SharedInfo.SkinsFolder.FullName,
                    game,
                    SharedInfo.InternalAssetsFolder,
                    guid
                );

                ShapeShifter.StopWatchingFolder(assetFolder);
                Directory.Delete(assetFolder, true);
                GitUtils.Stage(assetFolder);
            }
            GitUtils.Track(assetPath);
        }

        private static void SkinAssets(string[] assetPaths, bool saveFirst = true)
        {
            if (saveFirst)
            {
                ShapeShifterUtils.SavePendingChanges();
            }

            foreach (string assetPath in assetPaths)
            {
                SkinAsset(assetPath);
            }
        }

        public static void SkinAsset(string assetPath, bool saveFirst = true)
        {
            if (saveFirst)
            {
                // make sure any pending changes are saved before generating copies
                ShapeShifterUtils.SavePendingChanges();
            }

            foreach (string game in ShapeShifterConfiguration.Instance.GameNames)
            {
                string origin = assetPath;
                string guid = AssetDatabase.AssetPathToGUID(origin);
                string assetFolder = Path.Combine(
                    SharedInfo.SkinsFolder.FullName,
                    game,
                    SharedInfo.InternalAssetsFolder,
                    guid
                );

                if (IsSkinned(origin, game))
                {
                    continue;
                }

                IOUtils.TryCreateDirectory(assetFolder, true);

                string target = Path.Combine(assetFolder, Path.GetFileName(origin));

                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    DirectoryInfo targetFolder = Directory.CreateDirectory(target);
                    IOUtils.CopyFolder(new DirectoryInfo(origin), targetFolder);
                    IOUtils.CopyFile(origin+".meta", target+".meta");

                }
                else
                {
                    
                    IOUtils.CopyFile(origin, target);
                    IOUtils.CopyFile(origin+".meta", target+".meta");
                }
                GitUtils.Stage(assetFolder);
            }
            
            GitUtils.Untrack(assetPath, true);
        }
        
        public static bool IsSkinned(string assetPath) => ShapeShifterConfiguration.Instance.GameNames.Any(game => IsSkinned(assetPath, game));

        private static bool IsSkinned(string assetPath, string game)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            
            if (string.IsNullOrEmpty(guid))
                return false;
            
            string assetFolder = Path.Combine(
                SharedInfo.SkinsFolder.FullName,
                game, 
                SharedInfo.InternalAssetsFolder,
                guid
            );

            return Directory.Exists(assetFolder) && !IOUtils.IsFolderEmpty(assetFolder);
        }
        
        private static void OnDisable() {
            SharedInfo.DirtyAssets.Clear();
            SharedInfo.CachedPreviewPerAssetDict.Clear();
        }
        
        private static IEnumerable<string> GetEligibleAssetPaths(Object[] assets)
        {
            IEnumerable<string> assetPaths =
                assets.Select(AssetDatabase.GetAssetPath);
            RemoveEmptyAssetPaths(ref assetPaths);
            RemoveDuplicatedAssetPaths(ref assetPaths);
            RemoveAlreadySkinnedAssets(ref assetPaths);
            return assetPaths;
        }
        
        private static void RemoveEmptyAssetPaths(ref IEnumerable<string> assetPaths) =>
            assetPaths = assetPaths.Where(assetPath => !string.IsNullOrEmpty(assetPath));

        private static void RemoveDuplicatedAssetPaths(ref IEnumerable<string> assetPaths) =>
            assetPaths = assetPaths.Distinct();

        private static void RemoveAlreadySkinnedAssets(ref IEnumerable<string> assetPaths) =>
            assetPaths = assetPaths.Where(assetPath => !IsSkinned(assetPath));
    }
}