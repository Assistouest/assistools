using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace Assistools.Controls;

public sealed partial class LogsControl : UserControl
{
    public LogsControl() => InitializeComponent();

    public void AppendLine(string line)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            LogsText.Text += line + "\n";
            LogsExpander.IsExpanded = true;
            // scroll to bottom
            LogsScroller.ChangeView(null, LogsScroller.ScrollableHeight, null);
        });
    }

    public void Clear()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            LogsText.Text = "";
            LogsExpander.IsExpanded = false;
        });
    }
}
