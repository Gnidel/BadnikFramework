using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text.Json;

namespace libHSON
{
    internal class ProjectReader
    {
        private enum State
        {
            None,
            TopLevelObject,

            FileVersionNumber,

            MetadataSection,
            Metadata,
            MetadataName,
            MetadataAuthor,
            MetadataDate,
            MetadataVersion,
            MetadataDescription,

            ObjectsSection,
            Objects,
            Object,
            ObjectId,
            ObjectName,
            ObjectParentId,
            ObjectInstanceOf,
            ObjectType,
            ObjectPositionSection,
            ObjectPosition,
            ObjectRotationSection,
            ObjectRotation,
            ObjectScaleSection,
            ObjectScale,
            ObjectIsEditorVisible,
            ObjectIsExcluded,
            ObjectParametersSection,
            ObjectParameters,
        }

        private class ObjectIdMapping
        {
            #region Public Fields
            public Object Object = new Object(Guid.Empty);

            public Guid? ParentId;

            public Guid? InstanceOf;
            #endregion Public Fields
        }

        #region Private Fields
        private readonly Project _project;

        private ObjectIdMapping _curObjMap = new ObjectIdMapping();

        private List<ObjectIdMapping> _objIdMaps = new List<ObjectIdMapping>();

        private Stack<Parameter> _paramStack = new Stack<Parameter>();

        private uint _fileVersionNumber = 0;

        private uint _curVecElemIndex = 0;

        private float[] _curVecData = new float[4];

        private State _curState = State.None;
        #endregion Private Fields

        #region Public Constructors
        public ProjectReader(Project project)
        {
            _project = project;
        }
        #endregion Public Constructors

        #region Private Methods
        private void ParseStartObject(Utf8JsonReader reader,
            ProjectReadOptions hsonOptions = default)
        {
            // Handle parameters.
            if (_paramStack.Count > 0)
            {
                // If the current parameter is an array, add a new object to it.
                var curParam = _paramStack.Peek();
                if (curParam.IsArray)
                {
                    var newParam = new Parameter(ParameterType.Object);
                    curParam.ValueArray.Add(newParam);
                    _paramStack.Push(newParam);
                }

                // Otherwise, replace the current parameter with a new object.
                else
                {
                    curParam.ValueObject = new ParameterCollection();
                }

                return;
            }

            switch (_curState)
            {
                case State.None:
                    _curState = State.TopLevelObject;
                    break;

                case State.MetadataSection:
                    _curState = State.Metadata;
                    break;

                case State.Objects:
                    _curState = State.Object;
                    break;

                case State.ObjectParametersSection:
                    _curState = State.ObjectParameters;
                    break;

                default:
                    throw new InvalidDataException();
            }
        }

        private void ParseEndObject(Utf8JsonReader reader,
            ProjectReadOptions hsonOptions = default)
        {
            // Handle parameters.
            if (_paramStack.Count > 0)
            {
                // Error out if the top parameter is not of type object.
                if (!_paramStack.Peek().IsObject)
                {
                    throw new InvalidDataException();
                }

                // Otherwise, pop the top parameter off of the param stack.
                _paramStack.Pop();
                return;
            }

            switch (_curState)
            {
                case State.TopLevelObject:
                    _curState = State.None;
                    break;

                case State.Metadata:
                    _curState = State.TopLevelObject;
                    break;

                case State.Object:
                    // If the current object does not have an id, generate one for it.
                    if (_curObjMap.Object.Id == Guid.Empty)
                    {
                        _curObjMap.Object.Id = Guid.NewGuid();
                    }

                    // Add the ID mapping for the current object to the mappings list.
                    _objIdMaps.Add(_curObjMap);
                    _curObjMap = new ObjectIdMapping();
                    _curState = State.Objects;
                    break;

                case State.ObjectParameters:
                    _curState = State.Object;
                    break;

                default:
                    throw new InvalidDataException();
            }
        }

