using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Admin;

namespace VoteBanPlugin;

public class VoteBanPlugin : BasePlugin
{
    private Dictionary<string, int> _voteCounts = new Dictionary<string, int>();
    private bool _isVoteActionActive = false;
    private VoteBanConfig? _config;
    private Dictionary<string, HashSet<int>> _playerVotes = new Dictionary<string, HashSet<int>>(); // Игроки, проголосовавшие за каждого кандидата
    private Dictionary<int, string> _votedPlayers = new Dictionary<int, string>(); // Игроки, которые уже проголосовали

    private const string PluginAuthor = "DoctorishHD";
    public override string ModuleName => "VoteBKM";
    public override string ModuleVersion => "1.0";

    public override void Load(bool hotReload)
    {
        LoadConfig();
        AddCommand("voteban", "Initiate voteban process", (player, command) => CommandVote(player, command, ExecuteBan));
        AddCommand("votemute", "Initiate votemute process", (player, command) => CommandVote(player, command, ExecuteMute));
        AddCommand("votekick", "Initiate votekick process", (player, command) => CommandVote(player, command, ExecuteKick));
        AddCommand("votereset", "Reset the voting process", CommandVoteReset);
    }

    private void LoadConfig()
    {
        string configFilePath = Path.Combine(ModuleDirectory, "voteban_config.json");
        if (!File.Exists(configFilePath))
        {
            _config = new VoteBanConfig();
            string jsonConfig = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configFilePath, jsonConfig);
        }
        else
        {
            _config = JsonSerializer.Deserialize<VoteBanConfig>(File.ReadAllText(configFilePath)) ?? new VoteBanConfig();
        }
    }

    private void CommandVoteReset(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !IsAdminWithFlag(player, "@css/votebkm")) // Замените "admin_flag" на флаг, необходимый для использования команды
        {
            player?.PrintToChat("[VoteBKM] У вас нет разрешения на использование этой команды.");
            return;
        }

        ResetVotingProcess();
        player.PrintToChat("[VoteBKM] Процесс голосования был сброшен.");
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
            player?.PrintToChat("[VoteBKM] Игрок не найден.");
            return;
        }

        // Убираем проверку _isVoteActionActive, чтобы позволить другим игрокам голосовать
        // Проверка на минимальное количество игроков на сервере
        if (Utilities.GetPlayers().Count < _config.MinimumPlayersToStartVote)
        {
            player.PrintToChat($"[VoteBKM] Для начала голосования требуется как минимум {_config.MinimumPlayersToStartVote} игрока.");
            return;
        }

        ShowVoteMenu(player, executeAction);
    }

    private void ShowVoteMenu(CCSPlayerController player, Action<string> executeAction)
    {
        var voteMenu = new ChatMenu("Голосуйте за игрока");
        foreach (var p in Utilities.GetPlayers())
        {
            if (IsAdminWithFlag(p, "@css/votebkm"))
                continue;

            string playerName = p.PlayerName;
            voteMenu.AddMenuOption(playerName, (voter, option) => HandleVote(voter, playerName, executeAction));
        }
        ChatMenus.OpenMenu(player, voteMenu);
    }

    private void HandleVote(CCSPlayerController voter, string targetPlayerName, Action<string> executeAction)
    {
        // Проверяем, голосовал ли уже игрок
        if (_votedPlayers.TryGetValue(voter.UserId.Value, out var votedFor) && votedFor != targetPlayerName)
        {
            voter.PrintToChat("[VoteBKM] Вы уже проголосовали за другого игрока.");
            return;
        }

        // Добавляем или обновляем запись о голосе
        if (!_playerVotes.ContainsKey(targetPlayerName))
            _playerVotes[targetPlayerName] = new HashSet<int>();

        _playerVotes[targetPlayerName].Add(voter.UserId.Value);
        _votedPlayers[voter.UserId.Value] = targetPlayerName;

        int requiredVotes = (int)(Utilities.GetPlayers().Count * _config.RequiredMajority);
        int currentVotes = _playerVotes[targetPlayerName].Count;

        Server.PrintToChatAll($"[VoteBKM] Текущий счет голосов за {targetPlayerName}: {currentVotes}/{requiredVotes}");

        if (currentVotes >= requiredVotes)
        {
            executeAction(targetPlayerName);
            ResetVotingProcess(); // Сброс после успешного голосования
        }
    }


    private void ResetVotingProcess()
    {
        _playerVotes.Clear();
        _votedPlayers.Clear();
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
        string command;
        if (_config.BanByUserId)
        {
            var player = GetPlayerFromName(identifier);
            if (player != null && player.UserId.HasValue)
            {
                command = string.Format(_config.BanCommand, player.UserId.Value, _config.BanDuration);
            }
            else
            {
                Console.WriteLine($"[Ошибка] Игрок с именем {identifier} не найден или UserId недоступен.");
                return;
            }
        }
        else
        {
            command = string.Format(_config.BanCommand, identifier, _config.BanDuration);
        }

        Server.ExecuteCommand(command);
        Server.PrintToChatAll($"[VoteBan] Игрок {identifier} был забанен на {_config.BanDuration} секунд.");
    }

    private void ExecuteMute(string identifier)
    {
        string command;
        if (_config.MuteByUserId)
        {
            var player = GetPlayerFromName(identifier);
            if (player != null && player.UserId.HasValue)
            {
                command = string.Format(_config.MuteCommand, player.UserId.Value);
            }
            else
            {
                Console.WriteLine($"[Ошибка] Игрок с именем {identifier} не найден или UserId недоступен.");
                return;
            }
        }
        else
        {
            command = string.Format(_config.MuteCommand, identifier);
        }

        Server.ExecuteCommand(command);
        Server.PrintToChatAll($"[VoteMute] Игрок {identifier} был заглушен.");
    }

    private void ExecuteKick(string identifier)
    {
        string command;
        if (_config.KickByUserId)
        {
            var player = GetPlayerFromName(identifier);
            if (player != null && player.UserId.HasValue)
            {
                command = string.Format(_config.KickCommand, player.UserId.Value);
            }
            else
            {
                Console.WriteLine($"[Ошибка] Игрок с именем {identifier} не найден или UserId недоступен.");
                return;
            }
        }
        else
        {
            command = string.Format(_config.KickCommand, identifier);
        }

        Server.ExecuteCommand(command);
        Server.PrintToChatAll($"[VoteKick] Игрок {identifier} был кикнут с сервера.");
    }




    public class VoteBanConfig
    {
        public string BanCommand { get; set; } = "mm_ban #{0} {1} VoteBan";
        public string MuteCommand { get; set; } = "mm_mute #{0} {1} Votemute";
        public string KickCommand { get; set; } = "mm_kick #{0}";
        public int BanDuration { get; set; } = 10;
        public double RequiredMajority { get; set; } = 0.5;
        public bool BanByUserId { get; set; } = true; // Добавляем новую настройку
        public bool MuteByUserId { get; set; } = true;
        public bool KickByUserId { get; set; } = true;
        public int MinimumPlayersToStartVote { get; set; } = 4;
    }
}
 