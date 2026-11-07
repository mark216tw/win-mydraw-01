using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Com.StudentTools.MyDraw.Views;

public sealed class CursorGrid : Grid
{
    public void SetCursor(InputCursor? cursor) => ProtectedCursor = cursor;
}
