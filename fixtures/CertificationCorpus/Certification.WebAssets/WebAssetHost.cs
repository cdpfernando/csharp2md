// GCPC-028, GCPC-032: this is the project's only C# source. Every other file in this project is one
// of the six excluded-asset classes the audit found dominating the structural inventory and the
// `source/` projection (finding I4 / D-04): a TypeScript file, a compiled JS file with its own
// source map, an image, a zip archive, a package lock and a certificate. The supported-document
// policy that stops all six from producing a `Document` fact, a `source/` artifact and an
// individual diagnostic is a later phase (T9/T10); this task only proves today's over-inclusive
// baseline, which that policy inverts.

namespace Certification.WebAssets;

public sealed class WebAssetHost
{
    public string Describe() => "serves the bundled web assets alongside this C# host";
}
