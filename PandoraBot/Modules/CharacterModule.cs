using Discord.Interactions;
using PandoraBot.Services;
using System.Text;

namespace PandoraBot.Modules
{
    public class CharacterModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("\uB4F1\uB85D", "Import a character from a source sheet into character storage.")]
        public async Task RegisterCharacter(
            [Summary("\uC2DC\uD2B8", "Character source sheet name or Google Sheets URL")] string sourceSheet)
        {
            await DeferAsync();

            try
            {
                var result = await GoogleSheetService.Instance.RegisterAsync(sourceSheet, Context.User.Id.ToString());
                var action = result.WasUpdated ? "updated" : "saved";

                await FollowupAsync($"{Context.User.Mention}'s character `{result.Hunter.CharacterName}` was {action} at storage row {result.RowNumber}.");
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}");
            }
        }

        [SlashCommand("\uC120\uD0DD", "Select one of your registered characters.")]
        public async Task SelectCharacter(
            [Summary("\uCE90\uB9AD\uD130", "Registered character name")] string characterName)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var hunter = await GoogleSheetService.Instance.SelectCharacterAsync(characterName, Context.User.Id.ToString());
                await FollowupAsync($"Selected `{hunter.CharacterName}`.", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("\uD604\uC7AC", "Show your currently selected character.")]
        public async Task ShowCurrentCharacter()
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var hunter = await GoogleSheetService.Instance.GetSelectedCharacterAsync(Context.User.Id.ToString());
                await FollowupAsync(
                    $"현재 선택된 캐릭터는 `{hunter.CharacterName}`입니다.\n" +
                    $"HP `{hunter.CurrentHp} / {hunter.MaxHp}`\n" +
                    $"근력 `{hunter.Strength}` | 민첩 `{hunter.Dexterity}` | 체력 `{hunter.Constitution}` | " +
                    $"지능 `{hunter.Intelligence}` | 지혜 `{hunter.Wisdom}` | 매력 `{hunter.Charisma}`",
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("\uD574\uC81C", "Clear your currently selected character.")]
        public async Task ClearCurrentCharacter()
        {
            await ClearCurrentCharacterCoreAsync();
        }

        [SlashCommand("\uD5E4\uC81C", "Alias for /해제.")]
        public async Task ClearCurrentCharacterTypoAlias()
        {
            await ClearCurrentCharacterCoreAsync();
        }

        [SlashCommand("\uC0AD\uC81C", "Delete one of your registered characters.")]
        public async Task DeleteCharacter(
            [Summary("\uCE90\uB9AD\uD130", "Registered character name")] string characterName,
            [Summary("\uD655\uC778", "Set true to delete the character")] bool confirm = false)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (!confirm)
                {
                    await FollowupAsync(
                        $"`{characterName}` 삭제를 진행하려면 `/삭제 캐릭터:{characterName} 확인:True`로 다시 실행해주세요.",
                        ephemeral: true);
                    return;
                }

                var result = await GoogleSheetService.Instance.DeleteCharacterAsync(characterName, Context.User.Id.ToString());
                await FollowupAsync($"`{result.CharacterName}` 캐릭터 등록 정보를 삭제했습니다. (row {result.RowNumber})", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("\uBAA9\uB85D", "Show your registered character list.")]
        public async Task ListCharacters()
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var characters = await GoogleSheetService.Instance.ListCharactersAsync(Context.User.Id.ToString());
                if (characters.Count == 0)
                {
                    await FollowupAsync("No registered characters found. Use /등록 first.", ephemeral: true);
                    return;
                }

                var builder = new StringBuilder();
                builder.AppendLine("```text");
                builder.AppendLine("PROJECT:PANDORA / MY CHARACTERS");
                builder.AppendLine("--------------------------------");

                foreach (var character in characters)
                {
                    var marker = character.IsSelected ? "*" : " ";
                    builder.AppendLine($"{marker} {character.CharacterName}  HP {character.CurrentHp}/{character.MaxHp}  row:{character.RowNumber}");
                }

                builder.AppendLine("--------------------------------");
                builder.AppendLine("* = selected");
                builder.Append("```");

                await FollowupAsync(builder.ToString(), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("\uC815\uBCF4", "Show your selected character status card.")]
        public async Task ShowCharacterInfo(
            [Summary("\uCE90\uB9AD\uD130", "Optional registered character name")] string? characterName = null)
        {
            await DeferAsync();

            try
            {
                var sheetService = GoogleSheetService.Instance;
                var hunter = string.IsNullOrWhiteSpace(characterName)
                    ? await sheetService.GetSelectedCharacterAsync(Context.User.Id.ToString())
                    : await sheetService.GetCharacterAsync(characterName, Context.User.Id.ToString());

                using var card = CharacterCardService.Render(hunter, Context.User.Username);
                await FollowupWithFileAsync(card, $"pandora_{hunter.CharacterName}.png");
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        private async Task ClearCurrentCharacterCoreAsync()
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.ClearSelectedCharacterAsync(Context.User.Id.ToString());
                if (result.ClearedCount == 0)
                {
                    await FollowupAsync("현재 선택된 캐릭터가 없습니다.", ephemeral: true);
                    return;
                }

                await FollowupAsync("현재 선택된 캐릭터를 해제했습니다.", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }
    }
}
