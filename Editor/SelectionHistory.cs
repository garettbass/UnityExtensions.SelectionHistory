using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
#else
using UnityEngine.Experimental.UIElements;
#endif // UNITY_2018_1_OR_NEWER

using Object = UnityEngine.Object;

namespace UnityExtensions
{

    public static class SelectionHistory
    {

        [Serializable]
        public struct SerializedSelection
        {
            public int[] instanceIDs;

            public SerializedSelection(Object[] selection)
            {
                instanceIDs =
                    selection
                    .Where(obj => obj != null)
                    .Select(obj => obj.GetInstanceID())
                    .ToArray();
            }

            public Object[] Deserialize()
            {
                return
                    instanceIDs
                    .Select(EditorUtility.InstanceIDToObject)
                    .Where(obj => obj != null)
                    .ToArray();
            }
        }

        //----------------------------------------------------------------------

        [Serializable]
        public struct SerializedHistory
        {
            private static string Key =>
                $"UnityExtensions.SelectionHistory({Application.dataPath})";

            public SerializedSelection[] backward;
            public SerializedSelection[] forward;

            public static void Save()
            {
                var history = new SerializedHistory
                {
                    backward = Serialize(s_backward),
                    forward = Serialize(s_forward),
                };
                var json = JsonUtility.ToJson(history);
                EditorPrefs.SetString(Key, json);
                // Debug.Log("save: " + json);
            }

            public static void Load()
            {
                var json = EditorPrefs.GetString(Key, null);
                if (!string.IsNullOrEmpty(json))
                {
                    // Debug.Log("load: " + json);
                    var history = JsonUtility.FromJson<SerializedHistory>(json);
                    s_backward.Clear();
                    s_backward.AddRange(Deserialize(history.backward));
                    s_forward.Clear();
                    s_forward.AddRange(Deserialize(history.forward));
                }
            }

            private static SerializedSelection[]
            Serialize(List<Object[]> selections)
            {
                return
                    selections
                    .Select(selection => new SerializedSelection(selection))
                    .ToArray();
            }

            private static IEnumerable<Object[]>
            Deserialize(SerializedSelection[] selections)
            {
                return
                    selections
                    .Select(selection => selection.Deserialize());
            }
        }

        //----------------------------------------------------------------------

        private enum NavigationType
        {
            Backward = -1,
            External = 0,
            Forward = +1,
        }

        private static NavigationType s_navigationType;

        private static Object[] s_oldSelection;

        private static readonly List<Object[]> s_backward =
            new List<Object[]>();

        private static readonly List<Object[]> s_forward =
            new List<Object[]>();

        private const int MaxStackCount = 128;

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

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            SerializedHistory.Load();
            s_oldSelection = Selection.objects;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
            Selection.selectionChanged += OnSelectionChanged;
            processEvent += OnProcessEvent;
        }

        private static void BeforeAssemblyReload()
        {
            SerializedHistory.Save();
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