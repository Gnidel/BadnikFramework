using System;
using System.Numerics;
using System.Text.Json;

namespace libHSON
{
    public class Object
    {
        #region Public Constants
        public const string DefaultName = "";

        public static readonly Guid DefaultParentId = Guid.Empty;

        public static readonly Guid DefaultInstanceOf = Guid.Empty;

        public static readonly Vector3 DefaultPosition = Vector3.Zero;

        public static readonly Quaternion DefaultRotation = Quaternion.Identity;

        public static readonly Vector3 DefaultScale = Vector3.One;

        public const bool DefaultIsEditorVisible = true;

        public const bool DefaultIsExcluded = false;
        #endregion Public Constants

        #region Private Fields
        private Guid _id = Guid.NewGuid();

        private string? _name;

        private string? _type;

        private Object? _parent;

        private Object? _instanceOf;

        private Vector3? _position;

        private Quaternion? _rotation;

        private Vector3? _scale;

        private bool? _isEditorVisible;

        private bool? _isExcluded;

        private bool _hasSpecifiedParent = false;
        #endregion Private Fields

        #region Public Fields
        public ParameterCollection LocalParameters = new ParameterCollection();

        public ParameterCollection LocalCustomProperties = new ParameterCollection();
        #endregion Public Fields

        #region Private Properties
        private bool HasInvalidCustomProperties =>
            LocalCustomProperties.ContainsKey("id") ||
            LocalCustomProperties.ContainsKey("name") ||
            LocalCustomProperties.ContainsKey("parentId") ||
            LocalCustomProperties.ContainsKey("instanceOf") ||
            LocalCustomProperties.ContainsKey("type") ||
            LocalCustomProperties.ContainsKey("position") ||
            LocalCustomProperties.ContainsKey("rotation") ||
            LocalCustomProperties.ContainsKey("scale") ||
            LocalCustomProperties.ContainsKey("isEditorVisible") ||
            LocalCustomProperties.ContainsKey("isExcluded") ||
            LocalCustomProperties.ContainsKey("parameters");
        #endregion Private Properties

        #region Public Properties
        public string? SpecifiedName
        {
            get
            {
                // If the value we want is specified for this object, just return it.
                if (_name != null)
                {
                    return _name;
                }

                // Otherwise, if this is an instance of another object, we
                // inherit the value from the instanced object's property.
                else if (_instanceOf != null)
                {
                    return _instanceOf.SpecifiedName;
                }

                // Otherwise, return null.
                return null;
            }
            set => _name = value;
        }

        public string? SpecifiedType
        {
            get
            {
                // If the value we want is specified for this object, just return it.
                if (_type != null)
                {
                    return _type;
                }

                // Otherwise, if this is an instance of another object, we
                // inherit the value from the instanced object's property.
                else if (_instanceOf != null)
                {
                    return _instanceOf.SpecifiedType;
                }

                // Otherwise, return null.
                return null;
            }
            set =>_type = value;
        }

        public Object? SpecifiedParent
        {
            get
            {
                // If the value we want is specified for this object, just return it.
                if (_hasSpecifiedParent)
                {
                    return _parent;
                }

                // Otherwise, if this is an instance of another object, we
                // inherit the value from the instanced object's property.
                else if (_instanceOf != null)
                {
                    return _instanceOf.SpecifiedParent;
                }

                // Otherwise, return null.
                return null;
            }
            set
            {
                _parent = value;
                _hasSpecifiedParent = (value != null);
            }
        }

        public Vector3? SpecifiedPosition
        {
            get
            {
                // If the value we want is specified for this object, just return it.
                if (_position != null)
                {
                    return _position;
                }

                // Otherwise, if this is an instance of another object, we
                // inherit the value from the instanced object's property.
                else if (_instanceOf != null)
                {
                    return _instanceOf.SpecifiedPosition;
                }

                // Otherwise, return null.
                return null;
            }
            set => _position = value;
        }

        public Quaternion? SpecifiedRotation
        {
            get
            {
                // If the value we want is specified for this object, just return it.
                if (_rotation != null)
                {
                    return _rotation;
                }

                // Otherwise, if this is an instance of another object, we
                // inherit the value from the instanced object's property.
                else if (_instanceOf != null)
                {
                    return _instanceOf.SpecifiedRotation;
                }

                // Otherwise, return null.
                return null;
            }
            set => _rotation = value;
        }

