using System.Reflection;
using System.Reflection.Emit;
using FluentAssertions;

namespace Acmp.Architecture.Tests;

/*
 * DW-026, THE BROAD CHECK — a public aggregate method that NOTHING in production calls.
 *
 * The narrow half (AuthorizationCoverageTests) guards requirements wired to no policy. This guards the
 * wider class the row is actually about: a capability that exists, compiles, is often unit-tested and
 * passing, and is reachable from no production code path. It has fired FIVE times. The most recent was
 * CommitteeMember.SetVotingEligibility, which sat uncalled from P4 until PR #276 while its own unit
 * tests passed the whole time — because a unit test calls the method DIRECTLY, which is exactly why a
 * unit test cannot detect this.
 *
 * ⚠ WHY AN IL WALK RATHER THAN NetArchTest OR A NAMING RULE. The defect is "nothing REACHES this",
 * which is a property of call sites, not of names, namespaces or dependencies. NetArchTest reasons
 * about type-level references and would report a whole type as referenced the moment any ONE member is
 * used. So this reads method bodies: every method in every PRODUCTION assembly is walked far enough to
 * collect its call targets, and an aggregate method absent from that set is reachable from nothing.
 *
 * ⚠ PRODUCTION ASSEMBLIES ONLY, AND THAT IS THE ENTIRE POINT. Scanning the test assemblies too would
 * mark every unit-tested method as called and the check would pass vacuously forever — the same shape
 * as DEF-056's controls, which were green from the day they were written.
 *
 * ⚠ DISPATCH IS RESOLVED, NOT IGNORED. A call through an interface or a base class emits a token for
 * the INTERFACE or BASE method, never the implementation. Treating those as distinct would flag every
 * interface implementation in the codebase as dead.
 *
 * ⚠ THE KEY IS name + parameter COUNT, not the full signature — deliberately LENIENT, so this
 * under-reports rather than crying wolf. A guard that fires falsely is a guard that gets suppressed.
 */
