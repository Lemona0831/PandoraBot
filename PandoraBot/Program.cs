using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Microsoft.Extensions.Configuration;
using PandoraBot.Services;
using PandoraShared.Data;
using System.Reflection;
using System.Text.Json;

public class Program
{
    private DiscordSocketClient? client;
    private InteractionService? interactions;
    private PandoraDbContext? pandoraDb;
    private BotSettings settings = new();

    public static Task Main(string[] args) => new Program().MainAsync();

    public async Task MainAsync()
    {
        settings = BotSettings.Load();
        var configuration = BuildConfiguration();

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged
        };

        client = new DiscordSocketClient(config);
        interactions = new InteractionService(client.Rest, new InteractionServiceConfig
        {
            LogLevel = LogSeverity.Info
        });

        client.Log += Log;
        interactions.Log += Log;
        client.Ready += Ready;
        client.InteractionCreated += HandleInteractionAsync;

        pandoraDb = CreatePandoraDbContext(configuration);
        var sheetsService = CreateSheetsService();
        GoogleSheetService.Initialize(sheetsService);
        await interactions.AddModulesAsync(Assembly.GetEntryAssembly(), services: null);

        if (string.IsNullOrWhiteSpace(settings.DiscordToken))
        {
            throw new InvalidOperationException("Set DiscordToken in BotSettings.json.");
        }

        await client.LoginAsync(TokenType.Bot, NormalizeDiscordToken(settings.DiscordToken));
        await client.StartAsync();

        await Task.Delay(-1);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private async Task Ready()
    {
        Console.WriteLine($"[SYSTEM] {client?.CurrentUser?.Username} connected.");

        if (interactions != null)
        {
            var guildId = settings.GuildId.GetValueOrDefault();
            if (guildId > 0)
            {
                await interactions.RegisterCommandsToGuildAsync(guildId, deleteMissing: true);
                Console.WriteLine($"[SYSTEM] Slash commands registered to guild {guildId}.");
            }
            else
            {
                await interactions.RegisterCommandsGloballyAsync();
                Console.WriteLine("[SYSTEM] Slash commands registered globally.");
            }
        }
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        var context = new SocketInteractionContext(client, interaction);
        var result = await interactions!.ExecuteCommandAsync(context, services: null);

        if (!result.IsSuccess)
        {
        Console.WriteLine($"[Interaction Error] {result.ErrorReason}");
        }
    }

    private PandoraDbContext? CreatePandoraDbContext(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PandoraDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("[SYSTEM] PandoraDb connection string not configured. Continuing with Google Sheets mode.");
            return null;
        }

        try
        {
            var context = PandoraDbContextFactory.CreateOrNull(connectionString);
            Console.WriteLine("[SYSTEM] PandoraDb scaffold configured from ConnectionStrings:PandoraDb.");
            return context;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SYSTEM] PandoraDb scaffold disabled: {ex.Message}");
            return null;
        }
    }

    private SheetsService CreateSheetsService()
    {
        GoogleCredential credential;

        var credentialPath = settings.GoogleCredentialPath;
        if (string.IsNullOrWhiteSpace(credentialPath))
        {
            credentialPath = File.Exists("Credental.json") ? "Credental.json" : "credental.json";
        }

        credential = GoogleCredential.FromFile(credentialPath)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        var service = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "PandoraBot",
        });

        Console.WriteLine("[SYSTEM] Google Sheets service initialized.");
        return service;
    }

    private static string NormalizeDiscordToken(string token)
    {
        var normalized = token.Trim().Trim('"', '\'', ',', ';');
        const string botPrefix = "Bot ";
        if (normalized.StartsWith(botPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[botPrefix.Length..].Trim();
        }

        return normalized.Trim().Trim('"', '\'', ',', ';');
    }

    private sealed class BotSettings
    {
        public string DiscordToken { get; set; } = "";
        public ulong? GuildId { get; set; }
        public string GoogleCredentialPath { get; set; } = "";

        public static BotSettings Load()
        {
            const string path = "BotSettings.json";
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("BotSettings.json was not found. Create it from BotSettings.example.json.", path);
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<BotSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? new BotSettings();
        }
    }
}
