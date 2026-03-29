using System;
using System.Collections.Generic;
using System.Globalization;

namespace DedicatedServerMod.Shared.ModVerification
{
    internal static class ClientModVersionComparer
    {
        public static bool SatisfiesMinimumVersion(string actualVersion, string minimumVersion)
        {
            if (string.IsNullOrWhiteSpace(minimumVersion))
            {
                return true;
            }

            return Compare(actualVersion, minimumVersion) >= 0;
        }

        public static int Compare(string left, string right)
        {
            ParsedVersion parsedLeft = Parse(left);
            ParsedVersion parsedRight = Parse(right);

            int segmentCount = Math.Max(parsedLeft.Segments.Count, parsedRight.Segments.Count);
            for (int i = 0; i < segmentCount; i++)
            {
                int leftSegment = i < parsedLeft.Segments.Count ? parsedLeft.Segments[i] : 0;
                int rightSegment = i < parsedRight.Segments.Count ? parsedRight.Segments[i] : 0;
                int compare = leftSegment.CompareTo(rightSegment);
                if (compare != 0)
                {
                    return compare;
                }
            }

            if (parsedLeft.HasPrerelease == parsedRight.HasPrerelease)
            {
                return string.Compare(parsedLeft.Prerelease, parsedRight.Prerelease, StringComparison.OrdinalIgnoreCase);
            }

            return parsedLeft.HasPrerelease ? -1 : 1;
        }

        private static ParsedVersion Parse(string value)
        {
            ParsedVersion parsed = new ParsedVersion();
            if (string.IsNullOrWhiteSpace(value))
            {
                return parsed;
            }

            string normalized = value.Trim();
            int prereleaseIndex = normalized.IndexOf('-');
            if (prereleaseIndex >= 0)
            {
                parsed.HasPrerelease = true;
                parsed.Prerelease = normalized.Substring(prereleaseIndex + 1);
                normalized = normalized.Substring(0, prereleaseIndex);
            }

            string[] segments = normalized.Split('.');
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (string.IsNullOrWhiteSpace(segment))
                {
                    parsed.Segments.Add(0);
                    continue;
                }

                int numericLength = 0;
                while (numericLength < segment.Length && char.IsDigit(segment[numericLength]))
                {
                    numericLength++;
                }

                if (numericLength == 0)
                {
                    parsed.Segments.Add(0);
                    continue;
                }

                if (!int.TryParse(segment.Substring(0, numericLength), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSegment))
                {
                    parsedSegment = 0;
                }

                parsed.Segments.Add(parsedSegment);

                if (numericLength < segment.Length && !parsed.HasPrerelease)
                {
                    parsed.HasPrerelease = true;
                    parsed.Prerelease = segment.Substring(numericLength);
                }
            }

            return parsed;
        }

        private sealed class ParsedVersion
        {
            public List<int> Segments { get; } = new List<int>();

            public bool HasPrerelease { get; set; }

            public string Prerelease { get; set; } = string.Empty;
        }
    }
}
