using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

internal static class PayloadHash
{
    public static T Attach<T>(T dto, Func<T, string, T> withHash) =>
        withHash(dto, CanonicalJson.PayloadContentSha256(dto));
}
