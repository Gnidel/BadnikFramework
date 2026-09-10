using System;
using Godot;

public partial class EnemyLookAtMode : Node
{
    public enum Mode
    {
        Constant = 0,
        LookAtTarget = 1,
        LookAtTargetFlat = 2,
    }

    [Export]
    public EnemyControl EnemyControl;

    [Export]
    public Node3D ObjectToRotate;

    [Export]
    public Node3D LookReferencePoint;

    [Export]
    public Node3D Target;

    [Export]
    public Mode LookMode = Mode.LookAtTarget;

    [Export]
    public float RotationSpeed = 1;

    public override void _PhysicsProcess(double delta)
    {
        if (Target == null)
        {
            if (PlayerController.Instances.Count == 0)
                return;
            Target = PlayerController.Instances[0];
        }
        if (Target == null)
            return;

        if (LookMode == Mode.LookAtTarget || LookMode == Mode.LookAtTargetFlat)
        {
            //ObjectToRotate.LookAt(Target.GlobalPosition, -Gravity.Normalized());
            var offset = Target.GlobalPosition - LookReferencePoint.GlobalPosition;
            if (LookMode == Mode.LookAtTargetFlat)
            {
                offset = Vector3Utils.ProjectOnPlane(offset, EnemyControl.Gravity);
            }
            var targetRotation = new Quaternion(
                Quaternion.Identity * Vector3.Forward,
                offset.Normalized()
            ).Normalized();
            if (RotationSpeed > 0)
            {
                try
                {
                    targetRotation = ObjectToRotate
                        .GlobalTransform.Basis.GetRotationQuaternion()
                        .Slerp(targetRotation, RotationSpeed * (float)delta);
                }
                catch (Exception) // "Argument is not normalized (to)
                {
                    targetRotation = ObjectToRotate.GlobalTransform.Basis.GetRotationQuaternion();
                }
            }

            //var upRotation = new Quaternion(Vector3.Up, -EnemyControl.Gravity.Normalized()).Normalized();
            //targetRotation = (targetRotation * upRotation).Normalized();

            var newTransform = ObjectToRotate.GlobalTransform;

            newTransform.Basis = new Basis(targetRotation);
            if (LookMode == Mode.LookAtTargetFlat)
            {
                newTransform = newTransform.LookingAt(
                    newTransform.Origin
                        + (-newTransform.Basis.Z).ProjectOnPlane(EnemyControl.Gravity),
                    -EnemyControl.Gravity
                );
            }
            else
            {
                newTransform = newTransform.LookingAt(
                    newTransform.Origin + (-newTransform.Basis.Z),
                    -EnemyControl.Gravity
                );
            }
            ObjectToRotate.GlobalTransform = newTransform;
        }
    }
}