        public Vector3? SpecifiedScale
        {
            get
            {
                // If the value we want is specified for this object, just return it.
                if (_scale != null)
                {
                    return _scale;
                }

                // Otherwise, if this is an instance of another object, we
                // inherit the value from the instanced object's property.
                else if (_instanceOf != null)
                {
                    return _instanceOf.SpecifiedScale;
                }

                // Otherwise, return null.
                return null;
            }
            set => _scale = value;
        }

        public bool? SpecifiedIsEditorVisible
        {
            get
            {
                // If the value we want is specified for this object, just return it.
                if (_isEditorVisible != null)
                {
                    return _isEditorVisible;
                }

                // Otherwise, if this is an instance of another object, we
                // inherit the value from the instanced object's property.
                else if (_instanceOf != null)
                {
                    return _instanceOf.SpecifiedIsEditorVisible;
                }

                // Otherwise, return null.
                return null;
            }
            set => _isEditorVisible = value;
        }

        public bool? SpecifiedIsExcluded
        {
            get
            {
                // If the value we want is specified for this object, just return it.
                if (_isExcluded != null)
                {
                    return _isExcluded;
                }

                // Otherwise, if this is an instance of another object, we
                // inherit the value from the instanced object's property.
                else if (_instanceOf != null)
                {
                    return _instanceOf.SpecifiedIsExcluded;
                }

                // Otherwise, return null.
                return null;
            }
            set => _isExcluded = value;
        }

        public bool HasSpecifiedName => SpecifiedName != null;

        public bool HasSpecifiedType => SpecifiedType != null;

        public bool HasSpecifiedParent
        {
            get
            {
                // If the value we want is specified for this object, return true.
                if (_hasSpecifiedParent)
                {
                    return true;
                }

                // Otherwise, if this is an instance of another object, we
                // inherit the value from the instanced object's property.
                else if (_instanceOf != null)
                {
                    return _instanceOf.HasSpecifiedParent;
                }

                // Otherwise, return false.
                return false;
            }
        }

        public bool HasSpecifiedPosition => SpecifiedPosition != null;

        public bool HasSpecifiedRotation => SpecifiedRotation != null;

        public bool HasSpecifiedScale => SpecifiedScale != null;

        public bool HasSpecifiedIsEditorVisible => SpecifiedIsEditorVisible != null;

        public bool HasSpecifiedIsExcluded => SpecifiedIsExcluded != null;

        public Guid Id
        {
            get => _id;
            set
            {
                if (value == Guid.Empty)
                {
                    throw new ArgumentException(
                        "Object ids cannot be empty");
                }

                _id = value;
            }
        }

        public string Name
        {
            get => SpecifiedName ?? DefaultName;
            set => SpecifiedName = value;
        }

        public string Type
        {
            get => SpecifiedType ?? string.Empty;
            set => SpecifiedType = value;
        }

        public Object? Parent
        {
            get => SpecifiedParent;
            set
            {
                _parent = value;
                _hasSpecifiedParent = true;
            }
        }

        public Guid ParentId
        {
            get
            {
                var parent = Parent;
                return (parent != null) ? parent.Id : Guid.Empty;
            }
        }

        public Object? InstanceOf
        {
            get => _instanceOf;
            set => _instanceOf = value;
        }

        public Guid InstanceOfId
        {
            get
            {
                var instanceOf = InstanceOf;
                return (instanceOf != null) ? instanceOf.Id : Guid.Empty;
            }
        }

        public Vector3 LocalPosition
        {
            get => SpecifiedPosition.GetValueOrDefault(DefaultPosition);
            set => SpecifiedPosition = value;
        }

        public Quaternion LocalRotation
        {
            get => SpecifiedRotation.GetValueOrDefault(DefaultRotation);
            set => SpecifiedRotation = value;
        }

        public Vector3 LocalScale
        {
            get => SpecifiedScale.GetValueOrDefault(DefaultScale);
            set => SpecifiedScale = value;
        }

        public Matrix4x4 LocalTransform
        {
            get
            {
                return Matrix4x4.CreateScale(LocalScale) *
                    Matrix4x4.CreateFromQuaternion(LocalRotation) *
                    Matrix4x4.CreateTranslation(LocalPosition);
            }
        }

