using System.Collections.Generic;
using System.Linq;

namespace DedicatedServerMod.Shared.Toml.Binding
{
    /// <summary>
    /// Normalizes generated TOML comment collections.
    /// </summary>
    internal static class TomlCommentFormatter
    {
        public static void Replace(IList<string> target, IEnumerable<string> comments)
        {
            target.Clear();

            foreach (string comment in comments ?? Enumerable.Empty<string>())
            {
                if (comment != null)
                {
                    target.Add(comment);
                }
            }
        }

        public static void ReplaceComments(IList<string> target, IEnumerable<string> comments)
        {
            Replace(target, comments);
        }
    }
}
