using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CyberChat.Domain.Entities;
using CyberChat.Domain.Enums;

namespace CyberChat.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Uid).HasColumnName("uid").HasMaxLength(32).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Bio).HasColumnName("bio").HasColumnType("text");
        builder.Property(x => x.AvatarUrl).HasColumnName("avatar_url");
        builder.Property(x => x.CoverUrl).HasColumnName("cover_url");
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(255);
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").IsRequired();

        // Check constraints
        builder.HasCheckConstraint("uid_format", "uid ~ '^[a-zA-Z0-9_.]{3,32}$'");
        builder.HasCheckConstraint("contact_required", "email IS NOT NULL OR phone IS NOT NULL");

        // Indexes
        builder.HasIndex(x => x.Uid).IsUnique().HasDatabaseName("idx_users_uid");
        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("idx_users_email");
        builder.HasIndex(x => x.Phone).IsUnique();
    }
}

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.DeviceName).HasColumnName("device_name").HasMaxLength(200);
        builder.Property(x => x.DeviceType).HasColumnName("device_type").HasColumnType("device_type").HasDefaultValue(DeviceType.Web).IsRequired();
        builder.Property(x => x.IpAddress).HasColumnName("ip_address");
        builder.Property(x => x.FcmToken).HasColumnName("fcm_token");
        builder.Property(x => x.RefreshToken).HasColumnName("refresh_token").IsRequired();
        builder.Property(x => x.LastActiveAt).HasColumnName("last_active_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasIndex(x => x.RefreshToken).IsUnique();

        builder.HasOne(x => x.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.UserId).HasDatabaseName("idx_user_sessions_user");
        builder.HasIndex(x => x.ExpiresAt).HasDatabaseName("idx_user_sessions_exp");
    }
}

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Type).HasColumnName("type").HasColumnType("conversation_type").HasDefaultValue(ConversationType.Direct).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(x => x.AvatarUrl).HasColumnName("avatar_url");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Creator)
            .WithMany(u => u.ConversationsCreated)
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConversationMemberConfiguration : IEntityTypeConfiguration<ConversationMember>
{
    public void Configure(EntityTypeBuilder<ConversationMember> builder)
    {
        builder.ToTable("conversation_members");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ConversationId).HasColumnName("conversation_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Role).HasColumnName("role").HasColumnType("member_role").HasDefaultValue(MemberRole.Member).IsRequired();
        builder.Property(x => x.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.LastReadAt).HasColumnName("last_read_at");
        builder.Property(x => x.IsMuted).HasColumnName("is_muted").HasDefaultValue(false).IsRequired();

        builder.HasIndex(x => new { x.ConversationId, x.UserId }).IsUnique();

        builder.HasOne(x => x.Conversation)
            .WithMany(c => c.Members)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.ConversationId).HasDatabaseName("idx_conv_members_conv");
        builder.HasIndex(x => x.UserId).HasDatabaseName("idx_conv_members_user");
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ConversationId).HasColumnName("conversation_id").IsRequired();
        builder.Property(x => x.SenderId).HasColumnName("sender_id").IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasColumnType("message_content_type").HasDefaultValue(MessageContentType.Text).IsRequired();
        builder.Property(x => x.TextContent).HasColumnName("text_content");
        builder.Property(x => x.ReplyToId).HasColumnName("reply_to_id");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Sender)
            .WithMany(u => u.MessagesSent)
            .HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReplyTo)
            .WithMany(m => m.Replies)
            .HasForeignKey(x => x.ReplyToId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(x => new { x.ConversationId, x.CreatedAt }).HasDatabaseName("idx_messages_conv"); // composite index
        builder.HasIndex(x => x.SenderId).HasDatabaseName("idx_messages_sender");
    }
}

public class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachment>
{
    public void Configure(EntityTypeBuilder<MessageAttachment> builder)
    {
        builder.ToTable("message_attachments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasColumnType("attachment_type").IsRequired();
        builder.Property(x => x.Url).HasColumnName("url").IsRequired();
        builder.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(500);
        builder.Property(x => x.FileSize).HasColumnName("file_size");
        builder.Property(x => x.MimeType).HasColumnName("mime_type").HasMaxLength(100);
        builder.Property(x => x.Width).HasColumnName("width");
        builder.Property(x => x.Height).HasColumnName("height");
        builder.Property(x => x.DurationMs).HasColumnName("duration_ms");
        builder.Property(x => x.StickerPackId).HasColumnName("sticker_pack_id").HasMaxLength(100);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Message)
            .WithMany(m => m.Attachments)
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.MessageId).HasDatabaseName("idx_msg_attach_msg");
    }
}

