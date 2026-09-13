using Acmp.Modules.Notifications.Application.Abstractions;
using Acmp.Modules.Notifications.Application.Contracts;
using Acmp.Modules.Notifications.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Notifications;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Notifications.Application.Features.Preferences;

// FR-133 / AC-160 (DEC-186): the signed-in member's own notification preferences. Both requests are scoped
// to ICurrentUser.UserId and take no user parameter, so a member can only ever read or change their own
// (guardrail 4 — the scope is the authorization). Only the in-app channel is read or written here.
public sealed record GetNotificationPreferencesQuery : IRequest<NotificationPreferencesDto>;

public sealed record UpdateNotificationPreferencesCommand(IReadOnlyList<NotificationPreferenceChange> Items)
    : IRequest<NotificationPreferencesDto>;

public sealed class UpdateNotificationPreferencesValidator : AbstractValidator<UpdateNotificationPreferencesCommand>
{
    public UpdateNotificationPreferencesValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one preference is required.");
        RuleForEach(x => x.Items).Must(i => i is not null && NotificationCategories.Contains(i.Category))
            .WithMessage("Unknown notification event type.");
        RuleFor(x => x.Items).Must(items => items.Select(i => i?.Category).Distinct().Count() == items.Count)
            .When(x => x.Items is not null).WithMessage("Each event type may appear only once.");
    }
}

public sealed class GetNotificationPreferencesHandler : IRequestHandler<GetNotificationPreferencesQuery, NotificationPreferencesDto>
{
    private readonly INotificationsDbContext _db;
    private readonly ICurrentUser _user;

    public GetNotificationPreferencesHandler(INotificationsDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public Task<NotificationPreferencesDto> Handle(GetNotificationPreferencesQuery request, CancellationToken ct) =>
        NotificationPreferenceReader.ReadAsync(_db, NotificationPreferenceReader.Caller(_user), ct);
}

public sealed class UpdateNotificationPreferencesHandler
    : IRequestHandler<UpdateNotificationPreferencesCommand, NotificationPreferencesDto>
{
    private readonly INotificationsDbContext _db;
    private readonly ICurrentUser _user;

    public UpdateNotificationPreferencesHandler(INotificationsDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task<NotificationPreferencesDto> Handle(UpdateNotificationPreferencesCommand request, CancellationToken ct)
    {
        var userId = NotificationPreferenceReader.Caller(_user);
        var categories = request.Items.Select(i => i.Category).ToList();
        var existing = await _db.NotificationPreferences
            .Where(p => p.UserId == userId && p.Channel == NotificationChannels.InApp && categories.Contains(p.Category))
            .ToDictionaryAsync(p => p.Category, ct);

        foreach (var item in request.Items)
        {
            if (existing.TryGetValue(item.Category, out var row)) row.Set(item.InApp);
            else _db.NotificationPreferences.Add(
                NotificationPreference.Create(userId, item.Category, NotificationChannels.InApp, item.InApp));
        }

        await _db.SaveChangesAsync(ct);
        return await NotificationPreferenceReader.ReadAsync(_db, userId, ct);
    }
}

internal static class NotificationPreferenceReader
{
    public static string Caller(ICurrentUser user) =>
        user.UserId ?? throw new UnauthorizedAccessException("Authentication required.");

    // Every catalog event type, in catalog order; a type with no stored in-app row reads as on (DEC-186 e3).
    public static async Task<NotificationPreferencesDto> ReadAsync(INotificationsDbContext db, string userId, CancellationToken ct)
    {
        var off = await db.NotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == userId && p.Channel == NotificationChannels.InApp && !p.IsEnabled)
            .Select(p => p.Category)
            .ToListAsync(ct);

        return new NotificationPreferencesDto(NotificationCategories.All
            .Select(c => new NotificationPreferenceDto(c.Name, c.Group, !off.Contains(c.Name)))
            .ToList());
    }
}
