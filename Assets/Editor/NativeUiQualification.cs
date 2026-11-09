using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace NoteScriptGame.Editor
{
    public static class NativeUiQualification
    {
        [MenuItem("NoteScriptGame/Verify and Build Native UI")]
        public static void Run()
        {
            HighDpiSetup.Configure();
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Resources/NoteScriptGamePanel.asset");
            if (panel == null || panel.scaleMode != PanelScaleMode.ConstantPixelSize)
                throw new Exception("Missing native pixel PanelSettings.");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/NoteScriptGame.unity");
            GameObject go = GameObject.Find("NoteScriptGame Application");
            var document = go != null ? go.GetComponent<UIDocument>() : null;
            if (document == null || document.panelSettings != panel)
                throw new Exception("Runtime UIDocument is missing or uses the wrong PanelSettings.");
            string path = "Assets/Scripts/Game/NoteScriptGameApp.cs";
            if (File.ReadAllText(path).Contains("GUI.matrix"))
                throw new Exception("Legacy IMGUI scaling path still exists.");
            if (!File.ReadAllText("Assets/Scripts/Game/NoteScriptGameNativeUI.cs").Contains("16f / 9f"))
                throw new Exception("No explicit 16:9 playfield fitting.");
            ProjectSetup.RunSmokeChecks();
            ProjectSetup.BuildWindows();
            Debug.Log("NSG-NATIVE-UI-QUALIFICATION PASS: panel/scene/native aspect, Markdown chart suite and Windows build.");
        }
    }
}
