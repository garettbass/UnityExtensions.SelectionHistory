using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
#else
using UnityEngine.Experimental.UIElements;
#endif // UNITY_2018_1_OR_NEWER

namespace UnityExtensions
{

    internal static class SelectionHistoryToolbarGUI
    {

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            EditorApplication.delayCall += WaitForUnityEditorToolbar;
        }

        //----------------------------------------------------------------------

        private static Type Toolbar =
            typeof(EditorGUI)
            .Assembly
            .GetType("UnityEditor.Toolbar");

        private static FieldInfo Toolbar_get =
            Toolbar
            .GetField("get");

        //----------------------------------------------------------------------

        private static Type GUIView =
            typeof(EditorGUI)
            .Assembly
            .GetType("UnityEditor.GUIView");

        private static PropertyInfo GUIView_imguiContainer =
            GUIView
            .GetProperty(
                "imguiContainer",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        //----------------------------------------------------------------------

        private static FieldInfo IMGUIContainer_m_OnGUIHandler =
            typeof(IMGUIContainer)
            .GetField(
                "m_OnGUIHandler",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        //----------------------------------------------------------------------

        private static void WaitForUnityEditorToolbar()
        {
            var toolbar = Toolbar_get.GetValue(null);
            if (toolbar != null)
            {
                AttachToUnityEditorToolbar(toolbar);
                return;
            }
            EditorApplication.delayCall += WaitForUnityEditorToolbar;
        }

        private static void AttachToUnityEditorToolbar(object toolbar)
        {
            var toolbarGUIContainer =
                (IMGUIContainer)
                GUIView_imguiContainer
                .GetValue(toolbar, null);

            var toolbarGUIHandler =
                (Action)
                IMGUIContainer_m_OnGUIHandler
                .GetValue(toolbarGUIContainer);

            toolbarGUIHandler += OnGUI;

            IMGUIContainer_m_OnGUIHandler
            .SetValue(toolbarGUIContainer, toolbarGUIHandler);
        }

        //----------------------------------------------------------------------

        private class GUIResources
        {

            public const int GlyphFontSize = 26;

            public readonly GUIStyle
            commandStyle = new GUIStyle("Command"),
            commandLeftStyle = new GUIStyle("CommandLeft"),
            commandRightStyle = new GUIStyle("CommandRight"),
            blackBoldTextStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = GlyphFontSize,
            },
            whiteBoldTextStyle = new GUIStyle(EditorStyles.whiteBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = GlyphFontSize,
            };

            public const string
            prevTooltip = "Navigate to Previous Selection",
            nextTooltip = "Navigate to Next Selection";

            public readonly GUIContent
            prevButtonContent = new GUIContent(" ", prevTooltip),
            nextButtonContent = new GUIContent(" ", nextTooltip);

            public readonly GUIContent
            prevGlyphContent = new GUIContent("\u2039", prevTooltip),
            nextGlyphContent = new GUIContent("\u203A", nextTooltip);

        }

        private static GUIResources s_gui;
        private static GUIResources gui
        {
            get { return s_gui ?? (s_gui = new GUIResources()); }
        }

        //----------------------------------------------------------------------

        private static void OnGUI()
        {
            var prevEnabled = SelectionHistory.CanNavigateBackward();
            var nextEnabled = SelectionHistory.CanNavigateForward();


#if UNITY_2019_1_OR_NEWER
            var guiRect = new Rect(400, 5, 32, 22);
#else
            var guiRect = new Rect(370, 5, 32, 22);
#endif // UNITY_2018_1_OR_NEWER

            var prevRect = guiRect;
            var nextRect = guiRect;
            nextRect.x += guiRect.width;
            {
                var prevStyle = gui.commandLeftStyle;
                var prevContent = gui.prevButtonContent;

                EditorGUI.BeginDisabledGroup(!prevEnabled);
                if (GUI.Button(prevRect, prevContent, prevStyle))
                    SelectionHistory.NavigateBackward();
                EditorGUI.EndDisabledGroup();

                var nextStyle = gui.commandRightStyle;
                var nextContent = gui.nextButtonContent;

                EditorGUI.BeginDisabledGroup(!nextEnabled);
                if (GUI.Button(nextRect, nextContent, nextStyle))
                    SelectionHistory.NavigateForward();
                EditorGUI.EndDisabledGroup();
            }

            var isRepaint = Event.current.type == EventType.Repaint;
            if (isRepaint)
            {
                var black = gui.blackBoldTextStyle;
                var white = gui.whiteBoldTextStyle;

                var no = false;
                var prevContent = gui.prevGlyphContent;
                EditorGUI.BeginDisabledGroup(!prevEnabled);
                // prevRect.x -= 1;
                prevRect.y -= 1;
                white.Draw(prevRect, prevContent, no, no, no, no);
                prevRect.y -= 1;
                black.Draw(prevRect, prevContent, no, no, no, no);
                EditorGUI.EndDisabledGroup();
                // EditorGUI.DrawRect(prevRect, Color.Lerp(Color.cyan, Color.clear, 0.2f));

                var nextContent = gui.nextGlyphContent;
                EditorGUI.BeginDisabledGroup(!nextEnabled);
                // nextRect.x -= 1;
                nextRect.y -= 1;
                white.Draw(nextRect, nextContent, no, no, no, no);
                nextRect.y -= 1;
                black.Draw(nextRect, nextContent, no, no, no, no);
                EditorGUI.EndDisabledGroup();
                // EditorGUI.DrawRect(nextRect, Color.Lerp(Color.magenta, Color.clear, 0.2f));
            }
        }

    }

}