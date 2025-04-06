using System;
using System.Collections.Generic;
using System.Text;

namespace Extendroid.Lib
{
    class Utils
    {
        public static string GetSectionDataAsString(string text, string sectionName,int skipFirst=0)
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            bool inSection = false;
            var sectionData = new List<string>();
            var skipNext = skipFirst;
            string sectionHeader = sectionName + ":";

            foreach (var line in lines)
            {
                // Check for the section header.
                if (!inSection && line.Trim().Equals(sectionHeader, StringComparison.OrdinalIgnoreCase))
                {
                    inSection = true;
                    continue; // Skip the header line.
                }

                // If we're within the desired section.
                if (inSection && skipNext--<=0)
                {
                    // Stop if the line is not indented (or is empty).
                    if (string.IsNullOrWhiteSpace(line) || !char.IsWhiteSpace(line[0]))
                    {
                        break;
                    }
                    // Add the trimmed line to the result.
                    sectionData.Add(line.Trim());
                }
            }
            // Combine the lines into a single string separated by newlines.
            return string.Join(Environment.NewLine, sectionData);
        }


        public static void AppendSafe(StringBuilder builder, string? data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                lock (builder)
                {
                    builder.AppendLine(data);
                }
            }
        }
    }
}
