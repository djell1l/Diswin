using System.Text.Json;
using DiscordNative.Protocol.Models;

namespace DiscordNative.App.Services;

public sealed class EmojiItemViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? Id { get; set; }
    public bool IsCustom { get; set; }
    public bool IsAnimated { get; set; }
    public string? ImageUrl { get; set; }
    public string? UnicodeChar { get; set; }
    public string? GuildId { get; set; }
    public string? GuildName { get; set; }
    public string Category { get; set; } = "Émojis";
    public string TooltipText => IsCustom ? $":{Name}: ({GuildName ?? "Serveur"})" : $":{Name}:";
    public string TagToInsert => IsCustom ? (IsAnimated ? $"<a:{Name}:{Id}>" : $"<:{Name}:{Id}>") : (UnicodeChar ?? Name);
    public string ReactionCode => IsCustom ? $"{Name}:{Id}" : (UnicodeChar ?? Name);
}

public sealed class EmojiCategoryGroup
{
    public string Title { get; set; } = string.Empty;
    public string? IconGlyph { get; set; }
    public string? IconUrl { get; set; }
    public bool HasIconUrl => !string.IsNullOrEmpty(IconUrl);
    public List<EmojiItemViewModel> Emojis { get; set; } = new();
}

public sealed class EmojiService
{
    private static readonly Lazy<EmojiService> _instance = new(() => new EmojiService());
    public static EmojiService Instance => _instance.Value;

    private readonly List<EmojiItemViewModel> _unicodeEmojis = new();
    private readonly List<EmojiItemViewModel> _recentEmojis = new();
    private const int MaxRecentEmojis = 32;

    public EmojiService()
    {
        InitializeUnicodeCatalog();
    }

    public List<EmojiCategoryGroup> BuildPickerCatalog(IEnumerable<DiscordGuild>? guilds, string? currentGuildId, string? filterQuery = null)
    {
        var groups = new List<EmojiCategoryGroup>();
        var cleanQuery = filterQuery?.Trim().Trim(':').ToLowerInvariant();
        bool isSearching = !string.IsNullOrWhiteSpace(cleanQuery);

        if (isSearching)
        {
            var searchResults = new List<EmojiItemViewModel>();

            // 1. Search in custom server emojis
            if (guilds != null)
            {
                foreach (var g in guilds)
                {
                    if (g.Emojis == null) continue;
                    foreach (var em in g.Emojis)
                    {
                        if (string.IsNullOrEmpty(em.Name)) continue;
                        if (em.Name.Contains(cleanQuery!, StringComparison.OrdinalIgnoreCase))
                        {
                            searchResults.Add(FromDiscordEmoji(em, g));
                        }
                    }
                }
            }

            // 2. Search in standard unicode emojis
            foreach (var u in _unicodeEmojis)
            {
                if (u.Name.Contains(cleanQuery!, StringComparison.OrdinalIgnoreCase) ||
                    u.Category.Contains(cleanQuery!, StringComparison.OrdinalIgnoreCase))
                {
                    searchResults.Add(u);
                }
            }

            groups.Add(new EmojiCategoryGroup
            {
                Title = $"Résultats pour \"{cleanQuery}\" ({searchResults.Count})",
                IconGlyph = "\uE721",
                Emojis = searchResults
            });

            return groups;
        }

        // Si non en recherche : afficher les catégories Discord authentiques

        // 1. Récents
        lock (_recentEmojis)
        {
            if (_recentEmojis.Count > 0)
            {
                groups.Add(new EmojiCategoryGroup
                {
                    Title = "Récemment utilisés",
                    IconGlyph = "\uE823",
                    Emojis = _recentEmojis.ToList()
                });
            }
        }

        // 2. Émojis du serveur actuel (en premier)
        if (guilds != null && !string.IsNullOrEmpty(currentGuildId))
        {
            var currentGuild = guilds.FirstOrDefault(g => g.Id == currentGuildId);
            if (currentGuild?.Emojis is { Count: > 0 })
            {
                groups.Add(new EmojiCategoryGroup
                {
                    Title = currentGuild.Name,
                    IconUrl = currentGuild.IconUrl,
                    IconGlyph = "\uE774",
                    Emojis = currentGuild.Emojis.Select(e => FromDiscordEmoji(e, currentGuild)).ToList()
                });
            }
        }

        // 3. Autres serveurs
        if (guilds != null)
        {
            foreach (var g in guilds)
            {
                if (g.Id == currentGuildId) continue;
                if (g.Emojis is { Count: > 0 })
                {
                    groups.Add(new EmojiCategoryGroup
                    {
                        Title = g.Name,
                        IconUrl = g.IconUrl,
                        IconGlyph = "\uE774",
                        Emojis = g.Emojis.Select(e => FromDiscordEmoji(e, g)).ToList()
                    });
                }
            }
        }

        // 4. Catégories standard Unicode
        var unicodeCategories = _unicodeEmojis
            .GroupBy(e => e.Category)
            .OrderBy(g => GetCategorySortOrder(g.Key));

        foreach (var cat in unicodeCategories)
        {
            groups.Add(new EmojiCategoryGroup
            {
                Title = cat.Key,
                IconGlyph = GetCategoryGlyph(cat.Key),
                Emojis = cat.ToList()
            });
        }

        return groups;
    }

