using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SuperCopy
{
    internal sealed class BlockStore
    {
        internal const int MaxCharacters = 2000000;
        internal const int MaxBlocks = 50000;
        internal string[] Items = new string[0];
        internal void Replace(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) throw new ArgumentException("No non-empty text was copied. Saved blocks are unchanged.");
            if (text.Length > MaxCharacters) throw new ArgumentException("Text exceeds 2,000,000 characters. Select less content. Saved blocks are unchanged.");
            List<string> parts = new List<string>();
            foreach (Match match in Regex.Matches(text, @"[^\t\r\n\u0085\u2028\u2029]+"))
            {
                if (String.IsNullOrWhiteSpace(match.Value)) continue;
                if (parts.Count == MaxBlocks) throw new ArgumentException("More than 50,000 blocks. Select less content. Saved blocks are unchanged.");
                parts.Add(match.Value);
            }
            if (parts.Count == 0) throw new ArgumentException("No non-empty text was copied. Saved blocks are unchanged.");
            Items = parts.ToArray();
        }
    }
}
