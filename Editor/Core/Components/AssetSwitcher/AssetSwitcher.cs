using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Miniclip.ShapeShifter.Skinner;
using Miniclip.ShapeShifter.Utils;
using Miniclip.ShapeShifter.Utils.Git;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Miniclip.ShapeShifter.Switcher
{
    public static class AssetSwitcher
    {
        internal static void RestoreMissingAssets()
        {
            ShapeShifterUtils.DeleteDSStoreFiles();
            List<string> missingAssets = new List<string>();
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (ShapeShifter.ActiveGameSkin.HasInternalSkins())
            {
                List<AssetSkin> assetSkins = ShapeShifter.ActiveGameSkin.GetAssetSkins();

                foreach (AssetSkin assetSkin in assetSkins)
                {
                    if (!assetSkin.IsValid())
                    {
                        //Delete asset skin folder?
                        // assetSkin.Delete();
                    }

                    string assetDatabasePath = "";

                    if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(assetSkin.Guid)))
                    {
                        assetDatabasePath = PathUtils.GetFullPath(AssetDatabase.GUIDToAssetPath(assetSkin.Guid));
                    }

                    string assetGitIgnorePath = PathUtils.GetFullPath(GitIgnore.GetIgnoredPathByGuid(assetSkin.Guid));

                    if (!PathUtils.ArePathsEqual(assetDatabasePath, assetGitIgnorePath))
                    {
                        missingAssets.Add(assetSkin.Guid);
                        continue;
                    }

                    if (!PathUtils.FileOrDirectoryExists(assetDatabasePath))
                    {
                        missingAssets.Add(assetSkin.Guid);
                    }
                }

                if (missingAssets.Count == 0)
                {
                    ShapeShifterLogger.Log("Nothing to sync from skins folder.");
                }
                else
                {
                    PerformCopiesWithTracking(
                        ShapeShifter.ActiveGameSkin,
                        "Add missing skins",
                        CopyIfMissingInternal,
                        CopyFromSkinnedExternalToOrigin
                    );
                    stopwatch.Stop();
                    ShapeShifterLogger.Log(
                        missingAssets.Count > 0
                            ? $"Synced {missingAssets.Count} assets in {stopwatch.Elapsed.TotalSeconds} seconds"
                            : "Nothing to retrieve."
                    );
                }

                stopwatch.Stop();
            }
        }

        private static void CopyFromOriginToSkinnedExternal(DirectoryInfo directory)
        {
            string relativePath = ExternalAssetSkinner.GenerateRelativePathFromKey(directory.Name);
            string origin = Path.Combine(Application.dataPath, relativePath);
            string target = Path.Combine(directory.FullName, Path.GetFileName(origin));
            FileUtils.SafeCopy(origin, target);
        }

        private static void CopyFromSkinnedExternalToOrigin(DirectoryInfo directory)
        {
            string relativePath = ExternalAssetSkinner.GenerateRelativePathFromKey(directory.Name);
            string target = Path.Combine(Application.dataPath, relativePath);
            string searchPattern = Path.GetFileName(target);
            FileInfo[] fileInfos = directory.GetFiles(searchPattern);

            if (fileInfos.Length <= 0)
                return;

            FileInfo origin = fileInfos[0];
            FileUtils.SafeCopy(origin.FullName, target);
        }

        private static void CopyFromSkinsToUnity(DirectoryInfo directory)
        {
            string guid = directory.Name;

            // Ensure it has the same name, so we don't end up copying .DS_Store
            string target = AssetDatabase.GUIDToAssetPath(guid);
            string searchPattern = Path.GetFileName(target) + "*";

            FileInfo[] files = directory.GetFiles(searchPattern);

            if (files.Length > 0)
            {
                foreach (FileInfo fileInfo in files)
                {
                    if (fileInfo.Extension == ".meta")
                    {
                        FileUtils.SafeCopy(fileInfo.FullName, target + ".meta");
                    }
                    else
                    {
                        FileUtils.SafeCopy(fileInfo.FullName, target);
                    }
                }
            }

            DirectoryInfo[] directories = directory.GetDirectories();

            if (directories.Length > 0)
            {
                target = Path.Combine(
                    Application.dataPath.Replace("/Assets", string.Empty),
                    target
                );

                FileUtils.SafeCopy(directories[0].FullName, target);
            }
        }

        private static void CopyFromUnityToSkins(DirectoryInfo skinDirectory)
        {
            if (!FileUtils.DoesFolderExistAndHaveFiles(skinDirectory.FullName) && skinDirectory.Exists)
            {
                FileUtils.SafeDelete(skinDirectory.FullName);
                return;
            }

            string guid = skinDirectory.Name;
            string origin = AssetDatabase.GUIDToAssetPath(guid);

            string originFullPath = PathUtils.GetFullPath(origin);

            if (string.IsNullOrEmpty(origin))
            {
                ShapeShifterLogger.LogError(
                    $"Getting an empty path for guid {guid}. Can't push changes to skin folder."
                );
                return;
            }

            string target = Path.Combine(skinDirectory.FullName, Path.GetFileName(origin));

            if (AssetDatabase.IsValidFolder(origin))
            {
                if (!Directory.Exists(originFullPath))
                {
                    return;
                }

                DirectoryInfo originInfo = new DirectoryInfo(origin);
                DirectoryInfo targetInfo = new DirectoryInfo(target);
                FileUtils.SafeCopy(origin, target);
            }
            else
            {
                if (!File.Exists(originFullPath))
                {
                    return;
                }

                FileUtils.TryCreateDirectory(skinDirectory.FullName, true);
                FileUtils.SafeCopy(origin, target);
                FileUtils.SafeCopy(origin + ".meta", target + ".meta");
            }

            string game = skinDirectory.Parent.Parent.Name;
            string key = ShapeShifterUtils.GenerateUniqueAssetSkinKey(game, guid);

            ShapeShifter.DirtyAssets.Add(key);
        }

        internal static void OverwriteSelectedSkin(GameSkin selected, bool forceOverwrite = false)
        {
            ShapeShifterUtils.SavePendingChanges();

            string name = selected.Name;

            if (ShapeShifter.ActiveGameSkin != selected)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append($"This will overwrite the {name} skins with the current assets. ");

                stringBuilder.Append($"The last asset switch was to {ShapeShifter.ActiveGameName}");

                stringBuilder.Append(" Are you sure?");

                if (!forceOverwrite
                    && !EditorUtility.DisplayDialog(
                        "Shape Shifter",
                        stringBuilder.ToString(),
                        "Yeah, I'm sure, go ahead.",
                        "Wait, what? No, stop!"
                    ))
                {
                    return;
                }
            }

            PerformCopiesWithTracking(
                selected,
                "Overwrite selected skin",
                CopyFromUnityToSkins,
                CopyFromOriginToSkinnedExternal
            );

            ShapeShifterConfiguration.Instance.SetDirty(false);
        }

        private static void PerformCopiesWithTracking(GameSkin selected,
            string description,
            Action<DirectoryInfo> internalAssetOperation,
            Action<DirectoryInfo> externalAssetOperation)
        {
            ShapeShifterLogger.Log($"{description}: {selected.Name}");

            string gameFolderPath = selected.MainFolderPath;

            if (Directory.Exists(gameFolderPath))
            {
                int totalDirectories = Directory.EnumerateDirectories(
                        gameFolderPath,
                        "*",
                        SearchOption.AllDirectories
                    )
                    .Count();

                float progress = 0.0f;
                float progressBarStep = 1.0f / totalDirectories;
                Debug.Log("##! 5");

                PerformOperationOnPath(
                    gameFolderPath,
                    ShapeShifterConstants.INTERNAL_ASSETS_FOLDER,
                    internalAssetOperation,
                    description,
                    progressBarStep,
                    ref progress
                );
                Debug.Log("##! 6");

                PerformOperationOnPath(
                    gameFolderPath,
                    ShapeShifterConstants.EXTERNAL_ASSETS_FOLDER,
                    externalAssetOperation,
                    description,
                    progressBarStep,
                    ref progress
                );
                Debug.Log("##! 7");

                RefreshAllAssets();
                Debug.Log("##! 8");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Shape Shifter",
                    $"Could not {description.ToLower()}: {selected.Name}. Skins folder does not exist!",
                    "Fine, I'll take a look."
                );
            }

            EditorUtility.ClearProgressBar();
        }

        private static void PerformOperationOnPath(string gameFolderPath,
            string assetFolder,
            Action<DirectoryInfo> operation,
            string description,
            float progressBarStep,
            ref float progress)
        {
            string assetFolderPath = Path.Combine(gameFolderPath, assetFolder);

            if (Directory.Exists(assetFolderPath))
            {
                DirectoryInfo internalFolder = new DirectoryInfo(assetFolderPath);

                foreach (DirectoryInfo directory in internalFolder.GetDirectories())
                {
                    operation(directory);

                    progress += progressBarStep;
                    EditorUtility.DisplayProgressBar("Shape Shifter", $"{description}...", progress);
                }
            }
        }

        [MenuItem("Window/Shape Shifter/Refresh All Assets", false, 72)]
        private static void RefreshAllAssets()
        {
            if (HasAnyPackageRelatedSkin() && !Application.isBatchMode)
            {
                ForceUnityToLoseAndRegainFocus();

                //try  EditorUtility.RequestScriptReload();
            }

            AssetDatabase.Refresh();
        }

        private static bool HasAnyPackageRelatedSkin()
        {
            bool isManifestSkinned = ShapeShifterConfiguration.Instance.SkinnedExternalAssetPaths.Any(
                externalAssetPath => externalAssetPath.Contains("manifest.json")
            );

            return isManifestSkinned;
        }

        private static void ForceUnityToLoseAndRegainFocus()
        {
            // Force Unity to lose and regain focus, so it resolves any new changes on the packages
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments =
                        "-e 'tell application \"Finder\" to activate' -e 'delay 0.5' -e 'tell application \"Unity\" to activate'",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
        }

        internal static void SwitchToGame(GameSkin gameToSwitchTo, bool forceSwitch = false)
        {
            Debug.Log("##! 1");

            if (ShapeShifterConfiguration.Instance.IsDirty && !forceSwitch)
            {
                Debug.Log("##! 1.1");
                int choice = EditorUtility.DisplayDialogComplex(
                    "Shape Shifter",
                    "There are unsaved changes in your skinned assets. You should make sure to save them into your Active Game folder",
                    $"Save changes to {ShapeShifter.ActiveGameName} and switch to {gameToSwitchTo.Name}.",
                    "Cancel Switch",
                    $"Discard changes and switch to {gameToSwitchTo.Name}"
                );

                switch (choice)
                {
                    case 0:
                        OverwriteSelectedSkin(ShapeShifter.ActiveGameSkin);
                        break;

                    case 1:
                        return;

                    case 2:
                    default:
                        break;
                }
            }

            Debug.Log("##! 2");
            PerformCopiesWithTracking(
                gameToSwitchTo,
                "Switch to game",
                CopyFromSkinsToUnity,
                CopyFromSkinnedExternalToOrigin
            );
            Debug.Log("##! 3");
            ShapeShifter.ActiveGame = gameToSwitchTo.Name;
            Debug.Log("##! 4");
            ShapeShifterConfiguration.Instance.SetDirty(false);

            //TODO: ACPT-2843 make the code below optionable
            /*GameSkin gameSkin = ShapeShifter.ActiveGameSkin;
            
            foreach (AssetSkin assetSkin in gameSkin.GetAssetSkins())
            {
                string guid = assetSkin.Guid;
            
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }*/
        }

        private static void CopyIfMissingInternal(DirectoryInfo directory)
        {
            string guid = directory.Name;

            string assetPathFromAssetDatabase = PathUtils.GetFullPath(AssetDatabase.GUIDToAssetPath(guid));
            string assetPathFromGitIgnore = PathUtils.GetFullPath(GitIgnore.GetIgnoredPathByGuid(guid));

            //prioritize path from gitignore as is the only one version controlled
            string targetPath = assetPathFromGitIgnore;

            if (string.IsNullOrEmpty(targetPath))
            {
                ShapeShifterLogger.LogError($"Can't find Asset Path for guid: {guid}");
                return;
            }

            string assetFolder = Path.GetDirectoryName(targetPath);

            FileUtils.TryCreateDirectory(assetFolder);

            if (!string.Equals(assetPathFromAssetDatabase, assetPathFromGitIgnore))
            {
                if (PathUtils.FileOrDirectoryExists(assetPathFromAssetDatabase))
                {
                    //delete any file on AssetDatabasePath as is probably outdated and should not be there
                    FileUtils.SafeDelete(assetPathFromAssetDatabase);
                    FileUtils.SafeDelete(assetPathFromAssetDatabase + ".meta");
                }
            }

            string searchPattern = Path.GetFileName(PathUtils.NormalizePath(targetPath)) + "*";

            FileInfo[] files = directory.GetFiles(searchPattern);

            foreach (FileInfo fileInfo in files)
            {
                if (fileInfo.Extension == ".meta")
                {
                    string metaFile = assetFolder + ".meta";
                    if (File.Exists(PathUtils.GetFullPath(metaFile)))
                    {
                        continue;
                    }

                    ShapeShifterLogger.Log($"Retrieving: {metaFile}");
                    FileUtils.SafeCopy(fileInfo.FullName, metaFile);
                }
                else
                {
                    if (File.Exists(PathUtils.GetFullPath(targetPath)))
                    {
                        continue;
                    }

                    ShapeShifterLogger.Log($"Retrieving: {targetPath}");
                    FileUtils.SafeCopy(fileInfo.FullName, targetPath);
                }
            }

            DirectoryInfo[] directories = directory.GetDirectories();

            if (directories.Length > 0)
            {
                targetPath = Path.Combine(
                    Application.dataPath.Replace("/Assets", string.Empty),
                    targetPath
                );

                FileUtils.SafeCopy(directories[0].FullName, targetPath);
            }
        }

        public static void RefreshAsset(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return;

            GameSkin currentGameSkin = ShapeShifter.ActiveGameSkin;

            AssetSkin assetSkin = currentGameSkin.GetAssetSkin(guid);

            CopyFromSkinsToUnity(new DirectoryInfo(assetSkin.FolderPath));

            RefreshAllAssets();
        }
    }
}