    public void TrackRecentEmoji(EmojiItemViewModel emoji)
    {
        lock (_recentEmojis)
        {
            _recentEmojis.RemoveAll(e => e.TagToInsert == emoji.TagToInsert);
            _recentEmojis.Insert(0, emoji);
            if (_recentEmojis.Count > MaxRecentEmojis)
            {
                _recentEmojis.RemoveAt(_recentEmojis.Count - 1);
            }
        }
    }

    private static EmojiItemViewModel FromDiscordEmoji(DiscordEmoji em, DiscordGuild g)
    {
        return new EmojiItemViewModel
        {
            Name = em.Name ?? "emoji",
            Id = em.Id,
            IsCustom = true,
            IsAnimated = em.Animated,
            ImageUrl = em.ImageUrl,
            GuildId = g.Id,
            GuildName = g.Name,
            Category = g.Name
        };
    }

    private static int GetCategorySortOrder(string cat) => cat switch
    {
        "Smileys & Émotions" => 1,
        "Personnes & Corps" => 2,
        "Animaux & Nature" => 3,
        "Nourriture & Boissons" => 4,
        "Activités" => 5,
        "Voyages & Lieux" => 6,
        "Objets" => 7,
        "Symboles & Cœurs" => 8,
        "Drapeaux" => 9,
        _ => 10
    };

    private static string GetCategoryGlyph(string cat) => cat switch
    {
        "Smileys & Émotions" => "\uE76E",
        "Personnes & Corps" => "\uE77B",
        "Animaux & Nature" => "\uE706",
        "Nourriture & Boissons" => "\uE734",
        "Activités" => "\uE7FC",
        "Voyages & Lieux" => "\uE709",
        "Objets" => "\uE7BF",
        "Symboles & Cœurs" => "\uEB51",
        "Drapeaux" => "\uE7C1",
        _ => "\uED54"
    };

