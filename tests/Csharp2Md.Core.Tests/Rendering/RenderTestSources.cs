using Csharp2Md.Core.Rendering;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Rendering;

/// <summary>
/// Source samples covering the constructs AD-002 names as the silent-loss risks for structural
/// rendering: usings, inter-member code, <c>#region</c>, and top-level statements.
/// </summary>
internal static class RenderTestSources
{
    public const string UsingsAndBlockNamespace = """
        // File header comment.
        using System;
        using System.Threading.Tasks;

        namespace Acme.Orders
        {
            /// <summary>Places orders.</summary>
            public sealed class OrderService
            {
                private readonly int _retries = 3;

                /// <summary>Places an order.</summary>
                /// <param name="id">The order id.</param>
                public async Task PlaceOrderAsync(int id)
                {
                    for (var i = 0; i < _retries; i++)
                    {
                        Console.WriteLine(id);
                    }

                    await Task.Delay(1);
                }
            }
        }

        """;

    public const string FileScopedNamespaceWithRegions = """
        using System;

        namespace Acme.Payments;

        #region Services

        public class PaymentService
        {
            #region Fields
            private int _count;
            #endregion

            // A comment between members.

            public void Charge() => _count++;
        }

        #endregion

        """;

    public const string TopLevelStatements = """
        using System;

        Console.WriteLine("start");

        var total = 1 + 2;
        Console.WriteLine(total);

        """;

    public const string NestedTypesAndEnum = """
        namespace Acme.Shared;

        public enum Status
        {
            Pending,
            Shipped,
            Cancelled,
        }

        public class Outer
        {
            public class Inner
            {
                public int Value;
            }

            public enum Mode { Fast, Slow }
        }

        """;

    public const string RecordWithoutBody = """
        namespace Acme.Shared;

        public sealed record OrderPlaced(int Id, string Customer);

        public readonly record struct Money(decimal Amount);

        """;

    /// <summary>
    /// Every member kind the renderer titles, plus doc comments carrying <c>cref</c> and
    /// <c>paramref</c> references.
    /// </summary>
    public const string AllMemberKinds = """
        namespace Acme.Kinds;

        public delegate void WidgetChanged(int id);

        public interface IResettable
        {
            void Reset();
        }

        public class Widget : IResettable
        {
            public event WidgetChanged Field;

            public event WidgetChanged Property
            {
                add { }
                remove { }
            }

            private int _value, _other;

            static Widget() { }

            /// <summary>Creates a <see cref="Widget"/>.</summary>
            /// <param name="value">Initial value.</param>
            public Widget(int value) => _value = value;

            ~Widget() { }

            /// <summary>Gets the value held by this <see cref="Widget"/>.</summary>
            public int Value => _value;

            public int this[int index] => _value + index;

            /// <summary>Adds <paramref name="left"/> to <paramref name="right"/>.</summary>
            public static Widget operator +(Widget left, Widget right) => left;

            public static explicit operator int(Widget widget) => widget._value;

            public void Reset() => _value = 0;

            public struct Nested { }
        }

        """;

    /// <summary>No trailing newline, and a backtick run that would break a naive code fence.</summary>
    public const string BackticksAndNoTrailingNewline =
        "namespace Acme.Docs;\n\npublic class Fenced\n{\n    public string Text = \"``` not a fence ```\";\n}";

    public static RenderedDocument Render(string source, string relativePath = "Orders/OrderService.cs") =>
        new MarkdownRenderer().Render(
            new RenderContext(relativePath, CSharpSyntaxTree.ParseText(source), SemanticModel: null));
}