        private void ParseStartArray(Utf8JsonReader reader,
            ProjectReadOptions hsonOptions = default)
        {
            // Handle parameters.
            if (_paramStack.Count > 0)
            {
                // If the current parameter is an array, add a new array to it.
                var curParam = _paramStack.Peek();
                if (curParam.IsArray)
                {
                    var newParam = new Parameter(ParameterType.Array);
                    curParam.ValueArray.Add(newParam);
                    _paramStack.Push(newParam);
                }

                // Otherwise, replace the current parameter with a new array.
                else
                {
                    curParam.ValueArray = new List<Parameter>();
                }

                return;
            }

            switch (_curState)
            {
                case State.ObjectsSection:
                    _curState = State.Objects;
                    break;

                case State.ObjectPositionSection:
                    Object.DefaultPosition.CopyTo(_curVecData);
                    _curState = State.ObjectPosition;
                    break;

                case State.ObjectRotationSection:
                    // Why do Quaternions not have a CopyTo method???
                    _curVecData[0] = Object.DefaultRotation.X;
                    _curVecData[1] = Object.DefaultRotation.Y;
                    _curVecData[2] = Object.DefaultRotation.Z;
                    _curVecData[3] = Object.DefaultRotation.W;
                    _curState = State.ObjectRotation;
                    break;

                case State.ObjectScaleSection:
                    Object.DefaultScale.CopyTo(_curVecData);
                    _curState = State.ObjectScale;
                    break;

                default:
                    throw new InvalidDataException();
            }
        }

        private void ParseEndArray(Utf8JsonReader reader,
            ProjectReadOptions hsonOptions = default)
        {
            // Handle parameters.
            if (_paramStack.Count > 0)
            {
                // Pop the top parameter off of the param stack,
                // and ensure that it is of type array.
                if (!_paramStack.Pop().IsArray)
                {
                    throw new InvalidDataException();
                }
                return;
            }

            switch (_curState)
            {
                case State.Objects:
                    _curState = State.TopLevelObject;
                    break;

                case State.ObjectPosition:
                    _curObjMap.Object.LocalPosition = new Vector3(
                        _curVecData[0], _curVecData[1], _curVecData[2]);

                    _curVecElemIndex = 0;
                    _curState = State.Object;
                    break;

                case State.ObjectRotation:
                    _curObjMap.Object.LocalRotation = new Quaternion(
                        _curVecData[0], _curVecData[1],
                        _curVecData[2], _curVecData[3]);

                    _curVecElemIndex = 0;
                    _curState = State.Object;
                    break;

                case State.ObjectScale:
                    _curObjMap.Object.LocalScale = new Vector3(
                        _curVecData[0], _curVecData[1], _curVecData[2]);

                    _curVecElemIndex = 0;
                    _curState = State.Object;
                    break;

                default:
                    throw new InvalidDataException();
            }
        }

        private void InsertParameter(string key, ParameterCollection parameters)
        {
            // If an object parameter is the last in the param stack,
            // the new parameter should be a child of that parameter.
            ParameterCollection? curParams;
            if (_paramStack.TryPeek(out var curParam))
            {
                if (curParam.IsObject)
                {
                    curParams = curParam.ValueObject;
                }
                else
                {
                    throw new InvalidDataException();
                }
            }

            // Otherwise, the new parameter should just be added to
            // the current object's local parameter list.
            else
            {
                curParams = parameters;
            }

            // Add new parameter to the collection and push onto the param stack.
            var newParam = new Parameter();
            curParams.Add(key, newParam);
            _paramStack.Push(newParam);
        }