    private void InitializeUnicodeCatalog()
    {
        void Add(string category, string name, string ch)
        {
            _unicodeEmojis.Add(new EmojiItemViewModel
            {
                Category = category,
                Name = name,
                UnicodeChar = ch,
                IsCustom = false
            });
        }

        // Smileys & Émotions
        Add("Smileys & Émotions", "grinning", "😀");
        Add("Smileys & Émotions", "smiley", "😃");
        Add("Smileys & Émotions", "smile", "😄");
        Add("Smileys & Émotions", "grin", "😁");
        Add("Smileys & Émotions", "laughing", "😆");
        Add("Smileys & Émotions", "sweat_smile", "😅");
        Add("Smileys & Émotions", "rofl", "🤣");
        Add("Smileys & Émotions", "joy", "😂");
        Add("Smileys & Émotions", "slight_smile", "🙂");
        Add("Smileys & Émotions", "upside_down", "🙃");
        Add("Smileys & Émotions", "melting_face", "🫠");
        Add("Smileys & Émotions", "wink", "😉");
        Add("Smileys & Émotions", "blush", "😊");
        Add("Smileys & Émotions", "innocent", "😇");
        Add("Smileys & Émotions", "smiling_face_with_3_hearts", "🥰");
        Add("Smileys & Émotions", "heart_eyes", "😍");
        Add("Smileys & Émotions", "star_struck", "🤩");
        Add("Smileys & Émotions", "kissing_heart", "😘");
        Add("Smileys & Émotions", "kissing", "😗");
        Add("Smileys & Émotions", "kissing_closed_eyes", "😚");
        Add("Smileys & Émotions", "yum", "😋");
        Add("Smileys & Émotions", "stuck_out_tongue", "😛");
        Add("Smileys & Émotions", "stuck_out_tongue_winking_eye", "😜");
        Add("Smileys & Émotions", "zany_face", "🤪");
        Add("Smileys & Émotions", "stuck_out_tongue_closed_eyes", "😝");
        Add("Smileys & Émotions", "money_mouth", "🤑");
        Add("Smileys & Émotions", "hugging", "🤗");
        Add("Smileys & Émotions", "hand_over_mouth", "🤭");
        Add("Smileys & Émotions", "shushing_face", "🤫");
        Add("Smileys & Émotions", "thinking", "🤔");
        Add("Smileys & Émotions", "saluting_face", "🫡");
        Add("Smileys & Émotions", "zipper_mouth", "🤐");
        Add("Smileys & Émotions", "raised_eyebrow", "🤨");
        Add("Smileys & Émotions", "neutral_face", "😐");
        Add("Smileys & Émotions", "expressionless", "😑");
        Add("Smileys & Émotions", "no_mouth", "😶");
        Add("Smileys & Émotions", "smirk", "😏");
        Add("Smileys & Émotions", "unamused", "😒");
        Add("Smileys & Émotions", "roll_eyes", "🙄");
        Add("Smileys & Émotions", "grimacing", "😬");
        Add("Smileys & Émotions", "lying_face", "🤥");
        Add("Smileys & Émotions", "relieved", "😌");
        Add("Smileys & Émotions", "pensive", "😔");
        Add("Smileys & Émotions", "sleepy", "😪");
        Add("Smileys & Émotions", "drooling_face", "🤤");
        Add("Smileys & Émotions", "sleeping", "😴");
        Add("Smileys & Émotions", "mask", "😷");
        Add("Smileys & Émotions", "face_with_thermometer", "🤒");
        Add("Smileys & Émotions", "head_bandage", "🤕");
        Add("Smileys & Émotions", "nauseated_face", "🤢");
        Add("Smileys & Émotions", "vomiting_face", "🤮");
        Add("Smileys & Émotions", "sneezing_face", "🤧");
        Add("Smileys & Émotions", "hot_face", "🥵");
        Add("Smileys & Émotions", "cold_face", "🥶");
        Add("Smileys & Émotions", "woozy_face", "🥴");
        Add("Smileys & Émotions", "dizzy_face", "😵");
        Add("Smileys & Émotions", "exploding_head", "🤯");
        Add("Smileys & Émotions", "cowboy", "🤠");
        Add("Smileys & Émotions", "partying_face", "🥳");
        Add("Smileys & Émotions", "sunglasses", "😎");
        Add("Smileys & Émotions", "nerd", "🤓");
        Add("Smileys & Émotions", "monocle_face", "🧐");
        Add("Smileys & Émotions", "confused", "😕");
        Add("Smileys & Émotions", "worried", "😟");
        Add("Smileys & Émotions", "pleading_face", "🥺");
        Add("Smileys & Émotions", "open_mouth", "😮");
        Add("Smileys & Émotions", "screaming", "😱");
        Add("Smileys & Émotions", "sob", "😭");
        Add("Smileys & Émotions", "cry", "😢");
        Add("Smileys & Émotions", "rage", "😡");
        Add("Smileys & Émotions", "angry", "😠");
        Add("Smileys & Émotions", "cursing_face", "🤬");
        Add("Smileys & Émotions", "skull", "💀");
        Add("Smileys & Émotions", "skull_crossbones", "☠️");
        Add("Smileys & Émotions", "poop", "💩");
        Add("Smileys & Émotions", "clown", "🤡");
        Add("Smileys & Émotions", "ghost", "👻");
        Add("Smileys & Émotions", "alien", "👽");
        Add("Smileys & Émotions", "robot", "🤖");

        // Personnes & Corps
        Add("Personnes & Corps", "wave", "👋");
        Add("Personnes & Corps", "raised_hand", "✋");
        Add("Personnes & Corps", "ok_hand", "👌");
        Add("Personnes & Corps", "pinching_hand", "🤏");
        Add("Personnes & Corps", "v", "✌️");
        Add("Personnes & Corps", "fingers_crossed", "🤞");
        Add("Personnes & Corps", "love_you_gesture", "🤟");
        Add("Personnes & Corps", "metal", "🤘");
        Add("Personnes & Corps", "call_me", "🤙");
        Add("Personnes & Corps", "point_left", "👈");
        Add("Personnes & Corps", "point_right", "👉");
        Add("Personnes & Corps", "point_up", "👆");
        Add("Personnes & Corps", "point_down", "👇");
        Add("Personnes & Corps", "thumbsup", "👍");
        Add("Personnes & Corps", "thumbsdown", "👎");
        Add("Personnes & Corps", "fist", "✊");
        Add("Personnes & Corps", "punch", "👊");
        Add("Personnes & Corps", "clap", "👏");
        Add("Personnes & Corps", "raised_hands", "🙌");
        Add("Personnes & Corps", "handshake", "🤝");
        Add("Personnes & Corps", "pray", "🙏");
        Add("Personnes & Corps", "muscle", "💪");
        Add("Personnes & Corps", "eyes", "👀");
        Add("Personnes & Corps", "brain", "🧠");
        Add("Personnes & Corps", "fire", "🔥");

        // Symboles & Cœurs
        Add("Symboles & Cœurs", "red_heart", "❤️");
        Add("Symboles & Cœurs", "orange_heart", "🧡");
        Add("Symboles & Cœurs", "yellow_heart", "💛");
        Add("Symboles & Cœurs", "green_heart", "💚");
        Add("Symboles & Cœurs", "blue_heart", "💙");
        Add("Symboles & Cœurs", "purple_heart", "💜");
        Add("Symboles & Cœurs", "black_heart", "🖤");
        Add("Symboles & Cœurs", "white_heart", "🤍");
        Add("Symboles & Cœurs", "brown_heart", "🤎");
        Add("Symboles & Cœurs", "broken_heart", "💔");
        Add("Symboles & Cœurs", "sparkling_heart", "💖");
        Add("Symboles & Cœurs", "two_hearts", "💕");
        Add("Symboles & Cœurs", "heart_on_fire", "❤️‍🔥");
        Add("Symboles & Cœurs", "100", "💯");
        Add("Symboles & Cœurs", "sparkles", "✨");
        Add("Symboles & Cœurs", "star", "⭐");
        Add("Symboles & Cœurs", "glowing_star", "🌟");
        Add("Symboles & Cœurs", "collision", "💥");
        Add("Symboles & Cœurs", "sweat_drops", "💦");
        Add("Symboles & Cœurs", "check", "✅");
        Add("Symboles & Cœurs", "x", "❌");
        Add("Symboles & Cœurs", "warning", "⚠️");
        Add("Symboles & Cœurs", "stop_sign", "🛑");
        Add("Symboles & Cœurs", "question", "❓");
        Add("Symboles & Cœurs", "exclamation", "❗");
        Add("Symboles & Cœurs", "interrobang", "⁉️");

        // Animaux & Nature
        Add("Animaux & Nature", "dog", "🐶");
        Add("Animaux & Nature", "cat", "🐱");
        Add("Animaux & Nature", "mouse", "🐭");
        Add("Animaux & Nature", "hamster", "🐹");
        Add("Animaux & Nature", "rabbit", "🐰");
        Add("Animaux & Nature", "fox", "🦊");
        Add("Animaux & Nature", "bear", "🐻");
        Add("Animaux & Nature", "panda", "🐼");
        Add("Animaux & Nature", "koala", "🐨");
        Add("Animaux & Nature", "tiger", "🐯");
        Add("Animaux & Nature", "lion", "🦁");
        Add("Animaux & Nature", "cow", "🐮");
        Add("Animaux & Nature", "pig", "🐷");
        Add("Animaux & Nature", "frog", "🐸");
        Add("Animaux & Nature", "monkey", "🐵");
        Add("Animaux & Nature", "chicken", "🐔");
        Add("Animaux & Nature", "penguin", "🐧");
        Add("Animaux & Nature", "bird", "🐦");
        Add("Animaux & Nature", "duck", "🦆");
        Add("Animaux & Nature", "eagle", "🦅");
        Add("Animaux & Nature", "owl", "🦉");
        Add("Animaux & Nature", "bat", "🦇");
        Add("Animaux & Nature", "wolf", "🐺");
        Add("Animaux & Nature", "bee", "🐝");
        Add("Animaux & Nature", "butterfly", "🦋");
        Add("Animaux & Nature", "turtle", "🐢");
        Add("Animaux & Nature", "snake", "🐍");
        Add("Animaux & Nature", "octopus", "🐙");
        Add("Animaux & Nature", "shark", "🦈");
        Add("Animaux & Nature", "whale", "🐳");
        Add("Animaux & Nature", "four_leaf_clover", "🍀");
        Add("Animaux & Nature", "sunflower", "🌻");
        Add("Animaux & Nature", "rose", "🌹");
        Add("Animaux & Nature", "rainbow", "🌈");

        // Nourriture & Boissons
        Add("Nourriture & Boissons", "apple", "🍎");
        Add("Nourriture & Boissons", "banana", "🍌");
        Add("Nourriture & Boissons", "watermelon", "🍉");
        Add("Nourriture & Boissons", "grapes", "🍇");
        Add("Nourriture & Boissons", "strawberry", "🍓");
        Add("Nourriture & Boissons", "peach", "🍑");
        Add("Nourriture & Boissons", "pizza", "🍕");
        Add("Nourriture & Boissons", "hamburger", "🍔");
        Add("Nourriture & Boissons", "fries", "🍟");
        Add("Nourriture & Boissons", "hotdog", "🌭");
        Add("Nourriture & Boissons", "taco", "🌮");
        Add("Nourriture & Boissons", "burrito", "🌯");
        Add("Nourriture & Boissons", "sushi", "🍣");
        Add("Nourriture & Boissons", "ramen", "🍜");
        Add("Nourriture & Boissons", "ice_cream", "🍦");
        Add("Nourriture & Boissons", "doughnut", "🍩");
        Add("Nourriture & Boissons", "cookie", "🍪");
        Add("Nourriture & Boissons", "cake", "🍰");
        Add("Nourriture & Boissons", "chocolate", "🍫");
        Add("Nourriture & Boissons", "popcorn", "🍿");
        Add("Nourriture & Boissons", "coffee", "☕");
        Add("Nourriture & Boissons", "tea", "🍵");
        Add("Nourriture & Boissons", "beer", "🍺");
        Add("Nourriture & Boissons", "beers", "🍻");
        Add("Nourriture & Boissons", "wine", "🍷");
        Add("Nourriture & Boissons", "cocktail", "🍸");

        // Activités
        Add("Activités", "soccer", "⚽");
        Add("Activités", "basketball", "🏀");
        Add("Activités", "football", "🏈");
        Add("Activités", "baseball", "⚾");
        Add("Activités", "tennis", "🎾");
        Add("Activités", "volleyball", "🏐");
        Add("Activités", "video_game", "🎮");
        Add("Activités", "joystick", "🕹️");
        Add("Activités", "dice", "🎲");
        Add("Activités", "trophy", "🏆");
        Add("Activités", "medal", "🥇");
        Add("Activités", "party_popper", "🎉");
        Add("Activités", "balloon", "🎈");

        // Objets & Voyages
        Add("Objets & Voyages", "rocket", "🚀");
        Add("Objets & Voyages", "airplane", "✈️");
        Add("Objets & Voyages", "car", "🚗");
        Add("Objets & Voyages", "police_car", "🚓");
        Add("Objets & Voyages", "motorcycle", "🏍️");
        Add("Objets & Voyages", "computer", "💻");
        Add("Objets & Voyages", "phone", "📱");
        Add("Objets & Voyages", "bell", "🔔");
        Add("Objets & Voyages", "moneybag", "💰");
        Add("Objets & Voyages", "bulb", "💡");
        Add("Objets & Voyages", "package", "📦");
        Add("Objets & Voyages", "crown", "👑");
    }
}
