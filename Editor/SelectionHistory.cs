using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

using Object = UnityEngine.Object;

namespace UnityExtensions
{

    public static class SelectionHistory
    {

        private enum NavigationType
        {
            Backward = -1,
            External =  0,
            Forward  = +1,
        }

        private static NavigationType s_navigationType;

        private static Object[] s_oldSelection;

        private static readonly List<Object[]> s_backward =
            new List<Object[]>();

        private static readonly List<Object[]> s_forward =
            new List<Object[]>();

        private const int MaxStackCount = 128;

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

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            s_oldSelection = Selection.objects;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += WaitForUnityEditorToolbar;
        }

        //----------------------------------------------------------------------

        private static void OnSelectionChanged()
        {
            var oldSelection = s_oldSelection;
            s_oldSelection = Selection.objects;

            if (oldSelection == null || oldSelection.Length == 0)
                return;

            var navigationType = s_navigationType;
            s_navigationType = NavigationType.External;
            switch (navigationType)
            {
                case NavigationType.External:
                case NavigationType.Forward:
                    s_backward.Push(oldSelection);
                    break;
                case NavigationType.Backward:
                    s_forward.Push(oldSelection);
                    break;
            }
        }

        //----------------------------------------------------------------------

        private static void WaitForUnityEditorToolbar()
        {
            var toolbar = Toolbar_get.GetValue(null);
            if (toolbar == null)
                return;

            EditorApplication.update -= WaitForUnityEditorToolbar;
            AttachToUnityEditorToolbar(toolbar);
        }

        private static void AttachToUnityEditorToolbar(object toolbar)
        {
            var imguiContainer =
                (IMGUIContainer)
                GUIView_imguiContainer
                .GetValue(toolbar, null);

            var onGUIHandler =
                (Action)
                IMGUIContainer_m_OnGUIHandler
                .GetValue(imguiContainer);

            onGUIHandler += OnGUI;

            IMGUIContainer_m_OnGUIHandler
            .SetValue(imguiContainer, onGUIHandler);
        }

        //----------------------------------------------------------------------

        private class GUIResources {

            public readonly GUIStyle
            commandStyle = new GUIStyle("Command"),
            commandLeftStyle = new GUIStyle("CommandLeft"),
            commandRightStyle = new GUIStyle("CommandRight"),
            blackBoldTextStyle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 26,
            },
            whiteBoldTextStyle = new GUIStyle(EditorStyles.whiteBoldLabel) {
                fontSize = 26,
            };

            public const string
            prevTooltip =
#if UNITY_EDITOR_OSX
                "Select Previous \u2318["
#else
                "Select Previous ^["
#endif
            ,
            nextTooltip =
#if UNITY_EDITOR_OSX
                "Select Next \u2318]"
#else
                "Select Next ^]"
#endif
            ;

            public readonly GUIContent
            prevButtonContent = new GUIContent("\u2039", prevTooltip),
            nextButtonContent = new GUIContent("\u203A", nextTooltip);

            public readonly GUIContent[]
            navigationButtonContents = new GUIContent[] {
                new GUIContent(" ", prevTooltip),
                new GUIContent(" ", nextTooltip),
            };

        }

        private static GUIResources s_gui;
        private static GUIResources gui
        {
            get { return s_gui ?? (s_gui = new GUIResources()); }
        }

        //----------------------------------------------------------------------

        private static void OnGUI()
        {
            var prevEnabled = CanNavigateBackward();
            var nextEnabled = CanNavigateForward();

            var guiRect = new Rect(370, 5, 32, 24);
            {
                var prevRect = guiRect;
                var prevStyle = gui.commandLeftStyle;
                var prevContent = gui.prevButtonContent;

                EditorGUI.BeginDisabledGroup(!prevEnabled);
                if (GUI.Button(prevRect, prevContent, prevStyle))
                    NavigateBackward();
                EditorGUI.EndDisabledGroup();

                var nextRect = guiRect;
                nextRect.x += guiRect.width;
                var nextStyle = gui.commandRightStyle;
                var nextContent = gui.nextButtonContent;

                EditorGUI.BeginDisabledGroup(!nextEnabled);
                if (GUI.Button(nextRect, nextContent, nextStyle))
                    NavigateForward();
                EditorGUI.EndDisabledGroup();
            }

            var isRepaint = Event.current.type == EventType.Repaint;
            if (isRepaint)
            {
                var rowRect = guiRect;
                rowRect.y -= 7;
                var black = gui.blackBoldTextStyle;
                var white = gui.whiteBoldTextStyle;

                var no = false;
                var prevRect = rowRect;
                var prevContent = gui.prevButtonContent;
                prevRect.x += 10;
                prevRect.size = black.CalcSize(prevContent);
                EditorGUI.BeginDisabledGroup(!prevEnabled);
                white.Draw(prevRect, prevContent, no, no, no, no);
                prevRect.y -= 1;
                black.Draw(prevRect, prevContent, no, no, no, no);
                EditorGUI.EndDisabledGroup();

                var nextRect = rowRect;
                var nextContent = gui.nextButtonContent;
                nextRect.x += 42;
                nextRect.size = black.CalcSize(nextContent);
                EditorGUI.BeginDisabledGroup(!nextEnabled);
                white.Draw(nextRect, nextContent, no, no, no, no);
                nextRect.y -= 1;
                black.Draw(nextRect, nextContent, no, no, no, no);
                EditorGUI.EndDisabledGroup();
            }
        }

        //----------------------------------------------------------------------

        public static bool CanNavigateBackward()
        {
            return s_backward.Count > 0;
        }

        public static bool CanNavigateForward()
        {
            return s_forward.Count > 0;
        }

        //----------------------------------------------------------------------

        [MenuItem(
            "Edit/Selection/Back %[",
            isValidateFunction: false,
            priority: -29)]
        public static void NavigateBackward()
        {
            if (CanNavigateBackward())
            {
                var oldSelection = s_backward.Pop();
                NavigateTo(NavigationType.Backward, oldSelection);
            }
        }

        [MenuItem(
            "Edit/Selection/Forward %]",
            isValidateFunction: false,
            priority: -28)]
        public static void NavigateForward()
        {
            if (CanNavigateForward())
            {
                var oldSelection = s_forward.Pop();
                NavigateTo(NavigationType.Forward, oldSelection);
            }
        }

        //----------------------------------------------------------------------

        private static void NavigateTo(
            NavigationType navigationType,
            Object[] selection)
        {
            s_navigationType = navigationType;
            Selection.objects = selection;
        }

        //----------------------------------------------------------------------

        private static void Push<T>(this List<T> stack, T item)
        {
            stack.Insert(0, item);
            for (int count = 0; (count = stack.Count) > MaxStackCount;)
            {
                stack.RemoveAt(count - 1);
            }
        }

        private static T Pop<T>(this List<T> stack)
        {
            var item = default(T);
            if (stack.Count > 0)
            {
                item = stack[0];
                stack.RemoveAt(0);
            }
            return item;
        }

    }

}