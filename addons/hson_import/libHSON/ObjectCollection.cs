using System;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace libHSON
{
    public class ObjectCollection : KeyedCollection<Guid, Object>
    {
        #region Protected Methods
        protected override Guid GetKeyForItem(Object item)
        {
            return item.Id;
        }
        #endregion Protected Methods

        #region Internal Methods
        internal void WriteAll(Utf8JsonWriter writer, ProjectWriteOptions hsonOptions)
        {
            writer.WriteStartArray("objects");

            // Write objects.
            foreach (var obj in this)
            {
                obj.Write(writer, hsonOptions);
            }

            writer.WriteEndArray();
        }
        #endregion Internal Methods
    }
}
