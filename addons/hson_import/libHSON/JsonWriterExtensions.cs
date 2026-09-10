using System;
using System.Numerics;
using System.Text.Json;

namespace libHSON
{
    internal static class JsonWriterExtensions
    {
        #region Private Methods
        private static void WriteVectorValues(
            this Utf8JsonWriter writer, in Vector3 val)
        {
            writer.WriteNumberValue(val.X);
            writer.WriteNumberValue(val.Y);
            writer.WriteNumberValue(val.Z);
        }

        private static void WriteVectorValues(
            this Utf8JsonWriter writer, in Vector4 val)
        {
            writer.WriteNumberValue(val.X);
            writer.WriteNumberValue(val.Y);
            writer.WriteNumberValue(val.Z);
            writer.WriteNumberValue(val.W);
        }

        private static void WriteQuaternionValues(
            this Utf8JsonWriter writer, in Quaternion val)
        {
            writer.WriteNumberValue(val.X);
            writer.WriteNumberValue(val.Y);
            writer.WriteNumberValue(val.Z);
            writer.WriteNumberValue(val.W);
        }
        #endregion Private Methods

        #region Internal Methods
        internal static void WriteVectorValue(
            this Utf8JsonWriter writer, in Vector3 val)
        {
            writer.WriteStartArray();
            writer.WriteVectorValues(val);
            writer.WriteEndArray();
        }

        internal static void WriteVectorValue(
            this Utf8JsonWriter writer, in Vector4 val)
        {
            writer.WriteStartArray();
            writer.WriteVectorValues(val);
            writer.WriteEndArray();
        }

        internal static void WriteQuaternionValue(
            this Utf8JsonWriter writer, in Quaternion val)
        {
            writer.WriteStartArray();
            writer.WriteQuaternionValues(val);
            writer.WriteEndArray();
        }

        internal static void WriteVector(this Utf8JsonWriter writer,
            JsonEncodedText propertyName, in Vector3 val)
        {
            writer.WriteStartArray(propertyName);
            writer.WriteVectorValues(val);
            writer.WriteEndArray();
        }

        internal static void WriteVector(this Utf8JsonWriter writer,
            string propertyName, in Vector3 val)
        {
            writer.WriteStartArray(propertyName);
            writer.WriteVectorValues(val);
            writer.WriteEndArray();
        }

        internal static void WriteVector(this Utf8JsonWriter writer,
            ReadOnlySpan<char> propertyName, in Vector3 val)
        {
            writer.WriteStartArray(propertyName);
            writer.WriteVectorValues(val);
            writer.WriteEndArray();
        }

        internal static void WriteVector(this Utf8JsonWriter writer,
            ReadOnlySpan<byte> utf8PropertyName, in Vector3 val)
        {
            writer.WriteStartArray(utf8PropertyName);
            writer.WriteVectorValues(val);
            writer.WriteEndArray();
        }

        internal static void WriteVector(this Utf8JsonWriter writer,
            JsonEncodedText propertyName, in Vector4 val)
        {
            writer.WriteStartArray(propertyName);
            writer.WriteVectorValues(val);
            writer.WriteEndArray();
        }

        internal static void WriteVector(this Utf8JsonWriter writer,
            string propertyName, in Vector4 val)
        {
            writer.WriteStartArray(propertyName);
            writer.WriteVectorValues(val);
            writer.WriteEndArray();
        }

        internal static void WriteVector(this Utf8JsonWriter writer,
            ReadOnlySpan<char> propertyName, in Vector4 val)
        {
            writer.WriteStartArray(propertyName);
            writer.WriteVectorValues(val);
            writer.WriteEndArray();
        }

        internal static void WriteVector(this Utf8JsonWriter writer,
            ReadOnlySpan<byte> utf8PropertyName, in Vector4 val)
        {
            writer.WriteStartArray(utf8PropertyName);
            writer.WriteVectorValues(val);
            writer.WriteEndArray();
        }

        internal static void WriteQuaternion(this Utf8JsonWriter writer,
            JsonEncodedText propertyName, Quaternion val)
        {
            writer.WriteStartArray(propertyName);
            writer.WriteQuaternionValues(val);
            writer.WriteEndArray();
        }

        internal static void WriteQuaternion(this Utf8JsonWriter writer,
            string propertyName, Quaternion val)
        {
            writer.WriteStartArray(propertyName);
            writer.WriteQuaternionValues(val);
            writer.WriteEndArray();
        }

        internal static void WriteQuaternion(this Utf8JsonWriter writer,
            ReadOnlySpan<char> propertyName, Quaternion val)
        {
            writer.WriteStartArray(propertyName);
            writer.WriteQuaternionValues(val);
            writer.WriteEndArray();
        }

        internal static void WriteQuaternion(this Utf8JsonWriter writer,
            ReadOnlySpan<byte> utf8PropertyName, in Quaternion val)
        {
            writer.WriteStartArray(utf8PropertyName);
            writer.WriteQuaternionValues(val);
            writer.WriteEndArray();
        }
        #endregion Internal Methods
    }
}