        private void ParsePropertyName(Utf8JsonReader reader,
            ProjectReadOptions hsonOptions = default)
        {
            // Get the string from the reader; it should never be
            // null since we only ever call this method if TokenType
            // was String, NOT Null.
            var key = reader.GetString() ?? throw new InvalidOperationException(
                "string was null despite TokenType being String; this should never happen");

            // Parse property name based on current state.
            switch (_curState)
            {
                case State.TopLevelObject:
                    // NOTE: This check is to avoid conflicts with nested custom properties.
                    if (_paramStack.Count == 0)
                    {
                        if (key == "version")
                        {
                            _curState = State.FileVersionNumber;
                            break;
                        }
                        else if (key == "metadata")
                        {
                            _curState = State.MetadataSection;
                            break;
                        }
                        else if (key == "objects")
                        {
                            _curState = State.ObjectsSection;
                            break;
                        }
                    }

                    // Custom properties.
                    InsertParameter(key, _project.CustomProperties);
                    break;

                case State.Metadata:
                    // NOTE: This check is to avoid conflicts with nested custom properties.
                    if (_paramStack.Count == 0)
                    {
                        if (key == "name")
                        {
                            _curState = State.MetadataName;
                            break;
                        }
                        else if (key == "author")
                        {
                            _curState = State.MetadataAuthor;
                            break;
                        }
                        else if (key == "date")
                        {
                            _curState = State.MetadataDate;
                            break;
                        }
                        else if (key == "version")
                        {
                            _curState = State.MetadataVersion;
                            break;
                        }
                        else if (key == "description")
                        {
                            _curState = State.MetadataDescription;
                            break;
                        }
                    }

                    // Custom properties.
                    InsertParameter(key, _project.Metadata.CustomProperties);
                    break;

                case State.Object:
                    // NOTE: This check is to avoid conflicts with nested custom properties.
                    if (_paramStack.Count == 0)
                    {
                        if (key == "id")
                        {
                            _curState = State.ObjectId;
                            break;
                        }
                        else if (key == "name")
                        {
                            _curState = State.ObjectName;
                            break;
                        }
                        else if (key == "parentId")
                        {
                            _curState = State.ObjectParentId;
                            break;
                        }
                        else if (key == "instanceOf")
                        {
                            _curState = State.ObjectInstanceOf;
                            break;
                        }
                        else if (key == "type")
                        {
                            _curState = State.ObjectType;
                            break;
                        }
                        else if (key == "position")
                        {
                            _curState = State.ObjectPositionSection;
                            break;
                        }
                        else if (key == "rotation")
                        {
                            _curState = State.ObjectRotationSection;
                            break;
                        }
                        else if (key == "scale")
                        {
                            _curState = State.ObjectScaleSection;
                            break;
                        }
                        else if (key == "isEditorVisible")
                        {
                            _curState = State.ObjectIsEditorVisible;
                            break;
                        }
                        else if (key == "isExcluded")
                        {
                            _curState = State.ObjectIsExcluded;
                            break;
                        }
                        else if (key == "parameters")
                        {
                            _curState = State.ObjectParametersSection;
                            break;
                        }
                    }

                    // Custom properties.
                    InsertParameter(key, _curObjMap.Object.LocalCustomProperties);
                    break;

                case State.ObjectParameters:
                    InsertParameter(key, _curObjMap.Object.LocalParameters);
                    break;

                default:
                    throw new InvalidDataException();
            }
        }

