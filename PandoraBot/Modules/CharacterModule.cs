using Discord;
using Discord.Interactions;
using PandoraBot.Models;
using PandoraBot.Repositories;
using PandoraBot.Services;
using System.Text;

namespace PandoraBot.Modules
{
    public class CharacterModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("등록", "캐릭터 원본 시트를 읽어 캐릭터를 등록합니다.")]
        public async Task RegisterCharacter(
            [Summary("시트", "캐릭터 원본 시트 이름 또는 Google Sheets URL")] string sourceSheet)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.RegisterAsync(sourceSheet, Context.User.Id.ToString());
                var action = result.WasUpdated ? "갱신" : "등록";
                var nextStep = result.WasUpdated
                    ? "\n다음으로 /검수상태로 승인 상태를 확인하고, 승인된 캐릭터라면 /선택 후 /현재 또는 /정보를 확인해 주세요."
                    : "\n신규 캐릭터는 승인 전까지 사용할 수 없습니다. /검수상태로 진행 상태를 확인해 주세요.";

                await FollowupAsync($"{Context.User.Mention}님의 캐릭터 {result.Hunter.CharacterName} 정보를 {action}했습니다.{nextStep}", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("갱신", "원본 캐릭터 시트를 다시 읽어 캐릭터 정보를 갱신합니다.")]
        public async Task RefreshCharacter(
            [Summary("시트", "캐릭터 원본 시트 이름 또는 Google Sheets URL")] string sourceSheet)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.RegisterAsync(sourceSheet, Context.User.Id.ToString());
                var message = result.WasUpdated
                    ? $"{result.Hunter.CharacterName} 정보를 원본 시트 기준으로 갱신했습니다.\n다음으로 /검수상태를 확인하고, 승인된 캐릭터라면 /선택 후 /현재 또는 /정보를 확인해 주세요."
                    : $"{result.Hunter.CharacterName} 정보를 새로 등록했습니다. 진행자 승인 전까지는 사용할 수 없습니다.\n다음으로 /검수상태로 승인 상태를 확인해 주세요.";

                await FollowupAsync(message, ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("선택", "등록한 캐릭터 중 현재 사용할 캐릭터를 선택합니다.")]
        public async Task SelectCharacter(
            [Summary("캐릭터", "등록한 캐릭터 이름, 원본 이름, 시트 제목 또는 source_sheet_id")] string characterName)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var character = await PandoraRepositoryProvider.Characters.SelectCharacterAsync(
                    Context.User.Id.ToString(),
                    characterName);

                await FollowupAsync($"`{character.DisplayName}` 캐릭터를 선택했습니다.", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("현재", "현재 선택한 캐릭터를 확인합니다.")]
        public async Task ShowCurrentCharacter()
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var character = await PandoraRepositoryProvider.Characters.GetSelectedCharacterAsync(Context.User.Id.ToString());
                var hunter = ToHunter(character);

                await FollowupAsync(
                    $"현재 선택한 캐릭터는 `{hunter.CharacterName}`입니다.\n" +
                    $"HP `{hunter.CurrentHp} / {hunter.MaxHp}`\n" +
                    $"근력 `{hunter.Strength}` | 민첩 `{hunter.Dexterity}` | 체력 `{hunter.Constitution}` | " +
                    $"지능 `{hunter.Intelligence}` | 지혜 `{hunter.Wisdom}` | 매력 `{hunter.Charisma}`",
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("판정", "선택한 캐릭터로 2d6 판정을 굴립니다.")]
        public async Task RollJudgement(
            [Summary("능력치", "근력, 민첩, 체력, 지능, 지혜, 매력 중 하나")] string ability)
        {
            await DeferAsync();

            try
            {
                var character = await PandoraRepositoryProvider.Characters.GetSelectedCharacterAsync(Context.User.Id.ToString());
                var hunter = ToHunter(character);
                var stat = ResolveStat(hunter, ability);
                if (stat is null)
                {
                    await FollowupAsync("능력치는 `근력`, `민첩`, `체력`, `지능`, `지혜`, `매력` 중 하나로 입력해 주세요.", ephemeral: true);
                    return;
                }

                var die1 = Random.Shared.Next(1, 7);
                var die2 = Random.Shared.Next(1, 7);
                var diceTotal = die1 + die2;
                var total = diceTotal + stat.Modifier;
                var outcome = ResolveOutcome(total);

                await PandoraRepositoryProvider.Logs.AppendRollLogAsync(
                    character.CharacterId,
                    character.DisplayName,
                    stat.Code,
                    die1,
                    die2,
                    stat.Modifier,
                    total,
                    outcome.Label);

                var embed = new EmbedBuilder()
                    .WithTitle("PROJECT:PANDORA | 판정 결과")
                    .WithColor(outcome.Color)
                    .WithDescription($"**{character.DisplayName}**의 **{stat.KoreanName}({stat.Code})** 판정입니다.")
                    .AddField("주사위", $"`{die1}` + `{die2}` = **{diceTotal}**", inline: true)
                    .AddField("수정치", FormatModifier(stat.Modifier), inline: true)
                    .AddField("최종값", $"**{total}**", inline: true)
                    .AddField("기준", "`10+` 성공 / `7-9` 부분 성공 / `6-` 실패", inline: false)
                    .AddField("결과", outcome.Text, inline: false)
                    .WithFooter("PANDORA NETWORK / APOCALYPSE ENGINE")
                    .WithCurrentTimestamp()
                    .Build();

                await FollowupAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("해제", "현재 선택한 캐릭터를 해제합니다.")]
        public async Task ClearCurrentCharacter()
        {
            await ClearCurrentCharacterCoreAsync();
        }

        [SlashCommand("헤제", "해제 명령어의 오타 별칭입니다.")]
        public async Task ClearCurrentCharacterTypoAlias()
        {
            await ClearCurrentCharacterCoreAsync();
        }

        [SlashCommand("삭제", "등록한 캐릭터 정보를 삭제합니다.")]
        public async Task DeleteCharacter(
            [Summary("캐릭터", "등록한 캐릭터 이름, 원본 이름, 시트 제목 또는 source_sheet_id")] string characterName,
            [Summary("확인", "True로 설정하면 캐릭터를 삭제합니다.")] bool confirm = false)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (!confirm)
                {
                    await FollowupAsync(
                        $"`{characterName}` 삭제를 진행하려면 `/삭제 캐릭터:{characterName} 확인:True`로 다시 실행해 주세요.",
                        ephemeral: true);
                    return;
                }

                var result = await PandoraRepositoryProvider.Characters.DeleteCharacterAsync(
                    Context.User.Id.ToString(),
                    characterName);

                await FollowupAsync($"`{result.CharacterName}` 캐릭터 정보를 삭제했습니다.", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("목록", "내가 등록한 캐릭터 목록을 확인합니다.")]
        public async Task ListCharacters()
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var characters = await PandoraRepositoryProvider.Characters.ListCharactersAsync(Context.User.Id.ToString());
                if (characters.Count == 0)
                {
                    await FollowupAsync("등록한 캐릭터가 없습니다. 먼저 `/등록`으로 원본 시트를 불러와 주세요.", ephemeral: true);
                    return;
                }

                var builder = new StringBuilder();
                builder.AppendLine("```text");
                builder.AppendLine("PROJECT:PANDORA / MY CHARACTERS");
                builder.AppendLine("--------------------------------");

                foreach (var character in characters)
                {
                    var marker = character.IsSelected ? "*" : " ";
                    builder.AppendLine($"{marker} {character.CharacterName}  HP {character.CurrentHp}/{character.MaxHp}  status:{character.ReviewStatus}");
                }

                builder.AppendLine("--------------------------------");
                builder.AppendLine("* = selected");
                builder.Append("```");

                await FollowupAsync(builder.ToString(), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("정보", "선택한 캐릭터의 상태창 이미지를 표시합니다.")]
        public async Task ShowCharacterInfo(
            [Summary("캐릭터", "선택 사항: 등록한 캐릭터 이름, 원본 이름, 시트 제목 또는 source_sheet_id")] string? characterName = null)
        {
            await DeferAsync();

            try
            {
                var character = string.IsNullOrWhiteSpace(characterName)
                    ? await PandoraRepositoryProvider.Characters.GetSelectedCharacterAsync(Context.User.Id.ToString())
                    : await PandoraRepositoryProvider.Characters.GetCharacterAsync(Context.User.Id.ToString(), characterName);

                using var card = CharacterCardService.Render(ToHunter(character), Context.User.Username);
                await FollowupWithFileAsync(card, $"pandora_{character.DisplayName}.png");
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

        private async Task ClearCurrentCharacterCoreAsync()
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var clearedCount = await PandoraRepositoryProvider.Characters.ClearSelectedCharacterAsync(Context.User.Id.ToString());
                if (clearedCount == 0)
                {
                    await FollowupAsync("현재 선택한 캐릭터가 없습니다.", ephemeral: true);
                    return;
                }

                await FollowupAsync("현재 선택한 캐릭터를 해제했습니다.", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

        private static Hunter ToHunter(CharacterRecord character)
            => new()
            {
                UserId = character.OwnerDiscordId,
                CharacterName = character.DisplayName,
                Strength = character.Strength,
                Dexterity = character.Dexterity,
                Constitution = character.Constitution,
                Intelligence = character.Intelligence,
                Wisdom = character.Wisdom,
                Charisma = character.Charisma,
                CurrentHp = character.CurrentHp,
                MaxHp = character.MaxHp
            };

        private static StatInfo? ResolveStat(Hunter hunter, string ability)
        {
            var normalized = ability.Trim().ToLowerInvariant();
            return normalized switch
            {
                "근력" or "str" or "strength" => Create("STR", "근력", hunter.Strength, hunter),
                "민첩" or "민첩성" or "dex" or "dexterity" => Create("DEX", "민첩", hunter.Dexterity, hunter),
                "체력" or "con" or "constitution" => Create("CON", "체력", hunter.Constitution, hunter),
                "지능" or "int" or "intelligence" => Create("INT", "지능", hunter.Intelligence, hunter),
                "지혜" or "wis" or "wisdom" => Create("WIS", "지혜", hunter.Wisdom, hunter),
                "매력" or "cha" or "charisma" => Create("CHA", "매력", hunter.Charisma, hunter),
                _ => null
            };
        }

        private static StatInfo Create(string code, string koreanName, int value, Hunter hunter)
            => new(code, koreanName, value, hunter.GetModifier(value));

        private static JudgementOutcome ResolveOutcome(int total)
        {
            if (total >= 10)
            {
                return new JudgementOutcome("성공", "**성공** - 의도한 행동이 안정적으로 이루어집니다.", Color.Green);
            }

            if (total >= 7)
            {
                return new JudgementOutcome("부분 성공", "**부분 성공** - 성공하지만 대가, 선택, 위험이 따라붙습니다.", Color.Gold);
            }

            return new JudgementOutcome("실패", "**실패** - 진행자가 새로운 상황 변화나 실패 결과를 제시합니다.", Color.Red);
        }

        private static string FormatModifier(int modifier)
            => modifier >= 0 ? $"+{modifier}" : modifier.ToString();

        private static string ToFriendlyCharacterError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "처리 중 문제가 발생했습니다. 잠시 후 다시 시도해 주세요.";
            }

            if (message.Contains("PandoraDb connection string", StringComparison.OrdinalIgnoreCase))
            {
                return "캐릭터 운영용 DB 연결이 아직 준비되지 않았습니다. 설정을 확인한 뒤 다시 시도해 주세요.";
            }

            if (message.Contains("No registered character", StringComparison.OrdinalIgnoreCase))
            {
                return "등록한 캐릭터를 찾을 수 없습니다. 먼저 `/등록`으로 원본 시트를 불러오거나 `/목록`으로 보유 캐릭터를 확인해 주세요.";
            }

            if (message.Contains("No selected character", StringComparison.OrdinalIgnoreCase))
            {
                return "현재 선택한 캐릭터가 없습니다. `/목록`으로 보유 캐릭터를 확인한 뒤 `/선택`으로 사용할 캐릭터를 골라 주세요.";
            }

            if (message.Contains("not approved", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("approved", StringComparison.OrdinalIgnoreCase))
            {
                return $"{message}\n필요하면 `/검수상태`로 승인 여부를 먼저 확인해 주세요.";
            }

            if (message.Contains("Multiple characters matched", StringComparison.OrdinalIgnoreCase))
            {
                return "조건에 맞는 캐릭터가 여러 개입니다. 더 정확한 캐릭터 이름이나 원본 시트 제목, source_sheet_id로 다시 지정해 주세요.";
            }

            if (message.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                return "요청이 잠시 많아 처리 대기 중입니다. 몇 초 뒤 다시 시도해 주세요.";
            }

            return $"처리 중 문제가 발생했습니다: {message}";
        }

        private sealed record StatInfo(string Code, string KoreanName, int Value, int Modifier);
        private sealed record JudgementOutcome(string Label, string Text, Color Color);
    }
}