public class MessageReactionConfiguration : IEntityTypeConfiguration<MessageReaction>
{
    public void Configure(EntityTypeBuilder<MessageReaction> builder)
    {
        builder.ToTable("message_reactions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Emoji).HasColumnName("emoji").HasMaxLength(10).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasIndex(x => new { x.MessageId, x.UserId, x.Emoji }).IsUnique();

        builder.HasOne(x => x.Message)
            .WithMany(m => m.Reactions)
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(u => u.MessageReactions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.MessageId).HasDatabaseName("idx_msg_reactions_msg");
    }
}

public class FriendRequestConfiguration : IEntityTypeConfiguration<FriendRequest>
{
    public void Configure(EntityTypeBuilder<FriendRequest> builder)
    {
        builder.ToTable("friend_requests");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.FromUserId).HasColumnName("from_user_id").IsRequired();
        builder.Property(x => x.ToUserId).HasColumnName("to_user_id").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("friend_request_status").HasDefaultValue(FriendRequestStatus.Pending).IsRequired();
        builder.Property(x => x.Message).HasColumnName("message").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasCheckConstraint("no_self_request", "from_user_id <> to_user_id");
        builder.HasIndex(x => new { x.FromUserId, x.ToUserId }).IsUnique();

        builder.HasOne(x => x.FromUser)
            .WithMany(u => u.FriendRequestsSent)
            .HasForeignKey(x => x.FromUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ToUser)
            .WithMany(u => u.FriendRequestsReceived)
            .HasForeignKey(x => x.ToUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => new { x.ToUserId, x.Status }).HasDatabaseName("idx_friend_req_to");
        builder.HasIndex(x => new { x.FromUserId, x.Status }).HasDatabaseName("idx_friend_req_from");
    }
}

public class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.ToTable("friendships");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserAId).HasColumnName("user_a_id").IsRequired();
        builder.Property(x => x.UserBId).HasColumnName("user_b_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasCheckConstraint("no_self_friend", "user_a_id <> user_b_id");
        builder.HasCheckConstraint("ordered_pair", "user_a_id < user_b_id");
        builder.HasIndex(x => new { x.UserAId, x.UserBId }).IsUnique();

        builder.HasOne(x => x.UserA)
            .WithMany(u => u.FriendshipsA)
            .HasForeignKey(x => x.UserAId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.UserB)
            .WithMany(u => u.FriendshipsB)
            .HasForeignKey(x => x.UserBId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.UserAId).HasDatabaseName("idx_friendships_a");
        builder.HasIndex(x => x.UserBId).HasDatabaseName("idx_friendships_b");
    }
}

public class BlockConfiguration : IEntityTypeConfiguration<Block>
{
    public void Configure(EntityTypeBuilder<Block> builder)
    {
        builder.ToTable("blocks");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.BlockerId).HasColumnName("blocker_id").IsRequired();
        builder.Property(x => x.BlockedId).HasColumnName("blocked_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasCheckConstraint("no_self_block", "blocker_id <> blocked_id");
        builder.HasIndex(x => new { x.BlockerId, x.BlockedId }).IsUnique();

        builder.HasOne(x => x.Blocker)
            .WithMany(u => u.BlocksInitiated)
            .HasForeignKey(x => x.BlockerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Blocked)
            .WithMany(u => u.BlocksReceived)
            .HasForeignKey(x => x.BlockedId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.BlockerId).HasDatabaseName("idx_blocks_blocker");
    }
}

public class StoryConfiguration : IEntityTypeConfiguration<Story>
{
    public void Configure(EntityTypeBuilder<Story> builder)
    {
        builder.ToTable("stories");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.MediaUrl).HasColumnName("media_url").IsRequired();
        builder.Property(x => x.MediaType).HasColumnName("media_type").HasColumnType("story_media_type").IsRequired();
        builder.Property(x => x.Caption).HasColumnName("caption").HasMaxLength(500);
        builder.Property(x => x.BgColor).HasColumnName("bg_color").HasMaxLength(7);
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasDefaultValueSql("now() + interval '24 hours'").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(u => u.Stories)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.UserId).HasDatabaseName("idx_stories_user");
        builder.HasIndex(x => x.ExpiresAt).HasDatabaseName("idx_stories_expires");
    }
}