        public Matrix4x4 GlobalTransform
        {
            get
            {
                var localTrans = LocalTransform;
                var parent = Parent;

                // If we have a parent, the transform is local to it.
                // Otherwise, the transform is already global.
                return parent != null ?
                    localTrans * parent.GlobalTransform :
                    localTrans;
            }
        }

        public bool IsEditorVisible
        {
            get => SpecifiedIsEditorVisible ?? DefaultIsEditorVisible;
            set => SpecifiedIsEditorVisible = value;
        }

        public bool IsExcluded
        {
            get => SpecifiedIsExcluded ?? DefaultIsExcluded;
            set => SpecifiedIsExcluded = value;
        }
        #endregion Public Properties

        #region Internal Constructors
        internal Object(Guid id)
        {
            _id = id;
        }
        #endregion Internal Constructors

        #region Public Constructors
        public Object() { }

        public Object(Guid? id = null, string? name = null,
            string? type = null, Object? parent = null,
            Object? instanceOf = null, Vector3? position = null,
            Quaternion? rotation = null, Vector3? scale = null,
            bool? isEditorVisible = null, bool? isExcluded = null)
        {
            if (id.HasValue && id.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Object ids cannot be empty",
                    nameof(id));
            }

            _id = id ?? Guid.NewGuid();
            _name = name;
            _type = type;
            _parent = parent;
            _instanceOf = instanceOf;
            _position = position;
            _rotation = rotation;
            _scale = scale;
            _isEditorVisible = isEditorVisible;
            _isExcluded = isExcluded;
            _hasSpecifiedParent = _parent != null;
        }
        #endregion Public Constructors

        #region Internal Methods
        internal Parameter? GetParameterFromName(string name)
        {
            // Attempt to find the parameter in the current object.
            if (LocalParameters.TryGetParameterFromName(name, out var param))
            {
                return param;
            }

            // Otherwise, if this is an instance of another object,
            // inherit the parameter from the instanced object.
            else if (_instanceOf != null)
            {
                return _instanceOf.GetParameterFromName(name);
            }

            // Otherwise, just return null.
            return null;
        }

        internal Parameter? GetParameterFromPath(
            string path, int firstNameSepIndex)
        {
            // Attempt to find the parameter in the current object.
            if (LocalParameters.TryGetParameterFromPath(path,
                firstNameSepIndex, out var param))
            {
                return param;
            }

            // Otherwise, if this is an instance of another object,
            // inherit the parameter from the instanced object.
            else if (_instanceOf != null)
            {
                return _instanceOf.GetParameterFromPath(
                    path, firstNameSepIndex);
            }

            // Otherwise, just return null.
            return null;
        }

