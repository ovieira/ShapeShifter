using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Miniclip.ShapeShifter.Extensions;
using Miniclip.ShapeShifter.Utils.Git;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Miniclip.ShapeShifter.Utils
{
    public class GitUtils
    {
        private static readonly string git_status_modified = "M";
        private static readonly string git_status_added = "A";
        private static readonly string git_status_deleted = "D";
        private static readonly string git_status_renamed = "R";
        private static readonly string git_status_updated = "U";
        private static readonly string git_status_untracked = "??";

        private static string GitWorkingDirectory = string.Empty;

        internal static string MainRepositoryPath
        {
            get
            {
                string path;
                if (string.IsNullOrEmpty(GitWorkingDirectory))
                {
                    path = RunGitCommand("rev-parse --git-dir", Application.dataPath);
                    path = path.Remove(path.IndexOf(".git", StringComparison.Ordinal));
                    GitWorkingDirectory = path;
                }

                return GitWorkingDirectory;
            }
        }

        internal static bool CanStage(string assetPath)
        {
            ChangedFileGitInfo[] unstagedFiles = GetUnstagedFiles();

            return unstagedFiles.Any(
                file => file.path.Contains(PathUtils.GetPathRelativeToRepositoryFolder(assetPath))
            );
        }

        internal static bool CanUnstage(string assetPath)
        {
            ChangedFileGitInfo[] stagedFiles = GetStagedFiles();

            return stagedFiles.Any(file => file.path.Contains(PathUtils.GetPathRelativeToRepositoryFolder(assetPath)));
        }

        internal static void Stage(string assetPath)
        {
            if (CanStage(assetPath))
            {
                RunGitCommand($"add \"{PathUtils.GetFullPath(assetPath)}\"");
            }
        }

        internal static void UnStage(string assetPath)
        {
            if (CanUnstage(assetPath))
            {
                RunGitCommand($"restore --staged \"{PathUtils.GetFullPath(assetPath)}\"");
            }
        }

        internal static bool IsTracked(string assetPath)
        {
            using (Process process = new Process())
            {
                string arguments = $"ls-files --error-unmatch {PathUtils.GetFullPath(assetPath)}";
                int exitCode = RunProcessAndGetExitCode(arguments, process, out string output, out string errorOutput);

                return exitCode != 1;
            }
        }

        internal static void Track(string guid)
        {
            GitIgnore.Remove(guid);

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(assetPath))
            {
                throw new Exception($"GUID {guid} not found in AssetDatabase");
            }

            Stage(assetPath);
            Stage(assetPath + ".meta");
        }

        internal static void Untrack(string guid)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string fullFilePath = PathUtils.GetFullPath(assetPath);

            RemoveOrUnstage(fullFilePath);
            RemoveOrUnstage(fullFilePath + ".meta");

            GitIgnore.Add(guid);
        }

        private static void RemoveOrUnstage(string fullFilePath)
        {
            if (IsTracked(fullFilePath))
            {
                RunGitCommand($"rm -r --cached {fullFilePath}");
            }
            else if (CanUnstage(fullFilePath))
            {
                UnStage(fullFilePath);
            }
        }

        public static void DiscardChanges(string assetPath)
        {
            RunGitCommand($"checkout \"{PathUtils.GetFullPath(assetPath)}\"");
        }

        internal static ChangedFileGitInfo[] GetAllChangedFilesGitInfo()
        {
            string status = RunGitCommand("status --porcelain -u");
            string[] splitted = status.Split('\n');

            return splitted.Select(line => new ChangedFileGitInfo(line)).ToArray();
        }

        internal static ChangedFileGitInfo[] GetUnstagedFiles(ChangedFileGitInfo[] changedFiles = null)
        {
            if (changedFiles == null)
            {
                return GetAllChangedFilesGitInfo().Where(file => file.hasUnstagedChanges).ToArray();
            }

            return changedFiles.Where(file => file.hasUnstagedChanges).ToArray();
        }

        internal static ChangedFileGitInfo[] GetStagedFiles(ChangedFileGitInfo[] changedFiles = null)
        {
            if (changedFiles == null)
            {
                return GetAllChangedFilesGitInfo().Where(file => file.hasStagedChanges).ToArray();
            }

            return changedFiles.Where(file => file.hasStagedChanges).ToArray();
        }

        public static void CreateBranch(DirectoryInfo repository, string branchName)
        {
            RunGitCommand($"checkout -b {branchName}", repository.FullName);
        }

        public static void Commit(DirectoryInfo repository, string branchName, string commitMessage, bool pushCommit)
        {
            RunGitCommand($"commit -a -m \"{commitMessage}\"", repository.FullName);

            if (pushCommit)
            {
                RunGitCommand($"push -u origin {branchName}", repository.FullName);
            }
        }

        public static string GetCurrentBranch(DirectoryInfo repository)
        {
            return RunGitCommand("branch --show-current", repository);
        }

        public static void SwitchBranch(string branchToSwitchTo, DirectoryInfo repository)
        {
            RunGitCommand($"checkout {branchToSwitchTo}", repository);
        }

        public static bool IsConnectedToVPN() => true;

        //commit
        private static bool Ping(string url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 3000;
                request.AllowAutoRedirect = false; // find out if this site is up and don't follow a redirector
                request.Method = "HEAD";

                using (var response = request.GetResponse())
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        internal static string RunGitCommand(string arguments, DirectoryInfo workingDirectory)
        {
            return RunGitCommand(arguments, workingDirectory.FullName);
        }

        internal static string RunGitCommand(string arguments, string workingDirectory = null)
        {
            using (Process process = new Process())
            {
                if (arguments.StartsWith("git "))
                {
                    arguments = arguments.TrimStart("git ".ToCharArray());
                }

                int exitCode = RunProcessAndGetExitCode(
                    arguments,
                    process,
                    out string output,
                    out string errorOutput,
                    workingDirectory
                );

                if (exitCode == 0)
                {
                    return output;
                }

                throw new InvalidOperationException(
                    $"Failed to run git {arguments}: Exit Code: {exitCode.ToString()}"
                );
            }
        }

        private static int RunProcessAndGetExitCode(string arguments, Process process, out string output,
            out string errorOutput, string workingDirectory = null)
        {
            if (workingDirectory == null)
            {
                workingDirectory = MainRepositoryPath;
            }

            return process.Run(
                "git",
                arguments,
                workingDirectory,
                out output,
                out errorOutput
            );
        }

        internal struct ChangedFileGitInfo
        {
            public string status;
            public bool hasStagedChanges;
            public bool hasUnstagedChanges;
            public bool isTracked;
            public bool isFullyStaged => hasStagedChanges && !hasUnstagedChanges;
            public string path;

            public ChangedFileGitInfo(string line)
            {
                status = line.Substring(0, 2);
                path = line.Substring(3);

                if (status == git_status_untracked)
                {
                    isTracked = false;
                    hasUnstagedChanges = true;
                    hasStagedChanges = false;

                    //git status surrounds untracked files path with "", need to remove them
                    path = path.Trim('\"');
                    return;
                }

                isTracked = true;
                hasStagedChanges = !status.StartsWith(" ");
                hasUnstagedChanges = !status.EndsWith(" ");
            }
        }
    }
}