        private void ParseString(Utf8JsonReader reader,
            ProjectReadOptions hsonOptions = default)
        {
            // Handle parameters.
            if (_paramStack.Count > 0)
            {
                // If the current parameter is an array, add a new string to it.
                var curParam = _paramStack.Peek();
                if (curParam.IsArray)
                {
                    var newParam = new Parameter(reader.GetString()!);
                    curParam.ValueArray.Add(newParam);
                }

                // Otherwise, replace the current parameter with a
                // new string and pop it off of the param stack.
                else
                {
                    curParam.ValueString = reader.GetString()!;
                    _paramStack.Pop();
                }

                return;
            }

            switch (_curState)
            {
                case State.MetadataName:
                    _project.Metadata.Name = reader.GetString();
                    _curState = State.Metadata;
                    break;

                case State.MetadataAuthor:
                    _project.Metadata.Author = reader.GetString();
                    _curState = State.Metadata;
                    break;

                case State.MetadataDate:
                    _project.Metadata.Date = DateTime.Parse(reader.GetString());
                    _curState = State.Metadata;
                    break;

                case State.MetadataVersion:
                    _project.Metadata.Version = reader.GetString();
                    _curState = State.Metadata;
                    break;

                case State.MetadataDescription:
                    _project.Metadata.Description = reader.GetString();
                    _curState = State.Metadata;
                    break;

                case State.ObjectId:
                    _curObjMap.Object.Id = Guid.ParseExact(reader.GetString()!, "B");
                    _curState = State.Object;
                    break;

                case State.ObjectName:
                    _curObjMap.Object.Name = reader.GetString()!;
                    _curState = State.Object;
                    break;

                case State.ObjectParentId:
                    _curObjMap.ParentId = Guid.ParseExact(reader.GetString()!, "B");
                    _curState = State.Object;
                    break;

                case State.ObjectInstanceOf:
                    _curObjMap.InstanceOf = Guid.ParseExact(reader.GetString()!, "B");
                    _curState = State.Object;
                    break;

                case State.ObjectType:
                    _curObjMap.Object.Type = reader.GetString()!;
                    _curState = State.Object;
                    break;

                default:
                    throw new InvalidDataException();
            }
        }

        private void ParseNumber(Utf8JsonReader reader,
            ProjectReadOptions hsonOptions = default)
        {
            // Handle parameters.
            if (_paramStack.Count > 0)
            {
                // Try to parse the number as an unsigned integer.
                if (reader.TryGetUInt64(out var valUint))
                {
                    // If the current parameter is an array, add a new uint to it.
                    var curParam = _paramStack.Peek();
                    if (curParam.IsArray)
                    {
                        var newParam = new Parameter(valUint);
                        curParam.ValueArray.Add(newParam);
                    }

                    // Otherwise, replace the current parameter with a
                    // new uint and pop it off of the param stack.
                    else
                    {
                        curParam.ValueUnsignedInteger = valUint;
                        _paramStack.Pop();
                    }
                }

                // Try to parse the number as a signed integer.
                else if (reader.TryGetInt64(out var valInt))
                {
                    // If the current parameter is an array, add a new int to it.
                    var curParam = _paramStack.Peek();
                    if (curParam.IsArray)
                    {
                        var newParam = new Parameter(valInt);
                        curParam.ValueArray.Add(newParam);
                    }

                    // Otherwise, replace the current parameter with a
                    // new int and pop it off of the param stack.
                    else
                    {
                        curParam.ValueSignedInteger = valInt;
                        _paramStack.Pop();
                    }
                }

                // Try to parse the number as a floating point.
                else if (reader.TryGetDouble(out var valDouble))
                {
                    // If the current parameter is an array, add a new double to it.
                    var curParam = _paramStack.Peek();
                    if (curParam.IsArray)
                    {
                        var newParam = new Parameter(valDouble);
                        curParam.ValueArray.Add(newParam);
                    }

                    // Otherwise, replace the current parameter with a
                    // new double and pop it off of the param stack.
                    else
                    {
                        curParam.ValueFloatingPoint = valDouble;
                        _paramStack.Pop();
                    }
                }

                // We failed to parse the number into a type accepted by Parameter.
                else
                {
                    throw new InvalidDataException();
                }

                return;
            }

            switch (_curState)
            {
                case State.FileVersionNumber:
                    _fileVersionNumber = reader.GetUInt32();
                    _curState = State.TopLevelObject;
                    break;

                case State.ObjectPosition:
                    if (_curVecElemIndex > 2)
                    {
                        throw new InvalidDataException(
                            "Position in HSON data had more than 3 elements.");
                    }

                    _curVecData[_curVecElemIndex++] = reader.GetSingle();
                    break;

                case State.ObjectRotation:
                    if (_curVecElemIndex > 3)
                    {
                        throw new InvalidDataException(
                            "Quaternion in HSON data had more than 4 elements.");
                    }

                    _curVecData[_curVecElemIndex++] = reader.GetSingle();
                    break;

                case State.ObjectScale:
                    if (_curVecElemIndex > 2)
                    {
                        throw new InvalidDataException(
                            "Scale in HSON data had more than 3 elements.");
                    }

                    _curVecData[_curVecElemIndex++] = reader.GetSingle();
                    break;

                default:
                    throw new InvalidDataException();
            }
        }

