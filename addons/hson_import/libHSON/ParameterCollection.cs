// NOTE: This pragma warning disable is a work-around to what appears to be
// a mistake in .NET Standard 2.1, which we target in order to support things
// like the Unity Engine.
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text.Json;

namespace libHSON
{
    public class ParameterCollection : IDictionary<string, Parameter>,
        IDictionary, IReadOnlyDictionary<string, Parameter>,
        ISerializable, IDeserializationCallback
    {
        private class Impl : Dictionary<string, Parameter>
        {
            #region Public Constructors
            public Impl() : base() { }

            public Impl(IDictionary<string, Parameter> dictionary) :
                base(dictionary) { }

            public Impl(IDictionary<string, Parameter> dictionary,
                IEqualityComparer<string>? comparer) :
                base(dictionary, comparer) { }

            public Impl(
                IEnumerable<KeyValuePair<string, Parameter>> collection) :
                base(collection) { }

            public Impl(
                IEnumerable<KeyValuePair<string, Parameter>> collection,
                IEqualityComparer<string>? comparer) :
                base(collection, comparer) { }

            public Impl(IEqualityComparer<string>? comparer) :
                base(comparer) { }

            public Impl(int capacity) :
                base(capacity) { }

            public Impl(int capacity, IEqualityComparer<string>? comparer) :
                base(capacity, comparer) { }

            public Impl(SerializationInfo info, StreamingContext context) :
                base(info, context) { }
            #endregion Public Constructors
        }

        #region Private Fields
        private readonly Impl _data;
        #endregion Private Fields

        #region Private Properties
        bool ICollection.IsSynchronized =>
            ((ICollection)_data).IsSynchronized;

        object ICollection.SyncRoot =>
            ((ICollection)_data).SyncRoot;

        bool ICollection<KeyValuePair<string, Parameter>>.IsReadOnly =>
            ((ICollection<KeyValuePair<string, Parameter>>)_data).IsReadOnly;

        bool IDictionary.IsFixedSize =>
            ((IDictionary)_data).IsFixedSize;

        bool IDictionary.IsReadOnly =>
            ((IDictionary)_data).IsReadOnly;

        object? IDictionary.this[object key]
        {
            get
            {
                return key is string keyAsStr ?
                    this[keyAsStr] : (object?)null;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (key is string keyAsStr)
                {
                    if (value is Parameter valueAsParam)
                    {
                        this[keyAsStr] = valueAsParam;
                    }
                    else
                    {
                        throw new ArgumentException(
                            "The given value is not of type Parameter",
                            nameof(value));
                    }
                }
                else
                {
                    throw new ArgumentException(
                        "The given key is not of type string",
                        nameof(key));
                }
            }
        }

        ICollection IDictionary.Keys =>
            ((IDictionary)_data).Keys;

        ICollection IDictionary.Values =>
            ((IDictionary)_data).Values;

        ICollection<string> IDictionary<string, Parameter>.Keys =>
            ((IDictionary<string, Parameter>)_data).Keys;

        ICollection<Parameter> IDictionary<string, Parameter>.Values =>
            ((IDictionary<string, Parameter>)_data).Values;

        IEnumerable<string> IReadOnlyDictionary<string, Parameter>.Keys =>
            ((IReadOnlyDictionary<string, Parameter>)_data).Keys;

        IEnumerable<Parameter> IReadOnlyDictionary<string, Parameter>.Values =>
            ((IReadOnlyDictionary<string, Parameter>)_data).Values;

        #endregion Private Properties

        #region Public Properties
        public IEqualityComparer<string> Comparer => _data.Comparer;

        public int Count => _data.Count;

        public Parameter this[string key]
        {
            get
            {
                if (!TryGetValue(key, out var value))
                {
                    throw new KeyNotFoundException();
                }

                return value;
            }
            set
            {
                // Set parameter by name.
                var firstNameSepIndex = key.IndexOf('/');
                if (firstNameSepIndex == -1)
                {
                    _data[key] = value;
                }

                // Set parameter by path.
                else
                {
                    var curParams = this;
                    foreach (var (name, isFinalName) in
                        GetEachNameInPath(key, firstNameSepIndex))
                    {
                        if (isFinalName)
                        {
                            // Set the final parameter to the given value.
                            curParams._data[name] = value;
                            return;
                        }
                        else
                        {
                            // Add a new parameter of type object, or use the existing parameter.
                            var curParam = new Parameter(ParameterType.Object);
                            if (!curParams._data.TryAdd(name, curParam))
                            {
                                curParam = curParams._data[name];
                                if (!curParam.IsObject)
                                {
                                    throw new ArgumentException(
                                        "Could not add a parameter at the given " +
                                        "path; one or more of the parent parameters " +
                                        "are not of type object.", nameof(key));
                                }
                            }

                            // Recurse through the current parameter's children.
                            curParams = curParam.ValueObject;
                        }
                    }
                    
                    // This should never happen, since GetEachNameInPath
                    // should *always* return at least two names.
                    throw new InvalidOperationException("this should never happen");
                }
            }
        }

        public Impl.KeyCollection Keys => _data.Keys;

        public Impl.ValueCollection Values => _data.Values;
        #endregion Public Properties

        #region Protected Constructors
        protected ParameterCollection(SerializationInfo info,
            StreamingContext context)
        {
            _data = new Impl(info, context);
        }
        #endregion Protected Constructors

        #region Public Constructors
        public ParameterCollection()
        {
            _data = new Impl();
        }

        public ParameterCollection(IDictionary<string, Parameter> dictionary)
        {
            _data = new Impl(dictionary);
        }

        public ParameterCollection(IDictionary<string, Parameter> dictionary,
            IEqualityComparer<string>? comparer)
        {
            _data = new Impl(dictionary, comparer);
        }

        public ParameterCollection(
            IEnumerable<KeyValuePair<string, Parameter>> collection)
        {
            _data = new Impl(collection);
        }

        public ParameterCollection(
            IEnumerable<KeyValuePair<string, Parameter>> collection,
            IEqualityComparer<string>? comparer)
        {
            _data = new Impl(collection, comparer);
        }

        public ParameterCollection(IEqualityComparer<string>? comparer)
        {
            _data = new Impl(comparer);
        }

        public ParameterCollection(int capacity)
        {
            _data = new Impl(capacity);
        }

        public ParameterCollection(int capacity,
            IEqualityComparer<string>? comparer)
        {
            _data = new Impl(capacity, comparer);
        }
        #endregion Public Constructors

        #region Private Methods
        void ICollection.CopyTo(Array array, int index) =>
            ((ICollection)_data).CopyTo(array, index);

        void ICollection<KeyValuePair<string, Parameter>>.Add(
            KeyValuePair<string, Parameter> keyValuePair)
        {
            Add(keyValuePair.Key, keyValuePair.Value);
        }

        bool ICollection<KeyValuePair<string, Parameter>>.Contains(
            KeyValuePair<string, Parameter> keyValuePair)
        {
            if (TryGetValue(keyValuePair.Key, out var param))
            {
                return EqualityComparer<Parameter>.Default.Equals(
                    param, keyValuePair.Value);
            }
            else
            {
                return false;
            }
        }

        void ICollection<KeyValuePair<string, Parameter>>.CopyTo(
            KeyValuePair<string, Parameter>[] array, int index)
        {
            ((ICollection<KeyValuePair<string, Parameter>>)_data)
                .CopyTo(array, index);
        }

        bool ICollection<KeyValuePair<string, Parameter>>.Remove(
            KeyValuePair<string, Parameter> keyValuePair)
        {
            if (TryGetValue(keyValuePair.Key, out var param) &&
                EqualityComparer<Parameter>.Default.Equals(
                    param, keyValuePair.Value))
            {
                return Remove(keyValuePair.Key);
            }
            else
            {
                return false;
            }
        }

        void IDictionary.Add(object key, object? value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (key is string keyAsStr)
            {
                if (value is Parameter valueAsParam)
                {
                    Add(keyAsStr, valueAsParam);
                }
                else
                {
                    throw new ArgumentException(
                        "The given value is not of type Parameter",
                        nameof(value));
                }
            }
            else
            {
                throw new ArgumentException(
                    "The given key is not of type string",
                    nameof(key));
            }
        }

        bool IDictionary.Contains(object key) =>
            (key is string keyAsStr) && ContainsKey(keyAsStr);

        IDictionaryEnumerator IDictionary.GetEnumerator() =>
            ((IDictionary)_data).GetEnumerator();

        void IDictionary.Remove(object key)
        {
            if (key is string keyAsStr)
            {
                Remove(keyAsStr);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() =>
            ((IEnumerable)_data).GetEnumerator();

        IEnumerator<KeyValuePair<string, Parameter>>
            IEnumerable<KeyValuePair<string, Parameter>>.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, Parameter>>)_data)
                .GetEnumerator();
        }
        #endregion Public Methods

