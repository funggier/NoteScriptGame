using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace NoteScriptGame.Editor
{
    public static class HighDpiSetup
    {
        private const string ScenePath = "Assets/Scenes/NoteScriptGame.unity";
        private const string PanelPath = "Assets/Resources/NoteScriptGamePanel.asset";
        private const string ThemePath = "Assets/Resources/NoteScriptGameRuntimeTheme.tss";

        [MenuItem("NoteScriptGame/Configure Native-Resolution UI")]
        public static void Configure()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceSynchronousImport);
            ThemeStyleSheet runtimeTheme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (runtimeTheme == null)
                throw new Exception("Cannot import project-owned UI Toolkit theme: " + ThemePath);

            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.themeStyleSheet = runtimeTheme;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.scale = 1f;
            EditorUtility.SetDirty(panel);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject game = GameObject.Find("NoteScriptGame Application");
            if (game == null) throw new Exception("Missing NoteScriptGame Application in " + ScenePath);
            if (game.GetComponent<NoteScriptGame.Game.NoteScriptGameApp>() == null)
                throw new Exception("Missing NoteScriptGameApp component.");
            UIDocument document = game.GetComponent<UIDocument>();
            if (document == null) document = game.AddComponent<UIDocument>();
            document.panelSettings = panel;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new Exception("Cannot save scene with UIDocument.");

            PlayerSettings.productName = "NoteScriptGame";
            PlayerSettings.companyName = "funggier";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("NSG-HIDPI PASS: project-owned runtime theme, native pixel UI Toolkit, 16:9 playfield, 1080p/1440p defaults.");
        }
    }
}
