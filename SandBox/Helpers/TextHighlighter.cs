using Microsoft.AspNetCore.Components;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SandBox.Helpers
{
    public static class TextHighlighter
    {
        private const string VariationPart = @"[\[\]‹›]*";

        public static MarkupString HighlightVerse(
            string text,
            IReadOnlyList<string> terms,
            bool sequential,
            Func<string, string> hrefForTerm)
        {
            if (string.IsNullOrEmpty(text))
                return new MarkupString("");
            if (terms is null || terms.Count == 0)
                return new MarkupString(WebUtility.HtmlEncode(text));

            var matches = new List<(int Start, int End, string Term, string Matched)>();

            if (sequential)
            {
                int cursor = 0;
                foreach (var term in terms)
                {
                    if (string.IsNullOrEmpty(term)) continue;
                    var m = BuildTermRegex(term).Match(text, cursor);
                    if (!m.Success) continue;
                    matches.Add((m.Index, m.Index + m.Length, term, m.Value));
                    cursor = m.Index + m.Length;
                }
            }
            else
            {
                var valid = terms.Where(t => !string.IsNullOrEmpty(t))
                     .OrderByDescending(t => t.Length)
                     .ToList();
                if (valid.Count == 0)
                    return new MarkupString(WebUtility.HtmlEncode(text));

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match m in BuildUnionRegex(valid).Matches(text))
                {
                    if (!seen.Add(m.Value)) continue;
                    matches.Add((m.Index, m.Index + m.Length, m.Value, m.Value));
                }
            }

            if (matches.Count == 0)
                return new MarkupString(WebUtility.HtmlEncode(text));

            var sb = new StringBuilder();
            int idx = 0, counter = 0;
            foreach (var m in matches)
            {
                if (m.Start > idx)
                    sb.Append(WebUtility.HtmlEncode(text[idx..m.Start]));

                var href = WebUtility.HtmlEncode(hrefForTerm(m.Term));
                var safeTerm = WebUtility.HtmlEncode(m.Term);
                var safeWord = WebUtility.HtmlEncode(m.Matched);
                var color = $"ref-c{(counter % 4) + 1}";

                sb.Append($"<a href=\"{href}\" class=\"anchor-phrase {color}\" data-term=\"{safeTerm}\" data-enhance-nav=\"false\">{safeWord}</a>");

                idx = m.End;
                counter++;
            }
            if (idx < text.Length)
                sb.Append(WebUtility.HtmlEncode(text[idx..]));

            return new MarkupString(sb.ToString());
        }

        private static Regex BuildTermRegex(string term)
        {
            var dynamic = string.Join(VariationPart, term.Select(c => Regex.Escape(c.ToString())));
            return new Regex(@"(?<!\w)" + dynamic + @"(?!\w)", RegexOptions.IgnoreCase);
        }

        private static Regex BuildUnionRegex(IEnumerable<string> terms)
        {
            var alternatives = terms.Select(t =>
                string.Join(VariationPart, t.Select(c => Regex.Escape(c.ToString()))));
            return new Regex(@"(?<!\w)(" + string.Join("|", alternatives) + @")(?!\w)",
                             RegexOptions.IgnoreCase);
        }
    }
}
