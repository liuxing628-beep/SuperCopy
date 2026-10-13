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
        internal void Replace(string text) { Commit(Parse(text), false); }
        internal static string[] Parse(string text)
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
            return parts.ToArray();
        }
        internal void Commit(string[] incoming, bool append)
        {
            int oldCount = append ? Items.Length : 0;
            if (incoming == null || incoming.Length == 0) throw new ArgumentException("No new blocks. Saved blocks are unchanged.");
            if ((long)oldCount + incoming.Length > MaxBlocks) throw new ArgumentException("The combined set exceeds 50,000 blocks. Use Replace all or capture less content.");
            long characters = 0;
            if (append) foreach (string item in Items) characters += item.Length;
            foreach (string item in incoming) {
                if (String.IsNullOrWhiteSpace(item)) throw new ArgumentException("Invalid empty block. Saved blocks are unchanged.");
                characters += item.Length;
            }
            if (characters > MaxCharacters) throw new ArgumentException("The combined set exceeds 2,000,000 characters. Use Replace all or capture less content.");
            var result = new string[oldCount + incoming.Length];
            if (append) Array.Copy(Items, result, oldCount);
            Array.Copy(incoming, 0, result, oldCount, incoming.Length);
            Items = result;
        }
        // Insertion is a boundary in the original list: 0 is before the first
        // item, Items.Length is after the last. Moving removes only this occurrence.
        internal void Move(int source, int insertion)
        {
            if (source < 0 || source >= Items.Length) throw new ArgumentOutOfRangeException("source");
            if (insertion < 0 || insertion > Items.Length) throw new ArgumentOutOfRangeException("insertion");
            int destination = insertion > source ? insertion - 1 : insertion;
            if (destination == source) return;
            var result = (string[])Items.Clone();
            string value = result[source];
            if (destination < source) Array.Copy(result, destination, result, destination + 1, source - destination);
            else Array.Copy(result, source + 1, result, source, destination - source);
            result[destination] = value;
            Items = result;
        }
        internal void RemoveAt(int index)
        {
            if (index < 0 || index >= Items.Length) throw new ArgumentOutOfRangeException("index");
            var result = new string[Items.Length - 1];
            Array.Copy(Items, 0, result, 0, index);
            Array.Copy(Items, index + 1, result, index, Items.Length - index - 1);
            Items = result;
        }
    }
}
