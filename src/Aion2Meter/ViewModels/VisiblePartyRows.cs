using System.Collections.Generic;
using System.Linq;
using Aion2Meter.Services;
using AionFlex.Core.Overlay;

namespace Aion2Meter.ViewModels;

internal static class VisiblePartyRows
{
    // The input is already sorted by DPS. Keep the true rank when the local
    // player replaces the tenth row rather than pretending they came tenth.
    public static List<(MeterPlayerSnapshot Player, int Rank)> Select(
        IReadOnlyList<MeterPlayerSnapshot> sortedPlayers,
        int? localPlayerId,
        string? localPlayerName,
        bool pinLocalPlayer)
    {
        var result = sortedPlayers
            .Take(MeterWindowHeightPolicy.MaximumVisibleRows)
            .Select((player, index) => (player, index + 1))
            .ToList();

        if (!pinLocalPlayer)
            return result;

        int localIndex = -1;
        for (int index = 0; index < sortedPlayers.Count; index++)
        {
            MeterPlayerSnapshot player = sortedPlayers[index];
            if (!LocalPlayerRowPolicy.IsLocalPlayer(
                    player.PlayerId, player.Nickname, localPlayerId, localPlayerName))
                continue;

            localIndex = index;
            break;
        }

        if (localIndex >= MeterWindowHeightPolicy.MaximumVisibleRows)
        {
            result.RemoveAt(MeterWindowHeightPolicy.MaximumVisibleRows - 1);
            result.Add((sortedPlayers[localIndex], localIndex + 1));
        }

        return result;
    }
}