public class StoryViewConfiguration : IEntityTypeConfiguration<StoryView>
{
    public void Configure(EntityTypeBuilder<StoryView> builder)
    {
        builder.ToTable("story_views");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.StoryId).HasColumnName("story_id").IsRequired();
        builder.Property(x => x.ViewerId).HasColumnName("viewer_id").IsRequired();
        builder.Property(x => x.ViewedAt).HasColumnName("viewed_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasIndex(x => new { x.StoryId, x.ViewerId }).IsUnique();

        builder.HasOne(x => x.Story)
            .WithMany(s => s.Views)
            .HasForeignKey(x => x.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Viewer)
            .WithMany(u => u.StoryViews)
            .HasForeignKey(x => x.ViewerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.StoryId).HasDatabaseName("idx_story_views_story");
    }
}

public class StoryReactionConfiguration : IEntityTypeConfiguration<StoryReaction>
{
    public void Configure(EntityTypeBuilder<StoryReaction> builder)
    {
        builder.ToTable("story_reactions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.StoryId).HasColumnName("story_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Emoji).HasColumnName("emoji").HasMaxLength(10).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasIndex(x => new { x.StoryId, x.UserId }).IsUnique();

        builder.HasOne(x => x.Story)
            .WithMany(s => s.Reactions)
            .HasForeignKey(x => x.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(u => u.StoryReactions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TodoListConfiguration : IEntityTypeConfiguration<TodoList>
{
    public void Configure(EntityTypeBuilder<TodoList> builder)
    {
        builder.ToTable("todo_lists");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Color).HasColumnName("color").HasMaxLength(7).HasDefaultValue("#6366F1").IsRequired();
        builder.Property(x => x.Icon).HasColumnName("icon").HasMaxLength(50);
        builder.Property(x => x.IsDefault).HasColumnName("is_default").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(u => u.TodoLists)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TodoConfiguration : IEntityTypeConfiguration<Todo>
{
    public void Configure(EntityTypeBuilder<Todo> builder)
    {
        builder.ToTable("todos");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ListId).HasColumnName("list_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(x => x.DueDate).HasColumnName("due_date");
        builder.Property(x => x.IsAllDay).HasColumnName("is_all_day").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.Priority).HasColumnName("priority").HasColumnType("todo_priority").HasDefaultValue(TodoPriority.Medium).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("todo_status").HasDefaultValue(TodoStatus.Todo).IsRequired();
        builder.Property(x => x.ReminderMinutes).HasColumnName("reminder_minutes");
        builder.Property(x => x.RecurrenceRule).HasColumnName("recurrence_rule").HasColumnType("text");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.List)
            .WithMany(l => l.Todos)
            .HasForeignKey(x => x.ListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(u => u.Todos)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => new { x.UserId, x.Status }).HasDatabaseName("idx_todos_user");
        builder.HasIndex(x => x.DueDate).HasFilter("due_date IS NOT NULL").HasDatabaseName("idx_todos_due");
        builder.HasIndex(x => x.ListId).HasDatabaseName("idx_todos_list");
    }
}

public class TodoTagConfiguration : IEntityTypeConfiguration<TodoTag>
{
    public void Configure(EntityTypeBuilder<TodoTag> builder)
    {
        builder.ToTable("todo_tags");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.TodoId).HasColumnName("todo_id").IsRequired();
        builder.Property(x => x.Tag).HasColumnName("tag").HasMaxLength(100).IsRequired();

        builder.HasIndex(x => new { x.TodoId, x.Tag }).IsUnique();

        builder.HasOne(x => x.Todo)
            .WithMany(t => t.Tags)
            .HasForeignKey(x => x.TodoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.Tag).HasDatabaseName("idx_todo_tags_tag");
    }
}

public class TodoAttachmentConfiguration : IEntityTypeConfiguration<TodoAttachment>
{
    public void Configure(EntityTypeBuilder<TodoAttachment> builder)
    {
        builder.ToTable("todo_attachments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.TodoId).HasColumnName("todo_id").IsRequired();
        builder.Property(x => x.Url).HasColumnName("url").IsRequired();
        builder.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(500);
        builder.Property(x => x.FileSize).HasColumnName("file_size");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Todo)
            .WithMany(t => t.Attachments)
            .HasForeignKey(x => x.TodoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
