using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application;
using MiniErp.Domain;
using MiniErp.Domain.Catalog;
using MiniErp.Infrastructure.Persistence;
namespace MiniErp.Infrastructure.Reporting;

public sealed class ProductWorkbook(ErpDbContext db, TimeProvider clock)
{
    private static readonly XNamespace Sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relations = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace DocumentRelations = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly string[] Headers = ["SKU", "Name", "CategoryCode", "UnitCode", "PurchasePrice", "SalePrice", "MinimumStock", "Active"];
    private const int MaxCompressed = 2 * 1024 * 1024;
    private const int MaxExpanded = 16 * 1024 * 1024;

    public async Task<byte[]> ExportAsync(CancellationToken ct)
    {
        var rows = await db.Products.AsNoTracking().OrderBy(x => x.Sku).Take(10001)
            .Select(x => new { x.Sku, x.Name, Category = x.Category.Code, Unit = x.Unit.Code, x.PurchasePrice, x.SalePrice, x.MinimumStock, x.IsActive }).ToArrayAsync(ct);
        if (rows.Length > 10000) throw ErpException.Invalid("workbook_limit", "Xuất tối đa 10.000 sản phẩm mỗi tệp.");
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Write(zip, "[Content_Types].xml", new XDocument(new XElement(XName.Get("Types", "http://schemas.openxmlformats.org/package/2006/content-types"),
                new XElement(XName.Get("Default", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(XName.Get("Default", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(XName.Get("Override", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(XName.Get("Override", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
            Write(zip, "_rels/.rels", new XDocument(new XElement(Relations + "Relationships", new XElement(Relations + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", DocumentRelations.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml")))));
            Write(zip, "xl/workbook.xml", new XDocument(new XElement(Sheet + "workbook", new XAttribute(XNamespace.Xmlns + "r", DocumentRelations), new XElement(Sheet + "sheets", new XElement(Sheet + "sheet", new XAttribute("name", "Products"), new XAttribute("sheetId", 1), new XAttribute(DocumentRelations + "id", "rId1"))))));
            Write(zip, "xl/_rels/workbook.xml.rels", new XDocument(new XElement(Relations + "Relationships", new XElement(Relations + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", DocumentRelations.NamespaceName + "/worksheet"), new XAttribute("Target", "worksheets/sheet1.xml")))));
            var data = new XElement(Sheet + "sheetData");
            AddRow(data, 1, Headers);
            for (var i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                AddRow(data, i + 2, [r.Sku, r.Name, r.Category, r.Unit, r.PurchasePrice.ToString(CultureInfo.InvariantCulture), r.SalePrice.ToString(CultureInfo.InvariantCulture), r.MinimumStock.ToString(CultureInfo.InvariantCulture), r.IsActive ? "true" : "false"]);
            }
            Write(zip, "xl/worksheets/sheet1.xml", new XDocument(new XElement(Sheet + "worksheet", data)));
        }
        if (output.Length > MaxCompressed) throw ErpException.Invalid("workbook_limit", "Tệp vượt giới hạn 2 MB.");
        return output.ToArray();
    }
    public async Task<int> ImportAsync(Stream stream, RequestActor actor, CancellationToken ct)
    {
        var products = new List<(int Row, Product Product)>();
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[8192]; int read;
            while ((read = await stream.ReadAsync(chunk, ct)) > 0)
            { if (buffer.Length + read > MaxCompressed) throw Invalid("Tệp tối đa 2 MB."); await buffer.WriteAsync(chunk.AsMemory(0, read), ct); }
            buffer.Position = 0;
            using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);
            if (zip.Entries.Count is < 5 or > 100 || zip.Entries.Sum(x => x.Length) > MaxExpanded ||
                zip.Entries.Select(x => x.FullName).Distinct(StringComparer.Ordinal).Count() != zip.Entries.Count)
                throw Invalid("Cấu trúc hoặc kích thước giải nén không hợp lệ.");
            var xml = new Dictionary<string, XDocument>(StringComparer.Ordinal);
            long expanded = 0;
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.Contains("..", StringComparison.Ordinal) || entry.FullName.Contains('\\') || entry.FullName.StartsWith('/') ||
                    entry.FullName.Contains("externalLink", StringComparison.OrdinalIgnoreCase) || entry.FullName.Contains("vba", StringComparison.OrdinalIgnoreCase) || entry.FullName.Contains("embedding", StringComparison.OrdinalIgnoreCase))
                    throw Invalid("Không hỗ trợ liên kết ngoài, macro hoặc tệp nhúng.");
                if (!entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)) continue;
                using var source = entry.Open();
                using var xmlBuffer = new MemoryStream();
                while ((read = await source.ReadAsync(chunk, ct)) > 0)
                {
                    expanded += read;
                    if (expanded > MaxExpanded) throw Invalid("Dữ liệu giải nén tối đa 16 MB.");
                    await xmlBuffer.WriteAsync(chunk.AsMemory(0, read), ct);
                }
                xmlBuffer.Position = 0;
                using var reader = XmlReader.Create(xmlBuffer, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxExpanded });
                var doc = XDocument.Load(reader);
                if (doc.Descendants().Any(x => x.Name.LocalName is "f" or "formula" or "formula1" or "formula2" or "definedName" ||
                    string.Equals((string?)x.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase) ||
                    x.Name == Relations + "Relationship" && ((string?)x.Attribute("Target"))?.Contains(':') == true))
                    throw Invalid("Không hỗ trợ công thức hoặc liên kết ngoài; hãy dán giá trị.");
                xml.Add(entry.FullName, doc);
            }
            var contentTypes = Get(xml, "[Content_Types].xml");
            if (!contentTypes.Descendants().Any(x => (string?)x.Attribute("PartName") == "/xl/workbook.xml" &&
                    (string?)x.Attribute("ContentType") == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml") ||
                !Get(xml, "_rels/.rels").Descendants(Relations + "Relationship").Any(x =>
                    (string?)x.Attribute("Type") == DocumentRelations.NamespaceName + "/officeDocument" &&
                    (string?)x.Attribute("Target") is "xl/workbook.xml" or "/xl/workbook.xml"))
                throw Invalid("Gói XLSX không có cấu trúc workbook hợp lệ.");
            var workbook = Get(xml, "xl/workbook.xml");
            var sheets = workbook.Descendants(Sheet + "sheet").ToArray();
            // ponytail: one values-only sheet; extend style/date interpretation only for broader workbook support.
            if (sheets.Length != 1) throw Invalid("Dùng đúng một trang tính Products.");
            var relationId = (string?)sheets[0].Attribute(DocumentRelations + "id");
            var target = (string?)Get(xml, "xl/_rels/workbook.xml.rels").Descendants(Relations + "Relationship")
                .SingleOrDefault(x => (string?)x.Attribute("Id") == relationId)?.Attribute("Target");
            if (target is null || target.Contains(':') || target.Contains("..", StringComparison.Ordinal)) throw Invalid("Tham chiếu trang tính không hợp lệ.");
            var path = target.StartsWith('/') ? target[1..] : "xl/" + target;
            var shared = xml.TryGetValue("xl/sharedStrings.xml", out var strings) ? strings.Descendants(Sheet + "si").Select(x => string.Concat(x.Descendants(Sheet + "t").Select(t => t.Value))).ToArray() : [];
            if (shared.Length > 50000) throw Invalid("Quá nhiều chuỗi trong tệp.");
            var rows = Get(xml, path).Descendants(Sheet + "sheetData").Elements(Sheet + "row").ToArray();
            if (rows.Length is < 2 or > 10001) throw Invalid("Nhập từ 1 đến 10.000 sản phẩm.");
            var lookups = await db.Lookups.AsNoTracking().Where(x => x.IsActive && (x.Kind == LookupKind.Category || x.Kind == LookupKind.Unit)).ToArrayAsync(ct);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rowNumbers = new HashSet<int>();
            foreach (var row in rows)
            {
                if (!int.TryParse((string?)row.Attribute("r"), out var rowNumber) || rowNumber < 1 || rowNumber > 10001 || !rowNumbers.Add(rowNumber)) throw Invalid("Số dòng không hợp lệ.");
                var values = new string[8]; var occupied = new HashSet<int>();
                foreach (var cell in row.Elements(Sheet + "c"))
                {
                    var reference = (string?)cell.Attribute("r") ?? "";
                    if (reference.Length < 2 || reference[0] is < 'A' or > 'H' || !int.TryParse(reference[1..], out var referencedRow) || referencedRow != rowNumber || !occupied.Add(reference[0] - 'A'))
                        throw At(rowNumber, "Tham chiếu ô không hợp lệ; dùng cột A đến H.");
                    var type = (string?)cell.Attribute("t");
                    var value = type == "inlineStr" ? string.Concat(cell.Descendants(Sheet + "t").Select(x => x.Value)) : (string?)cell.Element(Sheet + "v") ?? "";
                    if (type == "s")
                    { if (!int.TryParse(value, out var index) || index < 0 || index >= shared.Length) throw At(rowNumber, "Chỉ mục chuỗi không hợp lệ."); value = shared[index]; }
                    else if (type is not (null or "inlineStr" or "str" or "n" or "b")) throw At(rowNumber, "Không hỗ trợ kiểu ngày, lỗi hoặc kiểu ô này.");
                    if (type is null or "n" && (string?)cell.Attribute("s") is not (null or "0")) throw At(rowNumber, "Dán giá trị số dạng General, không dùng định dạng ngày hoặc tiền tệ.");
                    if (value.Length > 500) throw At(rowNumber, "Nội dung ô tối đa 500 ký tự.");
                    values[reference[0] - 'A'] = value.Trim();
                }
                if (rowNumber == 1)
                { if (!values.SequenceEqual(Headers)) throw At(1, "Tiêu đề phải đúng mẫu xuất từ hệ thống."); continue; }
                if (values.All(string.IsNullOrEmpty)) continue;
                try
                {
                    var sku = CatalogRules.NormalizeCode(values[0] ?? "");
                    if (!seen.Add(sku)) throw At(rowNumber, "SKU trùng trong tệp: " + sku);
                    if (string.IsNullOrWhiteSpace(values[1]) || values[1].Length > 160) throw At(rowNumber, "Tên từ 1 đến 160 ký tự.");
                    var category = lookups.SingleOrDefault(x => x.Kind == LookupKind.Category && x.Code.Equals(values[2], StringComparison.OrdinalIgnoreCase));
                    var unit = lookups.SingleOrDefault(x => x.Kind == LookupKind.Unit && x.Code.Equals(values[3], StringComparison.OrdinalIgnoreCase));
                    if (category is null || unit is null) throw At(rowNumber, "Mã nhóm hàng và đơn vị phải tồn tại, đang hoạt động.");
                    decimal Amount(int column)
                    { if (!decimal.TryParse(values[column], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount)) throw At(rowNumber, "Số thập phân dùng dấu chấm, không dùng dấu phân cách nghìn."); return CatalogRules.NonNegative(amount); }
                    if (!bool.TryParse(values[7], out var active) && values[7] is not ("1" or "0")) throw At(rowNumber, "Active phải là true/false hoặc 1/0.");
                    if (values[7] is "1" or "0") active = values[7] == "1";
                    var now = clock.GetUtcNow().UtcDateTime;
                    products.Add((rowNumber, new Product { Sku = sku, Name = values[1], CategoryId = category.Id, UnitId = unit.Id, PurchasePrice = Amount(4), SalePrice = Amount(5), MinimumStock = Amount(6), IsActive = active, CreatedAtUtc = now, UpdatedAtUtc = now }));
                }
                catch (ErpException e) when (e.Code != "invalid_workbook") { throw At(rowNumber, e.Message); }
            }
            if (!rowNumbers.Contains(1) || products.Count == 0) throw Invalid("Thiếu tiêu đề hoặc dữ liệu.");
        }
        catch (Exception e) when (e is InvalidDataException or XmlException or InvalidOperationException or ArgumentException)
        { throw Invalid("Tệp XLSX không hợp lệ hoặc bị hỏng."); }
        var skus = products.Select(x => x.Product.Sku).ToArray();
        var existing = await db.Products.Where(x => skus.Contains(x.Sku)).Select(x => x.Sku).ToArrayAsync(ct);
        var duplicate = products.FirstOrDefault(x => existing.Contains(x.Product.Sku, StringComparer.OrdinalIgnoreCase));
        if (duplicate.Product is not null) throw At(duplicate.Row, "SKU đã tồn tại: " + duplicate.Product.Sku);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var (_, product) in products)
        {
            db.Products.Add(product);
            db.AuditEntries.Add(AuditEntry.Create(actor, clock.GetUtcNow().UtcDateTime, "ImportProduct", "Product", product.Id.ToString(), after: product.Sku));
        }
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await tx.RollbackAsync(ct); db.ChangeTracker.Clear();
            var conflicts = await db.Products.Where(x => skus.Contains(x.Sku)).Select(x => x.Sku).ToArrayAsync(ct);
            var item = products.First(x => conflicts.Contains(x.Product.Sku, StringComparer.OrdinalIgnoreCase));
            throw At(item.Row, "SKU vừa được nhập bởi yêu cầu khác: " + item.Product.Sku);
        }
        return products.Count;
    }
    private static ErpException Invalid(string message) => ErpException.Invalid("invalid_workbook", message);
    private static ErpException At(int row, string message) => Invalid($"Dòng {row}: {message}");
    private static XDocument Get(Dictionary<string, XDocument> xml, string path) => xml.GetValueOrDefault(path) ?? throw Invalid("Thiếu thành phần " + path);
    private static void Write(ZipArchive zip, string path, XDocument document)
    { using var entry = zip.CreateEntry(path, CompressionLevel.Optimal).Open(); document.Save(entry); }
    private static void AddRow(XElement data, int number, string[] values) => data.Add(new XElement(Sheet + "row", new XAttribute("r", number),
        values.Select((value, index) => new XElement(Sheet + "c", new XAttribute("r", $"{(char)('A' + index)}{number}"), new XAttribute("t", "inlineStr"), new XElement(Sheet + "is", new XElement(Sheet + "t", value))))));
}
