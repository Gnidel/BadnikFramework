using Godot;

public static class Vector3Utils
{
    public static Vector3 Project(this Vector3 vector, Vector3 onNormal)
    {
        return (vector.Dot(onNormal) / onNormal.LengthSquared()) * onNormal;
    }

    public static Vector3 ProjectOnPlane(this Vector3 vector, Vector3 planeNormal)
    {
        return vector - vector.Project(planeNormal);
    }

    public static float LengthAlongsideVector(Vector3 vector, Vector3 onNormal)
    {
        var ProjectedVector = Vector3Utils.Project(vector, onNormal);
        return vector.Normalized().Dot(onNormal.Normalized()) >= 0
            ? ProjectedVector.Length()
            : -ProjectedVector.Length();
    }

    public static Vector3 ClampLength(this Vector3 vector, float min, float max)
    {
        if (vector.Length() < min)
        {
            return vector.Normalized() * min;
        }
        if (vector.Length() > max)
        {
            return vector.Normalized() * max;
        }
        return vector;
    }
}
