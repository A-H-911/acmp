namespace Acmp.Shared.Domain;

// A DELIBERATE refusal of a request on the current state of the record — an illegal transition, a duplicate,
// a mutation of a frozen vote or issued decision, a precondition an aggregate guard or an application handler
// checks and rejects with a message written for the caller. The API maps it to HTTP 409 Conflict
// (docs/domain/architecture-detail.md §7.4 "409 on illegal transition / mutation of frozen vote/decision",
// NFR-041 / ADR-0009).
//
// DEF-156: this subclasses InvalidOperationException because every guard in the codebase threw that type and
// every assertion on them (FluentAssertions Throw<T>) accepts a derived type — so adopting it changed a NAME in
// each guard, not a contract. What it buys is discrimination at the API boundary: a BARE
// InvalidOperationException — .Single() on an empty sequence, an EF tracking conflict, a framework or
// integration fault — is no longer reported to the client as a "Conflict" it could resolve, and no longer
// hidden from a 5xx alert. Throw THIS for a rule the caller can act on; throw the bare type for a fault.
public sealed class DomainRuleException : InvalidOperationException
{
    public DomainRuleException(string message) : base(message) { }
}
