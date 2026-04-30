using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using PandoraBot.Models;
using System.Text.RegularExpressions;

namespace PandoraBot.Services
{
    public class GoogleSheetService
    {
        private static GoogleSheetService? instance;
        private static readonly object InstanceLock = new object();
        private static readonly SemaphoreSlim SheetLock = new SemaphoreSlim(1, 1);

        private const string StorageSheet = "\uCE90\uB9AD\uD130 \uC800\uC7A5\uC18C";
        private const string SelectedMarker = "selected";

        private readonly SheetsService service;
        private readonly string storageSpreadsheetId;

        private GoogleSheetService(SheetsService service, string storageSpreadsheetId)
        {
            this.service = service;
            this.storageSpreadsheetId = storageSpreadsheetId;
        }

        public static GoogleSheetService Instance =>
            instance ?? throw new InvalidOperationException("GoogleSheetService has not been initialized.");

        public static GoogleSheetService Initialize(SheetsService service)
        {
            lock (InstanceLock)
            {
                if (instance == null)
                {
                    var spreadsheetId = Environment.GetEnvironmentVariable("PANDORA_SPREADSHEET_ID")
                        ?? "13DKG_V3TD5GHxQrVpmFGQhFluPvGc3E_M5FXfdvRkqI";

                    instance = new GoogleSheetService(service, spreadsheetId);
                }

                return instance;
            }
        }

