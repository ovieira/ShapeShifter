using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Miniclip.ShapeShifter.Utils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.VersionControl;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Miniclip.ShapeShifter
{
    [Serializable]
    public partial class ShapeShifter : EditorWindow
    {
        private static readonly string ConfigurationResourceFolderPath = "Assets/Editor Default Resources/";
        private static readonly string ConfigurationResource = "ShapeShifterConfiguration.asset";
        internal static readonly string ExternalAssetsFolder = "external";
        internal static readonly string InternalAssetsFolder = "internal";

        private static ShapeShifterConfiguration configuration;

        public static ShapeShifterConfiguration Configuration
        {
            get
            {
                if (configuration != null)
                {
                    return configuration;
                }

                InitialiseConfiguration();
                return configuration;
            }
            set => configuration = value;
        }
        
        private static Editor defaultConfigurationEditor;
        private bool showConfiguration = false;

        private static DirectoryInfo skinsFolder;

        public static DirectoryInfo SkinsFolder
        {
            get
            {
                if (skinsFolder == null)
                {
                    skinsFolder = new DirectoryInfo(Application.dataPath + "/../../Skins/");
                    IOUtils.TryCreateDirectory(SkinsFolder.FullName, false);
                }

                return skinsFolder;
            }
            set => skinsFolder = value;
        }

        [MenuItem("Window/Shape Shifter/Open ShapeShifter Window", false, (int) 'G')]
        public static void OpenShapeShifter()
        {
            InitializeShapeShifterCore();

            ShowNextToInspector(true);
        }
        
        internal static ShapeShifter ShowNextToInspector(bool focus = false)
        {
            Assembly editorAssembly = typeof(Editor).Assembly;
            Type inspectorWindowType = editorAssembly.GetType("UnityEditor.InspectorWindow");

            return GetWindow<ShapeShifter>(
                "Shape Shifter",
                focus,
                inspectorWindowType
            );
        }

        private static string GenerateAssetKey(string game, string guid) => game + ":" + guid;
        
        internal static void InitializeShapeShifterCore()
        {
            ShapeShifterLogger.Log("Setting up");

            InitialiseConfiguration();
            RestoreMissingAssets();
        }

        private static void InitialiseConfiguration()
        {
            if (configuration != null)
            {
                return;
            }

            configuration = (ShapeShifterConfiguration) EditorGUIUtility.Load(
                ConfigurationResource
            );

            string configurationPath = Path.Combine(
                ConfigurationResourceFolderPath,
                ConfigurationResource
            );

            if (configuration == null && File.Exists(configurationPath))
            {
                configuration = AssetDatabase.LoadAssetAtPath<ShapeShifterConfiguration>(configurationPath);
            }

            if (configuration == null)
            {
                configuration = CreateInstance<ShapeShifterConfiguration>();

                if (!AssetDatabase.IsValidFolder(ConfigurationResourceFolderPath))
                {
                    AssetDatabase.CreateFolder("Assets", "Editor Default Resources");
                }

                AssetDatabase.CreateAsset(
                    configuration,
                    ConfigurationResourceFolderPath + ConfigurationResource
                );

                EditorUtility.SetDirty(configuration);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            defaultConfigurationEditor = Editor.CreateEditor(
                configuration,
                typeof(ShapeShifterConfigurationEditor)
            );
            
            externalConfigurationEditor = Editor.CreateEditor(
                Configuration,
                typeof(ShapeShifterExternalConfigurationEditor)
            );

            AssetDatabase.Refresh();

            if (configuration.GameNames.Count == 0)
            {
                ShapeShifterLogger.Log(
                    "Shapeshifter has no configured games, creating a default one and making it active"
                );
                configuration.GameNames.Add("Default");
                SwitchToGame(0);
                EditorUtility.SetDirty(configuration);
            }
            else
            {
                highlightedGame = ActiveGame;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void OnGUI()
        {
            if (configuration == null)
            {
                using (new GUILayout.VerticalScope())
                {
                    GUILayout.Label("Shapeshifter configuration not found.");

                    if (GUILayout.Button("Try To Fix"))
                    {
                        InitialiseConfiguration();
                    }
                    
                    Repaint();
                    return;
                }
            }

            using (new GUILayout.VerticalScope())
            {
                showConfiguration = EditorGUILayout.Foldout(showConfiguration, "Configuration");

                if (showConfiguration && defaultConfigurationEditor != null && externalConfigurationEditor != null)
                {
                    defaultConfigurationEditor.OnInspectorGUI();
                    
                    // TODO: hide this when it's no longer necessary, as direct access to this list may cause issues
                    externalConfigurationEditor.OnInspectorGUI();
                }

                OnAssetSwitcherGUI();
                OnAssetSkinnerGUI();
                OnExternalAssetSkinnerGUI();

                if (GUILayout.Button("Restore missing assets"))
                {
                    RestoreMissingAssets();
                }

                GUILayout.FlexibleSpace();
            }

            Repaint();
        }

        private static void SavePendingChanges()
        {
            AssetDatabase.SaveAssets();

            // since the above doesn't seem to work with ScriptableObjects, might as well just go for a full save
            EditorApplication.ExecuteMenuItem("File/Save Project");
        }

        public static void SaveChanges()
        {
            if (Configuration.ModifiedAssetPaths.Count > 0)
            {
                OverwriteSelectedSkin(ActiveGame);
            }
        }
    }
}