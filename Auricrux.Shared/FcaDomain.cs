namespace Auricrux.Shared.FcaDomain;

/// <summary>
/// FCA Project entity (imported from fca-ecosystem)
/// </summary>
public class Project
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? BidId { get; set; }
    public Guid? AwardId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; } = ProjectStatus.Planned;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public enum ProjectStatus
{
    Planned = 0,
    Active = 1,
    OnHold = 2,
    Completed = 3
}

/// <summary>
/// FCA Member entity (User representation from fca-ecosystem)
/// </summary>
public class Member
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public MemberStatus Status { get; set; } = MemberStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public enum MemberStatus
{
    Active = 0,
    Inactive = 1,
    Suspended = 2
}

/// <summary>
/// FCA Role Names (from fca-ecosystem)
/// </summary>
public static class FcaRoleNames
{
    public const string Admin = "Admin";
    public const string Pm = "PM";
    public const string Field = "Field";
    public const string Owner = "Owner";
    public const string Accountant = "Accountant";

    public static readonly string[] All = [Admin, Pm, Field, Owner, Accountant];
}

/// <summary>
/// Academy Lesson entity (from fca-ecosystem)
/// </summary>
public class AcademyLesson
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid ModuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public int DurationMinutes { get; set; }
    public AcademyLessonStatus Status { get; set; } = AcademyLessonStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public enum AcademyLessonStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}
