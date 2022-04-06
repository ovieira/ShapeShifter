using System.Collections.Generic;
using System.IO;
using System.Linq;
using Miniclip.ShapeShifter.Skinner;
using Miniclip.ShapeShifter.Utils;
using UnityEditor;

namespace Miniclip.ShapeShifter.Saver
{
    static class ExternalAssetsPostProcessor
    {
        private static readonly Queue<ModifiedAssetInfo> externalModifiedAssetsQueue = new Queue<ModifiedAssetInfo>();

        private static readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();

        internal static void InitializeWatchers()
        {
            List<string> paths = ShapeShifterConfiguration.Instance.SkinnedExternalAssetPaths;

            EditorApplication.update += OnEditorUpdate;

            foreach (string path in paths)
            {
                string fullPath = PathUtils.GetFullPath(path);
                FileSystemWatcher watcher = new FileSystemWatcher();
                string directoryName = Path.GetDirectoryName(fullPath);
                string fileName = Path.GetFileName(fullPath);

                watcher.Path = directoryName;
                watcher.Filter = fileName;
                watcher.EnableRaisingEvents = true;
                watcher.Changed += OnSkinnedExternalFileChanged;
                watchers.Add(watcher);
            }
        }

        internal static void PauseWatchers()
        {
            for (int index = 0; index < watchers.Count; index++)
            {
                FileSystemWatcher fileSystemWatcher = watchers[index];
                fileSystemWatcher.Changed -= OnSkinnedExternalFileChanged;
                fileSystemWatcher.Dispose();
            }

            watchers.Clear();
        }

        internal static void ResumeWatchers()
        {
            InitializeWatchers();
        }

        private static void OnEditorUpdate()
        {
            if (externalModifiedAssetsQueue.Count == 0)
            {
                return;
            }

            while (externalModifiedAssetsQueue.Count > 0)
            {
                ModifiedAssetInfo modifiedAssetInfo = externalModifiedAssetsQueue.Dequeue();

                if (modifiedAssetInfo == null)
                {
                    continue;
                }

                string relativePath = ExternalAssetSkinner.ConvertToRelativePath(modifiedAssetInfo.assetPath);
                string skinnedVersionPath = Path.Combine(
                    ShapeShifter.ActiveGameSkin.ExternalSkinsFolderPath,
                    ExternalAssetSkinner.GenerateKeyFromRelativePath(relativePath),
                    Path.GetFileName(modifiedAssetInfo.assetPath)
                );

                if (IsDifferent(modifiedAssetInfo.assetPath, skinnedVersionPath))
                {
                    UnsavedAssetsManager.Add(modifiedAssetInfo);
                }
                else
                {
                    UnsavedAssetsManager.RemoveByPath(modifiedAssetInfo.assetPath);
                }
            }
        }

        private static void OnSkinnedExternalFileChanged(object sender, FileSystemEventArgs args)
        {
            ModifiedAssetInfo modifiedAssetInfo = new ModifiedAssetInfo(
                args.FullPath,
                ModificationType.Modified,
                SkinType.External
            );

            modifiedAssetInfo.description = args.Name;

            if (externalModifiedAssetsQueue.Any(assetInfo => assetInfo.assetPath == modifiedAssetInfo.assetPath))
            {
                return;
            }

            externalModifiedAssetsQueue.Enqueue(modifiedAssetInfo);
        }

        private static bool IsDifferent(string argsFullPath, string skinnedVersionPath)
        {
            var bytes1 = File.ReadAllBytes(argsFullPath);
            var bytes2 = File.ReadAllBytes(skinnedVersionPath);

            return !bytes1.SequenceEqual(bytes2);
        }
    }
}