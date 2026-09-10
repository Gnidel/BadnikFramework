using System;
using Godot;

public partial class UVScroll : Node
{
    [Export]
    public int SurfaceIdx;

    [Export]
    public Vector3 ScrollValue;

    /*
     * WARNING! Do not use! Extremely slow. It's better to add scrolling through shader.
     */
    public override void _Process(double delta)
    {
        var MeshInstance = this.GetChildren()[0].GetNode<MeshInstance3D>(".");
        var material = MeshInstance.Mesh.SurfaceGetMaterial(SurfaceIdx);
        var uvOffset = material.Get("uv1_offset").AsVector3();
        var newUvOffset = uvOffset + ScrollValue * (float)delta;
        material.Set("uv1_offset", newUvOffset);
    }
}
