using Godot;

public partial class WallJumpPanel : Node3D
{
    public override void _Ready()
    {
        var area = GetNodeOrNull<Area3D>("Area3D");
        if (area == null)
            return;

        if (area.GetSignalConnectionList("body_entered").Count == 0)
            area.BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        var player = body as PlayerController;
        if (player == null)
            return;

        var wallNormal = -GlobalBasis.Z.Normalized();
        if (wallNormal.Dot(player.GlobalPosition - GlobalPosition) < 0)
            wallNormal = -wallNormal;

        player.GetNodeOrNull<ActionWallStick>("./PlayerControl/Actions/ActionWallStick")
            ?.BeginWallStick(wallNormal);
    }

}