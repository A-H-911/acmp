using FluentValidation;

namespace Acmp.Modules.Membership.Application.Features.InviteMember;

public sealed class InviteMemberValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberValidator()
    {
        RuleFor(x => x.KeycloakUserId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Role).IsInEnum();
    }
}
