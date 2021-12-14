﻿using UnityEditor;
using UnityEngine;

namespace Miniclip.ShapeShifter
{
    public class ShapeShifterAssetPostProcessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string importedAsset in importedAssets)
            {
                Debug.Log(importedAsset);
                string assetPath = importedAsset;

                if (!ShapeShifter.Instance.IsSkinned(importedAsset))
                {
                    if (ShapeShifter.Instance.TryGetParentSkinnedFolder(importedAsset, out string skinnedFolderPath))
                    {
                        ShapeShifter.Instance.RegisterModifiedAsset(skinnedFolderPath);
                    }

                    return;
                }

                ShapeShifter.Instance.RegisterModifiedAsset(assetPath);
            }
        }
    }
}