        public async Task<RegistrationResult> RegisterAsync(string sourceSheet, string userId)
        {
            await SheetLock.WaitAsync();

            try
            {
                var source = await ResolveSheetReferenceAsync(sourceSheet);
                if (source.SheetName == StorageSheet)
                {
                    throw new Exception("The linked sheet is the character storage tab. Use a character source tab or its gid URL.");
                }

                var hunter = await LoadHunterFromSourceAsync(source, userId);
                var result = await UpsertStorageAsync(hunter);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {ex.Message}");
                throw;
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<Hunter> SelectCharacterAsync(string characterName, string userId)
        {
            await SheetLock.WaitAsync();

            try
            {
                var rows = await ReadStorageRowsAsync();
                var requestedName = Normalize(characterName);
                var sameNameRows = rows.Where(row => Normalize(row.CharacterName) == requestedName).ToList();
                var selectedRow = sameNameRows.FirstOrDefault(row => row.UserId == userId);

                if (selectedRow == null)
                {
                    if (sameNameRows.Count > 0)
                    {
                        throw new Exception("That character is registered to another Discord user.");
                    }

                    throw new Exception("No registered character was found with that name.");
                }

                foreach (var row in rows.Where(row => row.UserId == userId))
                {
                    var marker = row.RowNumber == selectedRow.RowNumber ? SelectedMarker : "";
                    await UpdateSingleCellAsync($"K{row.RowNumber}", marker);
                }

                return selectedRow.ToHunter();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {ex.Message}");
                throw;
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<Hunter> GetSelectedCharacterAsync(string userId)
        {
            await SheetLock.WaitAsync();

            try
            {
                var rows = await ReadStorageRowsAsync();
                var selectedRow = rows.FirstOrDefault(row =>
                    row.UserId == userId &&
                    string.Equals(row.Selected, SelectedMarker, StringComparison.OrdinalIgnoreCase));

                if (selectedRow == null)
                {
                    throw new Exception("No selected character was found. Use /select first.");
                }

                return selectedRow.ToHunter();
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<Hunter> GetCharacterAsync(string characterName, string userId)
        {
            await SheetLock.WaitAsync();

            try
            {
                var rows = await ReadStorageRowsAsync();
                var requestedName = Normalize(characterName);
                var sameNameRows = rows.Where(row => Normalize(row.CharacterName) == requestedName).ToList();
                var row = sameNameRows.FirstOrDefault(row => row.UserId == userId);

                if (row == null)
                {
                    if (sameNameRows.Count > 0)
                    {
                        throw new Exception("That character is registered to another Discord user.");
                    }

                    throw new Exception("No registered character was found with that name.");
                }

                return row.ToHunter();
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<ClearSelectionResult> ClearSelectedCharacterAsync(string userId)
        {
            await SheetLock.WaitAsync();

            try
            {
                var rows = await ReadStorageRowsAsync();
                var userRows = rows.Where(row => row.UserId == userId).ToList();
                var selectedRows = userRows
                    .Where(row => string.Equals(row.Selected, SelectedMarker, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var row in selectedRows)
                {
                    await UpdateSingleCellAsync($"K{row.RowNumber}", "");
                }

                return new ClearSelectionResult(selectedRows.Count);
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<DeleteCharacterResult> DeleteCharacterAsync(string characterName, string userId)
        {
            await SheetLock.WaitAsync();

            try
            {
                var rows = await ReadStorageRowsAsync();
                var requestedName = Normalize(characterName);
                var sameNameRows = rows.Where(row => Normalize(row.CharacterName) == requestedName).ToList();
                var row = sameNameRows.FirstOrDefault(row => row.UserId == userId);

                if (row == null)
                {
                    if (sameNameRows.Count > 0)
                    {
                        throw new Exception("That character is registered to another Discord user.");
                    }

                    throw new Exception("No registered character was found with that name.");
                }

                await ClearStorageRowAsync(row.RowNumber);
                return new DeleteCharacterResult(row.CharacterName, row.RowNumber);
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<IReadOnlyList<CharacterSummary>> ListCharactersAsync(string userId)
        {
            await SheetLock.WaitAsync();

            try
            {
                var rows = await ReadStorageRowsAsync();
                return rows
                    .Where(row => row.UserId == userId)
                    .OrderBy(row => row.CharacterName)
                    .Select(row => new CharacterSummary(
                        row.CharacterName,
                        row.CurrentHp,
                        row.MaxHp,
                        string.Equals(row.Selected, SelectedMarker, StringComparison.OrdinalIgnoreCase),
                        row.RowNumber))
                    .ToList();
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<IReadOnlyList<AdminCharacterSummary>> ListAllCharactersAsync(int limit = 25)
        {
            await SheetLock.WaitAsync();

            try
            {
                var rows = await ReadStorageRowsAsync();
                return rows
                    .OrderBy(row => row.UserId)
                    .ThenBy(row => row.CharacterName)
                    .Take(Math.Clamp(limit, 1, 50))
                    .Select(row => new AdminCharacterSummary(
                        row.UserId,
                        row.CharacterName,
                        row.CurrentHp,
                        row.MaxHp,
                        string.Equals(row.Selected, SelectedMarker, StringComparison.OrdinalIgnoreCase),
                        row.RowNumber))
                    .ToList();
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<Hunter> GetCharacterForAdminAsync(string userId, string characterName)
        {
            await SheetLock.WaitAsync();

            try
            {
                var rows = await ReadStorageRowsAsync();
                return FindOwnedCharacterRow(rows, userId, characterName).ToHunter();
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<UpdateHpResult> SetCharacterHpAsync(string userId, string characterName, int currentHp)
        {
            await SheetLock.WaitAsync();

            try
            {
                var rows = await ReadStorageRowsAsync();
                var row = FindOwnedCharacterRow(rows, userId, characterName);
                var clampedHp = Math.Clamp(currentHp, 0, row.MaxHp);

                await UpdateSingleCellAsync($"J{row.RowNumber}", clampedHp.ToString());
                return new UpdateHpResult(row.CharacterName, row.UserId, clampedHp, row.MaxHp, row.RowNumber);
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<ClearSelectionResult> ClearSelectedCharacterForAdminAsync(string userId)
        {
            return await ClearSelectedCharacterAsync(userId);
        }

        public async Task<DeleteCharacterResult> DeleteCharacterForAdminAsync(string userId, string characterName)
        {
            await SheetLock.WaitAsync();

            try
            {
                var rows = await ReadStorageRowsAsync();
                var row = FindOwnedCharacterRow(rows, userId, characterName);

                await ClearStorageRowAsync(row.RowNumber);
                return new DeleteCharacterResult(row.CharacterName, row.RowNumber);
            }
            finally
            {
                SheetLock.Release();
            }
        }

        private async Task<Hunter> LoadHunterFromSourceAsync(SheetReference source, string userId)
        {
            var sourceSheetName = ToRangeSheetName(source.SheetName);
            var ranges = new List<string>
            {
                $"{sourceSheetName}!N9",
                $"{sourceSheetName}!U10:AL10",
                $"{sourceSheetName}!AE15:AL15"
            };

            var request = service.Spreadsheets.Values.BatchGet(source.SpreadsheetId);
            request.Ranges = ranges;
            var response = await request.ExecuteAsync();

            if (response.ValueRanges == null || response.ValueRanges.Count < 3)
            {
                throw new Exception("The source sheet structure does not match the expected Pandora format.");
            }

            var nameData = response.ValueRanges[0].Values;
            var statData = response.ValueRanges[1].Values?[0];
            var hpData = response.ValueRanges[2].Values?[0];

            if (nameData == null || statData == null || hpData == null)
            {
                throw new Exception("Required character data is missing from the source sheet.");
            }

            return new Hunter
            {
                UserId = userId,
                CharacterName = nameData[0][0]?.ToString() ?? "Unknown",
                Strength = ParseInt(statData, 0, "Strength"),
                Dexterity = ParseInt(statData, 3, "Dexterity"),
                Constitution = ParseInt(statData, 6, "Constitution"),
                Intelligence = ParseInt(statData, 9, "Intelligence"),
                Wisdom = ParseInt(statData, 12, "Wisdom"),
                Charisma = ParseInt(statData, 15, "Charisma"),
                CurrentHp = ParseInt(hpData, 0, "Current HP"),
                MaxHp = ParseInt(hpData, 5, "Max HP")
            };
        }

        private async Task<RegistrationResult> UpsertStorageAsync(Hunter hunter)
        {
            var rows = await ReadStorageRowsAsync();
            var matches = rows
                .Where(row => row.UserId == hunter.UserId && Normalize(row.CharacterName) == Normalize(hunter.CharacterName))
                .ToList();

            var rowNumber = matches.FirstOrDefault()?.RowNumber ?? await GetNextStorageRowAsync();
            var wasUpdated = matches.Count > 0;
            var selected = matches.FirstOrDefault(row => !string.IsNullOrWhiteSpace(row.Selected))?.Selected
                ?? matches.FirstOrDefault()?.Selected
                ?? "";

            await WriteStorageRowAsync(rowNumber, hunter, selected);

            foreach (var duplicate in matches.Skip(1))
            {
                await ClearStorageRowAsync(duplicate.RowNumber);
            }

            Console.WriteLine($"[Success] {hunter.CharacterName} {(wasUpdated ? "updated" : "saved")} at row {rowNumber}.");
            return new RegistrationResult(hunter, wasUpdated, rowNumber);
        }

        private async Task<List<StorageRow>> ReadStorageRowsAsync()
        {
            var storageSheetName = ToRangeSheetName(StorageSheet);
            var response = await service.Spreadsheets.Values.Get(storageSpreadsheetId, $"{storageSheetName}!A2:K").ExecuteAsync();
            var values = response.Values ?? new List<IList<object>>();
            var rows = new List<StorageRow>();

            for (var index = 0; index < values.Count; index++)
            {
                var row = values[index];
                var userId = GetString(row, 0);
                var characterName = GetString(row, 1);

                if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(characterName))
                {
                    continue;
                }

                rows.Add(new StorageRow(
                    RowNumber: index + 2,
                    UserId: userId,
                    CharacterName: characterName,
                    Strength: ParseOptionalInt(row, 2),
                    Dexterity: ParseOptionalInt(row, 3),
                    Constitution: ParseOptionalInt(row, 4),
                    Intelligence: ParseOptionalInt(row, 5),
                    Wisdom: ParseOptionalInt(row, 6),
                    Charisma: ParseOptionalInt(row, 7),
                    MaxHp: ParseOptionalInt(row, 8),
                    CurrentHp: ParseOptionalInt(row, 9),
                    Selected: GetString(row, 10)));
            }

            return rows;
        }

        private async Task<int> GetNextStorageRowAsync()
        {
            var storageSheetName = ToRangeSheetName(StorageSheet);
            var response = await service.Spreadsheets.Values.Get(storageSpreadsheetId, $"{storageSheetName}!A:B").ExecuteAsync();
            var values = response.Values ?? new List<IList<object>>();

            for (var i = values.Count - 1; i >= 0; i--)
            {
                var row = values[i];
                if (!string.IsNullOrWhiteSpace(GetString(row, 0)) || !string.IsNullOrWhiteSpace(GetString(row, 1)))
                {
                    return i + 2;
                }
            }

            return 2;
        }

        private async Task WriteStorageRowAsync(int rowNumber, Hunter hunter, string selected)
        {
            var rowValues = new List<object>
            {
                hunter.UserId,
                hunter.CharacterName,
                hunter.Strength,
                hunter.Dexterity,
                hunter.Constitution,
                hunter.Intelligence,
                hunter.Wisdom,
                hunter.Charisma,
                hunter.MaxHp,
                hunter.CurrentHp,
                selected
            };

            var storageSheetName = ToRangeSheetName(StorageSheet);
            var writeRange = $"{storageSheetName}!A{rowNumber}:K{rowNumber}";
            var valueRange = new ValueRange { Values = new List<IList<object>> { rowValues } };

            var updateRequest = service.Spreadsheets.Values.Update(valueRange, storageSpreadsheetId, writeRange);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await updateRequest.ExecuteAsync();
        }

        private async Task ClearStorageRowAsync(int rowNumber)
        {
            var storageSheetName = ToRangeSheetName(StorageSheet);
            var clearRequest = service.Spreadsheets.Values.Clear(new ClearValuesRequest(), storageSpreadsheetId, $"{storageSheetName}!A{rowNumber}:K{rowNumber}");
            await clearRequest.ExecuteAsync();
        }

        private async Task UpdateSingleCellAsync(string cell, string value)
        {
            var storageSheetName = ToRangeSheetName(StorageSheet);
            var valueRange = new ValueRange { Values = new List<IList<object>> { new List<object> { value } } };
            var updateRequest = service.Spreadsheets.Values.Update(valueRange, storageSpreadsheetId, $"{storageSheetName}!{cell}");
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await updateRequest.ExecuteAsync();
        }

        private async Task<SheetReference> ResolveSheetReferenceAsync(string input)
        {
            var trimmed = input.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new Exception("Enter a sheet name or Google Sheets URL.");
            }

            var match = Regex.Match(trimmed, @"/spreadsheets/d/([^/?#]+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return new SheetReference(storageSpreadsheetId, trimmed);
            }

            var sourceSpreadsheetId = match.Groups[1].Value;
            var gid = ExtractGid(trimmed);
            var metadata = await service.Spreadsheets.Get(sourceSpreadsheetId).ExecuteAsync();
            var sheets = metadata.Sheets ?? new List<Sheet>();

            if (gid.HasValue)
            {
                var matchedSheet = sheets.FirstOrDefault(sheet => sheet.Properties?.SheetId == gid.Value);
                if (matchedSheet?.Properties?.Title != null)
                {
                    return new SheetReference(sourceSpreadsheetId, matchedSheet.Properties.Title);
                }

                throw new Exception($"No sheet tab found for gid={gid.Value}.");
            }

            var firstSheetName = sheets.FirstOrDefault()?.Properties?.Title;
            if (string.IsNullOrWhiteSpace(firstSheetName))
            {
                throw new Exception("No sheet tab was found in the linked spreadsheet.");
            }

            return new SheetReference(sourceSpreadsheetId, firstSheetName);
        }

        private static int ParseInt(IList<object> values, int index, string fieldName)
        {
            if (values.Count <= index || values[index] == null)
            {
                throw new Exception($"{fieldName} is missing.");
            }

            if (!int.TryParse(values[index].ToString(), out var value))
            {
                throw new Exception($"{fieldName} is not a number: {values[index]}");
            }

            return value;
        }

        private static int ParseOptionalInt(IList<object> values, int index)
        {
            return int.TryParse(GetString(values, index), out var value) ? value : 0;
        }

        private static string GetString(IList<object> values, int index)
        {
            return values.Count > index ? values[index]?.ToString()?.Trim() ?? "" : "";
        }

        private static string Normalize(string value)
        {
            return value.Trim().ToUpperInvariant();
        }

        private static StorageRow FindOwnedCharacterRow(IReadOnlyList<StorageRow> rows, string userId, string characterName)
        {
            var requestedName = Normalize(characterName);
            var row = rows.FirstOrDefault(row =>
                row.UserId == userId &&
                Normalize(row.CharacterName) == requestedName);

            if (row == null)
            {
                throw new Exception("No registered character was found for that Discord user ID and character name.");
            }

            return row;
        }

        private static int? ExtractGid(string input)
        {
            var match = Regex.Match(input, @"[?#&]gid=(\d+)", RegexOptions.IgnoreCase);
            return match.Success ? int.Parse(match.Groups[1].Value) : null;
        }

        private static string ToRangeSheetName(string sheetName)
        {
            return $"'{sheetName.Trim().Replace("'", "''")}'";
        }

        public sealed record RegistrationResult(Hunter Hunter, bool WasUpdated, int RowNumber);

        public sealed record ClearSelectionResult(int ClearedCount);

        public sealed record DeleteCharacterResult(string CharacterName, int RowNumber);

        public sealed record CharacterSummary(string CharacterName, int CurrentHp, int MaxHp, bool IsSelected, int RowNumber);

        public sealed record AdminCharacterSummary(string UserId, string CharacterName, int CurrentHp, int MaxHp, bool IsSelected, int RowNumber);

        public sealed record UpdateHpResult(string CharacterName, string UserId, int CurrentHp, int MaxHp, int RowNumber);

        private sealed record SheetReference(string SpreadsheetId, string SheetName);

        private sealed record StorageRow(
            int RowNumber,
            string UserId,
            string CharacterName,
            int Strength,
            int Dexterity,
            int Constitution,
            int Intelligence,
            int Wisdom,
            int Charisma,
            int MaxHp,
            int CurrentHp,
            string Selected)
        {
            public Hunter ToHunter()
            {
                return new Hunter
                {
                    UserId = UserId,
                    CharacterName = CharacterName,
                    Strength = Strength,
                    Dexterity = Dexterity,
                    Constitution = Constitution,
                    Intelligence = Intelligence,
                    Wisdom = Wisdom,
                    Charisma = Charisma,
                    MaxHp = MaxHp,
                    CurrentHp = CurrentHp
                };
            }
        }
    }
}
