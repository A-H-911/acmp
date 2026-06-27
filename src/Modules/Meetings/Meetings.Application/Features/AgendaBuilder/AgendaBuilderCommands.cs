using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Modules.Meetings.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.AgendaBuilder;

// W6 agenda-building micro-commands (build/reorder authority = Agenda.Publish, Chairman/Secretary).
// Each loads the meeting's single agenda, mutates it, and returns the refreshed AgendaDto so the SPA
// re-renders. Topic key/title/urgent are display snapshots passed by the caller (the builder sourced
// them from the Prepared-topics pool) — Meetings never reads Topics' tables (ADR-0001). These builder
// edits are not individually audited; the governance event is AgendaPublished (see PublishAgenda).
// ponytail: discrete commands per operation match the design's distinct controls; the shared loader
// keeps them one line each.

internal static class AgendaBuilderRoles
{
    public static readonly string[] Editors = { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

internal static class AgendaLoader
{
    public static async Task<Agenda> ForMeetingAsync(IMeetingsDbContext db, Guid meetingId, CancellationToken ct) =>
        await db.Agendas.FirstOrDefaultAsync(a => a.MeetingId == meetingId, ct)
            ?? throw new KeyNotFoundException("Agenda not found for this meeting.");
}

// ---- add ----
public sealed record AddAgendaItemCommand(
    Guid MeetingId, Guid TopicId, string TopicKey, string TopicTitle, bool Urgent,
    int TimeboxMinutes, Guid? PresenterUserId, string? PresenterName) : IRequest<AgendaDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = AgendaBuilderRoles.Editors;
}

public sealed class AddAgendaItemValidator : AbstractValidator<AddAgendaItemCommand>
{
    public AddAgendaItemValidator()
    {
        RuleFor(x => x.MeetingId).NotEmpty();
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.TopicTitle).NotEmpty();
        RuleFor(x => x.TimeboxMinutes).InclusiveBetween(AgendaItem.MinTimebox, AgendaItem.MaxTimebox);
    }
}

public sealed class AddAgendaItemHandler(IMeetingsDbContext db) : IRequestHandler<AddAgendaItemCommand, AgendaDto>
{
    public async Task<AgendaDto> Handle(AddAgendaItemCommand request, CancellationToken ct)
    {
        var agenda = await AgendaLoader.ForMeetingAsync(db, request.MeetingId, ct);
        agenda.AddItem(request.TopicId, request.TopicKey, request.TopicTitle, request.Urgent,
            request.TimeboxMinutes, request.PresenterUserId, request.PresenterName);
        await db.SaveChangesAsync(ct);
        return MeetingMapping.ToDto(agenda);
    }
}

// ---- remove ----
public sealed record RemoveAgendaItemCommand(Guid MeetingId, Guid TopicId) : IRequest<AgendaDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = AgendaBuilderRoles.Editors;
}

public sealed class RemoveAgendaItemHandler(IMeetingsDbContext db) : IRequestHandler<RemoveAgendaItemCommand, AgendaDto>
{
    public async Task<AgendaDto> Handle(RemoveAgendaItemCommand request, CancellationToken ct)
    {
        var agenda = await AgendaLoader.ForMeetingAsync(db, request.MeetingId, ct);
        agenda.RemoveItem(request.TopicId);
        await db.SaveChangesAsync(ct);
        return MeetingMapping.ToDto(agenda);
    }
}

// ---- reorder (AC-044: pointer drag and keyboard move-up/-down both send a ±1 delta) ----
public sealed record MoveAgendaItemCommand(Guid MeetingId, Guid TopicId, int Delta) : IRequest<AgendaDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = AgendaBuilderRoles.Editors;
}

public sealed class MoveAgendaItemHandler(IMeetingsDbContext db) : IRequestHandler<MoveAgendaItemCommand, AgendaDto>
{
    public async Task<AgendaDto> Handle(MoveAgendaItemCommand request, CancellationToken ct)
    {
        var agenda = await AgendaLoader.ForMeetingAsync(db, request.MeetingId, ct);
        agenda.MoveItem(request.TopicId, request.Delta);
        await db.SaveChangesAsync(ct);
        return MeetingMapping.ToDto(agenda);
    }
}

// ---- time-box ----
public sealed record SetAgendaItemTimeboxCommand(Guid MeetingId, Guid TopicId, int Minutes) : IRequest<AgendaDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = AgendaBuilderRoles.Editors;
}

public sealed class SetAgendaItemTimeboxHandler(IMeetingsDbContext db) : IRequestHandler<SetAgendaItemTimeboxCommand, AgendaDto>
{
    public async Task<AgendaDto> Handle(SetAgendaItemTimeboxCommand request, CancellationToken ct)
    {
        var agenda = await AgendaLoader.ForMeetingAsync(db, request.MeetingId, ct);
        agenda.SetTimebox(request.TopicId, request.Minutes);
        await db.SaveChangesAsync(ct);
        return MeetingMapping.ToDto(agenda);
    }
}

// ---- presenter ----
public sealed record AssignPresenterCommand(Guid MeetingId, Guid TopicId, Guid PresenterUserId, string PresenterName) : IRequest<AgendaDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = AgendaBuilderRoles.Editors;
}

public sealed class AssignPresenterValidator : AbstractValidator<AssignPresenterCommand>
{
    public AssignPresenterValidator()
    {
        RuleFor(x => x.MeetingId).NotEmpty();
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.PresenterUserId).NotEmpty();
        RuleFor(x => x.PresenterName).NotEmpty();
    }
}

public sealed class AssignPresenterHandler(IMeetingsDbContext db) : IRequestHandler<AssignPresenterCommand, AgendaDto>
{
    public async Task<AgendaDto> Handle(AssignPresenterCommand request, CancellationToken ct)
    {
        var agenda = await AgendaLoader.ForMeetingAsync(db, request.MeetingId, ct);
        agenda.AssignPresenter(request.TopicId, request.PresenterUserId, request.PresenterName);
        await db.SaveChangesAsync(ct);
        return MeetingMapping.ToDto(agenda);
    }
}
