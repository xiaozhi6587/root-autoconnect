#if NETFRAMEWORK
using System.Web.Script.Serialization;
using System.Globalization;
#else
using System.Text.Json;
#endif

namespace UsbAutoConnect;

public static class JsonData
{
    public static string Serialize<T>(T value)
    {
#if NETFRAMEWORK
        return Serializer().Serialize(value);
#else
        return JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
#endif
    }
    public static T? Deserialize<T>(string text)
    {
#if NETFRAMEWORK
        return Serializer().Deserialize<T>(text);
#else
        return JsonSerializer.Deserialize<T>(text);
#endif
    }
#if NETFRAMEWORK
    private static JavaScriptSerializer Serializer()
    {
        var serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024, RecursionLimit = 64 };
        serializer.RegisterConverters(new[] { new ExclusionConverter() });
        return serializer;
    }
    private sealed class ExclusionConverter : JavaScriptConverter
    {
        public override IEnumerable<Type> SupportedTypes => new[] { typeof(DeviceExclusion) };
        public override IDictionary<string, object> Serialize(object value, JavaScriptSerializer serializer)
        {
            var rule = (DeviceExclusion)value;
            return new Dictionary<string, object>
            {
                ["Key"] = rule.Key, ["Name"] = rule.Name, ["Server"] = rule.Server,
                ["Hardware"] = rule.Hardware, ["Identifier"] = rule.Identifier,
                ["AddedAt"] = rule.AddedAt.ToString("O", CultureInfo.InvariantCulture)
            };
        }
        public override object Deserialize(IDictionary<string, object> values, Type type, JavaScriptSerializer serializer)
        {
            string Field(string name) => values.TryGetValue(name, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : "";
            DateTimeOffset.TryParse(Field("AddedAt"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var added);
            return new DeviceExclusion(Field("Key"), Field("Name"), Field("Server"), Field("Hardware"), Field("Identifier"), added);
        }
    }
#endif
}
