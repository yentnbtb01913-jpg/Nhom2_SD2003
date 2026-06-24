using System.IO.Compression;
using System.Text;

namespace WisdomITNews.Services;

// Xuất file .xlsx tối giản chuẩn OpenXML, KHÔNG cần thư viện ngoài
// (chỉ dùng System.IO.Compression có sẵn trong .NET). Mọi ô ghi dạng text.
public static class SimpleXlsx
{
    public static byte[] Build(string sheetName, string[] headers, List<string[]> rows)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            AddEntry(zip, "[Content_Types].xml", ContentTypes());
            AddEntry(zip, "_rels/.rels", Rels());
            AddEntry(zip, "xl/workbook.xml", Workbook(sheetName));
            AddEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRels());
            AddEntry(zip, "xl/worksheets/sheet1.xml", Sheet(headers, rows));
        }
        return ms.ToArray();
    }

    private static void AddEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path);
        using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        w.Write(content);
    }

    private static string Esc(string? s) => (s ?? "")
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string Col(int index) // 0-based -> A, B, ... , AA ...
    {
        var s = "";
        index++;
        while (index > 0) { index--; s = (char)('A' + index % 26) + s; index /= 26; }
        return s;
    }

    private static string Sheet(string[] headers, List<string[]> rows)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        sb.Append("<row r=\"1\">");
        for (int c = 0; c < headers.Length; c++)
            sb.Append($"<c r=\"{Col(c)}1\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Esc(headers[c])}</t></is></c>");
        sb.Append("</row>");
        int r = 2;
        foreach (var row in rows)
        {
            sb.Append($"<row r=\"{r}\">");
            for (int c = 0; c < row.Length; c++)
                sb.Append($"<c r=\"{Col(c)}{r}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Esc(row[c])}</t></is></c>");
            sb.Append("</row>");
            r++;
        }
        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static string ContentTypes() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
        "</Types>";

    private static string Rels() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private static string Workbook(string sheetName) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
        $"<sheets><sheet name=\"{Esc(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";

    private static string WorkbookRels() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
        "</Relationships>";
}
