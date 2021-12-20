using System.IO;

namespace Miniclip.ShapeShifter
{
    public partial class ShapeShifter
    {
#region Unity Folder
        public void RegisterModifiedAssetInUnity(string modifiedAssetPath)
        {
            if (configuration.ModifiedAssetPaths.Contains(modifiedAssetPath))
                return;

            if (!IsSkinned(modifiedAssetPath))
            {
                if (TryGetParentSkinnedFolder(modifiedAssetPath, out string skinnedFolderPath))
                {
                    RegisterModifiedAssetInUnity(skinnedFolderPath);
                }

                return;
            }

            configuration.ModifiedAssetPaths.Add(modifiedAssetPath);
        }

        private bool TryGetParentSkinnedFolder(string assetPath, out string skinnedParentFolderPath)
        {
            if (assetPath == "Assets/")
            {
                skinnedParentFolderPath = null;
                return false;
            }

            string[] parentFolders = assetPath.Split('/');

            for (int index = parentFolders.Length - 1; index >= 0; index--)
            {
                string parentFolder = string.Join("/", parentFolders, 0, index);

                if (IsSkinned(parentFolder))
                {
                    skinnedParentFolderPath = parentFolder;
                    return true;
                }
            }

            skinnedParentFolderPath = null;
            return false;
        }
#endregion

#region Skins Folder
        private void StartWatchingFolder(string pathToWatch) =>
            FileSystemWatcherManager.AddPathToWatchlist(pathToWatch, OnFileChanged);

        private void StopWatchingFolder(string pathToUnwatch) =>
            FileSystemWatcherManager.RemovePathFromWatchlist(pathToUnwatch);

        private void ClearAllWatchedPaths() => FileSystemWatcherManager.RemoveAllPathsFromWatchlist();

        private void OnFileChanged(object sender, FileSystemEventArgs args)
        {
            DirectoryInfo assetDirectory = new DirectoryInfo(Path.GetDirectoryName(args.FullPath));
            string game = assetDirectory.Parent.Parent.Name;
            string key = this.GenerateAssetKey(game, assetDirectory.Name);
            this.dirtyAssets.Add(key);
        }
#endregion
    }
}