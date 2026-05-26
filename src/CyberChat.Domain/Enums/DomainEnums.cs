namespace CyberChat.Domain.Enums;

public enum ConversationType
{
    Direct,
    Group
}

public enum MemberRole
{
    Owner,
    Admin,
    Member
}

public enum MessageContentType
{
    Text,
    Image,
    Video,
    Audio,
    File,
    Sticker,
    Gif
}

public enum AttachmentType
{
    Image,
    Video,
    Audio,
    File,
    Sticker,
    Gif
}

public enum FriendshipStatus
{
    Pending,
    Accepted,
    Rejected,
    Blocked
}

public enum FriendRequestStatus
{
    Pending,
    Accepted,
    Rejected,
    Cancelled
}

public enum StoryMediaType
{
    Image,
    Video
}

public enum TodoPriority
{
    Low,
    Medium,
    High,
    Urgent
}

public enum TodoStatus
{
    Todo,
    InProgress,
    Done,
    Cancelled
}

public enum DeviceType
{
    Ios,
    Android,
    Web,
    Desktop
}
