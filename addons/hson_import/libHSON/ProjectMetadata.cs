using System;
using System.Globalization;
using System.Text.Json;

namespace libHSON
{
    public class ProjectMetadata
    {
        #region Public Fields
        public string? Name;

        public string? Author;

        public DateTime? Date;

        public string? Version;

        public string? Description;

        public ParameterCollection CustomProperties = new ParameterCollection();
        #endregion Public Fields

        #region Internal Properties
        internal bool HasAnySpecifiedValue
        {
            get
            {
                return !string.IsNullOrEmpty(Name) ||
                    !string.IsNullOrEmpty(Author) || Date.HasValue ||
                    !string.IsNullOrEmpty(Version) ||
                    !string.IsNullOrEmpty(Description) ||
                    CustomProperties.Count > 0;
            }
        }
        #endregion Internal Properties

        #region Public Properties
        public string? DateString => Date?.ToString(
            "yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture);
        #endregion Public Properties

        #region Internal Methods
        internal void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject("metadata");

            // Write name if necessary.
            if (!string.IsNullOrEmpty(Name))
            {
                writer.WriteString("name", Name);
            }

            // Write author if necessary.
            if (!string.IsNullOrEmpty(Author))
            {
                writer.WriteString("author", Author);
            }

            // Write date if necessary.
            if (Date.HasValue)
            {
                writer.WriteString("date", DateString);
            }

            // Write version if necessary.
            if (!string.IsNullOrEmpty(Version))
            {
                writer.WriteString("version", Version);
            }

            // Write description if necessary.
            if (!string.IsNullOrEmpty(Description))
            {
                writer.WriteString("description", Description);
            }

            // Write custom properties if necessary.
            if (CustomProperties.Count > 0)
            {
                CustomProperties.WriteAll(writer);
            }

            writer.WriteEndObject();
        }
        #endregion Internal Methods
    }
}
