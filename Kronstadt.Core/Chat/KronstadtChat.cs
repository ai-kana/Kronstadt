using Microsoft.Extensions.Logging;
using SDG.Unturned;
using UnityEngine;
using Kronstadt.Core.Formatting;
using Kronstadt.Core.Players;
using Kronstadt.Core.Logging;
using Kronstadt.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Kronstadt.Core.Commands.Framework;
using Kronstadt.Core.Roles;
using Kronstadt.Core.Translations;

namespace Kronstadt.Core.Chat;

public class KronstadtChat
{
    private static readonly ILogger _Logger;
    private static float _LocalChatDistance;

    static KronstadtChat()
    {
        _Logger = LoggerProvider.CreateLogger<KronstadtChat>();
        ChatManager.onChatted += OnChatted;

        OnConfigurationReloaded();
        ConfigurationEvents.OnConfigurationReloaded += OnConfigurationReloaded;
    }

    private static void OnConfigurationReloaded()
    {
        float distance = KronstadtHost.Configuration.GetValue<float>("LocalChatDistance");
        _LocalChatDistance = distance * distance;
    }

    private static void SendLocal(KronstadtPlayer sender, string text)
    {
        string message = $"[{Formatter.RedColor.ColorText("L")}] {sender.Name}: {text}";
        _Logger.LogInformation($"[Local] {sender.LogName}: {text}");

        foreach (KronstadtPlayer player in KronstadtPlayerManager.Players.Values)
        {
            if (Vector3.SqrMagnitude(player.Movement.Position - sender.Movement.Position) > _LocalChatDistance)
            {
                continue;
            }

            ChatManager.serverSendMessage(message, Color.white, sender.SteamPlayer, player.SteamPlayer, EChatMode.GROUP, null, true);
        }
    }

    private static void SendGroup(KronstadtPlayer sender, string text)
    {
        string message = $"[{Formatter.RedColor.ColorText("G")}] {sender.Name}: {text}";
        _Logger.LogInformation($"[Group] {sender.LogName}: {text}");
        foreach (KronstadtPlayer player in KronstadtPlayerManager.Players.Values)
        {
            if (!player.SteamPlayer.isMemberOfSameGroupAs(sender.SteamPlayer))
            {
                continue;
            }

            ChatManager.serverSendMessage(message, Color.white, sender.SteamPlayer, player.SteamPlayer, EChatMode.GROUP, null, true);
        }
    }

    private static void SendGlobal(KronstadtPlayer sender, string text)
    {
        string message = $"{GetChatTag(sender)}{sender.Name}: {text}";
        _Logger.LogInformation($"{sender.LogName}: {text}");
        foreach (KronstadtPlayer player in KronstadtPlayerManager.Players.Values)
        {
            ChatManager.serverSendMessage(message, Color.white, sender.SteamPlayer, player.SteamPlayer, EChatMode.GROUP, null, true);
        }
    }

    private static string GetChatTag(KronstadtPlayer player)
    {
        IEnumerable<string> roles = RoleManager.GetRoles(player.Roles.Roles)
            .Where(x => x.DutyOnly ? player.Administration.OnDuty : true && x.ChatTag != string.Empty)
            .Select(x => x.ChatTag);

        if (roles.Count() == 0)
        {
            return "";
        }

        return $"[{Formatter.FormatList(roles, " | ")}] ";
    }

    private static void OnChatted(SteamPlayer steamPlayer, EChatMode mode, ref Color chatted, ref bool isRich, string text, ref bool isVisible)
    {
        isVisible = false;

        KronstadtPlayer player = KronstadtPlayerManager.Players[steamPlayer.playerID.steamID];

        if (text.StartsWith("/"))
        {
            CommandManager.ExecuteCommand(text, player);
            return;
        }

        if (player.Moderation.IsMuted && mode != EChatMode.GROUP)
        {
            return;
        }

        string message = text.Replace("<", "< ");

        switch (mode)
        {
            case EChatMode.SAY:
            case EChatMode.GLOBAL:
            case EChatMode.WELCOME:
                SendGlobal(player, message);
                return;
            case EChatMode.GROUP:
                SendGroup(player, message);
                return;
            case EChatMode.LOCAL:
                SendLocal(player, message);
                return;
        }
    }

    public static void BroadcastMessage(KronstadtPlayer player, string message, params object[] args)
    {
        Broadcast(player, Formatter.Format(message, args));
    }

    public static void BroadcastMessage(IEnumerable<KronstadtPlayer> players, string message, params object[] args)
    {
        Broadcast(players, Formatter.Format(message, args));
    }

    public static void BroadcastMessage(string message, params object[] args)
    {
        IEnumerable<KronstadtPlayer> players = KronstadtPlayerManager.Players.Values;
        Broadcast(players, Formatter.Format(message, args));
    }

    public static void BroadcastMessage(KronstadtPlayer player, Translation translation, params object[] args)
    {
        Broadcast(player, translation, args);
    }

    public static void BroadcastMessage(IEnumerable<KronstadtPlayer> players, Translation translation, params object[] args)
    {
        Broadcast(players, translation, args);
    }

    public static void BroadcastMessage(Translation translation, params object[] args)
    {
        IEnumerable<KronstadtPlayer> players = KronstadtPlayerManager.Players.Values;
        Broadcast(players, translation, args);
    }

    private static void Broadcast(IEnumerable<KronstadtPlayer> players, Translation translation, params object[] args)
    {
        foreach (KronstadtPlayer player in players)
        {
            Broadcast(player, translation, args);
        }
    }

    private static void Broadcast(KronstadtPlayer player, Translation translation, params object[] args)
    {
        _Logger.LogInformation(translation.Translate(player, args));
        string message = translation.Translate(player, args);
        ChatManager.serverSendMessage("<b>" + message, Color.white, null, player.SteamPlayer, EChatMode.GLOBAL, Formatter.ChatIconUrl, true);
    }

    private static void Broadcast(IEnumerable<KronstadtPlayer> players, string message)
    {
        foreach (KronstadtPlayer player in players)
        {
            Broadcast(player, message);
        }
    }

    private static void Broadcast(KronstadtPlayer player, string message)
    {
        _Logger.LogInformation(message);
        ChatManager.serverSendMessage("<b>" + message, Color.white, null, player.SteamPlayer, EChatMode.GLOBAL, Formatter.ChatIconUrl, true);
    }
    
    public static void SendPrivateMessage(KronstadtPlayer sender, KronstadtPlayer receiver, string text)
    {
        text = text.Replace("<", "< ");
        string message = $"[{Formatter.RedColor.ColorText("PM")}] {sender.Name} -> {receiver.Name}: {text}";
        _Logger.LogInformation(message);

        ChatManager.serverSendMessage(message, Color.white, sender.SteamPlayer, receiver.SteamPlayer, useRichTextFormatting:true);
        ChatManager.serverSendMessage(message, Color.white, sender.SteamPlayer, sender.SteamPlayer, useRichTextFormatting:true);
    }
}