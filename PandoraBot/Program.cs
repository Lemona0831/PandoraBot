using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Microsoft.Extensions.Configuration;
using PandoraBot.Repositories;
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
        var configuration = BuildConfiguration();
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();

        if (args.Length > 0 && string.Equals(args[0], "import-sheets", StringComparison.OrdinalIgnoreCase))
        {
            settings = BotSettings.Load(optional: true);
            await RunSheetsImportAsync(configuration, args.Skip(1).ToArray());
            return;
        }

        settings = BotSettings.Load();

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
        PandoraRepositoryProvider.Initialize(configuration.GetConnectionString("PandoraDb"));
        await interactions.AddModulesAsync(Assembly.GetEntryAssembly(), services: null);

        if (string.IsNullOrWhiteSpace(settings.DiscordToken))
        {
            throw new InvalidOperationException("Set DiscordToken in BotSettings.json.");
        }

        await client.LoginAsync(TokenType.Bot, NormalizeDiscordToken(settings.DiscordToken));
        await client.StartAsync();

        await Task.Delay(-1);
    }

    private async Task RunSheetsImportAsync(IConfiguration configuration, string[] args)
    {
        var dryRun = !args.Any(arg => string.Equals(arg, "--apply", StringComparison.OrdinalIgnoreCase));
        var sheetsService = CreateSheetsService();
        var spreadsheetId = Environment.GetEnvironmentVariable("PANDORA_SPREADSHEET_ID")
            ?? "13DKG_V3TD5GHxQrVpmFGQhFluPvGc3E_M5FXfdvRkqI";
        var db = dryRun ? null : CreatePandoraDbContext(configuration)
            ?? throw new InvalidOperationException("Set ConnectionStrings:PandoraDb to run import with --apply.");

        var importer = new SheetsToPostgresImporter(sheetsService, spreadsheetId, db);
        var result = await importer.RunAsync(new SheetsImportOptions(dryRun, spreadsheetId));

        Console.WriteLine($"[IMPORT] Mode: {(result.DryRun ? "dry-run" : "apply")}");
        Console.WriteLine($"[IMPORT] Spreadsheet: {result.SpreadsheetId}");
        Console.WriteLine($"[IMPORT] characters={result.CharacterCount}");
        Console.WriteLine($"[IMPORT] character_selections={result.CharacterSelectionCount}");
        Console.WriteLine($"[IMPORT] roll_logs={result.RollLogCount}");
        Console.WriteLine($"[IMPORT] admin_logs={result.AdminLogCount + result.NoticeLogCount} (admin={result.AdminLogCount}, notice={result.NoticeLogCount})");
        Console.WriteLine($"[IMPORT] enemies={result.EnemyCount}");
        Console.WriteLine($"[IMPORT] enemy_drops={result.EnemyDropCount}");
        Console.WriteLine($"[IMPORT] enemy_drop_settings={result.EnemyDropSettingCount}");
        Console.WriteLine($"[IMPORT] combat_sessions={result.CombatSessionCount}");
        Console.WriteLine($"[IMPORT] combat_participants={result.CombatParticipantCount}");
        Console.WriteLine($"[IMPORT] combat_logs={result.CombatLogCount}");

        if (result.MigrationNotes.Count == 0)
        {
            Console.WriteLine("[IMPORT] migration_notes=0");
            return;
        }

        Console.WriteLine($"[IMPORT] migration_notes={result.MigrationNotes.Count}");
        foreach (var note in result.MigrationNotes)
        {
            Console.WriteLine($"[IMPORT][NOTE] {note}");
        }
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
            credentialPath = ResolveLocalFilePath("Credental.json", "credental.json")
                ?? throw new FileNotFoundException("Credental.json was not found. Set GoogleCredentialPath or place the file in the project directory.");
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

        public static BotSettings Load(bool optional = false)
        {
            var path = ResolveLocalFilePath("BotSettings.json");
            if (string.IsNullOrWhiteSpace(path))
            {
                if (optional)
                {
                    return new BotSettings();
                }

                throw new FileNotFoundException("BotSettings.json was not found. Create it from BotSettings.example.json.", "BotSettings.json");
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<BotSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? new BotSettings();

            if (!string.IsNullOrWhiteSpace(settings.GoogleCredentialPath) &&
                !Path.IsPathRooted(settings.GoogleCredentialPath))
            {
                settings.GoogleCredentialPath = Path.GetFullPath(
                    Path.Combine(Path.GetDirectoryName(path)!, settings.GoogleCredentialPath));
            }

            return settings;
        }
    }

    private static string? ResolveLocalFilePath(params string[] candidateFileNames)
    {
        var roots = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "..", "..", ".."),
            Path.Combine(Directory.GetCurrentDirectory(), "PandoraBot"),
            Path.Combine(AppContext.BaseDirectory, "PandoraBot")
        }
        .Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        foreach (var root in roots)
        {
            foreach (var fileName in candidateFileNames)
            {
                var path = Path.Combine(root, fileName);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }
}
