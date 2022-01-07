using System.IO;
using System.Linq;
using Miniclip.ShapeShifter.Utils;

namespace Miniclip.ShapeShifter
{
    class AssetSkin
    {
        public AssetSkin(string guid, string skinnedFileContainerFullPath)
        {
            Guid = guid;
            SkinnedFileContainerFullPath = skinnedFileContainerFullPath;
        }

        internal string Guid { get; set; }

        internal string SkinnedFileContainerFullPath { get; set; }

        public bool IsValid() => !IOUtils.IsFolderEmpty(SkinnedFileContainerFullPath);

        public void Rename(string newName)
        {
            if (PathUtils.IsInternalPath(newName))
            {
                newName = Path.GetFileName(newName) ?? Path.GetDirectoryName(newName);
            }

            DirectoryInfo directoryInfo = new DirectoryInfo(SkinnedFileContainerFullPath);

            int totalDirectories = directoryInfo.EnumerateDirectories().Count();
            int totalFiles = directoryInfo.EnumerateFiles().Count();

            string newFullPath = string.Empty;

            if (totalDirectories > 0)
            {
                string skinnedFolderFullPath = directoryInfo.GetDirectories().FirstOrDefault().FullName;
                string oldName = new DirectoryInfo(skinnedFolderFullPath).Name;

                newFullPath = skinnedFolderFullPath.Replace(oldName, newName);

                Directory.Move(skinnedFolderFullPath, newFullPath);
                File.Move(skinnedFolderFullPath + ".meta", newFullPath + ".meta");
                return;
            }

            if (totalFiles > 0)
            {
                string skinnedFileFullPath = directoryInfo.GetFiles().FirstOrDefault().FullName;
                string oldName = Path.GetFileName(skinnedFileFullPath);

                newFullPath = skinnedFileFullPath.Replace(oldName, newName);

                File.Move(skinnedFileFullPath, newFullPath);
                File.Move(skinnedFileFullPath + ".meta", newFullPath + ".meta");
                return;
            }

            ShapeShifterLogger.LogError($"Empty skin contents on {SkinnedFileContainerFullPath}");
        }

        public void Stage()
        {
            GitUtils.Stage(SkinnedFileContainerFullPath);
        }
    }
}