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
        private const string JudgementLogSheet = "\uD310\uC815 \uB85C\uADF8";
        private const string AdminLogSheet = "\uAD00\uB9AC \uB85C\uADF8";
        private const string NoticeLogSheet = "\uACF5\uC9C0 \uB85C\uADF8";
        private const string SelectionSheet = "\uC120\uD0DD \uC0C1\uD0DC";
        private const string SelectedMarker = "selected";
        private const string ReviewPending = "pending";
        private const string ReviewApproved = "approved";
        private const string ReviewRejected = "rejected";

        private readonly SheetsService service;
        private readonly string storageSpreadsheetId;
        private bool operationalSheetsChecked;

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
                await EnsureOperationalSheetsAsync();

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
                await EnsureOperationalSheetsAsync();

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

                EnsureCharacterApproved(selectedRow);

                await UpsertSelectionRowAsync(userId, selectedRow.CharacterName);
                await ClearLegacySelectionMarkersAsync(rows, userId);

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
                await EnsureOperationalSheetsAsync();

                var rows = await ReadStorageRowsAsync();
                var selections = await ReadSelectionRowsAsync();
                var selection = selections.FirstOrDefault(row => row.UserId == userId);
                var selectedRow = selection == null
                    ? rows.FirstOrDefault(row =>
                        row.UserId == userId &&
                        string.Equals(row.Selected, SelectedMarker, StringComparison.OrdinalIgnoreCase))
                    : rows.FirstOrDefault(row =>
                        row.UserId == userId &&
                        Normalize(row.CharacterName) == Normalize(selection.CharacterName));

                if (selectedRow == null)
                {
                    throw new Exception("No selected character was found. Use /select first.");
                }

                EnsureCharacterApproved(selectedRow);

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
                await EnsureOperationalSheetsAsync();

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

                EnsureCharacterApproved(row);

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
                await EnsureOperationalSheetsAsync();

                var rows = await ReadStorageRowsAsync();
                var selections = await ReadSelectionRowsAsync();
                var selectionRows = selections.Where(row => row.UserId == userId).ToList();
                var userRows = rows.Where(row => row.UserId == userId).ToList();
                var selectedRows = userRows
                    .Where(row => string.Equals(row.Selected, SelectedMarker, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var selection in selectionRows)
                {
                    await ClearSelectionRowAsync(selection.RowNumber);
                }

                foreach (var row in selectedRows)
                {
                    await UpdateSingleCellAsync($"K{row.RowNumber}", "");
                }

                return new ClearSelectionResult(selectionRows.Count + selectedRows.Count);
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
                await EnsureOperationalSheetsAsync();

                var rows = await ReadStorageRowsAsync();
                var selectedCharacterName = await GetSelectedCharacterNameForUserAsync(userId);
                return rows
                    .Where(row => row.UserId == userId)
                    .OrderBy(row => row.CharacterName)
                    .Select(row => new CharacterSummary(
                        row.CharacterName,
                        row.CurrentHp,
                        row.MaxHp,
                        IsSelected(row, selectedCharacterName),
                        row.RowNumber,
                        NormalizeReviewStatus(row.ReviewStatus)))
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
                await EnsureOperationalSheetsAsync();

                var rows = await ReadStorageRowsAsync();
                var selections = await ReadSelectionRowsAsync();
                return rows
                    .OrderBy(row => row.UserId)
                    .ThenBy(row => row.CharacterName)
                    .Take(Math.Clamp(limit, 1, 50))
                    .Select(row => new AdminCharacterSummary(
                        row.UserId,
                        row.CharacterName,
                        row.CurrentHp,
                        row.MaxHp,
                        IsSelected(row, selections),
                        row.RowNumber,
                        NormalizeReviewStatus(row.ReviewStatus),
                        GetSelectedBy(row, selections)))
                    .ToList();
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<Hunter> GetCharacterForAdminAsync(string characterName)
        {
            await SheetLock.WaitAsync();

            try
            {
                var rows = await ReadStorageRowsAsync();
                return FindCharacterRow(rows, characterName).ToHunter();
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<UpdateHpResult> SetCharacterHpAsync(string characterName, int currentHp)
        {
            await SheetLock.WaitAsync();

            try
            {
                await EnsureOperationalSheetsAsync();

                var rows = await ReadStorageRowsAsync();
                var row = FindCharacterRow(rows, characterName);
                var clampedHp = Math.Clamp(currentHp, 0, row.MaxHp);

                await UpdateSingleCellAsync($"J{row.RowNumber}", clampedHp.ToString());
                return new UpdateHpResult(row.CharacterName, row.UserId, row.CurrentHp, clampedHp, row.MaxHp, row.RowNumber);
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<UpdateHpResult> AdjustCharacterHpAsync(
            string characterName,
            int amount,
            string adminUserId,
            string adminUsername,
            string action,
            string? memo = null)
        {
            await SheetLock.WaitAsync();

            try
            {
                await EnsureOperationalSheetsAsync();

                var rows = await ReadStorageRowsAsync();
                var row = FindCharacterRow(rows, characterName);
                var signedAmount = action == "heal" ? amount : -amount;
                var newHp = Math.Clamp(row.CurrentHp + signedAmount, 0, row.MaxHp);

                await UpdateSingleCellAsync($"J{row.RowNumber}", newHp.ToString());
                await AppendAdminLogRowAsync(
                    action == "heal" ? "회복" : "피해",
                    adminUserId,
                    adminUsername,
                    row.UserId,
                    row.CharacterName,
                    $"{row.CurrentHp} -> {newHp} ({signedAmount:+#;-#;0}) {memo}".Trim());

                return new UpdateHpResult(row.CharacterName, row.UserId, row.CurrentHp, newHp, row.MaxHp, row.RowNumber);
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<ClearSelectionResult> ClearSelectedCharacterForAdminAsync(string characterName)
        {
            await SheetLock.WaitAsync();

            try
            {
                await EnsureOperationalSheetsAsync();

                var rows = await ReadStorageRowsAsync();
                var row = FindCharacterRow(rows, characterName);
                var selections = await ReadSelectionRowsAsync();
                var selectionRows = selections.Where(item => item.UserId == row.UserId).ToList();
                var selectedRows = rows
                    .Where(item => item.UserId == row.UserId && string.Equals(item.Selected, SelectedMarker, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var selectionRow in selectionRows)
                {
                    await ClearSelectionRowAsync(selectionRow.RowNumber);
                }

                foreach (var selectedRow in selectedRows)
                {
                    await UpdateSingleCellAsync($"K{selectedRow.RowNumber}", "");
                }

                return new ClearSelectionResult(selectionRows.Count + selectedRows.Count);
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<DeleteCharacterResult> DeleteCharacterForAdminAsync(string characterName)
        {
            await SheetLock.WaitAsync();

            try
            {
                await EnsureOperationalSheetsAsync();

                var rows = await ReadStorageRowsAsync();
                var row = FindCharacterRow(rows, characterName);

                await ClearStorageRowAsync(row.RowNumber);
                return new DeleteCharacterResult(row.CharacterName, row.RowNumber);
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<ReviewResult> SetCharacterReviewStatusAsync(
            string characterName,
            string status,
            string adminUserId,
            string adminUsername,
            string? memo = null)
        {
            await SheetLock.WaitAsync();

            try
            {
                await EnsureOperationalSheetsAsync();

                var normalizedStatus = NormalizeReviewStatus(status);
                if (normalizedStatus is not ReviewApproved and not ReviewPending and not ReviewRejected)
                {
                    throw new Exception("Review status must be approved, pending, or rejected.");
                }

                var rows = await ReadStorageRowsAsync();
                var row = FindCharacterRow(rows, characterName);

                await UpdateSingleCellAsync($"L{row.RowNumber}", normalizedStatus);
                if (normalizedStatus == ReviewRejected)
                {
                    await UpdateSingleCellAsync($"K{row.RowNumber}", "");
                }

                await AppendAdminLogRowAsync("검수상태", adminUserId, adminUsername, row.UserId, row.CharacterName, $"{normalizedStatus} {memo}".Trim());
                return new ReviewResult(row.CharacterName, row.UserId, normalizedStatus, row.RowNumber);
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<IReadOnlyList<AdminCharacterSummary>> ListReviewCharactersAsync(string status = "pending", int limit = 25)
        {
            await SheetLock.WaitAsync();

            try
            {
                await EnsureOperationalSheetsAsync();

                var normalizedStatus = NormalizeReviewStatus(status);
                var rows = await ReadStorageRowsAsync();
                var selections = await ReadSelectionRowsAsync();
                return rows
                    .Where(row => NormalizeReviewStatus(row.ReviewStatus) == normalizedStatus)
                    .OrderBy(row => row.UserId)
                    .ThenBy(row => row.CharacterName)
                    .Take(Math.Clamp(limit, 1, 50))
                    .Select(row => new AdminCharacterSummary(
                        row.UserId,
                        row.CharacterName,
                        row.CurrentHp,
                        row.MaxHp,
                        IsSelected(row, selections),
                        row.RowNumber,
                        NormalizeReviewStatus(row.ReviewStatus),
                        GetSelectedBy(row, selections)))
                    .ToList();
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task AppendJudgementLogAsync(JudgementLogEntry entry)
        {
            await SheetLock.WaitAsync();

            try
            {
                await EnsureOperationalSheetsAsync();
                await AppendRowAsync(JudgementLogSheet, new List<object>
                {
                    DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    entry.GuildId,
                    entry.ChannelId,
                    entry.UserId,
                    entry.Username,
                    entry.CharacterName,
                    entry.StatCode,
                    entry.StatName,
                    entry.Die1,
                    entry.Die2,
                    entry.Modifier,
                    entry.Total,
                    entry.Outcome
                });
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<IReadOnlyList<JudgementLogSummary>> ListRecentJudgementLogsAsync(int limit = 10)
        {
            await SheetLock.WaitAsync();

            try
            {
                await EnsureOperationalSheetsAsync();

                var sheetName = ToRangeSheetName(JudgementLogSheet);
                var response = await service.Spreadsheets.Values.Get(storageSpreadsheetId, $"{sheetName}!A2:M").ExecuteAsync();
                var values = response.Values ?? new List<IList<object>>();

                return values
                    .Where(row => row.Count > 0)
                    .Reverse()
                    .Take(Math.Clamp(limit, 1, 30))
                    .Select(row => new JudgementLogSummary(
                        GetString(row, 0),
                        GetString(row, 4),
                        GetString(row, 5),
                        GetString(row, 6),
                        GetString(row, 11),
                        GetString(row, 12)))
                    .ToList();
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task<IReadOnlyList<JudgementLogSummary>> ListRecentJudgementLogsForUserAsync(string userId, int limit = 10)
        {
            await SheetLock.WaitAsync();

            try
            {
                await EnsureOperationalSheetsAsync();

                var sheetName = ToRangeSheetName(JudgementLogSheet);
                var response = await service.Spreadsheets.Values.Get(storageSpreadsheetId, $"{sheetName}!A2:M").ExecuteAsync();
                var values = response.Values ?? new List<IList<object>>();

                return values
                    .Where(row => GetString(row, 3) == userId)
                    .Reverse()
                    .Take(Math.Clamp(limit, 1, 30))
                    .Select(row => new JudgementLogSummary(
                        GetString(row, 0),
                        GetString(row, 4),
                        GetString(row, 5),
                        GetString(row, 6),
                        GetString(row, 11),
                        GetString(row, 12)))
                    .ToList();
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task AppendNoticeLogAsync(string noticeType, string title, string content, string adminUserId, string adminUsername, string channelId)
        {
            await SheetLock.WaitAsync();

            try
            {
                await EnsureOperationalSheetsAsync();
                await AppendRowAsync(NoticeLogSheet, new List<object>
                {
                    DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    noticeType,
                    title,
                    content,
                    adminUserId,
                    adminUsername,
                    channelId
                });
            }
            finally
            {
                SheetLock.Release();
            }
        }

        public async Task AppendAdminLogAsync(string action, string adminUserId, string adminUsername, string targetUserId, string characterName, string detail)
        {
            await SheetLock.WaitAsync();

            try
            {
                await EnsureOperationalSheetsAsync();
                await AppendAdminLogRowAsync(action, adminUserId, adminUsername, targetUserId, characterName, detail);
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
            var reviewStatus = matches.FirstOrDefault(row => !string.IsNullOrWhiteSpace(row.ReviewStatus))?.ReviewStatus
                ?? (wasUpdated ? "" : ReviewPending);

            await WriteStorageRowAsync(rowNumber, hunter, selected, reviewStatus);

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
            var response = await service.Spreadsheets.Values.Get(storageSpreadsheetId, $"{storageSheetName}!A2:L").ExecuteAsync();
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
                    Selected: GetString(row, 10),
                    ReviewStatus: GetString(row, 11)));
            }

            return rows;
        }

        private async Task<List<SelectionRow>> ReadSelectionRowsAsync()
        {
            var selectionSheetName = ToRangeSheetName(SelectionSheet);
            var response = await service.Spreadsheets.Values.Get(storageSpreadsheetId, $"{selectionSheetName}!A2:D").ExecuteAsync();
            var values = response.Values ?? new List<IList<object>>();
            var rows = new List<SelectionRow>();

            for (var index = 0; index < values.Count; index++)
            {
                var row = values[index];
                var userId = GetString(row, 0);
                var characterName = GetString(row, 1);

                if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(characterName))
                {
                    continue;
                }

                rows.Add(new SelectionRow(
                    RowNumber: index + 2,
                    UserId: userId,
                    CharacterName: characterName,
                    SelectedAt: GetString(row, 2)));
            }

            return rows;
        }

        private async Task<string?> GetSelectedCharacterNameForUserAsync(string userId)
        {
            var selections = await ReadSelectionRowsAsync();
            var selection = selections.FirstOrDefault(row => row.UserId == userId);
            return selection?.CharacterName;
        }

        private async Task UpsertSelectionRowAsync(string userId, string characterName)
        {
            var selections = await ReadSelectionRowsAsync();
            var rowNumber = selections.FirstOrDefault(row => row.UserId == userId)?.RowNumber ?? await GetNextSelectionRowAsync();
            var valueRange = new ValueRange
            {
                Values = new List<IList<object>>
                {
                    new List<object>
                    {
                        userId,
                        characterName,
                        DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ""
                    }
                }
            };

            var request = service.Spreadsheets.Values.Update(valueRange, storageSpreadsheetId, $"{ToRangeSheetName(SelectionSheet)}!A{rowNumber}:D{rowNumber}");
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await request.ExecuteAsync();
        }

        private async Task<int> GetNextSelectionRowAsync()
        {
            var response = await service.Spreadsheets.Values.Get(storageSpreadsheetId, $"{ToRangeSheetName(SelectionSheet)}!A:B").ExecuteAsync();
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

        private async Task ClearSelectionRowAsync(int rowNumber)
        {
            var clearRequest = service.Spreadsheets.Values.Clear(new ClearValuesRequest(), storageSpreadsheetId, $"{ToRangeSheetName(SelectionSheet)}!A{rowNumber}:D{rowNumber}");
            await clearRequest.ExecuteAsync();
        }

        private async Task ClearLegacySelectionMarkersAsync(IReadOnlyList<StorageRow> rows, string userId)
        {
            foreach (var row in rows.Where(row => row.UserId == userId && !string.IsNullOrWhiteSpace(row.Selected)))
            {
                await UpdateSingleCellAsync($"K{row.RowNumber}", "");
            }
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

        private async Task WriteStorageRowAsync(int rowNumber, Hunter hunter, string selected, string reviewStatus)
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
                selected,
                reviewStatus
            };

            var storageSheetName = ToRangeSheetName(StorageSheet);
            var writeRange = $"{storageSheetName}!A{rowNumber}:L{rowNumber}";
            var valueRange = new ValueRange { Values = new List<IList<object>> { rowValues } };

            var updateRequest = service.Spreadsheets.Values.Update(valueRange, storageSpreadsheetId, writeRange);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await updateRequest.ExecuteAsync();
        }

        private async Task ClearStorageRowAsync(int rowNumber)
        {
            var storageSheetName = ToRangeSheetName(StorageSheet);
            var clearRequest = service.Spreadsheets.Values.Clear(new ClearValuesRequest(), storageSpreadsheetId, $"{storageSheetName}!A{rowNumber}:L{rowNumber}");
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

        private async Task EnsureOperationalSheetsAsync()
        {
            if (operationalSheetsChecked)
            {
                return;
            }

            var metadata = await service.Spreadsheets.Get(storageSpreadsheetId).ExecuteAsync();
            var existingSheets = (metadata.Sheets ?? new List<Sheet>())
                .Select(sheet => sheet.Properties?.Title ?? "")
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .ToHashSet(StringComparer.Ordinal);

            var addRequests = new List<Request>();
            foreach (var sheetName in new[] { JudgementLogSheet, AdminLogSheet, NoticeLogSheet, SelectionSheet })
            {
                if (!existingSheets.Contains(sheetName))
                {
                    addRequests.Add(new Request
                    {
                        AddSheet = new AddSheetRequest
                        {
                            Properties = new SheetProperties { Title = sheetName }
                        }
                    });
                }
            }

            if (addRequests.Count > 0)
            {
                await service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest { Requests = addRequests }, storageSpreadsheetId).ExecuteAsync();
            }

            await WriteHeaderRowAsync(StorageSheet, "L1", new List<object> { "검수상태" });
            await WriteHeaderRowAsync(JudgementLogSheet, "A1:M1", new List<object>
            {
                "시각", "서버ID", "채널ID", "유저ID", "유저명", "캐릭터", "능력코드", "능력치", "주사위1", "주사위2", "수정치", "최종값", "결과"
            });
            await WriteHeaderRowAsync(AdminLogSheet, "A1:G1", new List<object>
            {
                "시각", "행동", "관리자ID", "관리자명", "대상유저ID", "캐릭터", "상세"
            });
            await WriteHeaderRowAsync(NoticeLogSheet, "A1:G1", new List<object>
            {
                "시각", "종류", "제목", "내용", "관리자ID", "관리자명", "채널ID"
            });
            await WriteHeaderRowAsync(SelectionSheet, "A1:D1", new List<object>
            {
                "유저ID", "선택캐릭터", "선택시각", "비고"
            });

            operationalSheetsChecked = true;
        }

        private async Task WriteHeaderRowAsync(string sheetName, string range, IList<object> values)
        {
            var valueRange = new ValueRange { Values = new List<IList<object>> { values } };
            var updateRequest = service.Spreadsheets.Values.Update(valueRange, storageSpreadsheetId, $"{ToRangeSheetName(sheetName)}!{range}");
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await updateRequest.ExecuteAsync();
        }

        private async Task AppendRowAsync(string sheetName, IList<object> values)
        {
            var valueRange = new ValueRange { Values = new List<IList<object>> { values } };
            var appendRequest = service.Spreadsheets.Values.Append(valueRange, storageSpreadsheetId, $"{ToRangeSheetName(sheetName)}!A:Z");
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;
            appendRequest.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
            await appendRequest.ExecuteAsync();
        }

        private async Task AppendAdminLogRowAsync(string action, string adminUserId, string adminUsername, string targetUserId, string characterName, string detail)
        {
            await AppendRowAsync(AdminLogSheet, new List<object>
            {
                DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                action,
                adminUserId,
                adminUsername,
                targetUserId,
                characterName,
                detail
            });
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

        private static bool IsSelected(StorageRow row, string? selectedCharacterName)
        {
            if (!string.IsNullOrWhiteSpace(selectedCharacterName))
            {
                return Normalize(row.CharacterName) == Normalize(selectedCharacterName);
            }

            return string.Equals(row.Selected, SelectedMarker, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSelected(StorageRow row, IReadOnlyList<SelectionRow> selections)
        {
            return selections.Any(selection =>
                selection.UserId == row.UserId &&
                Normalize(selection.CharacterName) == Normalize(row.CharacterName)) ||
                string.Equals(row.Selected, SelectedMarker, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSelectedBy(StorageRow row, IReadOnlyList<SelectionRow> selections)
        {
            var selection = selections.FirstOrDefault(selection =>
                selection.UserId == row.UserId &&
                Normalize(selection.CharacterName) == Normalize(row.CharacterName));

            if (selection != null)
            {
                return selection.UserId;
            }

            return string.Equals(row.Selected, SelectedMarker, StringComparison.OrdinalIgnoreCase)
                ? row.UserId
                : "";
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

        private static StorageRow FindCharacterRow(IReadOnlyList<StorageRow> rows, string characterName)
        {
            var requestedName = Normalize(characterName);
            var matches = rows
                .Where(row => Normalize(row.CharacterName) == requestedName)
                .ToList();

            if (matches.Count == 0)
            {
                throw new Exception("No registered character was found with that name.");
            }

            if (matches.Count > 1)
            {
                throw new Exception("Multiple characters were found with that name. Please clean up duplicate character names first.");
            }

            return matches[0];
        }

        private static void EnsureCharacterApproved(StorageRow row)
        {
            if (NormalizeReviewStatus(row.ReviewStatus) == ReviewRejected)
            {
                throw new Exception("This character was rejected by an operator.");
            }

            if (NormalizeReviewStatus(row.ReviewStatus) == ReviewPending)
            {
                throw new Exception("This character is waiting for operator approval.");
            }
        }

        private static string NormalizeReviewStatus(string value)
        {
            var normalized = value.Trim().ToLowerInvariant();
            return normalized switch
            {
                "" => ReviewApproved,
                "승인" or "approve" or "approved" => ReviewApproved,
                "대기" or "검수" or "pending" => ReviewPending,
                "반려" or "reject" or "rejected" => ReviewRejected,
                _ => normalized
            };
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

        public sealed record CharacterSummary(string CharacterName, int CurrentHp, int MaxHp, bool IsSelected, int RowNumber, string ReviewStatus);

        public sealed record AdminCharacterSummary(string UserId, string CharacterName, int CurrentHp, int MaxHp, bool IsSelected, int RowNumber, string ReviewStatus, string SelectedByUserId);

        public sealed record UpdateHpResult(string CharacterName, string UserId, int OldHp, int CurrentHp, int MaxHp, int RowNumber);

        public sealed record ReviewResult(string CharacterName, string UserId, string ReviewStatus, int RowNumber);

        public sealed record JudgementLogEntry(
            string GuildId,
            string ChannelId,
            string UserId,
            string Username,
            string CharacterName,
            string StatCode,
            string StatName,
            int Die1,
            int Die2,
            int Modifier,
            int Total,
            string Outcome);

        public sealed record JudgementLogSummary(string CreatedAt, string Username, string CharacterName, string StatCode, string Total, string Outcome);

        private sealed record SheetReference(string SpreadsheetId, string SheetName);

        private sealed record SelectionRow(int RowNumber, string UserId, string CharacterName, string SelectedAt);

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
            string Selected,
            string ReviewStatus)
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
