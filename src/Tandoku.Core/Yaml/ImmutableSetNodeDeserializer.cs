namespace Tandoku.Yaml;

using System;
using System.Collections.Immutable;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.Utilities;

// Inspired by https://github.com/aaubry/YamlDotNet/issues/551#issuecomment-844971467
internal sealed class ImmutableSetNodeDeserializer<T> : INodeDeserializer
{
    public bool Deserialize(
        IParser reader,
        Type expectedType,
        Func<IParser, Type, object?> nestedObjectDeserializer,
        out object? value)
    {
        if (expectedType != typeof(IImmutableSet<T>))
        {
            value = default;
            return false;
        }

        // Note: using ImmutableSortedSet for serialization stability
        var builder = ImmutableSortedSet.CreateBuilder<T>();
        DeserializeSequence(reader, builder, nestedObjectDeserializer);
        value = builder.ToImmutable();
        return true;
    }

    private static void DeserializeSequence(
        IParser parser,
        ImmutableSortedSet<T>.Builder builder,
        Func<IParser, Type, object?> nestedDeserializer)
    {
        parser.Consume<SequenceStart>();
        while (!parser.TryConsume<SequenceEnd>(out _))
        {
            var value = nestedDeserializer(parser, typeof(T));
            if (value is IValuePromise)
            {
                throw new NotSupportedException();
            }
            else
            {
                builder.Add(TypeConverter.ChangeType<T>(value));
            }
        }
    }
}