        internal void Write(Utf8JsonWriter writer, ProjectWriteOptions hsonOptions)
        {
            writer.WriteStartObject();

            // Write id.
            writer.WriteString("id", $"{{{_id}}}");

            // Write name if necessary or requested.
            if (_name != null && (_instanceOf != null || _name.Length > 0))
            {
                writer.WriteString("name", _name);
            }
            else if (hsonOptions.IncludeUnnecessaryProperties && _instanceOf == null)
            {
                writer.WriteString("name", DefaultName);
            }

            // Write parentId if necessary or requested.
            if (_hasSpecifiedParent && (_instanceOf != null || _parent != null))
            {
                writer.WriteString("parentId",
                    $"{{{(_parent != null ? _parent._id : DefaultParentId)}}}");
            }
            else if (hsonOptions.IncludeUnnecessaryProperties && _instanceOf == null)
            {
                writer.WriteString("parentId", $"{{{DefaultParentId}}}");
            }

            // Write instanceOf if necessary or requested.
            if (_instanceOf != null)
            {
                writer.WriteString("instanceOf", $"{{{_instanceOf._id}}}");
            }
            else if (hsonOptions.IncludeUnnecessaryProperties)
            {
                writer.WriteString("instanceOf", $"{{{DefaultInstanceOf}}}");
            }

            // Write type if necessary.
            if (string.IsNullOrEmpty(_type))
            {
                // Type can be null or empty *ONLY* if we inherit a specified non-empty type.
                if (string.IsNullOrEmpty(SpecifiedType))
                {
                    throw new InvalidOperationException(
                        "Invalid SpecifiedType value; type *must* be specified as a " +
                        "non-empty string, unless type is inherited from an instanced " +
                        "object.");
                }
            }
            else
            {
                writer.WriteString("type", _type);
            }

            // Write position if necessary or requested.
            if (_position.HasValue && (_instanceOf != null ||
                _position.Value != DefaultPosition))
            {
                writer.WriteVector("position", _position.Value);
            }
            else if (hsonOptions.IncludeUnnecessaryProperties && _instanceOf == null)
            {
                writer.WriteVector("position", DefaultPosition);
            }

            // Write rotation if necessary or requested.
            if (_rotation.HasValue && (_instanceOf != null ||
                _rotation.Value != DefaultRotation))
            {
                writer.WriteQuaternion("rotation", _rotation.Value);
            }
            else if (hsonOptions.IncludeUnnecessaryProperties && _instanceOf == null)
            {
                writer.WriteQuaternion("rotation", DefaultRotation);
            }

            // Write scale if necessary or requested.
            if (_scale.HasValue && (_instanceOf != null ||
                _scale.Value != DefaultScale))
            {
                writer.WriteVector("scale", _scale.Value);
            }
            else if (hsonOptions.IncludeUnnecessaryProperties && _instanceOf == null)
            {
                writer.WriteVector("scale", DefaultScale);
            }

            // Write isEditorVisible if necessary or requested.
            if (_isEditorVisible.HasValue && (_instanceOf != null ||
                _isEditorVisible.Value != DefaultIsEditorVisible))
            {
                writer.WriteBoolean("isEditorVisible", _isEditorVisible.Value);
            }
            else if (hsonOptions.IncludeUnnecessaryProperties && _instanceOf == null)
            {
                writer.WriteBoolean("isEditorVisible", DefaultIsEditorVisible);
            }

            // Write isExcluded if necessary or requested.
            if (_isExcluded.HasValue && (_instanceOf != null ||
                _isExcluded.Value != DefaultIsExcluded))
            {
                writer.WriteBoolean("isExcluded", _isExcluded.Value);
            }
            else if (hsonOptions.IncludeUnnecessaryProperties && _instanceOf == null)
            {
                writer.WriteBoolean("isExcluded", DefaultIsExcluded);
            }

            // Write parameters if necessary.
            if (LocalParameters.Count > 0 || hsonOptions.IncludeUnnecessaryProperties)
            {
                writer.WriteStartObject("parameters");
                LocalParameters.WriteAll(writer);
                writer.WriteEndObject();
            }

            // Write custom properties if necessary.
            if (LocalCustomProperties.Count > 0 || hsonOptions.IncludeUnnecessaryProperties)
            {
                if (HasInvalidCustomProperties)
                {
                    throw new InvalidOperationException(
                        "Invalid LocalCustomProperties; custom properties CANNOT be " +
                        "named the same as properties that are part of the HSON standard.");
                }

                LocalCustomProperties.WriteAll(writer);
            }

            writer.WriteEndObject();
        }
        #endregion Internal Methods

        #region Public Methods
        public Parameter? GetParameter(string path)
        {
            var firstNameSepIndex = path.IndexOf('/');
            return (firstNameSepIndex != -1) ?
                GetParameterFromPath(path, firstNameSepIndex) :
                GetParameterFromName(path);
        }

        public bool GetFlattenedParameters(
            ParameterCollection flatParams, string rootPath = "")
        {
            // Add all parameters from the current object.
            bool insertedAnyParams = false;
            if (rootPath.Length > 0)
            {
                if (LocalParameters.TryGetValue(rootPath, out var param) &&
                    param.IsObject)
                {
                    insertedAnyParams = flatParams.InsertCopiesFrom(
                        param.ValueObject);
                }
            }
            else
            {
                insertedAnyParams = flatParams.InsertCopiesFrom(
                    LocalParameters);
            }

            // Recurse through instanced objects.
            if (_instanceOf != null &&
                _instanceOf.GetFlattenedParameters(flatParams, rootPath))
            {
                insertedAnyParams = true;
            }

            return insertedAnyParams;
        }

        public ParameterCollection GetFlattenedParameters(string rootPath = "")
        {
            var flatParams = new ParameterCollection();
            GetFlattenedParameters(flatParams, rootPath);
            return flatParams;
        }
        #endregion Public Methods
    }
}
