using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;

namespace C1Installer.Core.Utility
{
    public static class JsonLoader
    {
        public static JsonObject LoadEmbeddedJson(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Cannot find embedded resource: {resourceName}");

            using var reader = new StreamReader(stream);
            string jsonText = reader.ReadToEnd();

            return JsonNode.Parse(jsonText)?.AsObject()
                ?? throw new InvalidDataException("JSON content is invalid.");
        }
    }
}