public sealed class AggregateReachabilityTests
{
    // Every opcode by value, built from the BCL's own table rather than a hand-copied list — a
    // hand-copied table is a second source of truth that drifts silently.
    private static readonly Dictionary<short, OpCode> AllOpCodes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode))
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => o.Value, o => o);

    /*
     * ⚠ DISCOVERED FROM DISK, NOT HAND-LISTED, AND THE FIRST DRAFT PROVED WHY. It named Domain and
     * Application assemblies explicitly and omitted every *.Infrastructure one — so the walk never saw
     * VoteChainIntegrityCheck calling Vote.VerifyBallotChain and Vote.VerifyTally, and reported both as
     * unreachable. Two vote-integrity methods were a keystroke away from being recorded as findings when
     * the fault was entirely in the measurement.
     *
     * A hand-maintained list of "everything that counts as production" is the DW-026 defect wearing the
     * costume of a test: it must be updated by the same person who forgot to wire something up. Reading
     * the output directory means a NEW module is covered the moment it builds.
     */
    private static Assembly[] ProductAssemblies() => Directory
        .GetFiles(AppContext.BaseDirectory, "Acmp.*.dll")
        .Where(f => !Path.GetFileName(f).Contains(".Tests.", StringComparison.Ordinal))
        .Select(f => { try { return Assembly.LoadFrom(f); } catch { return null; } })
        .Where(a => a is not null)
        .Select(a => a!)
        .Distinct()
        .ToArray();

    private static string Key(MethodBase m) =>
        $"{m.DeclaringType?.FullName}::{m.Name}/{m.GetParameters().Length}";

    // Collect every method this body calls. Walks the IL properly rather than scanning for opcode
    // bytes: an operand byte can coincide with a call opcode, and a naive scan would INVENT calls that
    // do not exist — inventing calls makes this test PASS, which is the dangerous direction.
    private static void CollectCalls(MethodBase method, ISet<string> into)
    {
        MethodBody? body;
        try { body = method.GetMethodBody(); } catch { return; }
        if (body is null) return;

        byte[] il;
        try { il = body.GetILAsByteArray() ?? Array.Empty<byte>(); } catch { return; }

        var module = method.Module;
        Type[]? typeArgs = null;
        Type[]? methodArgs = null;
        try { typeArgs = method.DeclaringType is { IsGenericType: true } dt ? dt.GetGenericArguments() : null; } catch { }
        try { methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null; } catch { }

        for (var i = 0; i < il.Length;)
        {
            short code = il[i];
            i++;
            if (code == 0xFE && i < il.Length) { code = (short)(0xFE00 | il[i]); i++; }
            if (!AllOpCodes.TryGetValue(code, out var op)) return; // desynced: stop rather than guess

            var operand = op.OperandType;
            if ((operand == OperandType.InlineMethod || operand == OperandType.InlineTok) && i + 4 <= il.Length)
            {
                var token = BitConverter.ToInt32(il, i);
                try
                {
                    var target = module.ResolveMethod(token, typeArgs, methodArgs);
                    if (target is not null) into.Add(Key(target));
                }
                catch { /* not a method token, or unresolvable across assemblies */ }
            }

            i += operand switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI
                    or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString
                    or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * (i + 4 <= il.Length ? BitConverter.ToInt32(il, i) : 0),
                _ => 0,
            };
        }
    }

    /*
     * ⚠ EVERY ENTRY NEEDS A REASON. An allowlist without reasons is where findings go to die — the
     * DEF-081 failure in miniature, where one unexplained path entry blinded a gating scanner to every
     * markdown file in the repository.
     *
     * THE EIGHT BELOW ARE NOT EXEMPTIONS, THEY ARE THE FINDINGS. This guard's first run reported them,
     * each was verified by hand to have no caller outside its own Domain assembly, and all eight are
     * recorded as DEF-084 — the SIXTH firing of DW-026 and the first to catch more than one at a time.
     * They are seeded here so the guard turns green on TODAY'S known set and fails on anything NEW,
     * which is the only way to introduce a guard into a codebase that already has instances. Remove an
     * entry when its capability is wired up; do not add one without a defect reference.
     */
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        // DEF-084: topic lifecycle transitions that exist on the aggregate with no endpoint or handler
        // reaching them. FR-level capabilities built ahead of their surface.
        "Acmp.Modules.Topics.Domain.Topic::Close/3",
        "Acmp.Modules.Topics.Domain.Topic::Convert/3",
        "Acmp.Modules.Topics.Domain.Topic::Reactivate/3",
        "Acmp.Modules.Topics.Domain.Topic::Reclassify/2",
        "Acmp.Modules.Topics.Domain.Topic::Reopen/4",
        // DEF-084: membership lifecycle + delegation predicates with no production caller.
        "Acmp.Modules.Membership.Domain.CommitteeMember::HasExpired/1",
        "Acmp.Modules.Membership.Domain.CommitteeMember::Reactivate/0",
        "Acmp.Modules.Membership.Domain.Delegation::IsActiveAt/1",
    };

    [Fact]
    public void No_public_aggregate_method_is_unreachable_from_production_code()
    {
        var assemblies = ProductAssemblies();
        var called = new HashSet<string>(StringComparer.Ordinal);

        foreach (var asm in assemblies)
        {
            foreach (var type in asm.GetTypes())
            {
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic
                                         | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (var m in type.GetMethods(all)) CollectCalls(m, called);
                foreach (var c in type.GetConstructors(all)) CollectCalls(c, called);
            }
        }

        // The walk is meaningless if it found nothing to walk — assert the instrument before the result.
        called.Should().NotBeEmpty("the IL walk collected no call targets at all, which means it is measuring nothing");

        var domains = assemblies.Where(a => a.GetName().Name!.EndsWith(".Domain", StringComparison.Ordinal)).ToArray();
        domains.Should().NotBeEmpty("no domain assemblies were found to inspect");

        var unreachable = new List<string>();
        foreach (var asm in domains)
        {
            foreach (var type in asm.GetTypes().Where(t => t.IsClass && t.IsPublic && !t.IsAbstract))
            {
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (m.IsSpecialName) continue;                  // property accessors, operators
                    if (m.GetBaseDefinition() != m) continue;        // an override: the base decl is the call target
                    if (m.Name is "ToString" or "Equals" or "GetHashCode" or "Deconstruct") continue;

                    // ⚠ COMPILER-GENERATED MEMBERS ARE NOISE, NOT FINDINGS, and excluding them is what makes
                    // this test readable enough to act on. Every `record` gets a `<Clone>$` the compiler emits
                    // and nothing calls by name; the first run reported 60+ of them and buried the two real
                    // findings among them. A guard whose output must be waded through is a guard that gets
                    // ignored — the failure mode is indistinguishable from having no guard.
                    if (m.Name.StartsWith('<')) continue;
                    if (m.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)) continue;

                    var key = Key(m);
                    if (called.Contains(key) || Allowed.Contains(key)) continue;

                    // Called through an interface? The token names the interface method, never this one.
                    var viaInterface = type.GetInterfaces().Any(i =>
                        called.Contains($"{i.FullName}::{m.Name}/{m.GetParameters().Length}"));
                    if (viaInterface) continue;

                    unreachable.Add(key);
                }
            }
        }

        unreachable.Should().BeEmpty(
            "a public aggregate method that no production code path reaches is a capability wired to nothing "
            + "— the DW-026 class, which has fired five times. If one is intentional, add it to Allowed WITH A "
            + "REASON; if it is the defect, wire it up.\n" + string.Join("\n", unreachable.OrderBy(x => x)));
    }
}
