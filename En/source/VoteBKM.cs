using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;


namespace VoteBanPlugin;

public class VoteBanPlugin : BasePlugin
{
    private Dictionary<string, int> _voteCounts = new Dictionary<string, int>();
    private bool _isVoteActionActive = false;
    private VoteBanConfig? _config;
    private Dictionary<string, HashSet<int>> _playerVotes = new Dictionary<string, HashSet<int>>(); // Игроки, проголосовавшие за каждого кандидата
    private Dictionary<int, string> _votedPlayers = new Dictionary<int, string>(); // Игроки, которые уже проголосовали
    private BannedPlayersConfig _bannedPlayersConfig;
    private string _bannedPlayersConfigFilePath;
    
    
    
    

    private const string PluginAuthor = "DoctorishHD";
    public override string ModuleName => "VoteBKM";
    public override string ModuleVersion => "1.0";
    public VoteBanConfig Config { get; set; }

    public override void Load(bool hotReload)
    {
        _bannedPlayersConfigFilePath = Path.Combine(ModuleDirectory, "BannedPlayersConfig.json");
       

        RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
        {
            HandlePlayerSpawnEvent(@event);
            return HookResult.Continue;
        });
        
        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            UnbanExpiredPlayers(); // Проверяем и удаляем истекшие баны
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerInfo>((@event, info) => 
        {
            HandlePlayerInfoEvent(@event);
            return HookResult.Continue;
        });
        
