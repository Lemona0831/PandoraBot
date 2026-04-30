using Discord;
using Discord.Interactions;

namespace PandoraBot.Modules
{
    public class HelpModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("도움말", "PROJECT:PANDORA 봇 사용법을 확인합니다.")]
        public async Task ShowHelp(
            [Summary("주제", "선택 사항: 플레이어, 판정, 관리자")] string? topic = null)
        {
            var normalized = topic?.Trim().ToLowerInvariant() ?? "";
            var embed = normalized switch
            {
                "판정" or "roll" => BuildRollHelp(),
                "관리자" or "admin" or "진행자" => BuildAdminHelp(),
                _ => BuildPlayerHelp()
            };

            await RespondAsync(embed: embed, ephemeral: true);
        }

        private static Embed BuildPlayerHelp()
        {
            return new EmbedBuilder()
                .WithTitle("HUNTER GUIDE | 플레이어 도움말")
                .WithColor(new Color(60, 190, 255))
                .WithDescription("캐릭터를 등록하고 진행자 승인을 받은 뒤, 선택/상태창/판정을 사용할 수 있습니다.")
                .AddField("/등록", "캐릭터 시트 이름 또는 Google Sheets URL로 캐릭터를 등록합니다. 신규 캐릭터는 검수 대기 상태가 됩니다.")
                .AddField("/선택", "승인된 내 캐릭터 중 현재 사용할 캐릭터를 선택합니다.")
                .AddField("/현재", "현재 선택된 캐릭터의 HP와 능력치를 확인합니다.")
                .AddField("/정보", "선택 캐릭터의 HUNTER LICENSE 상태창 이미지를 출력합니다.")
                .AddField("/판정", "선택 캐릭터로 `2d6 + 능력치 수정치` 판정을 굴리고, 판정 로그에 기록합니다.")
                .AddField("/목록", "내가 등록한 캐릭터 목록과 검수 상태를 확인합니다.")
                .AddField("/해제 / /삭제", "선택 해제 또는 등록 캐릭터 삭제가 필요할 때 사용합니다.")
                .WithFooter("/도움말 주제:판정 또는 /도움말 주제:관리자 로 세부 도움말을 볼 수 있습니다.")
                .Build();
        }

        private static Embed BuildRollHelp()
        {
            return new EmbedBuilder()
                .WithTitle("PANDORA CHECK | 판정 도움말")
                .WithColor(new Color(86, 220, 255))
                .WithDescription("판정은 Apocalypse Engine / Dungeon World 계열 공식을 사용합니다.")
                .AddField("사용법", "`/판정 능력치:근력`\n능력치는 `근력`, `민첩`, `체력`, `지능`, `지혜`, `매력` 중 하나입니다.")
                .AddField("공식", "`2d6 + 능력치 수정치`")
                .AddField("결과", "`10+` 성공\n`7-9` 부분 성공\n`6-` 실패")
                .AddField("기록", "모든 판정은 `판정 로그` 시트에 자동 기록됩니다.")
                .AddField("수정치 표", "`18 +3` / `16-17 +2` / `13-15 +1` / `9-12 0` / `6-8 -1` / `4-5 -2` / `3 이하 -3`")
                .Build();
        }

        private static Embed BuildAdminHelp()
        {
            return new EmbedBuilder()
                .WithTitle("OPERATOR GUIDE | 관리자 도움말")
                .WithColor(new Color(70, 160, 255))
                .WithDescription("관리자 명령어는 서버 관리 권한을 가진 진행자용입니다.")
                .AddField("/관리목록 / /관리정보", "등록 캐릭터 목록과 상세 정보를 조회합니다.")
                .AddField("/피해 / /회복 / /관리체력", "캐릭터 HP를 빠르게 조정하고 관리 로그에 기록합니다.")
                .AddField("/관리검수목록 / /관리승인 / /관리반려", "신규 캐릭터 검수 상태를 관리합니다.")
                .AddField("/관리판정로그", "최근 판정 기록을 확인합니다.")
                .AddField("/공지", "공지 템플릿을 발송하고 공지 로그에 기록합니다.")
                .AddField("/관리선택해제 / /관리삭제", "선택 상태 정리와 캐릭터 삭제를 처리합니다.")
                .Build();
        }
    }
}
