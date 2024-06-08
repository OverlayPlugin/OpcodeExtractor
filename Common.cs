namespace OpcodeExtractor;

public static class Common
{

    /// <summary>
    /// Signature scan.
    /// Based off OverlayPlugin methods
    /// </summary>
    /// <param name="data">Byte array of data to scan</param>
    /// <param name="pattern">String containing bytes represented in hex to search for, with "??" as a wildcard.</param>
    /// <returns>A list of offsets in |data| matching the |pattern|.</returns>
    public static List<int> SigScan(byte[] data, string pattern)
    {
        List<int> matchesList = [];

        if (pattern == null || pattern.Length % 2 != 0)
        {
            return matchesList;
        }

        // Build a byte array from the pattern string. "??" is a wildcard
        // represented as null in the array.
        byte?[] patternArray = new byte?[pattern.Length / 2];
        for (int i = 0; i < pattern.Length / 2; i++)
        {
            string text = pattern.Substring(i * 2, 2);
            if (text == "??")
            {
                patternArray[i] = null;
            }
            else
            {
                patternArray[i] = new byte?(Convert.ToByte(text, 16));
            }
        }

        int maxSearchOffset = data.Length - patternArray.Length;

        for (int searchOffset = 0; searchOffset < maxSearchOffset; ++searchOffset)
        {
            bool found_pattern = true;
            for (int patternIndex = 0; patternIndex < patternArray.Length; patternIndex++)
            {
                // Wildcard always matches, otherwise compare to the read_buffer.
                byte? pattern_byte = patternArray[patternIndex];
                if (pattern_byte.HasValue && pattern_byte.Value != data[searchOffset + patternIndex])
                {
                    found_pattern = false;
                    break;
                }
            }
            if (found_pattern)
            {
                matchesList.Add(searchOffset);
            }
        }

        return matchesList;
    }

    internal static unsafe int ExtractRIPOffsetFromPtr(byte* p) => 4 + *(int*)p;
}