using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SDG.Unturned;
using Steamworks;
using Kronstadt.Core.Chat;
using Kronstadt.Core.Extensions;
using Kronstadt.Core.Formatting;
using Kronstadt.Core.Logging;
using Kronstadt.Core.Offenses;
using Kronstadt.Core.Translations;

namespace Kronstadt.Core.Players;

public delegate void PlayerConnected(KronstadtPlayer player);
public delegate void PlayerDisconnected(KronstadtPlayer player);

public class KronstadtPlayerManager
{
    public static ConcurrentDictionary<CSteamID, KronstadtPlayer> Players {get; private set;}

    public static event PlayerConnected? OnPlayerConnected;
    public static event PlayerDisconnected? OnPlayerDisconnected;

    private static readonly ILogger _Logger;

    static KronstadtPlayerManager()
    {
        _Logger = LoggerProvider.CreateLogger<KronstadtPlayerManager>();
        Players = new();

        Provider.onServerConnected += OnServerConnected;
        Provider.onServerDisconnected += OnServerDisconnected;

        // God mode
        DamageTool.damagePlayerRequested += OnDamageRequested;
        PlayerLife.OnTellHealth_Global += GodModeHandler;
        PlayerLife.OnTellFood_Global += GodModeHandler;
        PlayerLife.OnTellWater_Global += GodModeHandler;
        PlayerLife.OnTellVirus_Global += GodModeHandler;
        PlayerLife.OnTellBroken_Global += GodModeHandler;
        PlayerLife.OnTellBleeding_Global += GodModeHandler;
        PlayerVoice.onRelayVoice += OnRelayVoice;
    }

    private static void OnRelayVoice(PlayerVoice speaker, bool wantsToUseWalkieTalkie, ref bool shouldAllow, ref bool shouldBroadcastOverRadio, ref PlayerVoice.RelayVoiceCullingHandler cullingHandler)
    {
        TryGetPlayer(speaker.player, out KronstadtPlayer player);
        shouldAllow = !player.Moderation.IsMuted;
    }

    private static void GodModeHandler(PlayerLife life)
    {
        // Do this with a patch later
        TryGetPlayer(life.player, out KronstadtPlayer player);
        if (player.Administration.GodMode)
        {
            life.sendRevive();
        }
    }

    private static void OnDamageRequested(ref DamagePlayerParameters parameters, ref bool shouldAllow)
    {
        TryGetPlayer(parameters.player, out KronstadtPlayer player);
        shouldAllow = !player?.Administration.GodMode ?? true;
    }

    private static IEnumerable<KronstadtPlayer> GetPlayerListCopy()
    {
        foreach (KronstadtPlayer player in Players.Values)
        {
            yield return player;
        }
    }

    public static void KickAll(string reason)
    {
        foreach (KronstadtPlayer player in GetPlayerListCopy())
        {
            player.Moderation.Kick(reason);
        }
    
        while (Players.Count != 0);
    }

    public static void KickAll(Translation translation, params object[] args)
    {
        foreach (KronstadtPlayer player in GetPlayerListCopy())
        {
            player.Moderation.Kick(translation, args);
        }
    
        while (Players.Count != 0);
    }

    public static bool TryGetPlayer(Player inPlayer, out KronstadtPlayer player)
    {
        return Players.TryGetValue(inPlayer.channel.owner.playerID.steamID, out player);
    }

    public static bool TryGetPlayer(SteamPlayer inPlayer, out KronstadtPlayer player)
    {
        return Players.TryGetValue(inPlayer.playerID.steamID, out player);
    }

    public static bool TryGetPlayer(CSteamID id, out KronstadtPlayer player)
    {
        return Players.TryGetValue(id, out player);
    }
    
    public static bool IsOnline(CSteamID steamID)
    {
        return Players.TryGetValue(steamID, out _);
    }

    public static bool TryFindPlayer(string search, out KronstadtPlayer player)
    {
        if (PlayerTool.tryGetSteamID(search, out CSteamID steamID))
        {
            return KronstadtPlayerManager.Players.TryGetValue(steamID, out player);
        }

        player = KronstadtPlayerManager.Players.Values.FirstOrDefault(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (player == null)
        {
            return false;
        }

        return true;
    }

    private static async UniTask OnConnected(CSteamID steamID)
    {
        SteamPlayer steamPlayer = Provider.clients.Find(x => x.playerID.steamID == steamID);
        KronstadtPlayer player = await KronstadtPlayer.CreateAsync(steamPlayer);
        Players.TryAdd(player.SteamID, player);

        IEnumerable<Offense> offenses = await PlayerIdManager.GetOffensesAsync(player);

        string discord = KronstadtHost.Configuration.GetValue<string>("DiscordInviteLink") ?? "Failed to get discord link";

        Offense? permBan = offenses.FirstOrDefault(x => x.OffenseType == OffenseType.Ban && x.Duration == 0 && !x.Pardoned);
        if (permBan != null)
        {
            player.Moderation.Kick(TranslationList.BanPermanent, permBan.Reason, discord);
            return;
        }
        else
        {
            Offense? nonPermBan = offenses.Where(x => x.OffenseType == OffenseType.Ban && !x.IsPermanent && x.IsActive)
                .OrderByDescending(x => x.Remaining).FirstOrDefault();
            if (nonPermBan != null)
            {
                player.Moderation.Kick(TranslationList.BanTemporary, nonPermBan.Reason, Formatter.FormatTime(nonPermBan.Remaining), discord);
                return;
            }
        }

        Offense? permMute = offenses.FirstOrDefault(x => x.OffenseType == OffenseType.Mute && x.IsPermanent);
        if (permMute != null)
        {
            player.SendMessage(TranslationList.MutePermanent, permMute.Reason, discord);
            player.Moderation.IsMuted = true;
        }
        else
        {
            Offense? tempMute = offenses.Where(x => x.OffenseType == OffenseType.Mute && !x.IsPermanent)
                .OrderByDescending(x => x.Remaining).FirstOrDefault();
            if (tempMute != null)
            {
                player.SendMessage(TranslationList.MuteTemporary, tempMute.Reason, Formatter.FormatTime(tempMute.Remaining), discord);
                player.Moderation.IsMuted = true;
                player.Moderation.EnqueueUnmute(tempMute.Remaining);
            }
        }

        KronstadtChat.BroadcastMessage(TranslationList.PlayerConnected, player.Name);
        _Logger.LogInformation($"{player.LogName} has joined the server");
        OnPlayerConnected?.Invoke(player);
    }

    private static async void OnServerConnected(CSteamID steamID)
    {
        try
        {
            await OnConnected(steamID);
        }
        catch (Exception ex)
        {
            _Logger.LogError(ex, "Exception while player connection; Kicking...");
            string discord = KronstadtHost.Configuration.GetValue<string>("DiscordInviteLink") ?? "Failed to get discord link";
            Provider.kick(steamID, $"Something failed while connecting; Please contact staff; {discord ?? "Failed to get link :C"}");
        }
    }

    private static async void OnServerDisconnected(CSteamID steamID)
    {
        Players.TryRemove(steamID, out KronstadtPlayer player);
        await PlayerDataManager.SaveDataAsync(player);

        OnPlayerDisconnected?.Invoke(player);

        player.Moderation.CancelUnmute();

        KronstadtChat.BroadcastMessage(TranslationList.PlayerDisconnected, player.Name);
        _Logger.LogInformation($"{player.LogName} has left the server");
    }
}