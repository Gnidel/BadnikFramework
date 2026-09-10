using System;
using System.Reflection;
using Godot;

public static class NodeUtils
{
    public static void CopyCustomFieldsFrom<T>(this T target, T source)
    {
        var type = typeof(T);

        foreach (
            var field in type.GetFields(
                BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Instance
                    | BindingFlags.DeclaredOnly
            )
        )
        {
            field.SetValue(target, field.GetValue(source));
        }

        foreach (
            var prop in type.GetProperties(
                BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Instance
                    | BindingFlags.DeclaredOnly
            )
        )
        {
            if (prop.CanRead && prop.CanWrite)
            {
                prop.SetValue(target, prop.GetValue(source));
            }
        }
    }
}
