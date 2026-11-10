namespace StockConsumptionPlanner.App.Features.StockReport.Shared;

public static class StockReportHeaderMatcher
{
    public const int ExactScore = 1000;

    private const int MinimumAbbreviatedTokenLength = 3;

    private static readonly char[] Apostrophes = ['\'', '`', '´', '‘', '’', 'ʼ'];

    public static IReadOnlyList<string> Tokenize(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return [];
        }

        List<string> tokens = new();
        List<char> current = new();
        foreach (char ch in header.ToLowerInvariant())
        {
            if (Array.IndexOf(Apostrophes, ch) >= 0)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch) || ch == '№')
            {
                current.Add(ch);
                continue;
            }

            if (current.Count > 0)
            {
                tokens.Add(new string(current.ToArray()));
                current.Clear();
            }
        }

        if (current.Count > 0)
        {
            tokens.Add(new string(current.ToArray()));
        }

        return tokens;
    }

    public static int Score(string? header, StockReportFieldMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        IReadOnlyList<string> headerTokens = Tokenize(header);
        if (headerTokens.Count == 0)
        {
            return 0;
        }

        int best = 0;
        foreach (string form in metadata.HeaderForms)
        {
            best = Math.Max(best, Score(headerTokens, Tokenize(form)));
            if (best == ExactScore)
            {
                break;
            }
        }

        return best;
    }

    public static Dictionary<StockReportField, int> SuggestMapping(
        IReadOnlyList<(int ColumnIndex, string HeaderText)> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        List<(int Score, int FieldOrder, StockReportField Field, int ColumnIndex)> candidates = new();
        for (int fieldOrder = 0; fieldOrder < StockReportFieldCatalog.All.Count; fieldOrder++)
        {
            StockReportFieldMetadata metadata = StockReportFieldCatalog.All[fieldOrder];
            foreach ((int columnIndex, string headerText) in columns)
            {
                int score = Score(headerText, metadata);
                if (score > 0)
                {
                    candidates.Add((score, fieldOrder, metadata.Field, columnIndex));
                }
            }
        }

        Dictionary<StockReportField, int> result = new();
        HashSet<int> assignedColumns = new();
        foreach ((_, _, StockReportField field, int columnIndex) in candidates
                     .OrderByDescending(c => c.Score)
                     .ThenBy(c => c.FieldOrder)
                     .ThenBy(c => c.ColumnIndex))
        {
            if (result.ContainsKey(field) || assignedColumns.Contains(columnIndex))
            {
                continue;
            }

            result[field] = columnIndex;
            assignedColumns.Add(columnIndex);
        }

        return result;
    }

    private static int Score(IReadOnlyList<string> header, IReadOnlyList<string> form)
    {
        if (form.Count == 0)
        {
            return 0;
        }

        if (string.Concat(header) == string.Concat(form))
        {
            return ExactScore;
        }

        int formIndex = 0;
        int matchedLength = 0;
        int skippedTokens = 0;
        for (int headerIndex = 0; headerIndex < header.Count; headerIndex++)
        {
            int match = FindToken(header[headerIndex], form, formIndex, anchored: headerIndex == 0);
            if (match < 0)
            {
                return 0;
            }

            skippedTokens += match - formIndex;
            matchedLength += Math.Min(header[headerIndex].Length, form[match].Length);
            formIndex = match + 1;
        }

        int formLength = form.Sum(token => token.Length);
        int score = 100 + (800 * matchedLength / formLength) - (10 * skippedTokens);
        return Math.Max(1, score);
    }

    private static int FindToken(string headerToken, IReadOnlyList<string> form, int start, bool anchored)
    {
        int last = anchored ? Math.Min(start, form.Count - 1) : form.Count - 1;
        for (int index = start; index <= last; index++)
        {
            if (TokenMatches(headerToken, form[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TokenMatches(string headerToken, string formToken)
    {
        return formToken.StartsWith(headerToken, StringComparison.Ordinal) ||
               (formToken.Length >= MinimumAbbreviatedTokenLength &&
                headerToken.StartsWith(formToken, StringComparison.Ordinal));
    }
}