        #region Internal Methods
        internal static IEnumerable<(string name, bool isFinalName)>
            GetEachNameInPath(string path, int firstNameSepIndex)
        {
            // Return the first name in the path.
            yield return (path[..firstNameSepIndex], false);

            // Loop through the path and return each subsequent name.
            var curNameIndex = firstNameSepIndex + 1;
            var nextNameSepIndex = path.IndexOf('/', curNameIndex);

            while (nextNameSepIndex != -1)
            {
                yield return (path[curNameIndex..nextNameSepIndex], false);

                curNameIndex = nextNameSepIndex + 1;
                nextNameSepIndex = path.IndexOf('/', curNameIndex);
            }

            // Return the final name in the path.
            yield return (path[curNameIndex..], true);
        }

        internal bool TryGetParameterFromName(string key,
            [MaybeNullWhen(false)] out Parameter value)
        {
            return _data.TryGetValue(key, out value);
        }

        internal bool TryGetParameterFromPath(
            string key, int firstNameSepIndex,
            [MaybeNullWhen(false)] out Parameter value)
        {
            var curParams = this;
            foreach (var (name, isFinalName) in
                GetEachNameInPath(key, firstNameSepIndex))
            {
                // Get the next parameter. Stop searching if we can't find it.
                if (!curParams._data.TryGetValue(name, out var curParam))
                {
                    break;
                }

                // If this is the final parameter, return it regardless of its type.
                if (isFinalName)
                {
                    value = curParam;
                    return true;
                }

                // Otherwise, ensure this parameter is of type object,
                // so we can recurse through its children.
                else if (!curParam.IsObject)
                {
                    break;
                }

                // Recurse through the current parameter's children.
                curParams = curParam.ValueObject;
            }

            value = default;
            return false;
        }

