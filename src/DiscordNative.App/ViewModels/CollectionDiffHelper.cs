using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DiscordNative.Protocol.Models;

namespace DiscordNative.App.ViewModels;

public static class CollectionDiffHelper
{
    public static void SyncMemberGroups(ObservableCollection<DiscordMemberGroup> target, List<DiscordMemberGroup> fresh)
    {
        // 1. Supprimer les groupes qui n'existent plus
        var freshNames = new HashSet<string>(fresh.Select(g => g.Name), StringComparer.OrdinalIgnoreCase);
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!freshNames.Contains(target[i].Name))
            {
                target.RemoveAt(i);
            }
        }

        // 2. Synchroniser ou insérer dans l'ordre exact
        for (int i = 0; i < fresh.Count; i++)
        {
            var freshGroup = fresh[i];
            if (i < target.Count && string.Equals(target[i].Name, freshGroup.Name, StringComparison.OrdinalIgnoreCase))
            {
                // Même groupe à la même position : mettre à jour les membres in-place
                target[i].RoleColorHex = freshGroup.RoleColorHex;
                target[i].Position = freshGroup.Position;
                SyncUsers(target[i].Members, freshGroup.Members);
                target[i].NotifyMembersChanged();
            }
            else
            {
                // Trouver si le groupe existe plus loin
                int existingIdx = -1;
                for (int j = i; j < target.Count; j++)
                {
                    if (string.Equals(target[j].Name, freshGroup.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        existingIdx = j;
                        break;
                    }
                }

                if (existingIdx >= 0)
                {
                    var existingGroup = target[existingIdx];
                    target.Move(existingIdx, i);
                    existingGroup.RoleColorHex = freshGroup.RoleColorHex;
                    existingGroup.Position = freshGroup.Position;
                    SyncUsers(existingGroup.Members, freshGroup.Members);
                    existingGroup.NotifyMembersChanged();
                }
                else
                {
                    // Insérer le nouveau groupe à cette position
                    target.Insert(i, freshGroup);
                }
            }
        }
    }

    public static void SyncUsers(ObservableCollection<DiscordUser> target, IEnumerable<DiscordUser> freshItems)
    {
        var freshList = freshItems.ToList();
        var freshIds = new HashSet<string>(freshList.Select(u => u.Id), StringComparer.OrdinalIgnoreCase);

        // Supprimer ceux qui ne sont plus présents
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!freshIds.Contains(target[i].Id))
            {
                target.RemoveAt(i);
            }
        }

        // Réordonner ou insérer
        for (int i = 0; i < freshList.Count; i++)
        {
            var freshUser = freshList[i];
            if (i < target.Count && target[i].Id == freshUser.Id)
            {
                // Même position : pas de recréation d'élément UI
                continue;
            }

            int existingIdx = -1;
            for (int j = i; j < target.Count; j++)
            {
                if (target[j].Id == freshUser.Id)
                {
                    existingIdx = j;
                    break;
                }
            }

            if (existingIdx >= 0)
            {
                target.Move(existingIdx, i);
            }
            else
            {
                target.Insert(i, freshUser);
            }
        }
    }
}
