using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DiscordNative.Protocol.Models;

namespace DiscordNative.Protocol.Rest;

/// <summary>
/// Client REST minimal pour les opérations courantes (lister guildes/salons/messages, envoyer un message).
/// Utilise le même en-tête Authorization brut (sans préfixe "Bot ") que le client officiel pour un token utilisateur.
/// </summary>
public sealed class DiscordRestClient : IDisposable
{
    private const string ApiBase = "https://discord.com/api/v9";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;

    public DiscordRestClient(string token)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36");
        // Pas de préfixe "Bot" : c'est un token de compte utilisateur.
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", token);
        _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Discord-Locale", "fr");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Super-Properties",
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                "{\"os\":\"Windows\",\"browser\":\"Discord Client\",\"release_channel\":\"stable\",\"client_version\":\"1.0.9168\",\"os_version\":\"10.0.22631\",\"os_arch\":\"x64\",\"app_arch\":\"x64\",\"system_locale\":\"fr\",\"browser_user_agent\":\"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) discord/1.0.9168 Chrome/128.0.6613.186 Electron/32.2.2 Safari/537.36\",\"browser_version\":\"32.2.2\",\"client_build_number\":342420,\"native_build_number\":53993,\"client_event_source\":null}")));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<DiscordUser?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var user = await _http.GetFromJsonAsync<DiscordUser>($"{ApiBase}/users/@me", ct);
        if (user is not null)
        {
            await EnrichUserProfileAsync(user, ct);
            if (user.ConnectedAccounts.Count == 0)
            {
                var visibleConns = await GetCurrentUserVisibleConnectionsAsync(ct);
                if (visibleConns.Count > 0)
                {
                    user.ConnectedAccounts = visibleConns;
                    Protocol.App.RunOnUiThread(() => user.NotifyProfileEnriched());
                }
            }
        }
        return user;
    }

    public async Task EnrichUserProfileAsync(DiscordUser user, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(user.Id)) return;
        try
        {
            using var resp = await _http.GetAsync($"{ApiBase}/users/{user.Id}/profile?with_mutual_guilds=true&with_mutual_friends_count=true", ct);
            if (!resp.IsSuccessStatusCode) return;

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;

            if (root.TryGetProperty("user", out var uEl))
            {
                if (uEl.TryGetProperty("bio", out var bioEl) && bioEl.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(user.Bio))
                    user.Bio = bioEl.GetString();
                if (uEl.TryGetProperty("banner", out var banEl) && banEl.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(user.Banner))
                    user.Banner = banEl.GetString();
                if (uEl.TryGetProperty("accent_color", out var uAcEl) && uAcEl.ValueKind == JsonValueKind.Number && uAcEl.TryGetInt32(out int uAcVal))
                    user.AccentColor = uAcVal;
                if (uEl.TryGetProperty("clan", out var clanEl) && clanEl.ValueKind == JsonValueKind.Object)
                    user.Clan = JsonSerializer.Deserialize<DiscordClan>(clanEl.GetRawText());
                if (uEl.TryGetProperty("primary_guild", out var pgEl) && pgEl.ValueKind == JsonValueKind.Object)
                    user.PrimaryGuild = JsonSerializer.Deserialize<DiscordClan>(pgEl.GetRawText());
                if (uEl.TryGetProperty("premium_type", out var ptEl) && ptEl.ValueKind == JsonValueKind.Number && ptEl.TryGetInt32(out int ptVal))
                    user.PremiumType = ptVal;
            }
            if (root.TryGetProperty("premium_type", out var rootPtEl) && rootPtEl.ValueKind == JsonValueKind.Number && rootPtEl.TryGetInt32(out int rootPtVal))
            {
                user.PremiumType = rootPtVal;
            }
            if (root.TryGetProperty("mutual_friends_count", out var mfcEl) && mfcEl.ValueKind == JsonValueKind.Number && mfcEl.TryGetInt32(out int mfcVal))
            {
                user.MutualFriendsCount = mfcVal;
            }
            if (root.TryGetProperty("mutual_guilds", out var mgEl) && mgEl.ValueKind == JsonValueKind.Array)
            {
                user.MutualGuildsCount = mgEl.GetArrayLength();
                var mGuilds = new List<DiscordMutualGuild>();
                foreach (var gEl in mgEl.EnumerateArray())
                {
                    var gid = gEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    var nick = gEl.TryGetProperty("nick", out var nickEl) && nickEl.ValueKind == JsonValueKind.String ? nickEl.GetString() : null;
                    var icon = gEl.TryGetProperty("icon", out var iconEl) && iconEl.ValueKind == JsonValueKind.String ? iconEl.GetString() : null;
                    if (!string.IsNullOrEmpty(gid))
                    {
                        var mg = new DiscordMutualGuild
                        {
                            Id = gid!,
                            Nick = nick,
                            Icon = icon,
                            Name = !string.IsNullOrEmpty(nick) ? nick : "Serveur"
                        };
                        if (!string.IsNullOrEmpty(icon))
                        {
                            mg.IconUrl = $"https://cdn.discordapp.com/icons/{gid}/{icon}.png?size=128";
                        }
                        mGuilds.Add(mg);
                    }
                }
                user.MutualGuilds = mGuilds;
            }

            if (root.TryGetProperty("user_profile", out var upEl) && upEl.ValueKind == JsonValueKind.Object)
            {
                if (upEl.TryGetProperty("bio", out var upBio) && upBio.ValueKind == JsonValueKind.String)
                    user.Bio = upBio.GetString();
                if (upEl.TryGetProperty("pronouns", out var pronEl) && pronEl.ValueKind == JsonValueKind.String)
                    user.Pronouns = pronEl.GetString();
                if (upEl.TryGetProperty("banner", out var upBan) && upBan.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(user.Banner))
                    user.Banner = upBan.GetString();
                if (upEl.TryGetProperty("accent_color", out var acEl) && acEl.ValueKind == JsonValueKind.Number && acEl.TryGetInt32(out int acVal))
                    user.AccentColor = acVal;
                if (upEl.TryGetProperty("theme_colors", out var tcEl) && tcEl.ValueKind == JsonValueKind.Array)
                {
                    var tColors = new List<int>();
                    foreach (var cItem in tcEl.EnumerateArray())
                    {
                        if (cItem.ValueKind == JsonValueKind.Number && cItem.TryGetInt32(out int cVal))
                        {
                            tColors.Add(cVal);
                        }
                    }
                    if (tColors.Count > 0) user.ThemeColors = tColors;
                }
            }

            if (root.TryGetProperty("premium_since", out var psEl) && psEl.ValueKind == JsonValueKind.String)
            {
                user.PremiumSince = psEl.GetString();
            }

            if (root.TryGetProperty("premium_guild_since", out var pgsEl) && pgsEl.ValueKind == JsonValueKind.String)
            {
                user.PremiumGuildSince = pgsEl.GetString();
            }

            if (root.TryGetProperty("badges", out var badgesEl) && badgesEl.ValueKind == JsonValueKind.Array)
            {
                var badges = JsonSerializer.Deserialize<List<DiscordBadge>>(badgesEl.GetRawText());
                if (badges is not null) user.Badges = badges;
            }

            EnrichBadges(user, root);

            if (root.TryGetProperty("connected_accounts", out var connEl) && connEl.ValueKind == JsonValueKind.Array)
            {
                var conns = JsonSerializer.Deserialize<List<DiscordConnectedAccount>>(connEl.GetRawText());
                if (conns is not null)
                {
                    user.ConnectedAccounts = conns.Where(c => c.Visibility != 0).ToList();
                }
            }

            // Widgets (played_games & favorite_games -> Game Collection)
            if (root.TryGetProperty("widgets", out var widgetsEl) && widgetsEl.ValueKind == JsonValueKind.Array)
            {
                var rawGames = new List<(string gameId, string? comment, bool isFav)>();
                foreach (var w in widgetsEl.EnumerateArray())
                {
                    if (!w.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Object) continue;
                    var wType = dataEl.TryGetProperty("type", out var tEl) ? tEl.GetString() : "";
                    bool isFav = string.Equals(wType, "favorite_games", StringComparison.OrdinalIgnoreCase);

                    if (dataEl.TryGetProperty("games", out var gamesEl) && gamesEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var g in gamesEl.EnumerateArray())
                        {
                            var gid = g.TryGetProperty("game_id", out var gidEl) ? gidEl.GetString() : null;
                            var comment = g.TryGetProperty("comment", out var cEl) && cEl.ValueKind == JsonValueKind.String ? cEl.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(gid) && rawGames.All(x => x.gameId != gid))
                            {
                                rawGames.Add((gid!, comment, isFav));
                            }
                        }
                    }
                }

                if (rawGames.Count > 0)
                {
                    var query = string.Join("&", rawGames.Select(g => $"application_ids={Uri.EscapeDataString(g.gameId)}"));
                    using var appResp = await _http.GetAsync($"{ApiBase}/applications/public?{query}", ct);
                    var appMap = new Dictionary<string, (string name, string icon)>();
                    if (appResp.IsSuccessStatusCode)
                    {
                        using var appDoc = await JsonDocument.ParseAsync(await appResp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                        if (appDoc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var appEl in appDoc.RootElement.EnumerateArray())
                            {
                                var id = appEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                                var name = appEl.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
                                var icon = appEl.TryGetProperty("icon", out var iEl) ? iEl.GetString() : null;
                                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                                {
                                    appMap[id!] = (name!, icon ?? "");
                                }
                            }
                        }
                    }

                    var gameList = new List<DiscordWidgetGame>();
                    foreach (var rg in rawGames)
                    {
                        if (appMap.TryGetValue(rg.gameId, out var info) && !string.IsNullOrWhiteSpace(info.icon))
                        {
                            gameList.Add(new DiscordWidgetGame
                            {
                                GameId = rg.gameId,
                                Name = info.name,
                                IconUrl = $"https://cdn.discordapp.com/app-icons/{rg.gameId}/{info.icon}.png?size=128",
                                Comment = rg.comment,
                                IsFavorite = rg.isFav
                            });
                        }
                    }
                    user.GameWidgets = gameList;
                }
            }

            user.HasProfileEnriched = true;
            Protocol.App.RunOnUiThread(() => user.NotifyProfileEnriched());
        }
        catch { }
    }

    public async Task<List<DiscordUser>> GetMutualFriendsAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync($"{ApiBase}/users/{userId}/relationships", ct);
            if (!resp.IsSuccessStatusCode) return new();

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new();

            var list = new List<DiscordUser>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                JsonElement userEl = el;
                if (el.TryGetProperty("user", out var innerUser) && innerUser.ValueKind == JsonValueKind.Object)
                {
                    userEl = innerUser;
                }

                var u = userEl.Deserialize<DiscordUser>(JsonOptions);
                if (u != null)
                {
                    if (el.TryGetProperty("clan", out var cEl) && cEl.ValueKind == JsonValueKind.Object && u.Clan == null)
                    {
                        u.Clan = cEl.Deserialize<DiscordClan>(JsonOptions);
                    }
                    if (el.TryGetProperty("presence", out var presEl) && presEl.ValueKind == JsonValueKind.Object)
                    {
                        if (presEl.TryGetProperty("status", out var stEl) && stEl.ValueKind == JsonValueKind.String)
                        {
                            u.Status = stEl.GetString() ?? "offline";
                        }
                        if (presEl.TryGetProperty("activities", out var actsEl) && actsEl.ValueKind == JsonValueKind.Array)
                        {
                            u.Activities = actsEl.Deserialize<List<DiscordActivity>>(JsonOptions) ?? new();
                        }
                    }
                    list.Add(u);
                }
            }
            return list;
        }
        catch
        {
            return new();
        }
    }

    public async Task<List<DiscordConnectedAccount>> GetCurrentUserVisibleConnectionsAsync(CancellationToken ct = default)
    {
        try
        {
            var conns = await _http.GetFromJsonAsync<List<DiscordConnectedAccount>>($"{ApiBase}/users/@me/connections", ct);
            if (conns is not null)
            {
                return conns.Where(c => c.Visibility == 1).ToList();
            }
        }
        catch { }
        return new();
    }

    public async Task<DiscordUser> ModifyCurrentUserAsync(
        string? globalName = null,
        string? username = null,
        string? currentPassword = null,
        string? newPassword = null,
        string? bio = null,
        string? pronouns = null,
        string? avatarDataUri = null,
        bool removeAvatar = false,
        string? bannerDataUri = null,
        bool removeBanner = false,
        int? accentColor = null,
        CancellationToken ct = default)
    {
        var userBody = new Dictionary<string, object?>();
        if (globalName is not null) userBody["global_name"] = globalName;
        if (!string.IsNullOrWhiteSpace(username)) userBody["username"] = username;
        if (!string.IsNullOrWhiteSpace(currentPassword)) userBody["password"] = currentPassword;
        if (!string.IsNullOrWhiteSpace(newPassword)) userBody["new_password"] = newPassword;
        if (removeAvatar) userBody["avatar"] = null;
        else if (!string.IsNullOrWhiteSpace(avatarDataUri)) userBody["avatar"] = avatarDataUri;
        if (removeBanner) userBody["banner"] = null;
        else if (!string.IsNullOrWhiteSpace(bannerDataUri)) userBody["banner"] = bannerDataUri;
        if (accentColor.HasValue) userBody["accent_color"] = accentColor.Value;

        if (bio is not null) userBody["bio"] = bio;
        if (pronouns is not null) userBody["pronouns"] = pronouns;

        // Mise à jour en parallèle de /users/@me/profile pour bio, pronouns et accent_color (utilisé par Discord v9)
        if (bio is not null || pronouns is not null || accentColor.HasValue)
        {
            try
            {
                var profileBody = new Dictionary<string, object?>();
                if (bio is not null) profileBody["bio"] = bio;
                if (pronouns is not null) profileBody["pronouns"] = pronouns;
                if (accentColor.HasValue) profileBody["accent_color"] = accentColor.Value;
                using var profResp = await _http.PatchAsJsonAsync($"{ApiBase}/users/@me/profile", profileBody, ct);
            }
            catch { }
        }

        var response = await _http.PatchAsJsonAsync($"{ApiBase}/users/@me", userBody, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errJson = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Erreur Discord ({(int)response.StatusCode}) : {errJson}");
        }

        var updated = await response.Content.ReadFromJsonAsync<DiscordUser>(cancellationToken: ct)
               ?? throw new InvalidOperationException("Réponse de mise à jour du profil illisible.");
        if (bio is not null && string.IsNullOrEmpty(updated.Bio)) updated.Bio = bio;
        if (pronouns is not null && string.IsNullOrEmpty(updated.Pronouns)) updated.Pronouns = pronouns;
        await EnrichUserProfileAsync(updated, ct);
        return updated;
    }

    public async Task<(string? nick, string? bio, string? pronouns, string? avatarHash, string? bannerHash)> GetGuildMemberProfileAsync(string guildId, string userId, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync($"{ApiBase}/users/{userId}/profile?with_mutual_guilds=false&guild_id={guildId}", ct);
            if (!resp.IsSuccessStatusCode) return (null, null, null, null, null);

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;
            string? nick = null, bio = null, pronouns = null, avatar = null, banner = null;

            if (root.TryGetProperty("guild_member", out var gmEl) && gmEl.ValueKind == JsonValueKind.Object)
            {
                if (gmEl.TryGetProperty("nick", out var nEl) && nEl.ValueKind == JsonValueKind.String) nick = nEl.GetString();
                if (gmEl.TryGetProperty("bio", out var bEl) && bEl.ValueKind == JsonValueKind.String) bio = bEl.GetString();
                if (gmEl.TryGetProperty("pronouns", out var pEl) && pEl.ValueKind == JsonValueKind.String) pronouns = pEl.GetString();
                if (gmEl.TryGetProperty("avatar", out var aEl) && aEl.ValueKind == JsonValueKind.String) avatar = aEl.GetString();
                if (gmEl.TryGetProperty("banner", out var bnEl) && bnEl.ValueKind == JsonValueKind.String) banner = bnEl.GetString();
            }
            if (root.TryGetProperty("guild_member_profile", out var gmpEl) && gmpEl.ValueKind == JsonValueKind.Object)
            {
                if (gmpEl.TryGetProperty("bio", out var bEl) && bEl.ValueKind == JsonValueKind.String) bio = bEl.GetString();
                if (gmpEl.TryGetProperty("pronouns", out var pEl) && pEl.ValueKind == JsonValueKind.String) pronouns = pEl.GetString();
                if (gmpEl.TryGetProperty("banner", out var bnEl) && bnEl.ValueKind == JsonValueKind.String) banner = bnEl.GetString();
            }
            return (nick, bio, pronouns, avatar, banner);
        }
        catch
        {
            return (null, null, null, null, null);
        }
    }

    public async Task<DiscordUser?> GetUserProfileUserAsync(string userId, string? guildId = null, CancellationToken ct = default)
    {
        try
        {
            var url = string.IsNullOrEmpty(guildId)
                ? $"{ApiBase}/users/{userId}/profile?with_mutual_guilds=false"
                : $"{ApiBase}/users/{userId}/profile?with_mutual_guilds=false&guild_id={guildId}";

            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;

            DiscordUser? user = null;
            if (root.TryGetProperty("user", out var uEl) && uEl.ValueKind == JsonValueKind.Object)
            {
                user = JsonSerializer.Deserialize<DiscordUser>(uEl.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            if (user != null)
            {
                if (root.TryGetProperty("guild_member", out var gmEl) && gmEl.ValueKind == JsonValueKind.Object)
                {
                    if (gmEl.TryGetProperty("nick", out var nEl) && nEl.ValueKind == JsonValueKind.String)
                    {
                        user.ServerNick = nEl.GetString();
                    }
                    if (gmEl.TryGetProperty("avatar", out var aEl) && aEl.ValueKind == JsonValueKind.String)
                    {
                        user.ServerAvatar = aEl.GetString();
                        user.GuildIdForAvatar = guildId;
                    }
                    if (gmEl.TryGetProperty("roles", out var rEl) && rEl.ValueKind == JsonValueKind.Array)
                    {
                        user.Roles = rEl.EnumerateArray().Select(r => r.GetString() ?? "").Where(r => !string.IsNullOrEmpty(r)).ToList();
                    }
                }
            }

            return user;
        }
        catch
        {
            return null;
        }
    }

    public async Task<DiscordUser?> GetUserAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            var profileUser = await GetUserProfileUserAsync(userId, null, ct);
            if (profileUser != null) return profileUser;
            return await _http.GetFromJsonAsync<DiscordUser>($"{ApiBase}/users/{userId}", ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task<DiscordUser?> GetGuildMemberAsync(string guildId, string userId, CancellationToken ct = default)
    {
        try
        {
            var profileUser = await GetUserProfileUserAsync(userId, guildId, ct);
            if (profileUser != null) return profileUser;

            using var resp = await _http.GetAsync($"{ApiBase}/guilds/{guildId}/members/{userId}", ct);
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;
            DiscordUser? user = null;
            if (root.TryGetProperty("user", out var uEl) && uEl.ValueKind == JsonValueKind.Object)
            {
                user = JsonSerializer.Deserialize<DiscordUser>(uEl.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            if (user != null)
            {
                if (root.TryGetProperty("nick", out var nEl) && nEl.ValueKind == JsonValueKind.String)
                {
                    user.ServerNick = nEl.GetString();
                }
                if (root.TryGetProperty("avatar", out var aEl) && aEl.ValueKind == JsonValueKind.String)
                {
                    user.ServerAvatar = aEl.GetString();
                    user.GuildIdForAvatar = guildId;
                }
            }
            return user;
        }
        catch
        {
            return null;
        }
    }

    public async Task ModifyGuildMemberProfileAsync(
        string guildId,
        string? nick = null,
        string? bio = null,
        string? pronouns = null,
        string? avatarDataUri = null,
        bool removeAvatar = false,
        string? bannerDataUri = null,
        bool removeBanner = false,
        int? accentColor = null,
        CancellationToken ct = default)
    {
        var memberBody = new Dictionary<string, object?>();
        if (nick is not null) memberBody["nick"] = string.IsNullOrWhiteSpace(nick) ? null : nick;
        if (bio is not null) memberBody["bio"] = bio;
        if (pronouns is not null) memberBody["pronouns"] = pronouns;
        if (removeAvatar) memberBody["avatar"] = null;
        else if (!string.IsNullOrWhiteSpace(avatarDataUri)) memberBody["avatar"] = avatarDataUri;
        if (removeBanner) memberBody["banner"] = null;
        else if (!string.IsNullOrWhiteSpace(bannerDataUri)) memberBody["banner"] = bannerDataUri;

        if (memberBody.Count > 0)
        {
            using var resp = await _http.PatchAsJsonAsync($"{ApiBase}/guilds/{guildId}/members/@me", memberBody, ct);
        }

        if (bio is not null || pronouns is not null || accentColor.HasValue || removeBanner || !string.IsNullOrWhiteSpace(bannerDataUri))
        {
            try
            {
                var profBody = new Dictionary<string, object?>();
                if (bio is not null) profBody["bio"] = bio;
                if (pronouns is not null) profBody["pronouns"] = pronouns;
                if (accentColor.HasValue) profBody["accent_color"] = accentColor.Value;
                if (removeBanner) profBody["banner"] = null;
                else if (!string.IsNullOrWhiteSpace(bannerDataUri)) profBody["banner"] = bannerDataUri;
                using var pResp = await _http.PatchAsJsonAsync($"{ApiBase}/guilds/{guildId}/profile/@me", profBody, ct);
            }
            catch { }
        }
    }

    public async Task UpdateCustomStatusAsync(string? customStatusText, string? status = null, string? emojiName = null, string? emojiId = null, DateTimeOffset? expiresAt = null, CancellationToken ct = default)
    {
        try
        {
            var settingsBody = new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(status))
            {
                settingsBody["status"] = status;
            }
            if (customStatusText is not null)
            {
                if (string.IsNullOrWhiteSpace(customStatusText) && string.IsNullOrWhiteSpace(emojiName))
                {
                    settingsBody["custom_status"] = null;
                }
                else
                {
                    var csObj = new Dictionary<string, object?>();
                    if (!string.IsNullOrWhiteSpace(customStatusText)) csObj["text"] = customStatusText;
                    if (!string.IsNullOrWhiteSpace(emojiName)) csObj["emoji_name"] = emojiName;
                    if (!string.IsNullOrWhiteSpace(emojiId)) csObj["emoji_id"] = emojiId;
                    if (expiresAt.HasValue) csObj["expires_at"] = expiresAt.Value.ToUniversalTime().ToString("o");
                    settingsBody["custom_status"] = csObj;
                }
            }
            if (settingsBody.Count > 0)
            {
                using var resp = await _http.PatchAsJsonAsync($"{ApiBase}/users/@me/settings", settingsBody, ct);
            }
        }
        catch { }
    }


    public async Task<List<DiscordGuild>> GetGuildsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<DiscordGuild>>($"{ApiBase}/users/@me/guilds", ct) ?? new();

    public async Task<List<DiscordEmoji>> GetGuildEmojisAsync(string guildId, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<DiscordEmoji>>($"{ApiBase}/guilds/{guildId}/emojis", ct) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<List<DiscordGifItem>> GetTrendingGifsAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await FetchGifsFromUrlAsync($"{ApiBase}/gifs/trending-gifs?media_format=gif&locale=fr-FR", ct);
            if (res.Count > 0) return res;

            res = await FetchGifsFromUrlAsync($"{ApiBase}/gifs/trending-gifs?locale=fr-FR", ct);
            if (res.Count > 0) return res;

            res = await FetchGifsFromUrlAsync($"{ApiBase}/gifs/trending?media_format=gif&locale=fr-FR", ct);
            if (res.Count > 0) return res;

            res = await FetchGifsFromUrlAsync($"{ApiBase}/gifs/trending", ct);
            if (res.Count > 0) return res;
        }
        catch
        {
        }

        return GetFallbackTrendingGifs();
    }

    public async Task<List<DiscordGifItem>> SearchGifsAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Equals("trending", StringComparison.OrdinalIgnoreCase))
        {
            return await GetTrendingGifsAsync(ct);
        }

        try
        {
            var encoded = Uri.EscapeDataString(query);
            var res = await FetchGifsFromUrlAsync($"{ApiBase}/gifs/search?q={encoded}&media_format=gif&locale=fr-FR", ct);
            if (res.Count > 0) return res;

            res = await FetchGifsFromUrlAsync($"{ApiBase}/gifs/search?q={encoded}&locale=fr-FR", ct);
            if (res.Count > 0) return res;

            res = await FetchGifsFromUrlAsync($"{ApiBase}/gifs/search?q={encoded}", ct);
            if (res.Count > 0) return res;
        }
        catch
        {
        }

        var fallbacks = GetFallbackTrendingGifs();
        var matches = fallbacks.FindAll(g => g.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || g.Id.Contains(query, StringComparison.OrdinalIgnoreCase));
        return matches.Count > 0 ? matches : fallbacks;
    }

    private async Task<List<DiscordGifItem>> FetchGifsFromUrlAsync(string url, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return new();

            var json = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(json)) return new();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var list = new List<DiscordGifItem>();

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in root.EnumerateArray())
                {
                    try
                    {
                        var item = JsonSerializer.Deserialize<DiscordGifItem>(el.GetRawText(), jsonOptions);
                        if (item != null && !string.IsNullOrEmpty(item.PreviewUrl)) list.Add(item);
                    }
                    catch { }
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                JsonElement arrayEl = default;
                if (root.TryGetProperty("results", out var resEl) && resEl.ValueKind == JsonValueKind.Array)
                    arrayEl = resEl;
                else if (root.TryGetProperty("gifs", out var gifsEl) && gifsEl.ValueKind == JsonValueKind.Array)
                    arrayEl = gifsEl;
                else if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                    arrayEl = dataEl;

                if (arrayEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in arrayEl.EnumerateArray())
                    {
                        try
                        {
                            var item = JsonSerializer.Deserialize<DiscordGifItem>(el.GetRawText(), jsonOptions);
                            if (item != null && !string.IsNullOrEmpty(item.PreviewUrl)) list.Add(item);
                        }
                        catch { }
                    }
                }
            }

            return list;
        }
        catch
        {
            return new();
        }
    }

    private static List<DiscordGifItem> GetFallbackTrendingGifs()
    {
        return new List<DiscordGifItem>
        {
            new() { Id = "cat-dance", Title = "Happy Dancing Cat", Url = "https://klipy.com/gifs/cat-cute-cat-42", Src = "https://static.klipy.com/ii/35ccce3d852f7995dd2da910f2abd795/87/20/yfyke2Vc.gif", Preview = "https://static.klipy.com/ii/35ccce3d852f7995dd2da910f2abd795/87/20/yfyke2Vc.gif" },
            new() { Id = "bobacat-lazy", Title = "Bobacat Lazy Sunday", Url = "https://klipy.com/gifs/bobacat-psps", Src = "https://static.klipy.com/ii/35ccce3d852f7995dd2da910f2abd795/e7/a6/E1eZg4nu.gif", Preview = "https://static.klipy.com/ii/35ccce3d852f7995dd2da910f2abd795/e7/a6/E1eZg4nu.gif" },
            new() { Id = "batman-approve", Title = "Batman Approves", Url = "https://klipy.com/gifs/batman-approves-approve", Src = "https://static.klipy.com/ii/c3a19a0b747a76e98651f2b9a3cca5ff/c2/53/NlKhuAlk.gif", Preview = "https://static.klipy.com/ii/c3a19a0b747a76e98651f2b9a3cca5ff/c2/53/NlKhuAlk.gif" },
            new() { Id = "eevee-dance", Title = "Eevee Dance", Url = "https://klipy.com/gifs/eevee-dance-4", Src = "https://static.klipy.com/ii/39f2394ae36df6e199be9eb7c9fa1012/6a/0d/kWwW4YZN.gif", Preview = "https://static.klipy.com/ii/39f2394ae36df6e199be9eb7c9fa1012/6a/0d/kWwW4YZN.gif" },
            new() { Id = "shocked-cat", Title = "Shocked Cat Reaction", Url = "https://klipy.com/gifs/shocked-cat-12", Src = "https://static.klipy.com/ii/8ce8357c78ea940b9c2015daf05ce1a5/06/f3/8ZhpQ7co.gif", Preview = "https://static.klipy.com/ii/8ce8357c78ea940b9c2015daf05ce1a5/06/f3/8ZhpQ7co.gif" },
            new() { Id = "burrito-cat", Title = "Space Cat Burrito", Url = "https://klipy.com/gifs/burrito-27", Src = "https://static.klipy.com/ii/935d7ab9d8c6202580a668421940ec81/ec/6c/S3Uf5nar.gif", Preview = "https://static.klipy.com/ii/935d7ab9d8c6202580a668421940ec81/ec/6c/S3Uf5nar.gif" },
            new() { Id = "mrbeast-water", Title = "Mr Beast Water", Url = "https://klipy.com/gifs/mr-beast-water", Src = "https://static.klipy.com/ii/4e7bea9f7a3371424e6c16ebc93252fe/86/ac/WvE8QdYqN109iy3ji.gif", Preview = "https://static.klipy.com/ii/4e7bea9f7a3371424e6c16ebc93252fe/86/ac/WvE8QdYqN109iy3ji.gif" },
            new() { Id = "dobby-snap", Title = "Dobby Magic Snap", Url = "https://klipy.com/gifs/snap-dobby-2e8", Src = "https://static.klipy.com/ii/d7aec6f6f171607374b2065c836f92f4/73/16/7eoa5u7A.gif", Preview = "https://static.klipy.com/ii/d7aec6f6f171607374b2065c836f92f4/73/16/7eoa5u7A.gif" },
            new() { Id = "sukuna-fight", Title = "Ryomen Sukuna Fight", Url = "https://klipy.com/gifs/jogo-vs-sukuna-ryomen-sukuna", Src = "https://static.klipy.com/ii/c44064a00e4b7451969381d90dea1769/4f/c4/qb075Ez2.gif", Preview = "https://static.klipy.com/ii/c44064a00e4b7451969381d90dea1769/4f/c4/qb075Ez2.gif" },
            new() { Id = "spongebob-thought", Title = "Spongebob Deep in Thought", Url = "https://klipy.com/gifs/spongebob-spongebob-squarepants-86", Src = "https://static.klipy.com/ii/da290b156d64898341638f3c299e7478/f7/2d/z3sJQiSz.gif", Preview = "https://static.klipy.com/ii/da290b156d64898341638f3c299e7478/f7/2d/z3sJQiSz.gif" },
            new() { Id = "patrick-bateman", Title = "Patrick Bateman Walking Headphones", Url = "https://klipy.com/gifs/walking-headphones", Src = "https://static.klipy.com/ii/d7aec6f6f171607374b2065c836f92f4/a6/61/ONvqckXv.gif", Preview = "https://static.klipy.com/ii/d7aec6f6f171607374b2065c836f92f4/a6/61/ONvqckXv.gif" },
            new() { Id = "stitch-rain", Title = "Stitch In The Rain", Url = "https://klipy.com/gifs/stitch-lilo-and-stitch-sY2", Src = "https://static.klipy.com/ii/8ce8357c78ea940b9c2015daf05ce1a5/61/ee/u6YcWIoT.gif", Preview = "https://static.klipy.com/ii/8ce8357c78ea940b9c2015daf05ce1a5/61/ee/u6YcWIoT.gif" }
        };
    }

    /// <summary>
    /// Récupère l'ordre officiel des serveurs et dossiers tel que défini dans les paramètres utilisateur Discord (guild_folders / guild_positions).
    /// </summary>
    public async Task<List<string>> GetOrderedGuildIdsAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync($"{ApiBase}/users/@me/settings", ct);
            if (!resp.IsSuccessStatusCode) return new();

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return ExtractOrderedGuildIds(doc.RootElement);
        }
        catch
        {
            return new();
        }
    }

    public async Task<List<DiscordGuildFolderData>> GetGuildFoldersAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync($"{ApiBase}/users/@me/settings", ct);
            if (!resp.IsSuccessStatusCode) return new();

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return ExtractGuildFolders(doc.RootElement);
        }
        catch
        {
            return new();
        }
    }

    public static List<DiscordGuildFolderData> ExtractGuildFolders(JsonElement userSettings)
    {
        var list = new List<DiscordGuildFolderData>();
        if (userSettings.TryGetProperty("guild_folders", out var foldersEl) && foldersEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var folderEl in foldersEl.EnumerateArray())
            {
                var folder = new DiscordGuildFolderData();
                if (folderEl.TryGetProperty("id", out var idEl))
                {
                    if (idEl.ValueKind == JsonValueKind.Number) folder.Id = idEl.GetInt64();
                    else if (idEl.ValueKind == JsonValueKind.String) folder.Id = idEl.GetString();
                }
                if (folderEl.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                {
                    folder.Name = nameEl.GetString();
                }
                if (folderEl.TryGetProperty("color", out var colorEl) && colorEl.ValueKind == JsonValueKind.Number)
                {
                    folder.Color = colorEl.GetInt64();
                }
                if (folderEl.TryGetProperty("guild_ids", out var gIdsEl) && gIdsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var id in gIdsEl.EnumerateArray())
                    {
                        var s = id.GetString();
                        if (!string.IsNullOrEmpty(s)) folder.GuildIds.Add(s);
                    }
                }
                list.Add(folder);
            }
        }
        return list;
    }

    /// <summary>
    /// Extrait la liste ordonnée des identifiants de serveurs depuis un payload JSON user_settings (REST ou Gateway).
    /// </summary>
    public static List<string> ExtractOrderedGuildIds(JsonElement userSettings)
    {
        var list = new List<string>();

        // 1. guild_folders : liste ordonnée de dossiers contenant chacun un tableau "guild_ids"
        if (userSettings.TryGetProperty("guild_folders", out var foldersEl) && foldersEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var folder in foldersEl.EnumerateArray())
            {
                if (folder.TryGetProperty("guild_ids", out var gIdsEl) && gIdsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var idEl in gIdsEl.EnumerateArray())
                    {
                        var id = idEl.GetString();
                        if (!string.IsNullOrEmpty(id) && !list.Contains(id))
                        {
                            list.Add(id);
                        }
                    }
                }
            }
        }

        // 2. Repli sur guild_positions si guild_folders est absent ou vide
        if (list.Count == 0 && userSettings.TryGetProperty("guild_positions", out var positionsEl) && positionsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var idEl in positionsEl.EnumerateArray())
            {
                var id = idEl.GetString();
                if (!string.IsNullOrEmpty(id) && !list.Contains(id))
                {
                    list.Add(id);
                }
            }
        }

        return list;
    }

    public async Task<List<DiscordChannel>> GetGuildChannelsAsync(string guildId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<DiscordChannel>>($"{ApiBase}/guilds/{guildId}/channels", ct) ?? new();

    public async Task<List<DiscordChannel>> GetPrivateChannelsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<DiscordChannel>>($"{ApiBase}/users/@me/channels", ct) ?? new();

    public async Task<List<DiscordRelationship>> GetRelationshipsAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync($"{ApiBase}/users/@me/relationships", ct);
            if (!resp.IsSuccessStatusCode) return new();

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new();

            var list = new List<DiscordRelationship>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var rel = ParseRelationshipElement(el);
                if (rel is not null) list.Add(rel);
            }
            return list;
        }
        catch
        {
            return new();
        }
    }

    public static DiscordRelationship? ParseRelationshipElement(JsonElement el)
    {
        try
        {
            var rel = el.Deserialize<DiscordRelationship>(JsonOptions);
            if (rel is null) return null;

            string? userId = null;
            if (el.TryGetProperty("user_id", out var uidEl) && uidEl.ValueKind == JsonValueKind.String)
                userId = uidEl.GetString();

            if (rel.User == null) rel.User = new DiscordUser();
            if (string.IsNullOrWhiteSpace(rel.User.Id))
            {
                rel.User.Id = !string.IsNullOrWhiteSpace(userId) ? userId! : rel.Id;
            }
            if (string.IsNullOrWhiteSpace(rel.Id))
            {
                rel.Id = rel.User.Id;
            }

            if (el.TryGetProperty("nickname", out var nickEl) && nickEl.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(nickEl.GetString()))
            {
                if (string.IsNullOrWhiteSpace(rel.User.GlobalName))
                    rel.User.GlobalName = nickEl.GetString();
            }

            return string.IsNullOrWhiteSpace(rel.Id) ? null : rel;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<DiscordMessage>> GetMessagesAsync(string channelId, int limit = 50, string? beforeId = null, CancellationToken ct = default)
    {
        var url = $"{ApiBase}/channels/{channelId}/messages?limit={limit}";
        if (beforeId is not null) url += $"&before={beforeId}";
        return await _http.GetFromJsonAsync<List<DiscordMessage>>(url, ct) ?? new();
    }

    public async Task<DiscordMessage> SendMessageAsync(string channelId, string content, string? replyToMessageId = null, string? nonce = null, CancellationToken ct = default)
    {
        var payload = new SendMessageRequest
        {
            Content = content,
            Nonce = nonce ?? Guid.NewGuid().ToString("N"),
            MessageReference = string.IsNullOrWhiteSpace(replyToMessageId)
                ? null
                : new MessageReferencePayload { ChannelId = channelId, MessageId = replyToMessageId }
        };
        var response = await _http.PostAsJsonAsync($"{ApiBase}/channels/{channelId}/messages", payload, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiscordMessage>(cancellationToken: ct)
               ?? throw new InvalidOperationException("Réponse d'envoi de message illisible.");
    }

    public async Task<DiscordMessage> SendStickerAsync(string channelId, string stickerId, string? replyToMessageId = null, CancellationToken ct = default)
    {
        var payload = new SendMessageRequest
        {
            Content = string.Empty,
            Nonce = Guid.NewGuid().ToString("N"),
            StickerIds = new List<string> { stickerId },
            MessageReference = string.IsNullOrWhiteSpace(replyToMessageId)
                ? null
                : new MessageReferencePayload { ChannelId = channelId, MessageId = replyToMessageId }
        };
        var response = await _http.PostAsJsonAsync($"{ApiBase}/channels/{channelId}/messages", payload, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiscordMessage>(cancellationToken: ct)
               ?? throw new InvalidOperationException("Réponse d'envoi d'autocollant illisible.");
    }

    public async Task<List<DiscordStickerPack>> GetStickerPacksAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<DiscordStickerPacksResponse>($"{ApiBase}/sticker-packs?locale=fr", cancellationToken: ct);
            return res?.StickerPacks ?? new List<DiscordStickerPack>();
        }
        catch
        {
            return new List<DiscordStickerPack>();
        }
    }

    public async Task<List<DiscordSticker>> GetGuildStickersAsync(string guildId, CancellationToken ct = default)
    {
        try
        {
            var stickers = await _http.GetFromJsonAsync<List<DiscordSticker>>($"{ApiBase}/guilds/{guildId}/stickers", cancellationToken: ct);
            return stickers ?? new List<DiscordSticker>();
        }
        catch
        {
            return new List<DiscordSticker>();
        }
    }

    public async Task<List<DiscordDiscoverableGuild>> GetDiscoverableGuildsAsync(string? query = null, int category = 0, CancellationToken ct = default)
    {
        try
        {
            var url = $"{ApiBase}/discoverable-guilds?offset=0&limit=30";
            if (!string.IsNullOrWhiteSpace(query)) url += $"&query={Uri.EscapeDataString(query)}";
            if (category > 0) url += $"&categories={category}";

            using var resp = await _http.GetAsync(url, ct);
            if (resp.IsSuccessStatusCode)
            {
                var data = await resp.Content.ReadFromJsonAsync<DiscordDiscoverableGuildsResponse>(cancellationToken: ct);
                if (data?.Guilds != null && data.Guilds.Count > 0)
                {
                    return data.Guilds;
                }
            }
        }
        catch { }

        return GetFeaturedDiscoverableGuilds(query, category);
    }

    private static List<DiscordDiscoverableGuild> GetFeaturedDiscoverableGuilds(string? query, int category)
    {
        var featured = new List<DiscordDiscoverableGuild>
        {
            new()
            {
                Id = "302094807424679936",
                Name = "Minecraft",
                Description = "Le serveur officiel de la communauté Minecraft ! Partagez vos constructions, vos aventures et trouvez des amis.",
                Icon = "a_5828469a47da2f5b66dcf15b13e9a1aa",
                ApproximateMemberCount = 1042500,
                ApproximatePresenceCount = 215400,
                VanityUrlCode = "minecraft",
                Keywords = new() { "minecraft", "gaming", "mojang", "survie" },
                Features = new() { "VERIFIED", "PARTNER", "COMMUNITY", "DISCOVERABLE" },
                Category = "Gaming"
            },
            new()
            {
                Id = "139565116151562240",
                Name = "Roblox",
                Description = "La communauté officielle Discord pour les développeurs, créateurs et joueurs Roblox !",
                Icon = "9b6ea17ebc7921f9175aa1317ba85a97",
                ApproximateMemberCount = 890000,
                ApproximatePresenceCount = 182300,
                VanityUrlCode = "roblox",
                Keywords = new() { "roblox", "gaming", "dev", "studio" },
                Features = new() { "VERIFIED", "PARTNER", "COMMUNITY", "DISCOVERABLE" },
                Category = "Gaming"
            },
            new()
            {
                Id = "662267976984297473",
                Name = "Midjourney",
                Description = "Le serveur officiel de Midjourney. Générez des images incroyables avec l'intelligence artificielle.",
                Icon = "35f79bf7ee8b2ea5379b1836a9b400ee",
                ApproximateMemberCount = 20500000,
                ApproximatePresenceCount = 1250000,
                VanityUrlCode = "midjourney",
                Keywords = new() { "midjourney", "ia", "art", "tech", "design" },
                Features = new() { "VERIFIED", "COMMUNITY", "DISCOVERABLE" },
                Category = "Tech"
            },
            new()
            {
                Id = "267624335836495872",
                Name = "Python",
                Description = "La plus grande communauté d'entraide, de partage et de tutoriels pour les développeurs Python.",
                Icon = "c460d6a26cf6f3ff8085f1c97a2d4be0",
                ApproximateMemberCount = 420000,
                ApproximatePresenceCount = 85000,
                VanityUrlCode = "python",
                Keywords = new() { "python", "code", "dev", "programming", "tech" },
                Features = new() { "VERIFIED", "COMMUNITY", "DISCOVERABLE" },
                Category = "Tech"
            },
            new()
            {
                Id = "143867839282020352",
                Name = "C# / .NET Community",
                Description = "La communauté Discord dédiée au langage C#, .NET, ASP.NET, WinUI 3 et au développement logiciel.",
                Icon = "a_255018653912788e9bb51f7bbdb04169",
                ApproximateMemberCount = 185000,
                ApproximatePresenceCount = 38000,
                VanityUrlCode = "csharp",
                Keywords = new() { "csharp", "dotnet", "microsoft", "windows", "dev" },
                Features = new() { "PARTNER", "COMMUNITY", "DISCOVERABLE" },
                Category = "Tech"
            },
            new()
            {
                Id = "737189254396002344",
                Name = "Valorant France",
                Description = "Le point de rassemblement des joueurs francophones de VALORANT : tournois, actus et recherche de coéquipiers.",
                Icon = "c6508f7aa11fb627254580bfbcebcf38",
                ApproximateMemberCount = 230000,
                ApproximatePresenceCount = 47500,
                VanityUrlCode = "valorant-fr",
                Keywords = new() { "valorant", "riot", "fps", "gaming", "esport" },
                Features = new() { "PARTNER", "COMMUNITY", "DISCOVERABLE" },
                Category = "Gaming"
            },
            new()
            {
                Id = "483259882255745025",
                Name = "Lofi Girl Café",
                Description = "Détendez-vous, étudiez et écoutez les meilleurs rythmes lofi hip hop en direct 24/7.",
                Icon = "9b646c86725287e076df3d2eef297891",
                ApproximateMemberCount = 850000,
                ApproximatePresenceCount = 135000,
                VanityUrlCode = "lofigirl",
                Keywords = new() { "lofi", "music", "chill", "study", "relax" },
                Features = new() { "VERIFIED", "COMMUNITY", "DISCOVERABLE" },
                Category = "Musique"
            },
            new()
            {
                Id = "974519864045756446",
                Name = "OpenAI",
                Description = "Rejoignez la communauté des développeurs et passionnés de ChatGPT, GPT-4 et de l'IA générative.",
                Icon = "0e86b240ff8373b9e4a3bfec6c74fbba",
                ApproximateMemberCount = 4900000,
                ApproximatePresenceCount = 680000,
                VanityUrlCode = "openai",
                Keywords = new() { "openai", "chatgpt", "ia", "tech", "dev" },
                Features = new() { "VERIFIED", "COMMUNITY", "DISCOVERABLE" },
                Category = "Tech"
            },
            new()
            {
                Id = "762291584347701258",
                Name = "Genshin Impact Official",
                Description = "La communauté officielle Genshin Impact : guides, événements, fanarts et aventures en Teyvat !",
                Icon = "e03ee23b0dfb920e816a3bc5943b0001",
                ApproximateMemberCount = 1200000,
                ApproximatePresenceCount = 260000,
                VanityUrlCode = "genshinimpact",
                Keywords = new() { "genshin", "hoyoverse", "anime", "gaming", "rpg" },
                Features = new() { "VERIFIED", "PARTNER", "COMMUNITY", "DISCOVERABLE" },
                Category = "Gaming"
            },
            new()
            {
                Id = "613425648685711360",
                Name = "Discord Developers",
                Description = "Le serveur officiel de documentation et d'entraide pour la création de bots, d'applications et de jeux Discord.",
                Icon = "61d2b8de5b1300947e44a04d3e5aa4ab",
                ApproximateMemberCount = 280000,
                ApproximatePresenceCount = 62000,
                VanityUrlCode = "discord-developers",
                Keywords = new() { "discord", "api", "bots", "dev", "tech" },
                Features = new() { "VERIFIED", "COMMUNITY", "DISCOVERABLE" },
                Category = "Tech"
            }
        };

        var result = featured;
        if (category > 0)
        {
            var catName = category switch
            {
                1 => "Gaming",
                2 => "Musique",
                3 => "Tech",
                4 => "Éducation",
                _ => ""
            };
            if (!string.IsNullOrEmpty(catName))
            {
                result = result.Where(g => g.Category == catName).ToList();
            }
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            result = result.Where(g => g.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                                    || (g.Description != null && g.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
                                    || (g.Keywords != null && g.Keywords.Any(k => k.Contains(q, StringComparison.OrdinalIgnoreCase)))).ToList();
        }

        return result;
    }

    public async Task AckMessageAsync(string channelId, string messageId, CancellationToken ct = default)
    {
        try
        {
            var payload = new { token = (string?)null };
            await _http.PostAsJsonAsync($"{ApiBase}/channels/{channelId}/messages/{messageId}/ack", payload, ct);
        }
        catch
        {
            // Ignore ack failures
        }
    }

    public async Task AddReactionAsync(string channelId, string messageId, string emoji, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(emoji);
        var response = await _http.PutAsync($"{ApiBase}/channels/{channelId}/messages/{messageId}/reactions/{encoded}/@me", content: null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveReactionAsync(string channelId, string messageId, string emoji, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(emoji);
        var response = await _http.DeleteAsync($"{ApiBase}/channels/{channelId}/messages/{messageId}/reactions/{encoded}/@me", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DiscordMessage> EditMessageAsync(string channelId, string messageId, string content, CancellationToken ct = default)
    {
        var payload = new { content };
        var response = await _http.PatchAsJsonAsync($"{ApiBase}/channels/{channelId}/messages/{messageId}", payload, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiscordMessage>(cancellationToken: ct)
               ?? throw new InvalidOperationException("Réponse d'édition illisible.");
    }

    public async Task<DiscordMessage> SendFileAsync(string channelId, string filePath, string? content = null, bool anonymizeFilename = false, CancellationToken ct = default)
    {
        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath, ct);
        var ext = System.IO.Path.GetExtension(filePath);
        var fileName = anonymizeFilename
            ? $"upload_{Guid.NewGuid().ToString("N")[..8]}{ext}"
            : System.IO.Path.GetFileName(filePath);
        return await SendBytesAsync(channelId, fileBytes, fileName, content, ct);
    }

    public async Task<DiscordMessage> SendBytesAsync(string channelId, byte[] fileBytes, string fileName, string? content = null, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        if (!string.IsNullOrEmpty(content))
        {
            form.Add(new StringContent(content), "content");
        }
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", fileName);

        var response = await _http.PostAsync($"{ApiBase}/channels/{channelId}/messages", form, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiscordMessage>(cancellationToken: ct)
               ?? throw new InvalidOperationException("Réponse d'envoi de fichier illisible.");
    }

    public async Task DeleteMessageAsync(string channelId, string messageId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"{ApiBase}/channels/{channelId}/messages/{messageId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<DiscordMessage>> GetPinnedMessagesAsync(string channelId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<DiscordMessage>>($"{ApiBase}/channels/{channelId}/pins", ct) ?? new();

    public async Task PinMessageAsync(string channelId, string messageId, CancellationToken ct = default)
    {
        var response = await _http.PutAsync($"{ApiBase}/channels/{channelId}/pins/{messageId}", content: null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UnpinMessageAsync(string channelId, string messageId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"{ApiBase}/channels/{channelId}/pins/{messageId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<DiscordMessage>> GetMentionsAsync(int limit = 25, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<DiscordMessage>>($"{ApiBase}/users/@me/mentions?limit={limit}&roles=true&everyone=true", ct) ?? new();

    public async Task<List<DiscordMessage>> SearchChannelMessagesAsync(string channelId, string query, CancellationToken ct = default)
    {
        try
        {
            var encoded = Uri.EscapeDataString(query);
            var res = await _http.GetFromJsonAsync<DiscordSearchResponse>($"{ApiBase}/channels/{channelId}/messages/search?content={encoded}", ct);
            if (res?.Messages != null)
            {
                var list = new List<DiscordMessage>();
                foreach (var group in res.Messages)
                {
                    if (group != null && group.Count > 0)
                    {
                        list.Add(group[0]);
                    }
                }
                return list;
            }
        }
        catch
        {
            // Ignore search API failure (e.g. rate limit or indexing in progress)
        }
        return new();
    }

    public static readonly List<DiscordSoundboardSound> FallbackDefaultSounds = new()
    {
        new() { SoundId = "1", Name = "Coin-coin", EmojiName = "🦆", Volume = 1.0 },
        new() { SoundId = "2", Name = "Klaxon de fête", EmojiName = "📢", Volume = 1.0 },
        new() { SoundId = "3", Name = "Grillons", EmojiName = "🦗", Volume = 1.0 },
        new() { SoundId = "4", Name = "Applaudissements", EmojiName = "👏", Volume = 1.0 },
        new() { SoundId = "5", Name = "Ba-Dum-Tss", EmojiName = "🥁", Volume = 1.0 },
        new() { SoundId = "6", Name = "Klaxon triste", EmojiName = "📯", Volume = 1.0 },
    };

    public async Task<List<DiscordSoundboardSound>> GetDefaultSoundboardSoundsAsync(CancellationToken ct = default)
    {
        try
        {
            var sounds = await _http.GetFromJsonAsync<List<DiscordSoundboardSound>>($"{ApiBase}/soundboard-default-sounds", ct);
            if (sounds != null && sounds.Count > 0) return sounds;
        }
        catch { }
        return FallbackDefaultSounds;
    }

    public async Task<List<DiscordSoundboardSound>> GetGuildSoundboardSoundsAsync(string guildId, CancellationToken ct = default)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<DiscordSoundboardGuildResponse>($"{ApiBase}/guilds/{guildId}/soundboard-sounds", ct);
            if (res?.Items != null) return res.Items;
        }
        catch { }
        return new();
    }

    public async Task SendVoiceChannelEffectAsync(string channelId, string soundId, string? sourceGuildId = null, string? emojiName = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                sound_id = soundId,
                source_guild_id = sourceGuildId,
                emoji_name = emojiName,
                animation_type = 0,
                animation_id = 0
            };
            var response = await _http.PostAsJsonAsync($"{ApiBase}/channels/{channelId}/voice-channel-effects", payload, ct);
            response.EnsureSuccessStatusCode();
        }
        catch { }
    }

    public async Task<List<DiscordChannel>> GetActiveThreadsAsync(string channelId, CancellationToken ct = default)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<DiscordActiveThreadsResponse>($"{ApiBase}/channels/{channelId}/threads/active", ct);
            if (res?.Threads != null) return res.Threads;
        }
        catch { }
        return new();
    }

    public async Task<DiscordChannel?> CreateThreadFromMessageAsync(string channelId, string messageId, string name, int autoArchiveDuration = 1440, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                name = name,
                auto_archive_duration = autoArchiveDuration
            };
            var response = await _http.PostAsJsonAsync($"{ApiBase}/channels/{channelId}/messages/{messageId}/threads", payload, ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<DiscordChannel>(cancellationToken: ct);
            }
        }
        catch { }
        return null;
    }

    public async Task<DiscordChannel?> CreateThreadAsync(string channelId, string name, int autoArchiveDuration = 1440, int type = 11, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                name = name,
                auto_archive_duration = autoArchiveDuration,
                type = type
            };
            var response = await _http.PostAsJsonAsync($"{ApiBase}/channels/{channelId}/threads", payload, ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<DiscordChannel>(cancellationToken: ct);
            }
        }
        catch { }
        return null;
    }

    public async Task<DiscordChannel?> CreateForumPostAsync(string channelId, string title, string content, List<string>? appliedTags = null, int autoArchiveDuration = 4320, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                name = title,
                auto_archive_duration = autoArchiveDuration,
                applied_tags = appliedTags ?? new List<string>(),
                message = new
                {
                    content = content
                }
            };
            var response = await _http.PostAsJsonAsync($"{ApiBase}/channels/{channelId}/threads", payload, ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<DiscordChannel>(cancellationToken: ct);
            }
        }
        catch { }
        return null;
    }

    public async Task<List<DiscordChannel>> GetArchivedThreadsAsync(string channelId, CancellationToken ct = default)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<DiscordActiveThreadsResponse>($"{ApiBase}/channels/{channelId}/threads/archived/public", ct);
            if (res?.Threads != null) return res.Threads;
        }
        catch { }
        return new();
    }



    public async Task<DiscordChannel?> CreateDmAsync(string recipientUserId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"{ApiBase}/users/@me/channels", new { recipient_id = recipientUserId }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiscordChannel>(cancellationToken: ct);
    }

    public async Task SendFriendRequestAsync(string username, CancellationToken ct = default)
    {
        var clean = (username ?? string.Empty).Trim();
        if (clean.StartsWith('@')) clean = clean[1..].Trim();
        if (string.IsNullOrWhiteSpace(clean))
            throw new InvalidOperationException("Veuillez saisir un nom d'utilisateur valide.");

        // Si l'utilisateur a saisi un ID numérique Discord (Snowflake 17-20 chiffres), utiliser directement PUT /users/@me/relationships/{id}
        if (clean.Length >= 16 && clean.Length <= 21 && clean.All(char.IsDigit))
        {
            await PutRelationshipAsync(clean, 1, ct);
            return;
        }

        object payload;
        int hashIdx = clean.IndexOf('#');
        if (hashIdx > 0 && hashIdx < clean.Length - 1)
        {
            var namePart = clean[..hashIdx].Trim();
            var discPart = clean[(hashIdx + 1)..].Trim();
            if (discPart == "0" || discPart == "0000")
            {
                payload = new { username = namePart, discriminator = (int?)null };
            }
            else if (int.TryParse(discPart, out int discNum))
            {
                payload = new { username = namePart, discriminator = discNum };
            }
            else
            {
                payload = new { username = namePart, discriminator = (int?)null };
            }
        }
        else
        {
            payload = new { username = clean, discriminator = (int?)null };
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/users/@me/relationships")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.TryAddWithoutValidation("X-Context-Properties",
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"location\":\"Add Friend\"}")));

        using var response = await _http.SendAsync(req, ct);
        if (!response.IsSuccessStatusCode)
        {
            string errBody = string.Empty;
            try { errBody = await response.Content.ReadAsStringAsync(ct); } catch { }
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(errBody)
                    ? $"Échec de l'envoi de la demande d'ami ({(int)response.StatusCode})."
                    : $"Discord a refusé la demande : {errBody}");
        }
    }

    public async Task PutRelationshipAsync(string userId, int type = 1, CancellationToken ct = default)
    {
        object payload = type == 1 ? new { } : new { type };
        using var req = new HttpRequestMessage(HttpMethod.Put, $"{ApiBase}/users/@me/relationships/{userId}")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.TryAddWithoutValidation("X-Context-Properties",
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"location\":\"Friends\"}")));

        using var response = await _http.SendAsync(req, ct);
        if (!response.IsSuccessStatusCode)
        {
            var responseFallback = await _http.PutAsJsonAsync($"{ApiBase}/users/@me/relationships/{userId}", new { type }, ct);
            responseFallback.EnsureSuccessStatusCode();
        }
    }

    public async Task RingCallAsync(string channelId, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.PostAsJsonAsync($"{ApiBase}/channels/{channelId}/call/ring", new { recipients = (object?)null }, ct);
        }
        catch { }
    }

    public async Task RemoveRelationshipAsync(string userId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"{ApiBase}/users/@me/relationships/{userId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DiscordGuild?> JoinInviteAsync(string inviteCodeOrUrl, CancellationToken ct = default)
    {
        var code = ExtractInviteCode(inviteCodeOrUrl);
        var response = await _http.PostAsJsonAsync($"{ApiBase}/invites/{code}", new { }, ct);
        response.EnsureSuccessStatusCode();
        try
        {
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("guild", out var guildElem))
            {
                return System.Text.Json.JsonSerializer.Deserialize<DiscordGuild>(guildElem.GetRawText());
            }
        }
        catch { }
        return null;
    }

    public async Task<string> CreateChannelInviteAsync(string channelId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"{ApiBase}/channels/{channelId}/invites", new { max_age = 86400, max_uses = 0 }, ct);
        response.EnsureSuccessStatusCode();
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var code = doc.RootElement.TryGetProperty("code", out var c) ? c.GetString() : null;
        return string.IsNullOrEmpty(code) ? string.Empty : $"https://discord.gg/{code}";
    }

    public async Task<DiscordGuild> CreateGuildAsync(
        string name,
        string? iconBase64 = null,
        List<Dictionary<string, object>>? channels = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = name.Trim(),
        };
        if (!string.IsNullOrWhiteSpace(iconBase64))
        {
            payload["icon"] = iconBase64;
        }
        if (channels != null && channels.Count > 0)
        {
            payload["channels"] = channels;
        }

        var response = await _http.PostAsJsonAsync($"{ApiBase}/guilds", payload, ct);
        response.EnsureSuccessStatusCode();
        var guild = await response.Content.ReadFromJsonAsync<DiscordGuild>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Impossible de lire la réponse du serveur créé.");
        return guild;
    }

    public async Task<DiscordChannel> CreateGuildChannelAsync(
        string guildId,
        string name,
        DiscordChannelType type = DiscordChannelType.GuildText,
        string? topic = null,
        string? parentId = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = name.Trim(),
            ["type"] = (int)type,
        };
        if (!string.IsNullOrWhiteSpace(topic)) payload["topic"] = topic.Trim();
        if (!string.IsNullOrWhiteSpace(parentId)) payload["parent_id"] = parentId;

        var response = await _http.PostAsJsonAsync($"{ApiBase}/guilds/{guildId}/channels", payload, ct);
        response.EnsureSuccessStatusCode();
        var channel = await response.Content.ReadFromJsonAsync<DiscordChannel>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Impossible de lire la réponse du salon créé.");
        channel.GuildId ??= guildId;
        return channel;
    }

    public async Task<bool> DeleteChannelAsync(string channelId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"{ApiBase}/channels/{channelId}", ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<DiscordChannel> ModifyChannelAsync(
        string channelId,
        string? name = null,
        string? topic = null,
        int? position = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(name)) payload["name"] = name.Trim();
        if (topic != null) payload["topic"] = topic.Trim();
        if (position.HasValue) payload["position"] = position.Value;

        var response = await _http.PatchAsJsonAsync($"{ApiBase}/channels/{channelId}", payload, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiscordChannel>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Impossible de lire la réponse du salon modifié.");
    }

    public async Task<DiscordChannel> ModifyGroupDmAsync(
        string channelId,
        string? name = null,
        string? iconDataUri = null,
        bool removeIcon = false,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>();
        if (name != null) payload["name"] = name.Trim();
        if (removeIcon) payload["icon"] = null;
        else if (!string.IsNullOrWhiteSpace(iconDataUri)) payload["icon"] = iconDataUri;

        var response = await _http.PatchAsJsonAsync($"{ApiBase}/channels/{channelId}", payload, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiscordChannel>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Impossible de lire la reponse du groupe modifie.");
    }

    public async Task<bool> LeaveGroupDmAsync(string channelId, bool silent = false, CancellationToken ct = default)
    {
        var url = $"{ApiBase}/channels/{channelId}?silent=" + (silent ? "true" : "false");
        var response = await _http.DeleteAsync(url, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task LeaveGuildAsync(string guildId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"{ApiBase}/users/@me/guilds/{guildId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DiscordMessage> CreatePollMessageAsync(
        string channelId,
        string question,
        List<string> answers,
        int durationHours = 24,
        bool allowMultiselect = false,
        CancellationToken ct = default)
    {
        var pollAnswers = answers
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select((a, idx) => new
            {
                answer_id = idx + 1,
                poll_media = new { text = a.Trim() }
            })
            .ToArray();

        var payload = new
        {
            content = "",
            nonce = Guid.NewGuid().ToString("N"),
            poll = new
            {
                question = new { text = question.Trim() },
                answers = pollAnswers,
                duration = Math.Clamp(durationHours, 1, 768),
                allow_multiselect = allowMultiselect,
                layout_type = 1
            }
        };

        var response = await _http.PostAsJsonAsync($"{ApiBase}/channels/{channelId}/messages", payload, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiscordMessage>(cancellationToken: ct)
               ?? throw new InvalidOperationException("Réponse de création de sondage illisible.");
    }

    public async Task TriggerTypingAsync(string channelId, CancellationToken ct = default) =>
        await _http.PostAsync($"{ApiBase}/channels/{channelId}/typing", content: null, ct);

    public void Dispose() => _http.Dispose();

    private static string ExtractInviteCode(string raw)
    {
        var trimmed = raw.Trim().TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        return lastSlash >= 0 ? trimmed[(lastSlash + 1)..] : trimmed;
    }

    public async Task<JsonElement?> GetGuildPreviewOrProfileRawAsync(string guildId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(guildId)) return null;

        // 1. Tenter GET /guilds/{guildId}/preview
        try
        {
            using var resp = await _http.GetAsync($"{ApiBase}/guilds/{guildId}/preview", ct);
            if (resp.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                return doc.RootElement.Clone();
            }
        }
        catch { }

        // 2. Tenter GET /guilds/{guildId}?with_counts=true
        try
        {
            using var resp = await _http.GetAsync($"{ApiBase}/guilds/{guildId}?with_counts=true", ct);
            if (resp.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                return doc.RootElement.Clone();
            }
        }
        catch { }

        // 3. Tenter GET /guilds/{guildId}/basic
        try
        {
            using var resp = await _http.GetAsync($"{ApiBase}/guilds/{guildId}/basic", ct);
            if (resp.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                return doc.RootElement.Clone();
            }
        }
        catch { }

        // 4. Tenter GET /guilds/{guildId}/profile
        try
        {
            using var resp = await _http.GetAsync($"{ApiBase}/guilds/{guildId}/profile", ct);
            if (resp.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                return doc.RootElement.Clone();
            }
        }
        catch { }

        return null;
    }

    private sealed class SendMessageRequest
    {
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
        [JsonPropertyName("nonce")] public string Nonce { get; set; } = string.Empty;
        [JsonPropertyName("tts")] public bool Tts { get; set; } = false;
        [JsonPropertyName("sticker_ids")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? StickerIds { get; set; }
        [JsonPropertyName("message_reference")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MessageReferencePayload? MessageReference { get; set; }
    }

    private static void EnrichBadges(DiscordUser user, JsonElement root)
    {
        user.Badges ??= new List<DiscordBadge>();

        // Ensure each badge has a valid tooltip description
        foreach (var b in user.Badges)
        {
            if (string.IsNullOrWhiteSpace(b.Description))
            {
                b.Description = b.Id.ToLowerInvariant() switch
                {
                    "staff" => "Employé Discord",
                    "partner" => "Propriétaire d'un serveur partenaire",
                    "hypesquad" => "Événements HypeSquad",
                    "bug_hunter_level_1" => "Chasseur de bugs Discord",
                    "bug_hunter_level_2" => "Chasseur de bugs certifié Discord",
                    "hypesquad_house_1" => "HypeSquad Bravery",
                    "hypesquad_house_2" => "HypeSquad Brilliance",
                    "hypesquad_house_3" => "HypeSquad Balance",
                    "early_supporter" => "Soutien de la première heure",
                    "active_developer" => "Développeur actif",
                    "verified_developer" or "early_verified_bot_developer" => "Développeur de bots vérifié de la première heure",
                    "certified_moderator" => "Modérateur certifié Discord",
                    var s when s.StartsWith("guild_booster") => "Booster de serveur",
                    var s when s.StartsWith("premium") => "Abonné Discord Nitro",
                    var s when s.Contains("gift") || s.Contains("gild") => "Gifting Icon",
                    _ => "Badge Discord"
                };
            }
            else if (b.Id.Contains("gift", StringComparison.OrdinalIgnoreCase) || b.Id.Contains("gild", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(b.Description)) b.Description = "Gifting Icon";
            }
        }

        // Evolutive Nitro Badge
        if (user.HasFullNitro || !string.IsNullOrWhiteSpace(user.PremiumSince))
        {
            if (user.Badges.All(b => !b.Id.StartsWith("premium", StringComparison.OrdinalIgnoreCase)))
            {
                string desc = "Abonné Discord Nitro";
                string iconHash = "2ba85e8026a8614b640c2837bcd2177e"; // Tier 1 par défaut
                if (!string.IsNullOrWhiteSpace(user.PremiumSince) && DateTimeOffset.TryParse(user.PremiumSince, out var pDate))
                {
                    int totalMonths = Math.Max(1, (DateTime.UtcNow.Year - pDate.Year) * 12 + DateTime.UtcNow.Month - pDate.Month);
                    desc = $"Abonné depuis le {pDate.ToString("d MMMM yyyy", new System.Globalization.CultureInfo("fr-FR"))}";
                    iconHash = totalMonths switch
                    {
                        >= 24 => "a2862d66ffab578a169b600f7236d396", // 24+ mois
                        >= 18 => "32a1e1e913a45c3639433db9b7aa4bc3", // 18 mois
                        >= 15 => "c57176a9a08e9a6cf71ee1b31ebef1b4", // 15 mois
                        >= 12 => "0f90e54457e5e791e84a2731174cc73f", // 1 an
                        >= 9 => "8667a421443217469a71be76007b7194",  // 9 mois
                        >= 6 => "914fd8d810ff424b5952f481c03da6b3",  // 6 mois
                        >= 3 => "3aa41de486fa12454c3761e8e223442e",  // 3 mois
                        >= 2 => "011940fd013da3f7fb926e4a1cd2e618",  // 2 mois
                        _ => "2ba85e8026a8614b640c2837bcd2177e"     // 1 mois
                    };
                }
                user.Badges.Add(new DiscordBadge
                {
                    Id = "premium",
                    Description = desc,
                    Icon = iconHash,
                    Link = "https://discord.com/settings/premium"
                });
            }
        }

        // Evolutive Server Booster Badge
        if (!string.IsNullOrWhiteSpace(user.PremiumGuildSince))
        {
            if (user.Badges.All(b => !b.Id.StartsWith("guild_booster", StringComparison.OrdinalIgnoreCase)))
            {
                string desc = "Booste un serveur Discord";
                string iconHash = "51241296b0b40d403c99b837b471482e";
                if (DateTimeOffset.TryParse(user.PremiumGuildSince, out var bDate))
                {
                    int totalMonths = Math.Max(1, (DateTime.UtcNow.Year - bDate.Year) * 12 + DateTime.UtcNow.Month - bDate.Month);
                    desc = $"Booste un serveur depuis le {bDate.ToString("d MMMM yyyy", new System.Globalization.CultureInfo("fr-FR"))}";
                    iconHash = totalMonths switch
                    {
                        >= 24 => "a2862d66ffab578a169b600f7236d396",
                        >= 18 => "c57176a9a08e9a6cf71ee1b31ebef1b4",
                        >= 15 => "0f90e54457e5e791e84a2731174cc73f",
                        >= 12 => "8667a421443217469a71be76007b7194",
                        >= 9 => "914fd8d810ff424b5952f481c03da6b3",
                        >= 6 => "3aa41de486fa12454c3761e8e223442e",
                        >= 3 => "2ba85e8026a8614b640c2837bcd2177e",
                        >= 2 => "404c0ea238ca790a3598d6c70126a117",
                        _ => "51241296b0b40d403c99b837b471482e"
                    };
                }
                user.Badges.Add(new DiscordBadge
                {
                    Id = "guild_booster",
                    Description = desc,
                    Icon = iconHash
                });
            }
        }

        // Diswin User Badge
        if (user.IsCurrentUser && user.Badges.All(b => b.Id != "diswin_user"))
        {
            user.Badges.Insert(0, new DiscordBadge
            {
                Id = "diswin_user",
                Description = "Diswin User",
                Icon = "ms-appx:///Assets/Badges/diswin_user.png",
                Link = "https://github.com/djellil/diswin"
            });
        }
    }

    private sealed class MessageReferencePayload
    {
        [JsonPropertyName("channel_id")] public string ChannelId { get; set; } = string.Empty;
        [JsonPropertyName("message_id")] public string MessageId { get; set; } = string.Empty;
    }
}



