using System;
using Godot;

public partial class HealthBarControl : Node
{
    [Export]
    public int HP = 100;

    [Export]
    public int MaxHP = 100;

    [Export]
    public ColorRect HpUiPointTemplate;

    [Export]
    public Node HpUiPointsContainer;

    [Export]
    public Godot.Collections.Array<ColorRect> HpUiPoints = new Godot.Collections.Array<ColorRect>();

    [Export]
    public Color HpYesColor = new Color(1, 1, 1, 1); // White

    [Export]
    public Color HpFreshDamageColor = new Color(1, 0, 0, 1); // Red

    [Export]
    public Color HpNoColor = new Color(0.1f, 0.1f, 0.1f, 1); // Gray, almost black

    public void SetHP(int newHP)
    {
        for (int i = 0; i < HpUiPoints.Count; i++)
        {
            if ((i + 1) > newHP && (i + 1) <= this.HP)
            {
                HpUiPoints[i].Color = HpFreshDamageColor;
            }
        }
        this.HP = newHP;
    }

    public void SetMaxHP(int newMaxHP)
    {
        this.MaxHP = newMaxHP;

        // Cleanup
        HpUiPoints.Clear();
        var children = HpUiPointsContainer.GetChildren();
        foreach (var child in children)
        {
            child.QueueFree();
        }

        for (int i = 0; i < MaxHP; i++)
        {
            var newHpPoint = HpUiPointTemplate.Duplicate();
            HpUiPointsContainer.AddChild(newHpPoint);
            var newHpPointColorRect = newHpPoint.GetNode<ColorRect>(".");
            newHpPointColorRect.Visible = true;
            HpUiPoints.Add(newHpPointColorRect);
        }
    }

    public override void _Ready()
    {
        SetMaxHP(this.MaxHP);
    }

    public override void _Process(double delta)
    {
        for (int i = 0; i < HpUiPoints.Count; i++)
        {
            if (HP > i)
            {
                HpUiPoints[i].Color = HpUiPoints[i].Color.Lerp(HpYesColor, (float)delta);
            }
            else
            {
                HpUiPoints[i].Color = HpUiPoints[i].Color.Lerp(HpNoColor, (float)delta);
            }
        }
    }

    public void FadeOut()
    {
        this.GetParent().QueueFree(); // TODO: Add some movement off screen
    }
}
