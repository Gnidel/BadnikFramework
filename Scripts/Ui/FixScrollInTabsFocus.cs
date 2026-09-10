using System;
using Godot;

public partial class FixScrollInTabsFocus : Node
{
    // There is a bug where tabs container steals focus from the scroll container inside, resulting in inability to scroll all the way up. This script is meant to disable the focus of the tabs if scroll container is not scrolled up to the max.

    [Export]
    public TabContainer TabContainer;

    [Export]
    public Button[] ButtonsAtBottom = new Button[0];

    //public override void _Ready()
    //{
    //	TabContainer = this.GetParent<TabContainer>();
    //}

    public override void _Process(double delta)
    {
        if (!TabContainer.Visible)
            return;

        // Top - tabs
        var scrollChild = TabContainer.GetCurrentTabControl() as ScrollContainer;
        if (TabContainer.HasFocus())
            scrollChild.ScrollVertical = 0;
        TabContainer.TabFocusMode =
            scrollChild.ScrollVertical > 0
                ? Control.FocusModeEnum.Click
                : Control.FocusModeEnum.All;

        // Bottom - buttons
        foreach (var button in ButtonsAtBottom)
        {
            if (button.HasFocus())
                scrollChild.GetVScrollBar().Value =
                    scrollChild.GetVScrollBar().MaxValue - scrollChild.GetVScrollBar().Size.Y;

            button.FocusMode = !Mathf.IsEqualApprox(
                scrollChild.GetVScrollBar().Value,
                scrollChild.GetVScrollBar().MaxValue - scrollChild.GetVScrollBar().Size.Y
            )
                ? Control.FocusModeEnum.Click
                : Control.FocusModeEnum.All;
        }
    }
}
