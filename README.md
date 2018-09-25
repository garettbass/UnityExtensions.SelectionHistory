# UnityExtensions.SelectionHistory

Adds menu items to navigate back and forward in the Unity Editor.

![Menu Items](Menu-Items.png)

## API

Your code can invoke these methods if you want to programmatically navigate backward or forward through the selection history:

```cs
UnityExtensions.SelectionHistory.NavigateBackward();
UnityExtensions.SelectionHistory.NavigateForward();
```