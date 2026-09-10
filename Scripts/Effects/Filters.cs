using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class Filters : Node
{
    public static List<Filters> Instances = new List<Filters>();

    [Export]
    public Control ImpactFrameFilter;

    [Export]
    public Control WaterFilter;

    [Export]
    public Control WhiteoutFilter;
    private int waterCounter = 0;

    public override void _EnterTree()
    {
        Instances.Add(this);
        DisableAllFilters();
    }

    public override void _ExitTree()
    {
        Instances.Remove(this);
    }

    void DisableAllFilters()
    {
        if (ImpactFrameFilter != null)
        {
            ImpactFrameFilter.Visible = false;
        }
        if (WaterFilter != null)
        {
            WaterFilter.Visible = false;
        }
        if (WhiteoutFilter != null)
        {
            WhiteoutFilter.Visible = false;
        }
    }

    public async void EnableImpactFrameFilter(double duration = 6f / 60f)
    {
        ImpactFrameFilter.Visible = true;
        await Task.Delay(TimeSpan.FromSeconds(duration));
        ImpactFrameFilter.Visible = false;
    }

    public void SetWaterFilter(bool enabled)
    {
        waterCounter += enabled ? 1 : -1;
        if (waterCounter < 0)
            waterCounter = 0;

        if (waterCounter > 0)
        {
            WaterFilter.Visible = true;
        }
        else
        {
            WaterFilter.Visible = false;
        }
    }

    //public async void WhiteoutFlash(double duration = 6f / 60f)
    //   {
    //       //WhiteoutFilter.Visible = true;
    //       //await Task.Delay(TimeSpan.FromSeconds(duration));
    //       //WhiteoutFilter.Visible = false;
    //   }
}
