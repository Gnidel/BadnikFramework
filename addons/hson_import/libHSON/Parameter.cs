using System;
using System.Collections.Generic;
using System.Text.Json;

namespace libHSON
{
    public class Parameter
    {
        #region Private Fields
        private ParameterType _type = ParameterType.FloatingPoint;

        private object _value = 0.0;
        #endregion Private Fields

        #region Public Properties
        public ParameterType Type => _type;

        public bool IsBoolean => _type == ParameterType.Boolean;

        public bool IsSignedInteger => _type == ParameterType.SignedInteger;

        public bool IsUnsignedInteger => _type == ParameterType.UnsignedInteger;

        public bool IsFloatingPoint => _type == ParameterType.FloatingPoint;

        public bool IsString => _type == ParameterType.String;

        public bool IsArray => _type == ParameterType.Array;

        public bool IsObject => _type == ParameterType.Object;

        public object Value => _value;

        public bool ValueBoolean
        {
            get => (bool)_value;
            set
            {
                _type = ParameterType.Boolean;
                _value = value;
            }
        }

        public long ValueSignedInteger
        {
            get
            {
                // If this is a signed integer, just return it.
                if (_type == ParameterType.SignedInteger)
                {
                    return (long)_value;
                }
                
                // If this is an unsigned integer, cast it.
                // NOTE: This will throw if the value is > long.MaxValue.
                else if (_type == ParameterType.UnsignedInteger)
                {
                    return (long)(ulong)_value;
                }

                // Otherwise, throw an InvalidCastException.
                // NOTE: We don't auto-convert floating points, since
                // that might result in data being lost (e.g. 0.5 -> 0).
                else
                {
                    throw new InvalidCastException(
                        "Cannot cast parameter value to a signed integer");
                }
            }
            set
            {
                _type = ParameterType.SignedInteger;
                _value = value;
            }
        }

        public ulong ValueUnsignedInteger
        {
            get
            {
                // If this is an unsigned integer, just return it.
                if (_type == ParameterType.UnsignedInteger)
                {
                    return (ulong)_value;
                }

                // If this is a signed integer, cast it.
                // NOTE: This will throw if the value is < 0.
                else if (_type == ParameterType.SignedInteger)
                {
                    return (ulong)(long)_value;
                }

                // Otherwise, throw an InvalidCastException.
                // NOTE: We don't auto-convert floating points, since
                // that might result in data being lost (e.g. 0.5 -> 0).
                else
                {
                    throw new InvalidCastException(
                        "Cannot cast parameter value to an unsigned integer");
                }
            }
            set
            {
                _type = ParameterType.UnsignedInteger;
                _value = value;
            }
        }

        public double ValueFloatingPoint
        {
            get
            {
                // If this is a floating point, just return it.
                if (_type == ParameterType.FloatingPoint)
                {
                    return (double)_value;
                }

                // If this is a signed integer, cast it.
                else if (_type == ParameterType.SignedInteger)
                {
                    return (long)_value;
                }

                // If this is an unsigned integer, cast it.
                else if (_type == ParameterType.UnsignedInteger)
                {
                    return (ulong)_value;
                }

                // Otherwise, throw an InvalidCastException.
                else
                {
                    throw new InvalidCastException(
                        "Cannot cast parameter value to a floating point");
                }
            }
            set
            {
                _type = ParameterType.FloatingPoint;
                _value = value;
            }
        }

        public string ValueString
        {
            get => (string)_value;
            set
            {
                _type = ParameterType.String;
                _value = value;
            }
        }

        public List<Parameter> ValueArray
        {
            get => (List<Parameter>)_value;
            set
            {
                _type = ParameterType.Array;
                _value = value;
            }
        }

        public ParameterCollection ValueObject
        {
            get => (ParameterCollection)_value;
            set
            {
                _type = ParameterType.Object;
                _value = value;
            }
        }
        #endregion Public Properties

        #region Public Constructors
        public Parameter() { }

        public Parameter(ParameterType type)
        {
            _type = type;
            _value = type switch
            {
                ParameterType.Boolean => false,
                ParameterType.SignedInteger => 0L,
                ParameterType.UnsignedInteger => 0UL,
                ParameterType.FloatingPoint => 0.0,
                ParameterType.String => string.Empty,
                ParameterType.Array => new List<Parameter>(),
                ParameterType.Object => new ParameterCollection(),
                _ => throw new ArgumentException(
                    "Invalid parameter type",
                    nameof(type)),
            };
        }

        public Parameter(bool valueBoolean)
        {
            _type = ParameterType.Boolean;
            _value = valueBoolean;
        }

        public Parameter(long valueSignedInteger)
        {
            _type = ParameterType.SignedInteger;
            _value = valueSignedInteger;
        }

        public Parameter(ulong valueUnsignedInteger)
        {
            _type = ParameterType.UnsignedInteger;
            _value = valueUnsignedInteger;
        }

        public Parameter(double valueFloatingPoint)
        {
            _type = ParameterType.FloatingPoint;
            _value = valueFloatingPoint;
        }

        public Parameter(string valueString)
        {
            _type = ParameterType.String;
            _value = valueString;
        }

        public Parameter(List<Parameter> valueArray)
        {
            _type = ParameterType.Array;
            _value = valueArray;
        }

        public Parameter(ParameterCollection valueObject)
        {
            _type = ParameterType.Object;
            _value = valueObject;
        }
        #endregion Public Constructors

        #region Internal Methods
        internal void WriteValue(Utf8JsonWriter writer)
        {
            switch (_type)
            {
                case ParameterType.Boolean:
                    writer.WriteBooleanValue(ValueBoolean);
                    break;

                case ParameterType.SignedInteger:
                    writer.WriteNumberValue(ValueSignedInteger);
                    break;

                case ParameterType.UnsignedInteger:
                    writer.WriteNumberValue(ValueUnsignedInteger);
                    break;

                case ParameterType.FloatingPoint:
                    writer.WriteNumberValue(ValueFloatingPoint);
                    break;

                case ParameterType.String:
                    writer.WriteStringValue(ValueString);
                    break;

                case ParameterType.Array:
                    writer.WriteStartArray();

                    foreach (var param in ValueArray)
                    {
                        param.WriteValue(writer);
                    }

                    writer.WriteEndArray();
                    break;

                case ParameterType.Object:
                    writer.WriteStartObject();
                    ValueObject.WriteAll(writer);
                    writer.WriteEndObject();
                    break;
            }
        }
        #endregion Internal Methods
    }
}
