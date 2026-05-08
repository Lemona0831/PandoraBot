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
        [SlashCommand("등록", "캐릭터 시트 정보를 저장소에 등록합니다.")]
        public async Task RegisterCharacter(
            [Summary("시트", "캐릭터 원본 시트 이름 또는 Google Sheets URL")] string sourceSheet)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.RegisterAsync(sourceSheet, Context.User.Id.ToString());
                var action = result.WasUpdated ? "\uAC31\uC2E0" : "\uB4F1\uB85D";
                var nextStep = result.WasUpdated
                    ? "\n\uB2E4\uC74C\uC73C\uB85C /\uAC80\uC218\uC0C1\uD0DC\uB85C \uC2B9\uC778 \uC0C1\uD0DC\uB97C \uD655\uC778\uD558\uACE0, \uC2B9\uC778\uB41C \uCE90\uB9AD\uD130\uB77C\uBA74 /\uC120\uD0DD \uD6C4 /\uD604\uC7AC \uB610\uB294 /\uC815\uBCF4\uB97C \uD655\uC778\uD574 \uC8FC\uC138\uC694."
                    : "\n\uC2E0\uADDC \uCE90\uB9AD\uD130\uB294 \uC2B9\uC778 \uC804\uAE4C\uC9C0 \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. /\uAC80\uC218\uC0C1\uD0DC\uB85C \uC9C4\uD589 \uC0C1\uD0DC\uB97C \uD655\uC778\uD574 \uC8FC\uC138\uC694.";

                await FollowupAsync($"{Context.User.Mention}\uB2D8\uC758 \uCE90\uB9AD\uD130 {result.Hunter.CharacterName} \uC815\uBCF4\uB97C {action}\uD588\uC2B5\uB2C8\uB2E4. (row {result.RowNumber}){nextStep}");
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("갱신", "원본 캐릭터 시트를 다시 읽어 저장소의 캐릭터 정보를 갱신합니다.")]
        public async Task RefreshCharacter(
            [Summary("시트", "캐릭터 원본 시트 이름 또는 Google Sheets URL")] string sourceSheet)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.RegisterAsync(sourceSheet, Context.User.Id.ToString());
                var message = result.WasUpdated
                    ? $"{result.Hunter.CharacterName} \uC815\uBCF4\uB97C \uC6D0\uBCF8 \uC2DC\uD2B8 \uAE30\uC900\uC73C\uB85C \uAC31\uC2E0\uD588\uC2B5\uB2C8\uB2E4. (row {result.RowNumber})\n\uB2E4\uC74C\uC73C\uB85C /\uAC80\uC218\uC0C1\uD0DC\uB97C \uD655\uC778\uD558\uACE0, \uC2B9\uC778\uB41C \uCE90\uB9AD\uD130\uB77C\uBA74 /\uC120\uD0DD \uD6C4 /\uD604\uC7AC \uB610\uB294 /\uC815\uBCF4\uB97C \uD655\uC778\uD574 \uC8FC\uC138\uC694."
                    : $"{result.Hunter.CharacterName} \uC815\uBCF4\uB97C \uC0C8\uB85C \uB4F1\uB85D\uD588\uC2B5\uB2C8\uB2E4. \uC9C4\uD589\uC790 \uC2B9\uC778 \uC804\uAE4C\uC9C0\uB294 \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. (row {result.RowNumber})\n\uB2E4\uC74C\uC73C\uB85C /\uAC80\uC218\uC0C1\uD0DC\uB85C \uC2B9\uC778 \uC0C1\uD0DC\uB97C \uD655\uC778\uD574 \uC8FC\uC138\uC694.";

                await FollowupAsync(message, ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("선택", "등록된 캐릭터 중 사용할 캐릭터를 선택합니다.")]
        public async Task SelectCharacter(
            [Summary("캐릭터", "등록된 캐릭터 이름")] string characterName)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var hunter = await GoogleSheetService.Instance.SelectCharacterAsync(characterName, Context.User.Id.ToString());
                await FollowupAsync($"`{hunter.CharacterName}` 캐릭터를 선택했습니다.", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("현재", "현재 선택된 캐릭터를 확인합니다.")]
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
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("판정", "선택된 캐릭터로 2d6 판정을 굴립니다.")]
        public async Task RollJudgement(
            [Summary("능력치", "근력, 민첩, 체력, 지능, 지혜, 매력 중 하나")] string ability)
        {
            await DeferAsync();

            try
            {
                var hunter = await GoogleSheetService.Instance.GetSelectedCharacterAsync(Context.User.Id.ToString());
                var stat = ResolveStat(hunter, ability);
                if (stat is null)
                {
                    await FollowupAsync("능력치는 `근력`, `민첩`, `체력`, `지능`, `지혜`, `매력` 중 하나로 입력해주세요.", ephemeral: true);
                    return;
                }

                var die1 = Random.Shared.Next(1, 7);
                var die2 = Random.Shared.Next(1, 7);
                var diceTotal = die1 + die2;
                var total = diceTotal + stat.Modifier;
                var outcome = ResolveOutcome(total);

                await PandoraRepositoryProvider.Logs.AppendRollLogAsync(
                    characterId: null,
                    characterDisplayName: hunter.CharacterName,
                    statName: stat.Code,
                    dice1: die1,
                    dice2: die2,
                    modifier: stat.Modifier,
                    total: total,
                    resultTier: outcome.Label);

                var embed = new EmbedBuilder()
                    .WithTitle("PROJECT:PANDORA | 판정 결과")
                    .WithColor(outcome.Color)
                    .WithDescription($"**{hunter.CharacterName}** 님의 **{stat.KoreanName}({stat.Code})** 판정입니다.")
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

        [SlashCommand("해제", "현재 선택된 캐릭터를 해제합니다.")]
        public async Task ClearCurrentCharacter()
        {
            await ClearCurrentCharacterCoreAsync();
        }

        [SlashCommand("헤제", "해제 명령어의 오타 대응용 별칭입니다.")]
        public async Task ClearCurrentCharacterTypoAlias()
        {
            await ClearCurrentCharacterCoreAsync();
        }

        [SlashCommand("삭제", "등록된 캐릭터 정보를 삭제합니다.")]
        public async Task DeleteCharacter(
            [Summary("캐릭터", "등록된 캐릭터 이름")] string characterName,
            [Summary("확인", "True로 설정하면 캐릭터를 삭제합니다.")] bool confirm = false)
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
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("목록", "내가 등록한 캐릭터 목록을 확인합니다.")]
        public async Task ListCharacters()
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var characters = await GoogleSheetService.Instance.ListCharactersAsync(Context.User.Id.ToString());
                if (characters.Count == 0)
                {
                    await FollowupAsync("등록된 캐릭터가 없습니다. 먼저 `/등록`을 사용해주세요.", ephemeral: true);
                    return;
                }

                var builder = new StringBuilder();
                builder.AppendLine("```text");
                builder.AppendLine("PROJECT:PANDORA / MY CHARACTERS");
                builder.AppendLine("--------------------------------");

                foreach (var character in characters)
                {
                    var marker = character.IsSelected ? "*" : " ";
                    builder.AppendLine($"{marker} {character.CharacterName}  HP {character.CurrentHp}/{character.MaxHp}  status:{character.ReviewStatus}  row:{character.RowNumber}");
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

        [SlashCommand("정보", "선택된 캐릭터의 상태창 이미지를 표시합니다.")]
        public async Task ShowCharacterInfo(
            [Summary("캐릭터", "선택 사항: 등록된 캐릭터 이름")] string? characterName = null)
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
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
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
                await FollowupAsync(ToFriendlyCharacterError(ex.Message), ephemeral: true);
            }
        }

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
        {
            return new StatInfo(code, koreanName, value, hunter.GetModifier(value));
        }

        private static JudgementOutcome ResolveOutcome(int total)
        {
            if (total >= 10)
            {
                return new JudgementOutcome("성공", "**성공** - 의도한 행동을 안정적으로 해냅니다.", Color.Green);
            }

            if (total >= 7)
            {
                return new JudgementOutcome("부분 성공", "**부분 성공** - 성공하지만 대가, 선택, 위험이 따라붙습니다.", Color.Gold);
            }

            return new JudgementOutcome("실패", "**실패** - 진행자가 실패에 따른 상황 변화를 제시합니다.", Color.Red);
        }

        private static string FormatModifier(int modifier)
        {
            return modifier >= 0 ? $"+{modifier}" : modifier.ToString();
        }

        private static string ToFriendlyCharacterError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "처리 중 문제가 발생했습니다. 잠시 후 다시 시도해 주세요.";
            }

            if (message.Contains("등록된 캐릭터가 없습니다", StringComparison.OrdinalIgnoreCase))
            {
                return "등록된 캐릭터가 없습니다. 먼저 `/등록`으로 원본 시트를 등록해 주세요.";
            }

            if (message.Contains("선택된 캐릭터가 없습니다", StringComparison.OrdinalIgnoreCase))
            {
                return "현재 선택된 캐릭터가 없습니다. `/목록`으로 보유 캐릭터를 확인한 뒤 `/선택`으로 사용할 캐릭터를 골라 주세요.";
            }

            if (message.Contains("approved", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("승인", StringComparison.OrdinalIgnoreCase))
            {
                return $"{message}\n필요하면 `/검수상태`로 승인 여부를 먼저 확인해 주세요.";
            }

            if (message.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                return "요청이 너무 빠르게 들어가 잠시 대기 중입니다. 몇 초 후 다시 시도해 주세요.";
            }

            return $"처리 중 문제가 발생했습니다: {message}";
        }

        private sealed record StatInfo(string Code, string KoreanName, int Value, int Modifier);

        private sealed record JudgementOutcome(string Label, string Text, Color Color);
    }
}
