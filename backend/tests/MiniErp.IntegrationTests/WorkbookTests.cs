using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Xml.Linq;
using MiniErp.Application;
using MiniErp.Application.Catalog;
namespace MiniErp.IntegrationTests;

[Collection("SqlServer")]
public sealed class WorkbookTests(ErpFixture fixture)
{
    private const string Route = "/api/v1/catalog/products/workbook";
    private static readonly XNamespace Sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    [SqlServerFact]
    public async Task Export_template_import_validates_rows_rejects_formula_and_rolls_back_whole_batch()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var code = Guid.NewGuid().ToString("N")[..12];
        async Task<Guid> Lookup(string resource, string suffix)
        {
            using var result = await client.PostAsJsonAsync("/api/v1/" + resource, new LookupInput(code + suffix, "Workbook " + code, true, null));
            result.EnsureSuccessStatusCode(); return (await result.Content.ReadFromJsonAsync<LookupResponse>())!.Id;
        }
        var category = await Lookup("categories", "C"); var unit = await Lookup("units", "U");
        using var created = await client.PostAsJsonAsync("/api/v1/products", new ProductInput(code, "Workbook " + code, category, unit, 12.3456m, 22.3456m, 3.5m, null, null, true, null));
        created.EnsureSuccessStatusCode();
        var original = await client.GetByteArrayAsync(Route);
        var newSku = code + "NEW";
        var valid = Rewrite(original, code, [(newSku, false, false)]);
        using var imported = await ImportAsync(client, valid);
        Assert.True(imported.IsSuccessStatusCode, await imported.Content.ReadAsStringAsync());
        var saved = await client.GetFromJsonAsync<Page<ProductResponse>>("/api/v1/products?search=" + newSku);
        var product = Assert.Single(saved!.Items);
        Assert.Equal(12.3456m, product.PurchasePrice); Assert.Equal(category, product.CategoryId); Assert.Equal(unit, product.UnitId);
        using var duplicate = await ImportAsync(client, valid);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("Dòng 2", await duplicate.Content.ReadAsStringAsync());

        var rejectedSku = code + "BAD";
        using var failed = await ImportAsync(client, Rewrite(original, code, [(rejectedSku, false, false), (code + "BAD2", true, false)]));
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
        Assert.Contains("Dòng 3", await failed.Content.ReadAsStringAsync());
        Assert.Empty((await client.GetFromJsonAsync<Page<ProductResponse>>("/api/v1/products?search=" + rejectedSku))!.Items);
        using var formula = await ImportAsync(client, Rewrite(original, code, [(code + "FORM", false, true)]));
        Assert.Equal(HttpStatusCode.BadRequest, formula.StatusCode);
        using var malformed = await ImportAsync(client, [1, 2, 3, 4]);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        using var anonymous = fixture.NewClient();
        using var denied = await anonymous.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }
    private static byte[] Rewrite(byte[] source, string originalSku, (string Sku, bool Invalid, bool Formula)[] requested)
    {
        using var output = new MemoryStream(); output.Write(source); output.Position = 0;
        using (var zip = new ZipArchive(output, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!;
            XDocument xml; using (var stream = entry.Open()) xml = XDocument.Load(stream);
            var data = xml.Descendants(Sheet + "sheetData").Single();
            var template = data.Elements(Sheet + "row").Single(x => string.Equals(x.Elements(Sheet + "c").First().Value, originalSku, StringComparison.OrdinalIgnoreCase));
            var header = new XElement(data.Elements(Sheet + "row").First());
            data.RemoveNodes(); data.Add(header);
            for (var i = 0; i < requested.Length; i++)
            {
                var row = new XElement(template); var number = i + 2; row.SetAttributeValue("r", number);
                foreach (var cell in row.Elements(Sheet + "c")) cell.SetAttributeValue("r", ((string)cell.Attribute("r")!)[0] + number.ToString());
                row.Elements(Sheet + "c").First().Descendants(Sheet + "t").Single().Value = requested[i].Sku;
                if (requested[i].Invalid) row.Elements(Sheet + "c").ElementAt(4).Descendants(Sheet + "t").Single().Value = "-10";
                if (requested[i].Formula) row.Elements(Sheet + "c").ElementAt(4).Add(new XElement(Sheet + "f", "1+1"));
                data.Add(row);
            }
            entry.Delete(); using var replacement = zip.CreateEntry("xl/worksheets/sheet1.xml").Open(); xml.Save(replacement);
        }
        return output.ToArray();
    }
    private static async Task<HttpResponseMessage> ImportAsync(HttpClient client, byte[] workbook)
    {
        using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(workbook), "file", "products.xlsx");
        return await client.PostAsync(Route, form);
    }
}
