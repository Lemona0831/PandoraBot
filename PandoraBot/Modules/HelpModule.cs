using Discord;
using Discord.Interactions;

namespace PandoraBot.Modules;

public class HelpModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("도움말", "PROJECT:PANDORA 명령 사용법을 확인합니다.")]
    public async Task ShowHelp(
        [Summary("주제", "선택 사항: 플레이어, 판정, 관리자")] string? topic = null)
    {
        var normalized = NormalizeTopic(topic);
        var embed = normalized switch
        {
            "판정" => BuildRollHelp(),
            "관리자" => BuildAdminHelp(),
            _ => BuildPlayerHelp()
        };

        await RespondAsync(embed: embed, ephemeral: true);
    }

    private static string NormalizeTopic(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return "플레이어";
        }

        return topic.Trim().ToLowerInvariant() switch
        {
            "판정" or "roll" or "check" => "판정",
            "관리자" or "admin" or "gm" or "운영" => "관리자",
            _ => "플레이어"
        };
    }

    private static Embed BuildPlayerHelp()
    {
        return new EmbedBuilder()
            .WithTitle("PANDORA 도움말 | 플레이어")
            .WithColor(new Color(60, 190, 255))
            .WithDescription("처음 들어온 플레이어라면 아래 순서대로 따라가면 가장 편합니다.")
            .AddField(
                "처음 사용할 때",
                "1. `/등록`으로 원본 Google Sheet를 불러옵니다.\n" +
                "2. `/검수상태`로 승인 진행 상태를 확인합니다.\n" +
                "3. 승인된 캐릭터는 `/선택`으로 골라 둡니다.\n" +
                "4. `/현재` 또는 `/정보`로 지금 쓸 캐릭터 상태를 확인합니다.\n" +
                "5. 장면 중 판정이 필요하면 `/판정`을 사용합니다.",
                inline: false)
            .AddField(
                "나만 보는 명령",
                "`/등록`, `/갱신`, `/검수상태`, `/내정보`, `/목록`, `/선택`, `/현재`, `/해제`, `/삭제`\n" +
                "등록 정보나 선택 상태처럼 개인 확인이 필요한 내용은 비공개로 보여 줍니다.",
                inline: false)
            .AddField(
                "채널에 보여도 되는 명령",
                "`/정보` - 상태창 이미지를 채널에 공유합니다.\n" +
                "`/판정` - 2d6 판정 결과를 채널에 보여 줍니다.\n" +
                "장면 진행 중 다른 사람도 같이 봐야 하는 정보일 때 쓰면 좋습니다.",
                inline: false)
            .AddField(
                "자주 막히는 지점",
                "캐릭터가 안 보이면 먼저 `/검수상태`를 확인해 주세요.\n" +
                "선택한 캐릭터가 없다고 나오면 `/선택`으로 다시 골라 주세요.\n" +
                "등록이 처음이라면 `/등록`에 원본 시트 URL을 넣는 방식이 가장 안전합니다.",
                inline: false)
            .WithFooter("다른 주제가 필요하면 /도움말 주제:판정 또는 /도움말 주제:관리자 를 사용해 주세요.")
            .Build();
    }

    private static Embed BuildRollHelp()
    {
        return new EmbedBuilder()
            .WithTitle("PANDORA 도움말 | 판정")
            .WithColor(new Color(86, 220, 255))
            .WithDescription("판정은 Apocalypse Engine 계열 기준으로 굴리며, 결과는 로그에도 남습니다.")
            .AddField(
                "사용 방법",
                "`/판정 능력치:근력`\n" +
                "사용 가능한 능력치는 `근력`, `민첩`, `체력`, `지능`, `지혜`, `매력`입니다.",
                inline: false)
            .AddField("공식", "`2d6 + 능력치 수정치`", inline: true)
            .AddField("결과 구간", "`10+` 성공\n`7-9` 부분 성공\n`6 이하` 실패", inline: true)
            .AddField(
                "수정치 기준",
                "`18` +3 / `16~17` +2 / `13~15` +1 / `9~12` 0 / `6~8` -1 / `4~5` -2 / `3 이하` -3",
                inline: false)
            .AddField(
                "사용 전 확인하면 좋은 것",
                "지금 어떤 캐릭터로 굴리는지 헷갈리면 먼저 `/현재`를 확인해 주세요.\n" +
                "상태창 이미지가 필요하면 `/정보`를 함께 쓰면 장면 정리에 도움이 됩니다.",
                inline: false)
            .AddField(
                "기록",
                "플레이어 판정은 자동으로 로그에 남습니다.\n" +
                "진행자는 `/로그 로그대상:판정`으로 최근 기록을 다시 확인할 수 있습니다.",
                inline: false)
            .Build();
    }

    private static Embed BuildAdminHelp()
    {
        return new EmbedBuilder()
            .WithTitle("PANDORA 도움말 | 관리자")
            .WithColor(new Color(70, 160, 255))
            .WithDescription("진행자는 조회, 운영, 전투, 로그를 나눠서 쓰면 훨씬 덜 헷갈립니다.")
            .AddField(
                "캐릭터 운영",
                "`/관리목록`, `/정보조회`, `/체력설정`\n" +
                "`/피해`, `/회복`, `/관리선택해제`, `/관리삭제`\n" +
                "`/관리검수목록`, `/관리승인`, `/관리반려`",
                inline: false)
            .AddField(
                "로그와 공지",
                "`/로그` - 판정, 관리, 공지, 전투 로그를 한 번에 확인합니다.\n" +
                "`/공지` - 공지를 채널에 발송하고 기록도 함께 남깁니다.",
                inline: false)
            .AddField(
                "에너미와 드롭",
                "`/에너미목록`, `/에너미조회`, `/에너미판정`\n" +
                "`/에너미추가`, `/에너미수정`, `/에너미능력치`, `/에너미비활성화`, `/에너미활성화`\n" +
                "`/드롭`, `/드롭테스트`, `/드롭추가`, `/드롭목록`, `/드롭수정`, `/드롭삭제`, `/드롭설정`, `/드롭설정보기`",
                inline: false)
            .AddField(
                "전투 세션 운영",
                "`/전투시작`, `/전투종료`, `/전투참가`, `/에너미소환`\n" +
                "`/전투상태`, `/전투피해`, `/전투회복`, `/전투퇴장`, `/전투정리`, `/전투보조`\n" +
                "세션 단위로 관리하지만, 턴/라운드/행동완료/이니셔티브는 1.0 범위에 포함되지 않습니다.",
                inline: false)
            .AddField(
                "운영 팁",
                "전투를 열기 전에는 `/전투보조`로 흐름을 먼저 확인해 두면 실수가 줄어듭니다.\n" +
                "세션이 이미 열려 있을 때는 `/전투상태`로 확인하고, 끝났다면 `/전투종료`로 정리해 주세요.",
                inline: false)
            .AddField(
                "저장 구조",
                "캐릭터 원본 갱신은 Google Sheet를 읽어 오고,\n" +
                "그 이후의 조회, 운영, 로그, 전투는 PostgreSQL 기준으로 동작합니다.",
                inline: false)
            .Build();
    }
}