        LoadConfig();
        LoadBannedPlayersConfig();
        AddCommand("voteban", "Initiate voteban process", (player, command) => CommandVote(player, command, ExecuteBan));
        AddCommand("votemute", "Initiate votemute process", (player, command) => CommandVote(player, command, ExecuteMute));
        AddCommand("votekick", "Initiate votekick process", (player, command) => CommandVote(player, command, ExecuteKick));
        AddCommand("votereset", "Reset the voting process", CommandVoteReset);

        
        
    }

    private void HandlePlayerInfoEvent(EventPlayerInfo @event)
    {
        // Здесь вы можете получить информацию об игроке и сохранить её для дальнейшего использования
        string playerName = @event.Name;
        ulong steamId = @event.Steamid;
        int userId = @event.Userid.UserId.Value;

        // Логика кика, если это необходимо
        // Пример: if(shouldKick(playerName)) ExecuteKick(playerName);
    }

    private void HandlePlayerConnectFullEvent(EventPlayerConnectFull @event)
    {
        try
        {
            UnbanExpiredPlayers(); // Проверяем и удаляем истекшие баны

            var userId = (int)(@event?.Userid?.Handle ?? -1);
            if (userId != -1)
            {
                var player = Utilities.GetPlayerFromUserid(userId);
                if (player != null && player.IsValid)
                {
                    string steamId = player.SteamID.ToString();
                    if (_bannedPlayersConfig.BannedPlayers.TryGetValue(steamId, out var bannedPlayerInfo))
                    {
                        // Теперь переменная bannedPlayerInfo доступна и содержит информацию о бане
                        // Вы можете использовать ее для дальнейшей логики
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VoteBanPlugin] Error in HandlePlayerConnectFullEvent: {ex.Message}");
        }
    }



    private void CheckAndReloadConfig()
    {
        string configFilePath = Path.Combine(ModuleDirectory, "voteban_config.json");
        if (!File.Exists(configFilePath) || Config == null)
        {
            LoadConfig();
        }
        // Здесь можно добавить дополнительные проверки и обновления конфигурации
    }

    private void HandlePlayerSpawnEvent(EventPlayerSpawn @event)
    {
        try
        {
            if (@event.Userid != null && @event.Userid.IsValid)
            {
                string steamId = @event.Userid.SteamID.ToString();
                if (IsPlayerBanned(steamId))
                {
                    if (_bannedPlayersConfig.BannedPlayers.TryGetValue(steamId, out var bannedPlayerInfo))
                    {
                        var banEndTime = DateTimeOffset.FromUnixTimeSeconds(bannedPlayerInfo.BanEndTime).UtcDateTime;
                        banEndTime = ConvertToMoscowTime(banEndTime);
                        var currentTime = ConvertToMoscowTime(DateTime.UtcNow);

                        if (currentTime < banEndTime)
                        {
                            Console.WriteLine($"[VoteBanPlugin] Banned player {@event.Userid.PlayerName} (SteamID: {steamId}) is being kicked.");
                            Server.ExecuteCommand($"kickid {@event.Userid.UserId.Value}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VoteBanPlugin] Error in HandlePlayerSpawnEvent: {ex.Message}");
        }
    }






    public void OnConfigParsed(VoteBanConfig config)
        {
            // Выполните проверку и настройку конфигурации здесь, если необходимо
            if (config.BanDuration > 600)
            {
                config.BanDuration = 600;
            }

            if (config.MinimumPlayersToStartVote < 2)
            {
                config.MinimumPlayersToStartVote = 2;
            }

            // Устанавливаем загруженную и проверенную конфигурацию
            Config = config;
        }


        
   private void LoadConfig()
    {
        string configFilePath = Path.Combine(ModuleDirectory, "voteban_config.json");
        if (!File.Exists(configFilePath))
        {
            Config = new VoteBanConfig();
            string jsonConfig = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configFilePath, jsonConfig);
        }
        else
        {
            Config = JsonSerializer.Deserialize<VoteBanConfig>(File.ReadAllText(configFilePath)) ?? new VoteBanConfig();
        }
        // Здесь можно добавить дополнительные настройки Config
    }



    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        try
        {
            // Используйте приведение типа для конвертации nint в int
            int userId = (int)@event.Userid.Handle;
            var player = Utilities.GetPlayerFromUserid(userId);
            if (player != null)
            {
                string disconnectedPlayerName = player.PlayerName;
                if (disconnectedPlayerName != null && _playerVotes.ContainsKey(disconnectedPlayerName))
                {
                    _playerVotes.Remove(disconnectedPlayerName);

                    foreach (var voteEntry in _votedPlayers.ToList().Where(entry => entry.Value == disconnectedPlayerName))
                    {
                        _votedPlayers.Remove(voteEntry.Key);
                    }

                    Server.PrintToChatAll($"[VoteBKM] The vote for {disconnectedPlayerName} was canceled because the player left the server.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling player disconnect: {ex.Message}");
        }
        return HookResult.Continue;
    }



    private void ResetVotingProcess()
    {
        _isVoteActionActive = false;
        _voteCounts.Clear();
        _playerVotes.Clear();
        _votedPlayers.Clear();
    }

    private void CommandVoteReset(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !IsAdminWithFlag(player, "@css/votebkm")) // Замените "admin_flag" на флаг, необходимый для использования команды
        {
            player?.PrintToChat("[VoteBKM] You do not have permission to use this command.");
            return;
        }

        ResetVotingProcess();
        player.PrintToChat("[VoteBKM] The voting process has been reset.");
    }

    

    private bool IsAdminWithFlag(CCSPlayerController? player, string? flag)
    {
        if (player == null || flag == null) return false;
        return AdminManager.PlayerHasPermissions(player, flag);
    }

   private void CommandVote(CCSPlayerController? player, CommandInfo commandInfo, Action<string> executeAction)
    {
        if (player == null)
        {
            Console.WriteLine("[VoteBanPlugin] Error: The player's value is zero.");
            return;
        }

        if (Config == null)
        {
            Console.WriteLine("[VoteBanPlugin] Error: Config value is null.");
            return;
        }

        // Получаем список активных игроков, исключая тех, кто вышел (UserId 65535)
        var activePlayers = Utilities.GetPlayers().Where(p => p.UserId.Value != 65535).ToList();
        if (activePlayers.Count < Config.MinimumPlayersToStartVote)
        {
            player.PrintToChat($"[VoteBKM] There are not enough active players on the server to start voting.");
            return;
        }

        // Подсчёт необходимого количества голосов на основе количества активных игроков
        int requiredVotes = (int)Math.Ceiling(activePlayers.Count * Config.RequiredMajority);

        // Проверка, достаточно ли активных игроков для начала голосования
        if (activePlayers.Count >= requiredVotes)
        {
            ShowVoteMenu(player, executeAction);
        }
        else
        {
            player.PrintToChat($"[VoteBKM] Для выполнения действия требуется как минимум {requiredVotes} голосов.");
        }
    }





    private void ShowVoteMenu(CCSPlayerController player, Action<string> executeAction)
    {
        var voteMenu = new ChatMenu("Vote for the player");
        foreach (var p in Utilities.GetPlayers())
        {
            if (IsAdminWithFlag(p, "@css/votebkm") || p.UserId.Value == 65535)
                continue;

            string playerName = p.PlayerName;
            voteMenu.AddMenuOption(playerName, (voter, option) => HandleVote(voter, playerName, executeAction));
        }
        ChatMenus.OpenMenu(player, voteMenu);
    }

    private void HandleVote(CCSPlayerController voter, string targetPlayerName, Action<string> executeAction)
    {
        if (voter == null || string.IsNullOrEmpty(targetPlayerName) || executeAction == null)
        {
            Console.WriteLine("[VoteBanPlugin] Error: The value of voter, targetPlayerName, or ExecuteAction is null.");
            return;
        }

        // Игнорирование голосов от игроков с UserId 65535
        if (voter.UserId.Value == 65535)
        {
            Console.WriteLine("[VoteBanPlugin] Ignoring the vote of a player with user ID 65535.");
            return;
        }

        int voterUserId = voter.UserId.Value;

        // Проверка, голосовал ли уже игрок
        if (_votedPlayers.TryGetValue(voterUserId, out var previousVote))
        {
            if (previousVote != targetPlayerName && _playerVotes.ContainsKey(previousVote))
            {
                _playerVotes[previousVote].Remove(voterUserId);
            }
        }

        // Добавление или обновление записи о голосе
        if (!_playerVotes.ContainsKey(targetPlayerName))
        {
            _playerVotes[targetPlayerName] = new HashSet<int>();
        }

        _playerVotes[targetPlayerName].Add(voterUserId);
        _votedPlayers[voterUserId] = targetPlayerName;

        // Подсчет активных голосов без учета UserId 65535
        var activeVotes = _playerVotes.Where(vote => vote.Value.All(id => Utilities.GetPlayerFromUserid(id)?.UserId.Value != 65535)).ToDictionary(entry => entry.Key, entry => entry.Value);

        int currentVotes = activeVotes.ContainsKey(targetPlayerName) ? activeVotes[targetPlayerName].Count : 0;
        int activePlayersCount = Utilities.GetPlayers().Count(p => p.UserId.Value != 65535);
        int requiredVotes = (int)Math.Ceiling(activePlayersCount * Config.RequiredMajority);

        // Вывод текущего подсчета голосов в чат
        Server.PrintToChatAll($"[VoteBKM] The current count of votes for {targetPlayerName}: {currentVotes}/{requiredVotes}");

        // Проверка, достигнуто ли необходимое количество голосов
        if (currentVotes >= requiredVotes)
        {
            executeAction(targetPlayerName);
            ResetVotingProcess();
        }
    }







    private void StartBanCheckTimer(CCSPlayerController player)
    {
        var timer = new Timer(2.0f, () => BanCheckTimerElapsed(player), TimerFlags.STOP_ON_MAPCHANGE);
    }

     private void BanCheckTimerElapsed(CCSPlayerController player)
    {
        string steamId = player.SteamID.ToString();
        if (IsPlayerBanned(steamId))
        {
            Console.WriteLine($"[VoteBanPlugin] Banned player {player.PlayerName} (SteamID: {steamId})  is being kicked.");
            Server.ExecuteCommand($"kickid {player.UserId}");
        }
    }



    private DateTime ConvertToMoscowTime(DateTime time)
    {
        var moscowZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
        return TimeZoneInfo.ConvertTimeFromUtc(time, moscowZone);
    }



   private void CheckAndKickBannedPlayer(CCSPlayerController player, string steamId)
    {
        LoadBannedPlayersConfig();
        if (_bannedPlayersConfig != null && IsPlayerBanned(steamId))
        {
            var bannedPlayerInfo = _bannedPlayersConfig.BannedPlayers[steamId];
            var banEndTime = DateTimeOffset.Parse(bannedPlayerInfo.BanEndTime.ToString("o"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            var currentTime = ConvertToMoscowTime(DateTime.UtcNow);

            if (currentTime < banEndTime)
            {
                Console.WriteLine($"[VoteBanPlugin] Banned player {player.PlayerName} (SteamID: {steamId}) is being kicked.");
                Server.ExecuteCommand($"kickid {player.UserId.Value}");
            }
        }
    }



    private CCSPlayerController? GetPlayerFromSteamID(string steamId)
    {
        return Utilities.GetPlayers().FirstOrDefault(p => p.SteamID.ToString() == steamId);
    }

    private string? GetSteamIDFromPlayerName(string playerName)
    {
        var player = Utilities.GetPlayers().FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase));
        return player?.SteamID.ToString();
    }

    private CCSPlayerController? GetPlayerFromName(string playerName)
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (player.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase))
            {
                return player;
            }
        }
        return null;
    }


    private void ExecuteBan(string identifier)
    {
        // Проверяем, инициализирован ли Config
        if (Config == null)
        {
            Console.WriteLine("[VoteBanPlugin] Error: Config is null.");
            return;
        }

        // Пытаемся получить игрока
        var player = GetPlayerFromName(identifier) ?? GetPlayerFromSteamID(identifier);

        // Проверяем, получен ли игрок
        if (player == null)
        {
            Console.WriteLine($"[VoteBanPlugin] Error: Player with identifier '{identifier}' not found.");
            return;
        }

        // Проверяем, инициализированы ли UserId и SteamID
        if (!player.UserId.HasValue || string.IsNullOrEmpty(player.SteamID.ToString()))
        {
            Console.WriteLine($"[VoteBanPlugin] Error: Player '{player.PlayerName}' has invalid UserId or SteamID.");
            return;
        }

        // Получаем SteamID игрока
        string steamId = player.SteamID.ToString();

        // Проверяем, не забанен ли уже игрок
        if (IsPlayerBanned(steamId))
        {
            Console.WriteLine($"[VoteBanPlugin] Player {player.PlayerName} (SteamID: {steamId}) is already banned.");
            return;
        }

        // Баним игрока
        BanPlayer(steamId, Config.BanDuration);

        Console.WriteLine($"[VoteBanPlugin] Player {player.PlayerName} (SteamID: {steamId}) has been banned.");
    }



    private void ExecuteMute(string identifier)
    {
        var playerToMute = GetPlayerFromName(identifier) ?? GetPlayerFromSteamID(identifier);
        
        if (playerToMute == null)
        {
            Console.WriteLine($"[VoteBanPlugin] Error: Player with identifier '{identifier}' not found.");
            return;
        }

        if (!playerToMute.UserId.HasValue || string.IsNullOrEmpty(playerToMute.SteamID.ToString()))
        {
            Console.WriteLine($"[VoteBanPlugin] Error: Player '{playerToMute.PlayerName}' has invalid UserId or SteamID.");
            return;
        }

        if (IsPlayerMuted(playerToMute))
        {
            Console.WriteLine($"[VoteBanPlugin] Player {playerToMute.PlayerName} (SteamID: {playerToMute.SteamID}) is already muted.");
            return;
        }

        MutePlayer(playerToMute);

        Console.WriteLine($"[VoteBanPlugin] Player {playerToMute.PlayerName} (SteamID: {playerToMute.SteamID}) has been muted.");
    }

    private void MutePlayer(CCSPlayerController player)
    {
        player.VoiceFlags = VoiceFlags.Muted;
        // Здесь может быть дополнительная логика, если нужно сохранить информацию о муте
    }

    private bool IsPlayerMuted(CCSPlayerController player)
    {
        return player.VoiceFlags.HasFlag(VoiceFlags.Muted);
    }

    private void ExecuteKick(string identifier)
    {
        // Пытаемся найти игрока по имени или SteamID
        var player = GetPlayerFromName(identifier) ?? GetPlayerFromSteamID(identifier);

        if (player != null && player.UserId.HasValue)
        {
            // Выполнение команды кика
            Server.ExecuteCommand($"kickid {player.UserId.Value}");
            Server.PrintToChatAll($"[VoteKick] The player {player.PlayerName} has been deleted from the server.");
        }
        else
        {
            // Если игрок не найден
            Console.WriteLine($"[VoteBanPlugin] The player with the name or SteamID {identifier} was not found.");
        }
    }


    private void LoadBannedPlayersConfig()
    {
        // Проверяем наличие файла конфигурации забаненных игроков
        if (File.Exists(_bannedPlayersConfigFilePath))
        {
            try
            {
                // Чтение JSON из файла
                string json = File.ReadAllText(_bannedPlayersConfigFilePath);

                // Десериализация JSON в объект BannedPlayersConfig
                _bannedPlayersConfig = JsonSerializer.Deserialize<BannedPlayersConfig>(json);

                // Если десериализация возвращает null, создаём новый экземпляр конфигурации
                if (_bannedPlayersConfig == null)
                {
                    _bannedPlayersConfig = new BannedPlayersConfig();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading banned players config: {ex.Message}");
                // В случае ошибки чтения файла создаём новый экземпляр конфигурации
                _bannedPlayersConfig = new BannedPlayersConfig();
            }
        }
        else
        {
            // Если файл конфигурации не существует, создаём новый экземпляр конфигурации
            _bannedPlayersConfig = new BannedPlayersConfig();
        }
    }


    private void SaveBannedPlayersConfig()
    {
        string json = JsonSerializer.Serialize(_bannedPlayersConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_bannedPlayersConfigFilePath, json);
    }

    private void BanPlayer(string steamId, int durationInSeconds)
    {
        var banEndTime = DateTimeOffset.UtcNow.AddSeconds(durationInSeconds).ToUnixTimeSeconds();
        string? nickname = GetPlayerFromSteamID(steamId)?.PlayerName;

        var bannedPlayerInfo = new BannedPlayerInfo
        {
            BanEndTime = banEndTime,
            Nickname = nickname
        };

        _bannedPlayersConfig.BannedPlayers[steamId] = bannedPlayerInfo;
        SaveBannedPlayersConfig();

        var player = GetPlayerFromSteamID(steamId);
        if (player != null && player.UserId.HasValue)
        {
            Server.ExecuteCommand($"kickid {player.UserId.Value}");
        }
    }

    private DateTime ConvertFromUnixTimestamp(long timestamp)
    {
        var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        return dateTimeOffset.LocalDateTime;
    }

    private bool IsPlayerBanned(string steamId)
    {
        if (_bannedPlayersConfig.BannedPlayers.TryGetValue(steamId, out var bannedPlayerInfo))
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() < bannedPlayerInfo.BanEndTime;
        }
        return false;
    }

     private void UnbanExpiredPlayers()
    {
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiredBans = _bannedPlayersConfig.BannedPlayers
            .Where(kvp => kvp.Value.BanEndTime < currentTime)
            .ToList();

        foreach (var kvp in expiredBans)
        {
            var steamId = kvp.Key;
            var bannedPlayerInfo = kvp.Value; // Теперь bannedPlayerInfo объявлена в этом контексте

            // Здесь вы можете использовать bannedPlayerInfo для дальнейшей логики
            // Например, для вывода информации о истекшем бане или его удаления

            _bannedPlayersConfig.BannedPlayers.Remove(steamId);
        }

        // Проверка на наличие удаленных банов и сохранение конфигурации
        if (expiredBans.Any())
        {
            SaveBannedPlayersConfig();
        }
    }


    public class BannedPlayersConfig
    {
        public Dictionary<string, BannedPlayerInfo> BannedPlayers { get; set; } = new Dictionary<string, BannedPlayerInfo>();
    }

    public class BannedPlayerInfo
    {
        public long BanEndTime { get; set; } // Изменено на long
        public string Nickname { get; set; }
        public string SteamID { get; set; }
    }

    public class VoteBanConfig : BasePluginConfig
    {
        [JsonPropertyName("BanDuration")]
        public int BanDuration { get; set; } = 3600;

        [JsonPropertyName("RequiredMajority")]
        public double RequiredMajority { get; set; } = 0.5;

        [JsonPropertyName("MinimumPlayersToStartVote")]
        public int MinimumPlayersToStartVote { get; set; } = 4;
    }

}
 