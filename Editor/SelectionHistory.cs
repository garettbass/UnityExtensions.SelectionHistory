using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

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

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            s_oldSelection = Selection.objects;
            Selection.selectionChanged += OnSelectionChanged;
        }

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

        [MenuItem(
            "Edit/Selection/Back %[",
            isValidateFunction: false,
            priority: -29)]
        public static void NavigateBackward()
        {
            if (s_backward.Count > 1)
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
            if (s_forward.Count > 0)
            {
                var oldSelection = s_forward.Pop();
                NavigateTo(NavigationType.Forward, oldSelection);
            }
        }

        private static void NavigateTo(
            NavigationType navigationType,
            Object[] selection)
        {
            s_navigationType = navigationType;
            Selection.objects = selection;
        }

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