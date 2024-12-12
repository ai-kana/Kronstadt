using Cysharp.Threading.Tasks;
using Meow.Core.Chat;
using Meow.Core.Commands.Framework;
using Meow.Core.Formatting;
using Meow.Core.Offenses;
using Meow.Core.Players;
using Meow.Core.Translations;
using Steamworks;

namespace Meow.Core.Commands;

[CommandData("ban")]
[CommandSyntax("[<Params: player>] [<Params: time>] [<Params: reason...>]")]
internal class BanCommand : Command
{
    public BanCommand(CommandContext context) : base(context)
    {
    }

    private static readonly Translation PlayerBannedTemp = new("PlayerBannedTemp", "{0} has been banned for {1} for {2}");
    private static readonly Translation PlayerBannedPerm = new("PlayerBannedPerm", "{0} has been banned for {1} permanently");

    public override async UniTask ExecuteAsync()
    {
        Context.AssertPermission("ban");
        Context.AssertOnDuty();
        Context.AssertArguments(3);

        CSteamID id = CSteamID.Nil;
        MeowPlayer? player = null;
        if (Context.TryParse<MeowPlayer>(out player))
        {
            id = player.SteamID;
        } 
        else 
        {
            id = Context.Parse<CSteamID>();
        }

        Context.MoveNext();

        long length = (long)Context.Parse<TimeSpan>().TotalSeconds;

        Context.MoveNext();

        string reason = Context.Form();

        if (player != null)
        {
            player.Moderation.Ban(Context.Caller.SteamID, length, reason);
        }
        else
        {
            await OffenseManager.AddOffense(Offense.Create(OffenseType.Ban, id, Context.Caller.SteamID, reason, length));
        }

        string name = player?.Name ?? id.ToString();

        MeowChat.BroadcastMessage(length == 0 ? PlayerBannedPerm : PlayerBannedTemp, name, reason, Formatter.FormatTime(length));
        throw Context.Exit;
    }
}