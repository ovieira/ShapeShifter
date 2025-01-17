﻿using System;
using System.IO;
using System.Reflection;
using Miniclip.ShapeShifter.Saver;
using Miniclip.ShapeShifter.Skinner;
using Miniclip.ShapeShifter.Switcher;
using Miniclip.ShapeShifter.Utils;
using Miniclip.ShapeShifter.Utils.Git;
using UnityEditor;
using UnityEngine;

namespace Miniclip.ShapeShifter
{
    class ShapeShifterEditorWindow : EditorWindow
    {
        private enum TabOptions
        {
            AssetSkinner = 0,
            ExternalSkinner = 1,
            Configuration = 2,
            Tools = 3,
            Settings = 4,
        }

        private int selectedTabOption;

        private bool showConfiguration;
        private string[] tabOptionsNames;

        private void OnEnable()
        {
            tabOptionsNames = Enum.GetNames(typeof(TabOptions));
        }

        private void OnSelectionChange()
        {
            ShapeShifter.DirtyAssets.Clear();
            ShapeShifter.CachedPreviewPerAssetDict.Clear();
            FileWatcher.RemoveAllPathsFromWatchlist();
        }

        private void OnGUI()
        {
            if (!ShapeShifterConfiguration.IsInitialized())
            {
                using (new GUILayout.VerticalScope())
                {
                    GUILayout.Label("Shapeshifter configuration needs to be initialized.");

                    if (GUILayout.Button("Initialize"))
                    {
                        ShapeShifterConfiguration.Initialize();
                    }

                    Repaint();
                    return;
                }
            }
            
            AssetSwitcherGUI.OnGUI();
            EditorGUILayout.Separator();

            GUILayout.Space(15);

            OnSelectedTabGUI();

            GUILayout.FlexibleSpace();

            AssetSaverGUI.OnGUI();

            Repaint();
        }

        [MenuItem("Window/Shape Shifter/Open ShapeShifter Window", false, 'G')]
        public static void OpenShapeShifter()
        {
            ShowNextToInspector(true);
        }

        private static void ShowNextToInspector(bool focus = false)
        {
            Assembly editorAssembly = typeof(Editor).Assembly;
            Type inspectorWindowType = editorAssembly.GetType("UnityEditor.InspectorWindow");

            GetWindow<ShapeShifterEditorWindow>(
                "Shape Shifter",
                focus,
                inspectorWindowType
            );
        }
        
        private void OnSelectedTabGUI()
        {
            selectedTabOption = GUILayout.Toolbar(selectedTabOption, tabOptionsNames);
            GUILayout.Space(15);
            switch ((TabOptions) selectedTabOption)
            {
                case TabOptions.AssetSkinner:
                    AssetSkinnerGUI.OnGUI();
                    break;
                case TabOptions.ExternalSkinner:
                    ExternalAssetSkinnerGUI.OnGUI();
                    break;
                case TabOptions.Configuration:
                    OnConfigurationGUI();
                    break;
                case TabOptions.Tools:
                    OnToolsGUI();
                    break;
                case TabOptions.Settings:
                    Settings.OnGUI();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private void OnConfigurationGUI()
        {
            if (ShapeShifterConfiguration.Instance.DefaultConfigurationEditor != null
                && ShapeShifterConfiguration.Instance.ExternalConfigurationEditor != null)
            {
                ShapeShifterConfiguration.Instance.DefaultConfigurationEditor.OnInspectorGUI();
                ShapeShifterConfiguration.Instance.ExternalConfigurationEditor.OnInspectorGUI();
            }
        }

        private static void OnToolsGUI()
        {
            GUILayout.Space(10);

            if (GUILayout.Button($"Check for double skins in {ShapeShifter.ActiveGameName}"))
            {
                ShapeShifterUtils.CheckForDoubleSkinnedAssetsInGame(ShapeShifter.ActiveGameName);
            }
            GUILayout.Space(10);

            PreMergeCheckGUI.OnGUI();
            GUILayout.Space(10);
            GUILayout.BeginVertical(StyleUtils.BoxStyle);
            GUILayout.Label("Dangerous Operations");
            GUILayout.Space(10);
            OnRemoveAllSkinsGUI();
            GUILayout.EndVertical();
        }
        private static void OnRemoveAllSkinsGUI()
        {
            Color backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Remove all skins") && RemoveAllSkinsDisplayDialog())
            {
                EditorUtility.DisplayProgressBar("ShapeShifter", "Deleting internal skins", 0.0f);
                AssetSkinner.RemoveAllInternalSkins();
                EditorUtility.DisplayProgressBar("ShapeShifter", "Deleting external skins", 0.3f);
                ExternalAssetSkinner.RemoveAllExternalSkins();
                EditorUtility.DisplayProgressBar("ShapeShifter", "Cleaning up git ignore", 0.6f);
                GitIgnore.ClearShapeShifterEntries();
                EditorUtility.DisplayProgressBar("ShapeShifter", "Deleting main skins folder", 1f);
                if (Directory.Exists(ShapeShifter.SkinsFolder.FullName))
                {
                    FileUtils.SafeDelete(ShapeShifter.SkinsFolder.FullName);
                }

                EditorUtility.ClearProgressBar();
                GUIUtility.ExitGUI();
            }

            GUI.backgroundColor = backgroundColor;
        }

        private static bool RemoveAllSkinsDisplayDialog()
        {
            return EditorUtility.DisplayDialog(
                "ShapeShifter",
                "You are about to remove shapeshifter's skin folders.\n Your project assets will remain "
                + $"the same as the current game skin ({ShapeShifter.ActiveGameName}).\n You will loose the other game skins",
                "Continue",
                "Cancel"
            );
        }
    }
}