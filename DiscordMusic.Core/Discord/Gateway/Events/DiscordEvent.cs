using System.Runtime.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public enum DiscordEvent
{
    [EnumMember(Value = "HELLO")]
    Hello,

    [EnumMember(Value = "READY")]
    Ready,

    [EnumMember(Value = "RESUMED")]
    Resumed,

    [EnumMember(Value = "RECONNECT")]
    Reconnect,

    [EnumMember(Value = "INVALID_SESSION")]
    InvalidSession,

    [EnumMember(Value = "APPLICATION_COMMAND_PERMISSIONS_UPDATE")]
    ApplicationCommandPermissionsUpdate,

    [EnumMember(Value = "AUTO_MODERATION_RULE_CREATE")]
    AutoModerationRuleCreate,

    [EnumMember(Value = "AUTO_MODERATION_RULE_UPDATE")]
    AutoModerationRuleUpdate,

    [EnumMember(Value = "AUTO_MODERATION_RULE_DELETE")]
    AutoModerationRuleDelete,

    [EnumMember(Value = "AUTO_MODERATION_ACTION_EXECUTION")]
    AutoModerationActionExecution,

    [EnumMember(Value = "CHANNEL_CREATE")]
    ChannelCreate,

    [EnumMember(Value = "CHANNEL_UPDATE")]
    ChannelUpdate,

    [EnumMember(Value = "CHANNEL_DELETE")]
    ChannelDelete,

    [EnumMember(Value = "CHANNEL_PINS_UPDATE")]
    ChannelPinsUpdate,

    [EnumMember(Value = "THREAD_CREATE")]
    ThreadCreate,

    [EnumMember(Value = "THREAD_UPDATE")]
    ThreadUpdate,

    [EnumMember(Value = "THREAD_DELETE")]
    ThreadDelete,

    [EnumMember(Value = "THREAD_LIST_SYNC")]
    ThreadListSync,

    [EnumMember(Value = "THREAD_MEMBER_UPDATE")]
    ThreadMemberUpdate,

    [EnumMember(Value = "THREAD_MEMBERS_UPDATE")]
    ThreadMembersUpdate,

    [EnumMember(Value = "ENTITLEMENT_CREATE")]
    EntitlementCreate,

    [EnumMember(Value = "ENTITLEMENT_UPDATE")]
    EntitlementUpdate,

    [EnumMember(Value = "ENTITLEMENT_DELETE")]
    EntitlementDelete,

    [EnumMember(Value = "GUILD_CREATE")]
    GuildCreate,

    [EnumMember(Value = "GUILD_UPDATE")]
    GuildUpdate,

    [EnumMember(Value = "GUILD_DELETE")]
    GuildDelete,

    [EnumMember(Value = "GUILD_AUDIT_LOG_ENTRY_CREATE")]
    GuildAuditLogEntryCreate,

    [EnumMember(Value = "GUILD_BAN_ADD")]
    GuildBanAdd,

    [EnumMember(Value = "GUILD_BAN_REMOVE")]
    GuildBanRemove,

    [EnumMember(Value = "GUILD_EMOJIS_UPDATE")]
    GuildEmojisUpdate,

    [EnumMember(Value = "GUILD_STICKERS_UPDATE")]
    GuildStickersUpdate,

    [EnumMember(Value = "GUILD_INTEGRATIONS_UPDATE")]
    GuildIntegrationsUpdate,

    [EnumMember(Value = "GUILD_MEMBER_ADD")]
    GuildMemberAdd,

    [EnumMember(Value = "GUILD_MEMBER_REMOVE")]
    GuildMemberRemove,

    [EnumMember(Value = "GUILD_MEMBER_UPDATE")]
    GuildMemberUpdate,

    [EnumMember(Value = "GUILD_MEMBERS_CHUNK")]
    GuildMembersChunk,

    [EnumMember(Value = "GUILD_ROLE_CREATE")]
    GuildRoleCreate,

    [EnumMember(Value = "GUILD_ROLE_UPDATE")]
    GuildRoleUpdate,

    [EnumMember(Value = "GUILD_ROLE_DELETE")]
    GuildRoleDelete,

    [EnumMember(Value = "GUILD_SCHEDULED_EVENT_CREATE")]
    GuildScheduledEventCreate,

    [EnumMember(Value = "GUILD_SCHEDULED_EVENT_UPDATE")]
    GuildScheduledEventUpdate,

    [EnumMember(Value = "GUILD_SCHEDULED_EVENT_DELETE")]
    GuildScheduledEventDelete,

    [EnumMember(Value = "GUILD_SCHEDULED_EVENT_USER_ADD")]
    GuildScheduledEventUserAdd,

    [EnumMember(Value = "GUILD_SCHEDULED_EVENT_USER_REMOVE")]
    GuildScheduledEventUserRemove,

    [EnumMember(Value = "INTEGRATION_CREATE")]
    IntegrationCreate,

    [EnumMember(Value = "INTEGRATION_UPDATE")]
    IntegrationUpdate,

    [EnumMember(Value = "INTEGRATION_DELETE")]
    IntegrationDelete,

    [EnumMember(Value = "INTERACTION_CREATE")]
    InteractionCreate,

    [EnumMember(Value = "INVITE_CREATE")]
    InviteCreate,

    [EnumMember(Value = "INVITE_DELETE")]
    InviteDelete,

    [EnumMember(Value = "MESSAGE_CREATE")]
    MessageCreate,

    [EnumMember(Value = "MESSAGE_UPDATE")]
    MessageUpdate,

    [EnumMember(Value = "MESSAGE_DELETE")]
    MessageDelete,

    [EnumMember(Value = "MESSAGE_DELETE_BULK")]
    MessageDeleteBulk,

    [EnumMember(Value = "MESSAGE_REACTION_ADD")]
    MessageReactionAdd,

    [EnumMember(Value = "MESSAGE_REACTION_REMOVE")]
    MessageReactionRemove,

    [EnumMember(Value = "MESSAGE_REACTION_REMOVE_ALL")]
    MessageReactionRemoveAll,

    [EnumMember(Value = "MESSAGE_REACTION_REMOVE_EMOJI")]
    MessageReactionRemoveEmoji,

    [EnumMember(Value = "PRESENCE_UPDATE")]
    PresenceUpdate,

    [EnumMember(Value = "STAGE_INSTANCE_CREATE")]
    StageInstanceCreate,

    [EnumMember(Value = "STAGE_INSTANCE_UPDATE")]
    StageInstanceUpdate,

    [EnumMember(Value = "STAGE_INSTANCE_DELETE")]
    StageInstanceDelete,

    [EnumMember(Value = "TYPING_START")]
    TypingStart,

    [EnumMember(Value = "USER_UPDATE")]
    UserUpdate,

    [EnumMember(Value = "VOICE_STATE_UPDATE")]
    VoiceStateUpdate,

    [EnumMember(Value = "VOICE_SERVER_UPDATE")]
    VoiceServerUpdate,

    [EnumMember(Value = "WEBHOOKS_UPDATE")]
    WebhooksUpdate
}