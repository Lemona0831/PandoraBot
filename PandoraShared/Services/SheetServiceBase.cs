using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace PandoraShared.Services;

public abstract class SheetServiceBase
{
    protected readonly SheetsService Service;
    protected readonly string SpreadsheetId;

    private bool operationalSheetsChecked;

    protected SheetServiceBase(SheetsService service, string spreadsheetId)
    {
        Service = service;
        SpreadsheetId = spreadsheetId;
    }

    protected async Task EnsureEnemySheetsAsync()
    {
        if (operationalSheetsChecked)
        {
            return;
        }

        var spreadsheet = await Service.Spreadsheets.Get(SpreadsheetId).ExecuteAsync();
        var existing = spreadsheet.Sheets
            .Select(sheet => sheet.Properties.Title)
            .ToHashSet(StringComparer.Ordinal);

        await AddSheetIfMissingAsync(existing, SheetNames.EnemyStorage);
        await AddSheetIfMissingAsync(existing, SheetNames.EnemyDrop);
        await AddSheetIfMissingAsync(existing, SheetNames.EnemyDropSetting);
        await AddSheetIfMissingAsync(existing, SheetNames.AdminLog);

        await WriteHeaderRowAsync(SheetNames.EnemyStorage, "A1:P1", new object[]
        {
            "\uC5D0\uB108\uBBF8ID",
            "\uC9C0\uC5ED",
            "\uC774\uB984",
            "\uCD9C\uD604\uAD6C\uBD84",
            "\uADFC\uB825",
            "\uBBFC\uCCA9",
            "\uCCB4\uB825",
            "\uC9C0\uB2A5",
            "\uC9C0\uD61C",
            "\uB9E4\uB825",
            "\uD53C\uD574\uC2DD",
            "DP",
            "\uD604\uC7ACHP",
            "\uCD5C\uB300HP",
            "\uC124\uBA85",
            "\uC0AC\uC6A9\uC5EC\uBD80"
        });
        await WriteHeaderRowAsync(SheetNames.EnemyDrop, "A1:I1", new object[]
        {
            "\uC5D0\uB108\uBBF8ID",
            "\uC544\uC774\uD15C\uBA85",
            "\uD655\uB960",
            "\uCD5C\uC18C\uAC1C\uC218",
            "\uCD5C\uB300\uAC1C\uC218",
            "\uBB34\uAC8C",
            "\uD76C\uADC0\uB3C4",
            "\uD0DC\uADF8",
            "\uBA54\uBAA8"
        });
        await WriteHeaderRowAsync(SheetNames.EnemyDropSetting, "A1:E1", new object[]
        {
            "\uC5D0\uB108\uBBF8ID",
            "\uBC1C\uC0DD\uD655\uB960",
            "\uCD94\uCCA8\uD69F\uC218",
            "\uC911\uBCF5\uD5C8\uC6A9",
            "\uBA54\uBAA8"
        });

        operationalSheetsChecked = true;
    }

    protected async Task AppendRowAsync(string sheetName, IList<object> values)
    {
        var valueRange = new ValueRange { Values = new List<IList<object>> { values } };
        var request = Service.Spreadsheets.Values.Append(
            valueRange,
            SpreadsheetId,
            $"{RangeSheet(sheetName)}!A:Z");

        request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;
        request.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
        await request.ExecuteAsync();
    }

    protected async Task UpdateRowAsync(string sheetName, string range, IList<object> values)
    {
        var valueRange = new ValueRange { Values = new List<IList<object>> { values } };
        var request = Service.Spreadsheets.Values.Update(valueRange, SpreadsheetId, $"{RangeSheet(sheetName)}!{range}");
        request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
        await request.ExecuteAsync();
    }

    protected async Task ClearRangeAsync(string sheetName, string range)
    {
        await Service.Spreadsheets.Values.Clear(
            new ClearValuesRequest(),
            SpreadsheetId,
            $"{RangeSheet(sheetName)}!{range}").ExecuteAsync();
    }

    protected async Task AppendAdminLogRowAsync(string action, string userId, string target, string detail)
    {
        await AppendRowAsync(SheetNames.AdminLog, new List<object>
        {
            DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            action,
            userId,
            target,
            detail
        });
    }

    protected static string GetString(IList<object> values, int index)
    {
        return index < values.Count ? values[index]?.ToString()?.Trim() ?? "" : "";
    }

    protected static int GetInt(IList<object> values, int index)
    {
        return int.TryParse(GetString(values, index), out var value) ? value : 0;
    }

    protected static int ClampHp(int currentHp, int maxHp)
    {
        return maxHp > 0 ? Math.Clamp(currentHp, 0, maxHp) : Math.Max(currentHp, 0);
    }

    protected static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    protected static string RangeSheet(string sheetName)
    {
        return $"'{sheetName.Replace("'", "''")}'";
    }

    private async Task AddSheetIfMissingAsync(HashSet<string> existing, string sheetName)
    {
        if (existing.Contains(sheetName))
        {
            return;
        }

        var request = new BatchUpdateSpreadsheetRequest
        {
            Requests = new List<Request>
            {
                new Request
                {
                    AddSheet = new AddSheetRequest
                    {
                        Properties = new SheetProperties { Title = sheetName }
                    }
                }
            }
        };

        await Service.Spreadsheets.BatchUpdate(request, SpreadsheetId).ExecuteAsync();
        existing.Add(sheetName);
    }

    private async Task WriteHeaderRowAsync(string sheetName, string range, IList<object> values)
    {
        await UpdateRowAsync(sheetName, range, values);
    }
}
