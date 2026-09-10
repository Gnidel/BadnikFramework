using System;
using Godot;

public partial class WaterTrigger : Area3D
{
    public override void _Ready()
    {
        if (GetSignalConnectionList("area_entered").Count == 0)
        {
            this.AreaEntered += this.OnAreaEnter;
        }

        if (GetSignalConnectionList("area_exited").Count == 0)
        {
            this.AreaExited += this.OnAreaExit;
        }
    }

    public void OnAreaEnter(Area3D other)
    {
        var cam = other.GetNodeOrNull<PlayerCamera>("..");
        if (cam != null && cam.Filters != null)
        {
            cam.Filters.SetWaterFilter(true);
        }

        var drowningTrig = other.GetNodeOrNull<WaterDrowningTrigger>(".");
        if (drowningTrig != null)
        {
            drowningTrig.WaterInteraction.AddWaterMouthCounter(1);
        }

        var waterRunTrig = other.GetNodeOrNull<WaterRunTrigger>(".");
        if (waterRunTrig != null)
        {
            waterRunTrig.WaterInteraction.AddWaterFeetCounter(1);
        }
    }

    public void OnAreaExit(Area3D other)
    {
        var cam = other.GetNodeOrNull<PlayerCamera>("..");
        if (cam != null)
        {
            cam.Filters.SetWaterFilter(false);
        }

        var drowningTrig = other.GetNodeOrNull<WaterDrowningTrigger>(".");
        if (drowningTrig != null)
        {
            drowningTrig.WaterInteraction.AddWaterMouthCounter(-1);
        }

        var waterRunTrig = other.GetNodeOrNull<WaterRunTrigger>(".");
        if (waterRunTrig != null)
        {
            waterRunTrig.WaterInteraction.AddWaterFeetCounter(-1);
        }
    }
}
