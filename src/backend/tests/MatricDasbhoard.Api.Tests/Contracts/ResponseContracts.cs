namespace MatricDasbhoard.Api.Tests.Contracts;

/// <summary>Frozen public school spatial contract, no private correction metadata.</summary>
internal record SpatialStateContract(string EmisNumber, System.Text.Json.JsonElement? Boundary,
    System.Text.Json.JsonElement? Location, bool HasLocationOverride, string LocationSource,
    long Revision, double? AreaSquareMetres, List<string> Warnings);

/// <summary>Frozen v1 school detail fields retained for existing API consumers.</summary>
internal record SchoolDetailV1Contract(string Id, string Name, string Province, string Circuit,
    int TotalWrote, int TotalPassed, double PassRate, int? TotalAchieved,
    int? SubjectsOfferedYear = null, List<OfferedSubjectContract>? SubjectsOffered = null);

/// <summary>Frozen subject offered by a school, published with school detail.</summary>
internal record OfferedSubjectContract(string Code, string Name, int Year, int TotalWrote, double? PercentAchieved);

/// <summary>Frozen per-subject school result contract.</summary>
internal record SchoolSubjectContract(string SubjectId, string Code, string Name, int TotalWrote,
    int? Achieved, double? PercentAchieved);

/// <summary>Frozen per-subject school results contract.</summary>
internal record SchoolSubjectsContract(string SchoolId, int? Year, List<int> AvailableYears,
    List<SchoolSubjectContract> Subjects);

/// <summary>Frozen v1 per-year region pass rate trend contract.</summary>
internal record RegionYearContract(int Year, int TotalWrote, int TotalPassed, double PassRate);

/// <summary>Frozen v1 region (country/province/district) stats contract.</summary>
internal record RegionStatsContract(string Scope, string Name, string? Province, int Year,
    int SchoolCount, int TotalWrote, int TotalPassed, double PassRate, List<RegionYearContract> Years);

/// <summary>Frozen aggregated subject row contract for a region.</summary>
internal record RegionSubjectContract(string Code, string Name, int TotalWrote, int? Achieved,
    double? PercentAchieved, int SchoolCount);

/// <summary>Frozen aggregated region subjects contract.</summary>
internal record RegionSubjectsContract(string Scope, string Name, string? Province, int? Year,
    List<int> AvailableYears, int SchoolCount, List<RegionSubjectContract> Subjects);

// Auth
internal record AuthTokensResponse(string AccessToken, string RefreshToken, bool RequiresTwoFactor, string? ChallengeToken);
internal record TwoFactorSetupContract(string SharedKey, string AuthenticatorUri);
internal record TwoFactorVerifySetupContract(List<string> RecoveryCodes);
internal record RegisterUserResponse(Guid Id);
internal record ExternalChallengeContract(string AuthorizationUrl);
internal record ExternalCallbackContract(string? AccessToken, string? RefreshToken, bool IsNewUser, string Provider, bool IsLinkOnly);
internal record ExternalProviderContract(string Name, string DisplayName);

// Users
internal record UserMeResponse(Guid Id, string Username, string Email, string? FirstName, string? LastName,
    string? PhoneNumber, string? Bio, bool HasAvatar, List<string> Roles, List<string> Permissions,
    bool EmailConfirmed, bool TwoFactorEnabled);

// Admin - Users
internal record AdminUserResponse(Guid Id, string Username, string Email, string? FirstName, string? LastName,
    string? PhoneNumber, string? Bio, bool HasAvatar, List<string> Roles,
    bool EmailConfirmed, bool LockoutEnabled, DateTimeOffset? LockoutEnd, int AccessFailedCount, bool IsLockedOut,
    bool TwoFactorEnabled);
internal record AdminUserListResponse(List<AdminUserResponse> Items, int TotalCount, int PageNumber, int PageSize,
    int TotalPages, bool HasPreviousPage, bool HasNextPage);

// Admin - Roles
internal record AdminRoleResponse(Guid Id, string Name, string? Description, bool IsSystem, int UserCount);
internal record RoleDetailResponse(Guid Id, string Name, string? Description, bool IsSystem,
    List<string> Permissions, int UserCount);
internal record CreateRoleResponse(Guid Id);
internal record PermissionGroupResponse(string Category, List<string> Permissions);

// Jobs
internal record RecurringJobResponse(string Id, string Cron, DateTimeOffset? NextExecution,
    DateTimeOffset? LastExecution, string? LastStatus, bool IsPaused, DateTimeOffset? CreatedAt);
internal record RecurringJobDetailResponse(string Id, string Cron, DateTimeOffset? NextExecution,
    DateTimeOffset? LastExecution, string? LastStatus, bool IsPaused, DateTimeOffset? CreatedAt,
    List<JobExecutionResponse> ExecutionHistory);
internal record JobExecutionResponse(string JobId, string Status, DateTimeOffset? StartedAt,
    TimeSpan? Duration, string? Error);

// Admin - OAuth Providers
internal record OAuthProviderConfigContract(string Provider, string DisplayName, bool IsEnabled,
    string? ClientId, bool HasClientSecret, string Source, DateTime? UpdatedAt, Guid? UpdatedBy);

// Audit
internal record AuditEventContract(Guid Id, Guid? UserId, string Action, string? TargetEntityType,
    Guid? TargetEntityId, string? Metadata, DateTime CreatedAt);
internal record ListAuditEventsContract(List<AuditEventContract> Items, int TotalCount, int PageNumber,
    int PageSize, int TotalPages, bool HasPreviousPage, bool HasNextPage);
