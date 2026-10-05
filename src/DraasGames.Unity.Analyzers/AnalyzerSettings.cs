using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DraasGames.Unity.Analyzers
{
    internal sealed class AnalyzerSettings
    {
        private const string PlacementKey = "draas_unity_serialized_field_placement";
        private const string OrderKey = "draas_unity_callback_order";
        private const string AdditionalFileSuffix = ".DraasGames.Unity.Analyzers.additionalfile";
        private const string DefaultOrderText =
            "Reset,OnValidate,Awake,OnEnable,Start,FixedUpdate,Update,LateUpdate,OnDisable,OnDestroy";

        private static readonly ImmutableArray<string> DefaultOrder =
            ImmutableArray.Create(DefaultOrderText.Split(','));

        private readonly string placementText;
        private readonly string orderText;

        private AnalyzerSettings(string placementText, string orderText)
        {
            this.placementText = placementText;
            this.orderText = orderText;
            SerializedFieldsFirst = placementText.Trim() != "last";
            CallbackOrder = ParseOrder(orderText);
        }

        public bool SerializedFieldsFirst { get; }

        public ImmutableArray<string> CallbackOrder { get; }

        public static AnalyzerSettings Load(AnalyzerOptions options, CancellationToken cancellationToken)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in options.AdditionalFiles.Where(IsConfigurationFile)
                         .OrderBy(file => file.Path, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = file.GetText(cancellationToken);
                if (text == null)
                {
                    continue;
                }

                foreach (var line in text.Lines)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = line.ToString().Trim();
                    if (entry.Length == 0 || entry[0] == '#' || entry[0] == ';')
                    {
                        continue;
                    }

                    var equals = entry.IndexOf('=');
                    if (equals < 0)
                    {
                        continue;
                    }

                    var key = entry.Substring(0, equals).Trim();
                    if (key == PlacementKey || key == OrderKey)
                    {
                        values[key] = entry.Substring(equals + 1).Trim();
                    }
                }
            }

            return new AnalyzerSettings(
                values.TryGetValue(PlacementKey, out var placement) ? placement : "first",
                values.TryGetValue(OrderKey, out var order) ? order : DefaultOrderText);
        }

        public AnalyzerSettings ForTree(AnalyzerConfigOptionsProvider provider, SyntaxTree tree)
        {
            var options = provider.GetOptions(tree);
            var hasPlacement = options.TryGetValue(PlacementKey, out var placement);
            var hasOrder = options.TryGetValue(OrderKey, out var order);
            if (!hasPlacement && !hasOrder)
            {
                return this;
            }

            return new AnalyzerSettings(
                hasPlacement ? placement ?? string.Empty : placementText,
                hasOrder ? order ?? string.Empty : orderText);
        }

        private static bool IsConfigurationFile(AdditionalText file)
        {
            var name = Path.GetFileName(file.Path);
            if (!name.EndsWith(AdditionalFileSuffix, StringComparison.Ordinal))
            {
                return false;
            }

            var prefix = name.Substring(0, name.Length - AdditionalFileSuffix.Length);
            return prefix.Length > 0 && prefix.IndexOf('.') < 0;
        }

        private static ImmutableArray<string> ParseOrder(string text)
        {
            var names = text.Split(',');
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = ImmutableArray.CreateBuilder<string>(names.Length);
            foreach (var value in names)
            {
                var name = value.Trim();
                if (!DefaultOrder.Contains(name) || !seen.Add(name))
                {
                    return DefaultOrder;
                }

                result.Add(name);
            }

            return result.ToImmutable();
        }
    }
}
