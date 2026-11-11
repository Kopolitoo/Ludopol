using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Ludopol
{
    /// <summary>One cell of a spreadsheet: its value, and its link and formula if it has some.</summary>
    public sealed class XlsxCell
    {
        /// <summary>The value as text. A number is written the invariant way ("19.5", "44980").</summary>
        public string Text { get; set; } = "";

        /// <summary>True when the value is a number (dates are numbers in Excel).</summary>
        public bool IsNumber { get; set; }

        /// <summary>The address of the cell's hyperlink, or null.</summary>
        public string? Hyperlink { get; set; }

        /// <summary>The cell's formula without "=", for example IMAGE("https://…"), or null.</summary>
        public string? Formula { get; set; }
    }

    /// <summary>
    /// LOGIC: reads the first sheet of an Excel file (.xlsx), without any extra library.
    ///
    /// An .xlsx file is a ZIP archive of XML files:
    ///   xl/workbook.xml              the list of sheets;
    ///   xl/_rels/workbook.xml.rels   where each sheet's file is;
    ///   xl/sharedStrings.xml         the texts, stored once and referred to by number;
    ///   xl/worksheets/sheetN.xml     the cells of a sheet (and its hyperlinks);
    ///   xl/worksheets/_rels/…        the addresses of the sheet's hyperlinks.
    /// The CSV export loses the hyperlinks and the formulas; this reader keeps them.
    /// </summary>
    public static class XlsxReader
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        /// <summary>
        /// The rows of the first sheet, the first row being the headers. Each row maps a header
        /// ("Jeu", "Lien Steam"…) to its cell. Empty rows are left out.
        /// </summary>
        public static List<Dictionary<string, XlsxCell>> ReadFirstSheet(string path)
        {
            // FileShare.ReadWrite: the file can be read even while it is open in Excel.
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                string sheetPath = FirstSheetPath(zip);
                List<string> sharedStrings = ReadSharedStrings(zip);
                XDocument sheet = Load(zip, sheetPath)
                    ?? throw new InvalidDataException("Sheet not found: " + sheetPath);
                Dictionary<string, string> links = ReadHyperlinks(zip, sheet, sheetPath);

                // Cells of each row, by column letters ("A", "B"…).
                List<Dictionary<string, XlsxCell>> rawRows = new List<Dictionary<string, XlsxCell>>();
                Dictionary<string, string> sharedFormulas = new Dictionary<string, string>();
                foreach (XElement row in sheet.Descendants(Main + "row"))
                {
                    Dictionary<string, XlsxCell> cells = new Dictionary<string, XlsxCell>();
                    foreach (XElement c in row.Elements(Main + "c"))
                    {
                        string reference = (string?)c.Attribute("r") ?? "";
                        XlsxCell cell = ReadCell(c, sharedStrings, sharedFormulas);
                        if (links.TryGetValue(reference, out string? link))
                        {
                            cell.Hyperlink = link;
                        }
                        cells[ColumnOf(reference)] = cell;
                    }
                    rawRows.Add(cells);
                }

                // The first row gives the headers.
                List<Dictionary<string, XlsxCell>> result = new List<Dictionary<string, XlsxCell>>();
                if (rawRows.Count == 0)
                {
                    return result;
                }
                Dictionary<string, string> headers = rawRows[0]
                    .Where(h => h.Value.Text.Trim().Length > 0)
                    .ToDictionary(h => h.Key, h => h.Value.Text.Trim());
                foreach (Dictionary<string, XlsxCell> raw in rawRows.Skip(1))
                {
                    Dictionary<string, XlsxCell> named = new Dictionary<string, XlsxCell>(StringComparer.OrdinalIgnoreCase);
                    foreach (KeyValuePair<string, string> header in headers)
                    {
                        if (raw.TryGetValue(header.Key, out XlsxCell? cell))
                        {
                            named[header.Value] = cell;
                        }
                    }
                    if (named.Values.Any(c => c.Text.Trim().Length > 0))
                    {
                        result.Add(named);
                    }
                }
                return result;
            }
        }

        private static XlsxCell ReadCell(XElement c, List<string> sharedStrings, Dictionary<string, string> sharedFormulas)
        {
            XlsxCell cell = new XlsxCell();
            string type = (string?)c.Attribute("t") ?? "n";
            string value = (string?)c.Element(Main + "v") ?? "";
            switch (type)
            {
                case "s":
                    cell.Text = int.TryParse(value, out int index) && index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : "";
                    break;
                case "inlineStr":
                    cell.Text = string.Concat(c.Descendants(Main + "t").Select(t => t.Value));
                    break;
                case "str":
                case "e":
                    cell.Text = value;
                    break;
                case "b":
                    cell.Text = value == "1" ? "TRUE" : "FALSE";
                    break;
                default:
                    cell.Text = value;
                    cell.IsNumber = value.Length > 0;
                    break;
            }

            // Formula. A "shared" formula is written once (with ref="I219:I220") and the
            // following cells only give its number (si): the text is taken from the first one.
            XElement? f = c.Element(Main + "f");
            if (f != null)
            {
                string? si = (string?)f.Attribute("si");
                string text = f.Value;
                if (text.Length > 0)
                {
                    if (si != null)
                    {
                        sharedFormulas[si] = text;
                    }
                    cell.Formula = text;
                }
                else if (si != null && sharedFormulas.TryGetValue(si, out string? shared))
                {
                    cell.Formula = shared;
                }
            }
            return cell;
        }

        /// <summary>"H12" → "H".</summary>
        private static string ColumnOf(string reference) =>
            new string(reference.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();

        private static string FirstSheetPath(ZipArchive zip)
        {
            XDocument workbook = Load(zip, "xl/workbook.xml") ?? throw new InvalidDataException("Not an Excel file (no xl/workbook.xml).");
            XElement sheet = workbook.Descendants(Main + "sheet").FirstOrDefault()
                ?? throw new InvalidDataException("The file has no sheet.");
            string? id = (string?)sheet.Attribute(Rel + "id");
            XDocument? rels = Load(zip, "xl/_rels/workbook.xml.rels");
            string? target = rels?.Descendants(PackageRel + "Relationship")
                .FirstOrDefault(r => (string?)r.Attribute("Id") == id)?.Attribute("Target")?.Value;
            if (target == null)
            {
                return "xl/worksheets/sheet1.xml";
            }
            return target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            XDocument? document = Load(zip, "xl/sharedStrings.xml");
            if (document == null)
            {
                return new List<string>();
            }
            // A text with several styles is split in runs (r/t): they are joined.
            return document.Root!.Elements(Main + "si")
                .Select(si => string.Concat(si.Descendants(Main + "t").Select(t => t.Value)))
                .ToList();
        }

        /// <summary>Cell reference ("H3") → address of its hyperlink.</summary>
        private static Dictionary<string, string> ReadHyperlinks(ZipArchive zip, XDocument sheet, string sheetPath)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string folder = sheetPath.Substring(0, sheetPath.LastIndexOf('/') + 1);
            string relsPath = folder + "_rels/" + sheetPath.Substring(folder.Length) + ".rels";
            XDocument? rels = Load(zip, relsPath);
            if (rels == null)
            {
                return result;
            }
            Dictionary<string, string> targets = rels.Descendants(PackageRel + "Relationship")
                .Where(r => r.Attribute("Id") != null && r.Attribute("Target") != null)
                .ToDictionary(r => r.Attribute("Id")!.Value, r => r.Attribute("Target")!.Value);
            foreach (XElement link in sheet.Descendants(Main + "hyperlink"))
            {
                string? id = (string?)link.Attribute(Rel + "id");
                string? reference = (string?)link.Attribute("ref");
                if (id != null && reference != null && targets.TryGetValue(id, out string? target))
                {
                    // ref can be a range ("H3:H5"): the first cell is enough here.
                    result[reference.Split(':')[0]] = target;
                }
            }
            return result;
        }

        private static XDocument? Load(ZipArchive zip, string path)
        {
            ZipArchiveEntry? entry = zip.GetEntry(path);
            if (entry == null)
            {
                return null;
            }
            using (Stream stream = entry.Open())
            {
                return XDocument.Load(stream);
            }
        }
    }
}