        private void ParseBool(Utf8JsonReader reader,
            ProjectReadOptions hsonOptions = default)
        {
            // Handle parameters.
            if (_paramStack.Count > 0)
            {
                // If the current parameter is an array, add a new boolean to it.
                var curParam = _paramStack.Peek();
                if (curParam.IsArray)
                {
                    var newParam = new Parameter(reader.GetBoolean());
                    curParam.ValueArray.Add(newParam);
                }

                // Otherwise, replace the current parameter with a
                // new boolean and pop it off of the param stack.
                else
                {
                    curParam.ValueBoolean = reader.GetBoolean();
                    _paramStack.Pop();
                }

                return;
            }

            switch (_curState)
            {
                case State.ObjectIsEditorVisible:
                    _curObjMap.Object.IsEditorVisible = reader.GetBoolean();
                    _curState = State.Object;
                    break;

                case State.ObjectIsExcluded:
                    _curObjMap.Object.IsExcluded = reader.GetBoolean();
                    _curState = State.Object;
                    break;

                default:
                    throw new InvalidDataException();
            }
        }

        public void ReadIntoAssignedProject(Utf8JsonReader reader,
            ProjectReadOptions hsonOptions = default)
        {
            // Reset state.
            _curObjMap = new ObjectIdMapping();
            _objIdMaps.Clear();
            _paramStack.Clear();
            _fileVersionNumber = 0;
            _curVecElemIndex = 0;
            _curState = State.None;

            // Read JSON tokens and parse into HSON project data.
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        ParseStartObject(reader, hsonOptions);
                        break;

                    case JsonTokenType.EndObject:
                        ParseEndObject(reader, hsonOptions);
                        break;

                    case JsonTokenType.StartArray:
                        ParseStartArray(reader, hsonOptions);
                        break;

                    case JsonTokenType.EndArray:
                        ParseEndArray(reader, hsonOptions);
                        break;

                    case JsonTokenType.PropertyName:
                        ParsePropertyName(reader, hsonOptions);
                        break;

                    case JsonTokenType.String:
                        ParseString(reader, hsonOptions);
                        break;

                    case JsonTokenType.Number:
                        ParseNumber(reader, hsonOptions);
                        break;

                    case JsonTokenType.True:
                    case JsonTokenType.False:
                        ParseBool(reader, hsonOptions);
                        break;
                }
            }

            // Fix object references using object id mappings, and add objects to project.
            foreach (var objIdMap in _objIdMaps)
            {
                // Fix parent references.
                if (objIdMap.ParentId.HasValue)
                {
                    if (!_project.Objects.TryGetValue(objIdMap.ParentId.Value, out var parent))
                    {
                        throw new InvalidDataException(
                            "An object in the HSON data references a " +
                            "parent object which does not exist.");
                    }

                    objIdMap.Object.Parent = parent;
                }

                // Fix instance references.
                if (objIdMap.InstanceOf.HasValue)
                {
                    if (!_project.Objects.TryGetValue(objIdMap.InstanceOf.Value, out var instance))
                    {
                        throw new InvalidDataException(
                            "An object in the HSON data references an " +
                            "instance object which does not exist.");
                    }

                    objIdMap.Object.InstanceOf = instance;
                }

                // Add object to project.
                _project.Objects.Add(objIdMap.Object);
            }
        }
        #endregion Private Methods
    }
}
