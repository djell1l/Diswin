using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
namespace DiscordNative.Protocol.Models;
public sealed class DiscordMessage : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("type")]
    public int Type { get; set; }
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = string.Empty;
    [JsonPropertyName("guild_id")]
    public string? GuildId { get; set; }
    [JsonPropertyName("author")]
    public DiscordUser Author { get; set; } = new();
    private DiscordGuildMember? _member;
    [JsonPropertyName("member")]
    public DiscordGuildMember? Member
    {
        get => _member;
        set
        {
            _member = value;
            if (_member != null && Author != null)
            {
                if (_member.Roles != null && _member.Roles.Count > 0)
                {
                    Author.Roles = _member.Roles;
                }
                if (!string.IsNullOrEmpty(_member.Nick))
                {
                    Author.ServerNick = _member.Nick;
                }
                if (!string.IsNullOrEmpty(_member.Avatar))
                {
                    Author.ServerAvatar = _member.Avatar;
                }
            }
        }
    }
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }
    [JsonPropertyName("edited_timestamp")]
    public DateTimeOffset? EditedTimestamp { get; set; }
    [JsonPropertyName("pinned")]
    public bool Pinned { get; set; }
    private bool _isHighlighted;
    [JsonIgnore]
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            if (_isHighlighted != value)
            {
                _isHighlighted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HighlightBackgroundBrushHex));
            }
        }
    }
    [JsonIgnore]
    public string HighlightBackgroundBrushHex => _isHighlighted ? "#2A5865F2" : "Transparent";
    private List<DiscordAttachment> _attachments = new();
    [JsonPropertyName("attachments")]
    public List<DiscordAttachment> Attachments
    {
        get
        {
            foreach (var a in _attachments) a.ParentMessage = this;
            return _attachments;
        }
        set
        {
            _attachments = value ?? new();
            foreach (var a in _attachments) a.ParentMessage = this;
        }
    }
    private List<DiscordEmbed> _embeds = new();
    [JsonPropertyName("embeds")]
    public List<DiscordEmbed> Embeds
    {
        get
        {
            foreach (var e in _embeds) e.ParentMessage = this;
            return _embeds;
        }
        set
        {
            _embeds = value ?? new();
            foreach (var e in _embeds) e.ParentMessage = this;
        }
    }
    [JsonPropertyName("sticker_items")]
    public List<DiscordStickerItem> StickerItems { get; set; } = new();
    [JsonPropertyName("reactions")]
    public List<DiscordReaction> Reactions { get; set; } = new();
    [JsonPropertyName("mentions")]
    public List<DiscordUser> Mentions { get; set; } = new();
    [JsonPropertyName("mention_everyone")]
    public bool MentionEveryone { get; set; }
    [JsonPropertyName("mention_roles")]
    public List<string> MentionRoles { get; set; } = new();
    [JsonPropertyName("referenced_message")]
    public DiscordMessage? ReferencedMessage { get; set; }
    [JsonPropertyName("interaction")]
    public DiscordMessageInteraction? Interaction { get; set; }
    [JsonPropertyName("interaction_metadata")]
    public DiscordMessageInteractionMetadata? InteractionMetadata { get; set; }
    [JsonPropertyName("poll")]
    public DiscordPoll? Poll { get; set; }
    [JsonPropertyName("nonce")]
    public object? Nonce { get; set; }
    [JsonPropertyName("call")]
    public DiscordCallMetadata? Call { get; set; }
    // ---------- Propriétés calculées pour l'UI ----------
    [JsonIgnore] public string? GuildName { get; set; }
    [JsonIgnore] public string? ChannelName { get; set; }
    [JsonIgnore] public string? GuildIconUrl { get; set; }
    [JsonIgnore] public bool HasGuildIcon => !string.IsNullOrEmpty(GuildIconUrl);
    [JsonIgnore] public string HeaderContextText => !string.IsNullOrEmpty(GuildName)
        ? (!string.IsNullOrEmpty(ChannelName) ? $"{GuildName} › #{ChannelName}" : GuildName)
        : (!string.IsNullOrEmpty(ChannelName) ? $"#{ChannelName}" : "Message");
    [JsonIgnore] public bool IsBookmarked { get; set; }
    /// <summary>Faux = message "suite" du même auteur (en-tête masqué, comme Discord).</summary>
    [JsonIgnore] public bool ShowHeader { get; set; } = true;
    [JsonIgnore] public bool HideHeader => !ShowHeader;
    private bool _isOwnMessage;
    [JsonIgnore]
    public bool IsOwnMessage
    {
        get => _isOwnMessage;
        set
        {
            if (_isOwnMessage != value)
            {
                _isOwnMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotOwnMessage));
            }
        }
    }
    [JsonIgnore] public bool IsNotOwnMessage => !IsOwnMessage;
    /// <summary>Types 0 (défaut), 19 (réponse), 20 (commande slash), 23 (menu contextuel) = messages normaux.</summary>
    public bool IsSystem => Type is not (0 or 19 or 20 or 23);
    public bool IsNormal => !IsSystem;
    public bool ShowNormalHeader => IsNormal && ShowHeader;
    private string SafeAuthorName => $"\u200E{Author.DisplayName}\u200E";
    public string SystemIconGlyph => Type switch
    {
        1 => "\uE8FA",            // AddFriend (ajout au groupe)
        2 => "\uE8F8",            // RemoveFriend / Leave (retrait du groupe)
        3 => "\uE717",            // Phone (appel démarré / terminé)
        4 or 5 => "\uE70F",       // Edit (changement de nom ou d'icône du salon)
        6 => "\uE718",            // Pin (message épinglé)
        7 => "\uE72A",            // Forward arrow (arrivée d'un membre sur le serveur)
        8 or 9 or 10 or 11 => "\uE734", // Boost / Star
        12 => "\uE789",           // Megaphone / Salon suivi
        18 or 21 => "\uE8BD",     // Thread / Fil de discussion
        24 => "\uE730",           // Shield / AutoMod
        _ => "\uE946"
    };
    public string SystemIconColor => Type switch
    {
        1 => "#23A55A",           // Vert Discord
        2 => "#F23F43",           // Rouge Discord
        3 => "#23A55A",           // Téléphone vert (comme Discord)
        4 or 5 => "#B5BAC1",      // Gris clair
        6 => "#B5BAC1",           // Épingle gris clair
        7 => "#23A55A",           // Flèche verte d'arrivée
        8 or 9 or 10 or 11 => "#FF73FA", // Rose Nitro Boost
        12 => "#5865F2",          // Blurple
        18 or 21 => "#5865F2",    // Blurple
        24 => "#F23F43",          // Rouge AutoMod
        _ => "#949BA4"
    };
    public string SystemText => Type switch
    {
        1 => $"{SafeAuthorName} a ajouté quelqu'un au groupe.",
        2 => $"{SafeAuthorName} a retiré quelqu'un du groupe.",
        3 => FormatCallSystemText(),
        4 => $"{SafeAuthorName} a changé le nom du salon : {Content}",
        5 => $"{SafeAuthorName} a changé l'icône du groupe.",
        6 => $"{SafeAuthorName} a épinglé un message dans ce salon.",
        7 => JoinMessage(),
        8 => $"{SafeAuthorName} vient de booster le serveur !",
        9 or 10 or 11 => $"{SafeAuthorName} a boosté le serveur ! Le serveur a atteint un nouveau niveau.",
        12 => $"{SafeAuthorName} a ajouté un salon suivi.",
        18 => $"{SafeAuthorName} a créé un fil : {Content}",
        21 => "Message de départ du fil.",
        24 => "AutoMod a bloqué un message.",
        _ => string.IsNullOrEmpty(Content) ? $"Message système (type {Type})" : Content,
    };
    private string FormatCallSystemText()
    {
        if (Call?.EndedTimestamp is DateTimeOffset ended)
        {
            var dur = ended - Timestamp;
            if (dur.TotalSeconds > 0)
            {
                string durText = dur.TotalHours >= 1
                    ? $"{(int)dur.TotalHours} h {dur.Minutes} min"
                    : dur.TotalMinutes >= 1
                        ? $"{(int)dur.TotalMinutes} min"
                        : $"{Math.Max(1, (int)dur.TotalSeconds)} s";
                return $"{SafeAuthorName} a démarré un appel qui a duré {durText}.";
            }
        }
        return $"{SafeAuthorName} a démarré un appel.";
    }
    private string JoinMessage()
    {
        var name = SafeAuthorName;
        string[] templates =
        {
            "{0} vient d'arriver. Faites-lui un câlin !",
            "Bienvenue, {0}. On espère que tu as apporté de la pizza.",
            "{0} a rejoint le groupe.",
            "{0} vient de se glisser dans le serveur.",
            "{0} vient d'atterrir.",
            "Un {0} sauvage apparaît.",
            "Content de te voir, {0}.",
        };
        var idx = (int)(Timestamp.ToUnixTimeMilliseconds() % templates.Length);
        return string.Format(templates[idx], name);
    }
    public bool HasContent => IsNormal && !string.IsNullOrEmpty(Content);
    public string FormattedContent
    {
        get
        {
            if (string.IsNullOrEmpty(Content)) return string.Empty;
            var text = Content;
            if (Mentions != null && Mentions.Count > 0)
            {
                foreach (var m in Mentions)
                {
                    if (string.IsNullOrEmpty(m.Id)) continue;
                    var name = "@" + (!string.IsNullOrWhiteSpace(m.DisplayName) ? m.DisplayName : m.Username);
                    text = text.Replace("<@" + m.Id + ">", name)
                               .Replace("<@!" + m.Id + ">", name);
                }
            }
            return text;
        }
    }
    public bool IsEdited => EditedTimestamp is not null;
    public string EditedText => IsEdited ? "(modifié)" : string.Empty;
    public string TimeText
    {
        get
        {
            var local = Timestamp.ToLocalTime();
            var today = DateTime.Today;
            if (local.Date == today) return $"Aujourd'hui à {local:HH:mm}";
            if (local.Date == today.AddDays(-1)) return $"Hier à {local:HH:mm}";
            return local.ToString("dd/MM/yyyy HH:mm");
        }
    }
    public string ShortTime => Timestamp.ToLocalTime().ToString("HH:mm");
    public bool HasInteraction => Interaction is not null || InteractionMetadata is not null;
    public bool HasReplyOrInteraction => IsReply || HasInteraction;
    public bool IsReply => ReferencedMessage is not null;
    public string ReplyAuthorName => HasInteraction
        ? (Interaction?.User?.DisplayName ?? InteractionMetadata?.User?.DisplayName ?? "Application")
        : (ReferencedMessage?.Author.DisplayName ?? "Message original");
    public string? ReplyAuthorAvatarUrl => HasInteraction
        ? (Interaction?.User?.AvatarUrl ?? InteractionMetadata?.User?.AvatarUrl)
        : ReferencedMessage?.Author.AvatarUrl;
    public string ReplyAuthorRoleColor => HasInteraction
        ? (Interaction?.User?.RoleColorHex ?? InteractionMetadata?.User?.RoleColorHex ?? "#FFFFFF")
        : (ReferencedMessage?.Author.RoleColorHex ?? "#FFFFFF");
    public string ReplyInteractionCommand => HasInteraction
        ? (!string.IsNullOrEmpty(Interaction?.Name) ? ("/" + Interaction.Name) : (!string.IsNullOrEmpty(InteractionMetadata?.Name) ? ("/" + InteractionMetadata.Name) : "commande"))
        : string.Empty;
    public string ReplyPreview => HasInteraction
        ? ("a utilisé " + ReplyInteractionCommand)
        : (ReferencedMessage is null
            ? string.Empty
            : (string.IsNullOrEmpty(ReferencedMessage.Content)
                ? "Cliquez pour voir la pièce jointe"
                : ReferencedMessage.Content.ReplaceLineEndings(" ")));
    public List<DiscordAttachment> ImageAttachments => Attachments.Where(a => a.IsImage).ToList();
    public List<DiscordAttachment> AudioAttachments => Attachments.Where(a => a.IsAudio).ToList();
    public List<DiscordAttachment> FileAttachments => Attachments.Where(a => !a.IsImage && !a.IsAudio).ToList();
    public bool HasImages => Attachments.Any(a => a.IsImage);
    public bool HasAudio => Attachments.Any(a => a.IsAudio);
    public bool HasFiles => Attachments.Any(a => !a.IsImage && !a.IsAudio);
    public bool HasEmbeds => Embeds.Count > 0;
    public bool HasStickers => StickerItems.Count > 0;
    public bool HasReactions => Reactions.Count > 0;
    public bool HasPoll => Poll is not null;
    [JsonPropertyName("thread")]
    public DiscordChannel? Thread { get; set; }
    public bool HasThread => Thread != null;
}
public sealed class DiscordAttachment
{
    [JsonIgnore]
    public DiscordMessage? ParentMessage { get; set; }
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    [JsonPropertyName("proxy_url")]
    public string ProxyUrl { get; set; } = string.Empty;
    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }
    [JsonPropertyName("width")]
    public int? Width { get; set; }
    [JsonPropertyName("height")]
    public int? Height { get; set; }
    [JsonPropertyName("size")]
    public long Size { get; set; }
    [JsonPropertyName("waveform")]
    public string? Waveform { get; set; }
    [JsonPropertyName("duration_secs")]
    public double? DurationSecs { get; set; }
    public bool IsImage => ContentType?.StartsWith("image/") == true
        || Filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || Filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || Filename.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
        || Filename.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
        || Filename.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
    public bool IsAudio => ContentType?.StartsWith("audio/") == true
        || Filename.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
        || Filename.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
        || Filename.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
        || Filename.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)
        || Filename.EndsWith(".flac", StringComparison.OrdinalIgnoreCase);
    public bool IsVoiceMessage => IsAudio && (!string.IsNullOrEmpty(Waveform) || Filename.StartsWith("voice-message", StringComparison.OrdinalIgnoreCase));
    public string TitleText => IsVoiceMessage ? "Message vocal" : Filename;
    public string DurationFormattedText
    {
        get
        {
            if (DurationSecs.HasValue && DurationSecs.Value > 0)
            {
                var ts = TimeSpan.FromSeconds(DurationSecs.Value);
                return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
            }
            return SizeText;
        }
    }
    /// <summary>Taille d'affichage bornée (max 400x300) en conservant le ratio.</summary>
    public double DisplayWidth => Scale().w;
    public double DisplayHeight => Scale().h;
    private (double w, double h) Scale()
    {
        double w = Width ?? 300, h = Height ?? 200;
        var f = Math.Min(1.0, Math.Min(400 / w, 300 / h));
        return (Math.Max(16, w * f), Math.Max(16, h * f));
    }
    public string SizeText => Size switch
    {
        < 1024 => $"{Size} o",
        < 1024 * 1024 => $"{Size / 1024.0:0.#} Ko",
        _ => $"{Size / 1024.0 / 1024.0:0.##} Mo",
    };
}
public sealed class DiscordEmbed
{
    [JsonIgnore]
    public DiscordMessage? ParentMessage { get; set; }
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    [JsonPropertyName("color")]
    public int? Color { get; set; }
    [JsonPropertyName("author")]
    public DiscordEmbedAuthor? Author { get; set; }
    [JsonPropertyName("provider")]
    public DiscordEmbedAuthor? Provider { get; set; }
    [JsonPropertyName("footer")]
    public DiscordEmbedFooter? Footer { get; set; }
    [JsonPropertyName("fields")]
    public List<DiscordEmbedField> Fields { get; set; } = new();
    [JsonPropertyName("thumbnail")]
    public DiscordEmbedImage? Thumbnail { get; set; }
    [JsonPropertyName("image")]
    public DiscordEmbedImage? Image { get; set; }
    [JsonPropertyName("video")]
    public DiscordEmbedImage? Video { get; set; }
    /// <summary>Image principale : image, sinon miniature pour les embeds image/gifv. Priorise les URLs .gif directes pour l'animation.</summary>
    public string? MainImageUrl
    {
        get
        {
            if (Image?.Url != null && Image.Url.Contains(".gif", StringComparison.OrdinalIgnoreCase)) return Image.Url;
            if (Image?.ProxyUrl != null && Image.ProxyUrl.Contains(".gif", StringComparison.OrdinalIgnoreCase)) return Image.ProxyUrl;
            var raw = Image?.ProxyUrl ?? Image?.Url;
            if (raw == null && (Type is "image" or "gifv"))
            {
                if (Thumbnail?.Url != null && Thumbnail.Url.Contains(".gif", StringComparison.OrdinalIgnoreCase)) return Thumbnail.Url;
                return Thumbnail?.ProxyUrl ?? Thumbnail?.Url;
            }
            return raw;
        }
    }
    public string? SideThumbnailUrl => Image is null && Type is not ("image" or "gifv") ? Thumbnail?.ProxyUrl ?? Thumbnail?.Url : null;
    public bool HasTitle => !string.IsNullOrEmpty(Title);
    public bool HasDescription => !string.IsNullOrEmpty(Description);
    public bool HasAuthor => !string.IsNullOrEmpty(Author?.Name);
    public bool HasProvider => !string.IsNullOrEmpty(Provider?.Name);
    public bool HasFooter => !string.IsNullOrEmpty(Footer?.Text);
    public bool HasMainImage => MainImageUrl is not null;
    public bool HasSideThumbnail => SideThumbnailUrl is not null;
    public bool HasFields => Fields.Count > 0;
    /// <summary>Embed "média seul" (lien direct vers une image/gif) : pas de cadre.</summary>
    public bool IsMediaOnly => Type is "image" or "gifv" && !HasTitle && !HasDescription;
    public bool IsCard => !IsMediaOnly;
    public string ColorHex => Color is int c ? $"#FF{c:X6}" : "#FF4E5058";
}
public sealed class DiscordEmbedImage
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    [JsonPropertyName("proxy_url")]
    public string? ProxyUrl { get; set; }
    [JsonPropertyName("width")]
    public int? Width { get; set; }
    [JsonPropertyName("height")]
    public int? Height { get; set; }
}
public sealed class DiscordEmbedAuthor
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
}
public sealed class DiscordEmbedFooter
{
    [JsonPropertyName("text")] public string? Text { get; set; }
}
public sealed class DiscordEmbedField
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("value")] public string Value { get; set; } = string.Empty;
    [JsonPropertyName("inline")] public bool Inline { get; set; }
}
public sealed class DiscordStickerItem
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("format_type")] public int FormatType { get; set; }
    public string Url => $"https://media.discordapp.net/stickers/{Id}.{(FormatType == 4 ? "gif" : "png")}?size=160";
}
public sealed class DiscordReaction
{
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("me")] public bool Me { get; set; }
    [JsonPropertyName("emoji")] public DiscordEmoji Emoji { get; set; } = new();
    public bool IsCustom => Emoji?.IsCustom ?? false;
    public string? ImageUrl => Emoji?.ImageUrl;
    public string EmojiText => Emoji?.Name ?? string.Empty;
    public string CountText => Count.ToString();
    public string BorderBrushHex => Me ? "#5865F2" : "#383A40";
    public string BackgroundBrushHex => Me ? "#3C4270" : "#2B2D31";
}
public sealed class DiscordEmoji
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("animated")] public bool Animated { get; set; }
    [JsonPropertyName("available")] public bool? Available { get; set; } = true;
    [JsonPropertyName("require_colons")] public bool? RequireColons { get; set; }
    public string? GuildId { get; set; }
    public string? GuildName { get; set; }
    public string? GuildIconUrl { get; set; }
    public bool IsCustom => Id is not null;
    public bool IsUnicode => Id is null;
    public string? ImageUrl => Id is null ? null : $"https://cdn.discordapp.com/emojis/{Id}.{(Animated ? "gif" : "png")}?size=48";
    public string FormattedTag => IsCustom ? (Animated ? $"<a:{Name}:{Id}>" : $"<:{Name}:{Id}>") : (Name ?? string.Empty);
    public string ReactionCode => IsCustom ? $"{Name}:{Id}" : (Name ?? string.Empty);
    public string TooltipText => IsCustom ? $":{Name}: ({GuildName ?? "Serveur"})" : $":{Name}:";
}
public sealed class DiscordPoll
{
    [JsonPropertyName("question")] public DiscordPollMedia Question { get; set; } = new();
    [JsonPropertyName("answers")] public List<DiscordPollAnswer> Answers { get; set; } = new();
    [JsonPropertyName("allow_multiselect")] public bool AllowMultiselect { get; set; }
    public string QuestionText => Question?.Text ?? "Sondage";
}
public sealed class DiscordPollMedia
{
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("emoji")] public DiscordEmoji? Emoji { get; set; }
}
public sealed class DiscordPollAnswer
{
    [JsonPropertyName("answer_id")] public int AnswerId { get; set; }
    [JsonPropertyName("poll_media")] public DiscordPollMedia PollMedia { get; set; } = new();
    public string DisplayText => PollMedia?.Emoji?.Name is string e && !string.IsNullOrEmpty(e)
        ? $"{e}  {PollMedia.Text}"
        : PollMedia?.Text ?? $"Option {AnswerId}";
}
public sealed class DiscordCallMetadata
{
    [JsonPropertyName("participants")]
    public List<string>? Participants { get; set; }
    [JsonPropertyName("ended_timestamp")]
    public DateTimeOffset? EndedTimestamp { get; set; }
}
public sealed class DiscordGuildMember
{
    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();
    [JsonPropertyName("nick")]
    public string? Nick { get; set; }
    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }
}
public sealed class DiscordMessageInteraction
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("type")] public int Type { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("user")] public DiscordUser User { get; set; } = new();
}
public sealed class DiscordMessageInteractionMetadata
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("type")] public int Type { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("user")] public DiscordUser User { get; set; } = new();
}
