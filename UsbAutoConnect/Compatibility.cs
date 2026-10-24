using System.Diagnostics;
using System.IO;
using System.Text;

namespace UsbAutoConnect
{
    public static class Compatibility
    {
        public static int Clamp(int value, int minimum, int maximum) => Math.Min(maximum, Math.Max(minimum, value));
        public static void ReplaceFile(string temporary, string destination)
        {
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }
        public static string QuoteArgument(string value)
        {
            var quoted = new StringBuilder("\"");
            var slashes = 0;
            foreach (var character in value)
            {
                if (character == '\\') { slashes++; continue; }
                if (character == '"') quoted.Append('\\', slashes * 2 + 1).Append('"');
                else quoted.Append('\\', slashes).Append(character);
                slashes = 0;
            }
            return quoted.Append('\\', slashes * 2).Append('"').ToString();
        }
        public static async Task WaitForExitAsync(Process process, CancellationToken token)
        {
            var exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler handler = (_, _) => exited.TrySetResult(true);
            process.EnableRaisingEvents = true;
            process.Exited += handler;
            try
            {
                if (process.HasExited) return;
                using (token.Register(() => exited.TrySetCanceled())) await exited.Task.ConfigureAwait(false);
            }
            finally { process.Exited -= handler; }
        }
    }
}

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
namespace System
{
    internal static class FrameworkStringExtensions
    {
        public static bool Contains(this string value, string part, StringComparison comparison) => value.IndexOf(part, comparison) >= 0;
    }
}
namespace System.Linq
{
    internal static class FrameworkEnumerableExtensions
    {
        public static System.Collections.Generic.HashSet<T> ToHashSet<T>(this System.Collections.Generic.IEnumerable<T> values) => new(values);
    }
}
namespace System.Collections.Generic
{
    internal static class FrameworkPairExtensions
    {
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        { key = pair.Key; value = pair.Value; }
    }
}
#endif