        internal void WriteAll(Utf8JsonWriter writer)
        {
            foreach (var param in this)
            {
                writer.WritePropertyName(param.Key);
                param.Value.WriteValue(writer);
            }
        }

        internal bool InsertCopiesFrom(ParameterCollection other)
        {
            bool insertedAnyParams = false;
            foreach (var otherParam in other._data)
            {
                // For object parameters, insert a recursive deep copy.
                if (otherParam.Value.IsObject)
                {
                    // Get the existing object parameter, if any.
                    if (_data.TryGetValue(otherParam.Key, out var myParam))
                    {
                        // Ensure it is actually of type object.
                        if (!myParam.IsObject)
                        {
                            continue;
                        }
                    }

                    // If no object parameter exists yet, create a new one.
                    else
                    {
                        myParam = new Parameter(ParameterType.Object);
                        _data.Add(otherParam.Key, myParam);
                        insertedAnyParams = true;
                    }

                    // Recursively insert copies into the object parameter we got/created.
                    if (myParam.ValueObject.InsertCopiesFrom(
                        otherParam.Value.ValueObject))
                    {
                        insertedAnyParams = true;
                    }
                }

                // For non-object parameters, just insert a reference.
                else if (_data.TryAdd(otherParam.Key, otherParam.Value))
                {
                    insertedAnyParams = true;
                }
            }

            return insertedAnyParams;
        }
        #endregion Internal Methods

