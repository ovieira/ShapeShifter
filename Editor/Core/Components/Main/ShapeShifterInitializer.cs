using System.Collections;
using System.Threading.Tasks;
using Miniclip.ShapeShifter;
using Miniclip.ShapeShifter.Switcher;
using Miniclip.ShapeShifter.Utils;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class ShapeShifterInitializer
{
    static ShapeShifterInitializer()
    {
        EditorApplication.delayCall += Init;
        EditorApplication.quitting += EditorApplicationOnQuitting;
        WindowFocusUtility.OnUnityEditorFocus -= OnEditorFocus;
        WindowFocusUtility.OnUnityEditorFocus += OnEditorFocus;
    }

    private static void OnEditorFocus(bool isFocused)
    {
        RestoreAssetsAfterCompiling(isFocused);
    }

    private static async void RestoreAssetsAfterCompiling(bool isFocused)
    {
        if (!isFocused)
            return;

        if(!ShapeShifterConfiguration.IsInitialized())
            return;
        
        await Task.Delay(1000);
        

        while (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            await Task.Delay(1000);
        }

        if (isFocused && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            AssetSwitcher.RestoreMissingAssets();
            ShapeShifterUtils.DeleteDSStoreFiles();
        }
    }

    private static void Init()
    {
        ShapeShifterLogger.Log("Setting up");

        ShapeShifterConfiguration.Initialise();
        AssetSwitcher.RestoreMissingAssets();
    }

    private static void EditorApplicationOnQuitting()
    {
        EditorApplication.delayCall -= Init;
        EditorApplication.quitting -= EditorApplicationOnQuitting;
        WindowFocusUtility.OnUnityEditorFocus -= OnEditorFocus;
    }
}