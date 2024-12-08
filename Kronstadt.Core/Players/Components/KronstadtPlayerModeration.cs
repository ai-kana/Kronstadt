using System.Collections;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SDG.Unturned;
using Steamworks;
using Kronstadt.Core.Offenses;
using Kronstadt.Core.Translations;

namespace Kronstadt.Core.Players.Components;

public class KronstadtPlayerModeration
{
    public readonly KronstadtPlayer Owner;
    public bool IsMuted {get; set;} = false;
    private CancellationTokenSource? _UnmuteSource = null;

    public KronstadtPlayerModeration(KronstadtPlayer owner)
    {
        Owner = owner;
        KronstadtPlayerManager.OnPlayerDisconnected += OnDisconnected;
    }

    ~KronstadtPlayerModeration()
    {
        KronstadtPlayerManager.OnPlayerDisconnected -= OnDisconnected;
    }

    private void OnDisconnected(KronstadtPlayer player)
    {
        _UnmuteSource?.Cancel();
    }

    public void Spy(KronstadtPlayer caller)
    {
        Owner.Player.sendScreenshot(caller.SteamID, null);
    }

    private static async UniTask WaitForUnmute(CSteamID id, long time, CancellationToken token)
    {
        await UniTask.Delay((int)(time * 1000), cancellationToken: token);
        if (token.IsCancellationRequested)
        {
            return;
        }

        if (KronstadtPlayerManager.TryGetPlayer(id, out KronstadtPlayer player))
        {
            player.Moderation.IsMuted = false;
            player.SendMessage(TranslationList.Unmuted);
        }
    }

    public void EnqueueUnmute(long duration)
    {
        _UnmuteSource = new();
        WaitForUnmute(Owner.SteamID, duration, _UnmuteSource.Token).Forget();
    }

    public void CancelUnmute()
    {
        if (_UnmuteSource == null)
        {
            return;
        }

        _UnmuteSource.Cancel();
        _UnmuteSource = null;
    }

    public async UniTask<IEnumerable<Offense>> GetAllOffenses()
    {
        return await OffenseManager.GetOffenses(Owner.SteamID);
    }

    public async UniTask<IEnumerable<Offense>> GetWarns()
    {
        return await OffenseManager.GetWarnOffenses(Owner.SteamID);
    }

    public async UniTask AddWarn(CSteamID issuer, string reason)
    {
        await OffenseManager.AddOffense(Offense.Create(OffenseType.Warn, Owner.SteamID, issuer, reason, 0));
    }

    public async UniTask AddMute(CSteamID issuer, long duration, string reason)
    {
        if (duration != 0)
        {
            EnqueueUnmute(duration);
        }

        await OffenseManager.AddOffense(Offense.Create(OffenseType.Mute, Owner.SteamID, issuer, reason, duration));
    }

    public async UniTask AddBan(CSteamID issuer, long duration, string reason)
    {
        await OffenseManager.AddOffense(Offense.Create(OffenseType.Ban, Owner.SteamID, issuer, reason, duration));
    }

    public void Mute(CSteamID issuerId)
    {
        string discordInvite = KronstadtHost.Configuration.GetValue<string>("DiscordInviteLink")!;
        Kick(TranslationList.MutePermanent, "No reason provided", discordInvite);
        _ = AddMute(issuerId, 0, "No reason provided");
    }

    public void Mute(CSteamID issuerId, long duration)
    {
        string discordInvite = KronstadtHost.Configuration.GetValue<string>("DiscordInviteLink")!;
        _ = AddMute(issuerId, duration, "No reason provided");
    }

    public void Mute(CSteamID issuerId, long duration, string reason)
    {
        string discordInvite = KronstadtHost.Configuration.GetValue<string>("DiscordInviteLink")!;
        _ = AddMute(issuerId, duration, reason);
    }

    public void Ban(CSteamID issuerId)
    {
        string discordInvite = KronstadtHost.Configuration.GetValue<string>("DiscordInviteLink")!;
        Kick(TranslationList.BanPermanent, "No reason provided", discordInvite);
        _ = AddBan(issuerId, 0, "No reason provided");
    }
    
    public void Ban(CSteamID issuerId, long duration)
    {
        string discordInvite = KronstadtHost.Configuration.GetValue<string>("DiscordInviteLink")!;
        Kick(TranslationList.BanTemporary, "No reason provided", duration, discordInvite);
        _ = AddBan(issuerId, duration, "No reason provided");
    }
    
    public void Ban(CSteamID issuerId, string reason)
    {
        string discordInvite = KronstadtHost.Configuration.GetValue<string>("DiscordInviteLink")!;
        Kick(TranslationList.BanPermanent, reason, discordInvite);
        _ = AddBan(issuerId, long.MaxValue, reason);
    }
    
    public void Ban(CSteamID issuerId, long duration, string reason)
    {
        string discordInvite = KronstadtHost.Configuration.GetValue<string>("DiscordInviteLink")!;
        Kick(TranslationList.BanTemporary, reason, duration, discordInvite);
        _ = AddBan(issuerId, duration, reason);
    }

    private async UniTask DoKick(string reason)
    {
        await UniTask.Yield();
        Provider.kick(Owner.SteamID, reason);
    }

    public void Kick()
    {
        DoKick("No reason provided").Forget();
    }

    public void Kick(string reason)
    {
        DoKick(reason).Forget();
    }

    public void Kick(Translation translation, params object[] args)
    {
        Provider.kick(Owner.SteamID, translation.TranslateNoColor(Owner.Language, args));
    }

}