        #region Public Methods
        public void Add(string key, Parameter value)
        {
            if (!TryAdd(key, value))
            {
                throw new ArgumentException(
                    "A parameter already exists at the given path.",
                    nameof(key));
            }
        }

        public void Clear() => _data.Clear();

        public bool ContainsKey(string key) =>
            TryGetValue(key, out _);

        public bool ContainsValue(Parameter value) =>
            _data.ContainsValue(value);

        public int EnsureCapacity(int capacity) =>
            _data.EnsureCapacity(capacity);

        public Impl.Enumerator GetEnumerator() =>
            _data.GetEnumerator();

        public virtual void GetObjectData(SerializationInfo info,
            StreamingContext context)
        {
            _data.GetObjectData(info, context);
        }

        public virtual void OnDeserialization(object? sender) =>
            _data.OnDeserialization(sender);

        public bool Remove(string key) => Remove(key, out _);

        public bool Remove(string key,
            [MaybeNullWhen(false)] out Parameter value)
        {
            // Remove parameter by name.
            var firstNameSepIndex = key.IndexOf('/');
            if (firstNameSepIndex == -1)
            {
                return _data.Remove(key, out value);
            }

            // Remove parameter by path.
            else
            {
                var curParams = this;
                foreach (var (name, isFinalName) in
                    GetEachNameInPath(key, firstNameSepIndex))
                {
                    // If this is the final parameter, just remove it and return.
                    if (isFinalName)
                    {
                        return curParams._data.Remove(name, out value);
                    }

                    // Otherwise, get the next parameter. Stop searching if we can't
                    // find it, or if we do find it, but it is not of type object
                    // (since in that case, we can't recurse through its children).
                    if (!curParams._data.TryGetValue(name, out var curParam) ||
                        !curParam.IsObject)
                    {
                        break;
                    }

                    // Recurse through the current parameter's children.
                    curParams = curParam.ValueObject;
                }

                value = default;
                return false;
            }
        }

        public void TrimExcess() => _data.TrimExcess();

        public void TrimExcess(int capacity) => _data.TrimExcess(capacity);

        public bool TryAdd(string key, Parameter value)
        {
            // Add parameter by name.
            var firstNameSepIndex = key.IndexOf('/');
            if (firstNameSepIndex == -1)
            {
                return _data.TryAdd(key, value);
            }

            // Add parameter by path.
            else
            {
                var curParams = this;
                foreach (var (name, isFinalName) in
                    GetEachNameInPath(key, firstNameSepIndex))
                {
                    // This is not the final name; add a new parameter of type
                    // object, or use any existing parameters with the same name.
                    if (!isFinalName)
                    {
                        if (!curParams._data.TryGetValue(name, out var curParam))
                        {
                            curParam = new Parameter(ParameterType.Object);
                            curParams._data.Add(name, curParam);
                        }
                        else if (!curParam.IsObject)
                        {
                            throw new ArgumentException(
                                "Could not add a parameter at the given " +
                                "path; one or more of the parent parameters " +
                                "are not of type object.", nameof(key));
                        }

                        curParams = curParam.ValueObject;
                    }

                    // This is the final name; just add the parameter.
                    else
                    {
                        return curParams._data.TryAdd(name, value);
                    }
                }

                // This should never happen, since GetEachNameInPath
                // should *always* return at least two names.
                throw new InvalidOperationException("this should never happen");
            }
        }

        public bool TryGetValue(string key,
            [MaybeNullWhen(false)] out Parameter value)
        {
            // Get parameter by name or by path.
            var firstNameSepIndex = key.IndexOf('/');
            return firstNameSepIndex == -1 ?
                _data.TryGetValue(key, out value) :                         // by name
                TryGetParameterFromPath(key, firstNameSepIndex, out value); // by path
        }
        #endregion Public Methods
    }
}
