using System;
using System.Buffers;
using System.IO;
using System.Text.Json;

namespace libHSON
{
    public class Project
    {
        #region Public Constants
        public const uint MaxSupportedVersion = 1U;
        #endregion Public Constants

        #region Public Fields
        public ProjectMetadata Metadata = new ProjectMetadata();

        public ObjectCollection Objects = new ObjectCollection();

        public ParameterCollection CustomProperties = new ParameterCollection();
        #endregion Public Fields

        #region Public Methods
        public static Project FromFile(string filePath,
            ProjectReadOptions hsonOptions = default,
            JsonReaderOptions jsonOptions = default)
        {
            var project = new Project();
            project.Load(filePath, hsonOptions, jsonOptions);
            return project;
        }

        public static Project FromData(ReadOnlySequence<byte> hsonData,
            ProjectReadOptions hsonOptions = default,
            JsonReaderOptions jsonOptions = default)
        {
            var project = new Project();
            project.Read(hsonData, hsonOptions, jsonOptions);
            return project;
        }

        public static Project FromData(ReadOnlySpan<byte> hsonData,
            ProjectReadOptions hsonOptions = default,
            JsonReaderOptions jsonOptions = default)
        {
            var project = new Project();
            project.Read(hsonData, hsonOptions, jsonOptions);
            return project;
        }

        public void Read(Utf8JsonReader reader,
            ProjectReadOptions hsonOptions = default)
        {
            var projectReader = new ProjectReader(this);
            projectReader.ReadIntoAssignedProject(reader, hsonOptions);
        }

        public void Read(ReadOnlySequence<byte> hsonData,
            ProjectReadOptions hsonOptions = default,
            JsonReaderOptions jsonOptions = default)
        {
            var reader = new Utf8JsonReader(hsonData, jsonOptions);
            Read(reader, hsonOptions);
        }

        public void Read(ReadOnlySpan<byte> hsonData,
            ProjectReadOptions hsonOptions = default,
            JsonReaderOptions jsonOptions = default)
        {
            var reader = new Utf8JsonReader(hsonData, jsonOptions);
            Read(reader, hsonOptions);
        }

        public void Load(string filePath,
            ProjectReadOptions hsonOptions = default,
            JsonReaderOptions jsonOptions = default)
        {
            var hsonData = File.ReadAllBytes(filePath);
            Read(hsonData, hsonOptions, jsonOptions);
        }

        public void Write(Utf8JsonWriter writer,
            ProjectWriteOptions hsonOptions = default)
        {
            // Write HSON header.
            writer.WriteStartObject();
            writer.WriteNumber("version", MaxSupportedVersion);

            // Write metadata if necessary.
            if (hsonOptions.IncludeUnnecessaryProperties || Metadata.HasAnySpecifiedValue)
            {
                Metadata.Write(writer);
            }

            // Write objects if necessary.
            if (hsonOptions.IncludeUnnecessaryProperties || Objects.Count > 0)
            {
                Objects.WriteAll(writer, hsonOptions);
            }

            // Write custom properties if necessary.
            if (CustomProperties.Count > 0)
            {
                CustomProperties.WriteAll(writer);
            }

            writer.WriteEndObject();
        }

        public void Write(IBufferWriter<byte> bufferWriter,
            ProjectWriteOptions hsonOptions = default,
            JsonWriterOptions jsonOptions = default)
        {
            using var writer = new Utf8JsonWriter(bufferWriter, jsonOptions);
            Write(writer, hsonOptions);
        }

        public void Write(Stream stream,
            ProjectWriteOptions hsonOptions = default,
            JsonWriterOptions jsonOptions = default)
        {
            using var writer = new Utf8JsonWriter(stream, jsonOptions);
            Write(writer, hsonOptions);
        }

        public void Save(string filePath,
            ProjectWriteOptions hsonOptions = default,
            JsonWriterOptions jsonOptions = default)
        {
            using var fileStream = File.Create(filePath);
            Write(fileStream, hsonOptions, jsonOptions);
        }
        #endregion Public Methods
    }
}
