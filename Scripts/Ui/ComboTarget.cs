using System;
using Godot;

public partial class ComboTarget : Node3D
{
    [Export]
    public Node2D Child2D;

    [Export]
    public Label ComboCounterLabel;

    public Node3D CopyPositionFrom;
    public Camera3D Camera;

    public override void _Process(double delta)
    {
        if (
            CopyPositionFrom == null
            || !IsInstanceValid(CopyPositionFrom)
            || !CopyPositionFrom.IsInsideTree()
        )
        {
            QueueFree();
            return;
        }
        this.GlobalPosition = CopyPositionFrom.GlobalPosition;
        if (Camera != null)
        {
            this.Visible =
                !this.Camera.IsPositionBehind(this.GlobalPosition)
                && this.Camera.IsPositionInFrustum(this.GlobalPosition);
            //GD.Print("Behind:", this.Camera.IsPositionBehind(this.GlobalPosition), "Frustrum:", this.Camera.IsPositionInFrustum(this.GlobalPosition), this.Visible);
            Child2D.Visible = this.Visible;
            ComboCounterLabel.Visible = ComboCounterLabel.Visible;

            Child2D.GlobalPosition = this.Camera.UnprojectPosition(this.GlobalPosition);
        }
        else
        {
            this.Visible = false;
        }
    }

    public void SetComboCount(int comboCount)
    {
        ComboCounterLabel.Text = "x" + comboCount.ToString();
    }
}
