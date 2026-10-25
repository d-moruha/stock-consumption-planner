using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockReportGenerator;

public static class StockReportWorkbookGenerator
{
    private static readonly string[] MaterialDescriptions =
    [
        "Труба сталева ДУ-50",
        "Фланець плоский ПН16",
        "Болт М16х60",
        "Гайка М16",
        "Підшипник 6205",
        "Steel pipe DN50",
        "Flat flange PN16",
        "Hex bolt M16x60",
        "Cable 3x2.5",
        "Paint enamel white"
    ];

    private static readonly DateTimeOffset PackageTimestamp = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Regex DocumentTimestamp = new(
        @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+\-]\d{2}:\d{2})",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CorePropertiesPart = new(
        @"package/services/metadata/core-properties/[0-9A-Fa-f]+\.psmdcp",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RelationshipId = new(
        @"(?<prefix>(?:r:)?[Ii]d="")(?<id>R[0-9A-Fa-f]{8,})(?<suffix>"")",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const string FixedCorePropertiesPart = "package/services/metadata/core-properties/core.psmdcp";

    public static void Generate(Stream destination, StockReportGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        options.EnsureValid();

        using MemoryStream raw = new();
        WriteWorkbook(raw, options);
        byte[] normalized = NormalizePackage(raw.ToArray());
        destination.Write(normalized, 0, normalized.Length);
        if (destination.CanSeek)
        {
            destination.Position = 0;
        }
    }

    public static void WriteFile(string outputPath, StockReportGeneratorOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(options);

        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = File.Create(fullPath);
        Generate(stream, options);
    }

    private static void WriteWorkbook(Stream stream, StockReportGeneratorOptions options)
    {
        Random random = options.Seed is int seed ? new Random(seed) : new Random();

        using SpreadsheetDocument document = SpreadsheetDocument.Create(
            stream,
            SpreadsheetDocumentType.Workbook,
            true);
        document.PackageProperties.Creator = "StockReportGenerator";
        document.PackageProperties.LastModifiedBy = "StockReportGenerator";
        document.PackageProperties.Created = PackageTimestamp.UtcDateTime;
        document.PackageProperties.Modified = PackageTimestamp.UtcDateTime;

        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());

        WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        SheetData sheetData = new();
        worksheetPart.Worksheet = new Worksheet(sheetData);

        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1U,
            Name = options.SheetName
        });

        sheetData.Append(CreateHeaderRow(options));
        for (int rowOffset = 0; rowOffset < options.RowCount; rowOffset++)
        {
            sheetData.Append(CreateDataRow(rowOffset + 2, random));
        }

        worksheetPart.Worksheet.Save();
        workbookPart.Workbook.Save();
    }

    private static Row CreateHeaderRow(StockReportGeneratorOptions options)
    {
        Row row = new() { RowIndex = 1U };
        int columnIndex = 1;
        foreach (StockReportFieldMetadata metadata in StockReportFieldCatalog.All)
        {
            string header = options.HeaderLanguage == StockReportHeaderLanguage.Ukrainian
                ? metadata.UkrainianName
                : metadata.DisplayName;
            AppendTextCell(row, columnIndex, 1, header);
            columnIndex++;
        }

        return row;
    }

    private static Row CreateDataRow(int rowNumber, Random random)
    {
        Row row = new() { RowIndex = (uint)rowNumber };
        AppendTextCell(row, 1, rowNumber, NextDocumentNumber(random));
        AppendTextCell(row, 2, rowNumber, NextDocumentItem(random));
        AppendNumberCell(row, 3, rowNumber, NextDocumentYear(random));
        AppendTextCell(row, 4, rowNumber, NextMaterialNumber(random));
        AppendTextCell(row, 5, rowNumber, MaterialDescriptions[random.Next(MaterialDescriptions.Length)]);
        AppendNumberCell(row, 6, rowNumber, NextQuantity(random));
        AppendNumberCell(row, 7, rowNumber, NextValue(random));
        return row;
    }

    private static string NextDocumentNumber(Random random)
    {
        return random.NextInt64(0, 10_000_000_000L).ToString("D10", CultureInfo.InvariantCulture);
    }

    private static string NextDocumentItem(Random random)
    {
        return random.Next(1, 10_000).ToString("D4", CultureInfo.InvariantCulture);
    }

    private static string NextDocumentYear(Random random)
    {
        return random.Next(2020, 2027).ToString(CultureInfo.InvariantCulture);
    }

    private static string NextMaterialNumber(Random random)
    {
        return random.NextInt64(0, 1_000_000_000_000L).ToString("D12", CultureInfo.InvariantCulture);
    }

    private static string NextQuantity(Random random)
    {
        int millis = random.Next(1, 10_000_001);
        decimal quantity = millis / 1000m;
        return quantity.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string NextValue(Random random)
    {
        int cents = random.Next(1, 100_000_001);
        decimal value = cents / 100m;
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static void AppendTextCell(Row row, int columnIndex, int rowNumber, string text)
    {
        row.Append(new Cell
        {
            CellReference = CellReference(columnIndex, rowNumber),
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(text))
        });
    }

    private static void AppendNumberCell(Row row, int columnIndex, int rowNumber, string invariantNumber)
    {
        row.Append(new Cell
        {
            CellReference = CellReference(columnIndex, rowNumber),
            DataType = CellValues.Number,
            CellValue = new CellValue(invariantNumber)
        });
    }

    private static string CellReference(int columnIndex, int rowNumber)
    {
        return $"{ExcelWorksheetHelper.GetColumnLetterFromNumber(columnIndex)}{rowNumber}";
    }

    private static byte[] NormalizePackage(byte[] packageBytes)
    {
        Dictionary<string, byte[]> entries = ReadPackageEntries(packageBytes);
        RenameCorePropertiesPart(entries);

        Dictionary<string, string> relationshipIds = new(StringComparer.Ordinal);
        int nextRelationshipId = 1;
        foreach (string name in entries.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            string xml = DecodeXml(entries[name]);
            xml = CorePropertiesPart.Replace(xml, FixedCorePropertiesPart);
            if (name.Contains("core-properties", StringComparison.Ordinal))
            {
                xml = DocumentTimestamp.Replace(xml, "2020-01-01T00:00:00Z");
            }

            xml = RelationshipId.Replace(xml, match =>
            {
                string oldId = match.Groups["id"].Value;
                if (!relationshipIds.TryGetValue(oldId, out string? stableId))
                {
                    stableId = "rId" + nextRelationshipId.ToString(CultureInfo.InvariantCulture);
                    nextRelationshipId++;
                    relationshipIds[oldId] = stableId;
                }

                return string.Concat(match.Groups["prefix"].Value, stableId, match.Groups["suffix"].Value);
            });

            entries[name] = Encoding.UTF8.GetBytes(xml);
        }

        using MemoryStream output = new();
        using (ZipArchive destination = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (string name in entries.Keys.OrderBy(item => item, StringComparer.Ordinal))
            {
                ZipArchiveEntry created = destination.CreateEntry(name, CompressionLevel.Optimal);
                created.LastWriteTime = PackageTimestamp;
                using Stream written = created.Open();
                written.Write(entries[name]);
            }
        }

        return output.ToArray();
    }

    private static Dictionary<string, byte[]> ReadPackageEntries(byte[] packageBytes)
    {
        Dictionary<string, byte[]> entries = new(StringComparer.Ordinal);
        using MemoryStream sourceStream = new(packageBytes);
        using ZipArchive source = new(sourceStream, ZipArchiveMode.Read);
        foreach (ZipArchiveEntry entry in source.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            using Stream input = entry.Open();
            using MemoryStream copy = new();
            input.CopyTo(copy);
            entries[entry.FullName] = copy.ToArray();
        }

        return entries;
    }

    private static void RenameCorePropertiesPart(Dictionary<string, byte[]> entries)
    {
        string? coreKey = entries.Keys.FirstOrDefault(CorePropertiesPart.IsMatch);
        if (coreKey is null || string.Equals(coreKey, FixedCorePropertiesPart, StringComparison.Ordinal))
        {
            return;
        }

        entries[FixedCorePropertiesPart] = entries[coreKey];
        entries.Remove(coreKey);
    }

    private static string DecodeXml(byte[] bytes)
    {
        string xml = Encoding.UTF8.GetString(bytes);
        if (xml.Length > 0 && xml[0] == '\uFEFF')
        {
            return xml[1..];
        }

        return xml;
    }
}
