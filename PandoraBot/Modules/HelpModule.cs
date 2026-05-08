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

        var normalized = topic.Trim().ToLowerInvariant();
        return normalized switch
        {
            "판정" or "roll" or "check" => "판정",
            "관리자" or "admin" or "진행자" or "운영" => "관리자",
            _ => "플레이어"
        };
    }

    private static Embed BuildPlayerHelp()
    {
        return new EmbedBuilder()
            .WithTitle("PANDORA 도움말 | 플레이어")
            .WithColor(new Color(60, 190, 255))
            .WithDescription("처음 들어온 플레이어는 이하의 순서를 따라주세요.")
            .AddField(
                "추천 사용 순서",
                "1. /등록으로 원본 시트를 등록합니다.\n2. /검수상태로 승인 여부를 확인합니다.\n3. 승인된 캐릭터는 /선택으로 골라 둡니다.\n4. /현재 또는 /정보로 상태를 확인합니다.\n5. 판정이 필요하면 /판정을 사용합니다.",
                inline: false)
            .AddField(
                "나만 보는 명령",
                "/등록 원본 시트 등록\n/갱신 등록 정보 다시 읽기\n/검수상태 승인/대기/반려 확인\n/내정보 내 선택 캐릭터와 최근 기록 확인\n/목록 내가 등록한 캐릭터 목록\n/선택 사용할 캐릭터 선택\n/현재 빠른 상태 확인\n/해제 현재 선택 해제\n/삭제 내 캐릭터 등록 정보 삭제",
                inline: false)
            .AddField(
                "채널에 보여도 되는 명령",
                "/정보 선택한 캐릭터 상태창 이미지를 공유합니다.\n/판정 2d6 판정 결과를 채널에 보여 줍니다. 함께 보는 장면에서 쓰기 좋습니다.",
                inline: false)
            .AddField(
                "알아두면 좋아",
                "/등록과 /갱신은 원본 시트 주소와 등록 정보를 다루기 때문에 비공개로 응답합니다.\n반대로 /정보와 /판정은 함께 보는 장면에서 공유하기 좋도록 채널 기준으로 사용해도 자연스럽습니다.",
                inline: false)
            .WithFooter("다른 주제가 필요하면 /도움말 주제:판정 또는 /도움말 주제:관리자 를 사용해 주세요.")
            .Build();
    }

    private static Embed BuildRollHelp()
    {
        return new EmbedBuilder()
            .WithTitle("PANDORA 도움말 | 판정")
            .WithColor(new Color(86, 220, 255))
            .WithDescription("판정은 Apocalypse Engine 계열 기준을 따르며, 결과는 로그에도 남습니다.")
            .AddField(
                "사용 방법",
                "/판정 능력치:근력\n가능한 능력치는 근력, 민첩, 체력, 지능, 지혜, 매력입니다.",
                inline: false)
            .AddField("공식", "2d6 + 능력치 수정치", inline: true)
            .AddField("결과 구간", "10+ 성공\n7-9 부분 성공\n6 이하 실패", inline: true)
            .AddField(
                "수정치 기준",
                "18 +3 / 16~17 +2 / 13~15 +1 / 9~12 0 / 6~8 -1 / 4~5 -2 / 3 이하 -3",
                inline: false)
            .AddField(
                "공개 범위",
                "/판정 결과는 채널에 표시됩니다. 혼자 상태만 확인하고 싶을 때는 먼저 /현재, /내정보로 준비 상태를 확인해 주세요.",
                inline: false)
            .AddField(
                "기록",
                "모든 판정은 판정 로그에 자동 기록됩니다. 진행자는 /로그 로그대상:판정으로 최근 기록을 다시 볼 수 있습니다.",
                inline: false)
            .Build();
    }

    private static Embed BuildAdminHelp()
    {
        return new EmbedBuilder()
            .WithTitle("PANDORA 도움말 | 관리자")
            .WithColor(new Color(70, 160, 255))
            .WithDescription("진행자 명령은 확인용 명령과 채널 알림용 명령을 구분해서 쓰면 훨씬 덜 헷갈립니다.")
            .AddField(
                "비공개로 보는 명령",
                "/관리목록, /관리정보 캐릭터 조회\n/관리검수목록, /관리승인, /관리반려 검수 처리\n/로그 통합 로그 확인\n/에너미목록, /에너미조회, /에너미판정 에너미 확인\n/에너미추가, /에너미수정, /에너미능력치 에너미 관리\n/드롭추가, /드롭테스트 드롭 설정/확률 점검\n/전투시작, /전투종료, /전투참가, /에너미소환, /전투상태, /전투퇴장, /전투정리, /전투보조 전투판 관리",
                inline: false)
            .AddField(
                "채널에 영향을 주는 명령",
                "/공지는 공지를 채널에 발송합니다.\n/피해, /회복은 처리 결과와 함께 대상 플레이어에게 공개 알림을 보냅니다.\n/드롭은 결과를 출력하고, 활성 전투 세션이 있으면 전투 로그에도 남깁니다.",
                inline: false)
            .AddField(
                "운영 팁",
                "상태 확인은 /전투상태, 전투 흐름 점검은 /전투보조, 실전 보상은 /드롭, 확률 점검은 /드롭테스트로 나누면 실수가 줄어듭니다.",
                inline: false)
            .AddField(
                "주의할 점",
                "전투 세션이 없는 상태에서 전투 명령을 쓰면 먼저 /전투시작 안내가 나옵니다.\n에너미/드롭/전투/통합 로그는 이제 PostgreSQL 기준으로 관리하며, 원본 Google Sheets는 캐릭터 등록 원본과 기존 운영 흐름 쪽에만 남아 있습니다.",
                inline: false)
            .Build();
    }
}