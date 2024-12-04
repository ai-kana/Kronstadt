using Newtonsoft.Json;
using Kronstadt.Core.Players;

namespace Kronstadt.Core.Fishing;

public class LootItem
{
    [JsonProperty]
    public ushort Xp {get; private set;}
    [JsonProperty]
    public ushort Weight {get; private set;}
    [JsonProperty]
    public ushort Id {get; private set;}
    [JsonProperty]
    public uint Level {get; private set;}

    public LootItem(ushort id, ushort weight, ushort xp, uint level)
    {
        Id = id;
        Xp = xp;
        Weight = weight;
        Level = level;
    }

    public void SendReward(KronstadtPlayer player)
    {
        player.Inventory.GiveItem(Id);
    }
}
