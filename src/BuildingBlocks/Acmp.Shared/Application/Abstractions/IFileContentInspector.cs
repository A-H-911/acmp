namespace Acmp.Shared.Application.Abstractions;

// C-FILE-01 (P16-B4): validates that an upload's ACTUAL content matches its DECLARED content type, so a
// caller cannot smuggle an executable/HTML/other payload past the declared-Content-Type allow-list by simply
// mislabelling it. Reads only a bounded head of the stream (magic bytes live in the first bytes) and restores
// the stream position, so the caller can still stream the full body to storage afterwards.
public interface IFileContentInspector
{
    // True when the content's magic bytes (or, for text formats with no binary signature, its structure) are
    // consistent with declaredContentType. False (fail-closed) on a mismatch or an unrecognised declared type.
    bool ContentMatchesDeclared(Stream content, string declaredContentType);
}
