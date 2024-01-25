using System.Net.Mail;
using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class Message
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = null!;
    
    [JsonPropertyName("author")]
    public User Author { get; set; } = null!;
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;
    
    // [JsonPropertyName("timestamp")]
    // public DateTime Timestamp { get; set; }
    //
    // [JsonPropertyName("edited_timestamp")]
    // public DateTime? EditedTimestamp { get; set; }
    //
    // [JsonPropertyName("tts")]
    // public bool Tts { get; set; }
    //
    // [JsonPropertyName("mention_everyone")]
    // public bool MentionEveryone { get; set; }
    //
    // [JsonPropertyName("mentions")]
    // public User[] Mentions { get; set; } = Array.Empty<User>();
    //
    // [JsonPropertyName("mention_roles")]
    // public string[] MentionRoles { get; set; } = Array.Empty<string>();
    //
    // [JsonPropertyName("mention_channels")]
    // public ChannelMention[] MentionChannels { get; set; } = Array.Empty<ChannelMention>();
    //
    // [JsonPropertyName("attachments")]
    // public Attachment[] Attachments { get; set; } = Array.Empty<Attachment>();

    // [JsonPropertyName("embeds")]
    // public Embed[] Embeds { get; set; } = Array.Empty<Embed>();

    // [JsonPropertyName("reactions")]
    // public Reaction[]? Reactions { get; set; }

    // [JsonPropertyName("nonce")]
    // public string? Nonce { get; set; }
    //
    // [JsonPropertyName("pinned")]
    // public bool Pinned { get; set; }
    //
    // [JsonPropertyName("webhook_id")]
    // public string? WebhookId { get; set; }

    [JsonPropertyName("type")]
    public MessageType Type { get; set; }

    // [JsonPropertyName("activity")]
    // public MessageActivity? Activity { get; set; }
    
    // [JsonPropertyName("application")]
    // public Application? Application { get; set; }
    //
    // [JsonPropertyName("application_id")]
    // public string? ApplicationId { get; set; }

    // [JsonPropertyName("message_reference")]
    // public MessageReference? MessageReference { get; set; }
    
    // [JsonPropertyName("flags")]
    // public int? Flags { get; set; }
    //
    // [JsonPropertyName("referenced_message")]
    // public Message? ReferencedMessage { get; set; }
    
    // [JsonPropertyName("interaction")]
    // public MessageInteraction? Interaction { get; set; }
    
    [JsonPropertyName("thread")]
    public Channel? Thread { get; set; }
    
    // [JsonPropertyName("components")]
    // public Component[]? Components { get; set; }
    //
    // [JsonPropertyName("sticker_items")]
    // public StickerItem[]? StickerItems { get; set; }
    
    // [JsonPropertyName("stickers")]
    // public Sticker[]? Stickers { get; set; }
    //
    // [JsonPropertyName("position")]
    // public int? Position { get; set; }
    
    // [JsonPropertyName("role_subscription_data")]
    // public RoleSubscriptionData? RoleSubscriptionData { get; set; }
    //
    // [JsonPropertyName("resolved")]
    // public MessageResolved? Resolved { get; set; }
}

public class MessageCreate : Message
{
    [JsonPropertyName("guild_id")]
    public string? GuildId { get; set; }
    
    [JsonPropertyName("member")]
    public GuildMember? Member { get; set; }
    
    // [JsonPropertyName("mentions")]
    // public User[]? Mentions { get; set; }
}