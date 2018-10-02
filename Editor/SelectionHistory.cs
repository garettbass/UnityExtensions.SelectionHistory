using System;
using System.Collections.Generic;
using System.Linq;
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

        private static FieldInfo GUIUtility_processEvent =
            typeof(GUIUtility)
            .GetField(
                "processEvent",
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        private static event Func<int, IntPtr, bool> processEvent
        {
            add
            {
                var processEvent =
                    (Delegate)
                    GUIUtility_processEvent.GetValue(null);
                processEvent = Delegate.Combine(processEvent, value);
                GUIUtility_processEvent.SetValue(null, processEvent);
            }
            remove
            {
                var processEvent =
                    (Delegate)
                    GUIUtility_processEvent.GetValue(null);
                processEvent = Delegate.Remove(processEvent, value);
                GUIUtility_processEvent.SetValue(null, processEvent);
            }
        }

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
            processEvent += OnProcessEvent;
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
                    s_forward.Clear();
                    s_backward.Push(oldSelection);
                    break;
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
            prevTooltip = "Select Previous",
            nextTooltip = "Select Next";

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

        private static bool OnProcessEvent(
            int instanceID,
            IntPtr nativeEventPtr)
        {
            HandleMouseButtonEvents();
            return false;
        }

        private static void HandleMouseButtonEvents()
        {
            var currentEvent = Event.current;
            var currentEventType = currentEvent.type;
            var clickCount = currentEvent.clickCount;
            var isMouseUp = currentEventType == EventType.MouseUp;
            if (isMouseUp && clickCount == 1)
            {
                switch (currentEvent.button)
                {
                    case 3:
                        NavigateBackward();
                        Event.current.Use();
                        break;
                    case 4:
                        NavigateForward();
                        Event.current.Use();
                        break;
                }
            }
        }

        //----------------------------------------------------------------------

        public static bool CanNavigateBackward()
        {
            return s_backward.Any(objs => objs.Any(obj => obj != null));
        }

        public static bool CanNavigateForward()
        {
            return s_forward.Any(objs => objs.Any(obj => obj != null));
        }

        //----------------------------------------------------------------------

        [MenuItem(
            "Edit/Selection/Back %[",
            isValidateFunction: false,
            priority: -29)]
        public static void NavigateBackward()
        {
            while (CanNavigateBackward())
            {
                var oldSelection = s_backward.Pop().RemoveNullObjects();
                if (oldSelection.Length > 0)
                {
                    NavigateTo(NavigationType.Backward, oldSelection);
                    return;
                }
            }
        }

        [MenuItem(
            "Edit/Selection/Forward %]",
            isValidateFunction: false,
            priority: -28)]
        public static void NavigateForward()
        {
            while (CanNavigateForward())
            {
                var oldSelection = s_forward.Pop().RemoveNullObjects();
                if (oldSelection.Length > 0)
                {
                    NavigateTo(NavigationType.Forward, oldSelection);
                    return;
                }
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

        private static Object[] RemoveNullObjects(
            this IEnumerable<Object> objects)
        {
            return objects.Where(obj => obj != null).ToArray();
        }

    }

}