using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using PaymentLedger.Domain.Common;

namespace PaymentLedger.Infrastructure.Serialization;

/// <summary>
/// Maps domain events to and from a stable type name plus JSON payload.
/// The type name, not the CLR full name, is what gets persisted, so namespaces can be
/// refactored without rewriting history. Renaming an event class is a breaking change
/// and needs an upcaster.
/// </summary>
public sealed class EventSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<string, Type> _typesByName;
    private readonly Dictionary<Type, string> _namesByType;

    public EventSerializer()
        : this(typeof(IDomainEvent).Assembly)
    {
    }

    public EventSerializer(params Assembly[] assemblies)
    {
        var eventTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IDomainEvent).IsAssignableFrom(t))
            .ToArray();

        _typesByName = eventTypes.ToDictionary(t => t.Name, StringComparer.Ordinal);
        _namesByType = eventTypes.ToDictionary(t => t, t => t.Name);
    }

    public string GetEventType(IDomainEvent @event) =>
        _namesByType.TryGetValue(@event.GetType(), out var name)
            ? name
            : throw new InvalidOperationException($"Event type {@event.GetType().FullName} is not registered.");

    public string Serialize(IDomainEvent @event) =>
        JsonSerializer.Serialize(@event, @event.GetType(), JsonOptions);

    public IDomainEvent Deserialize(string eventType, string json)
    {
        if (!_typesByName.TryGetValue(eventType, out var type))
        {
            throw new InvalidOperationException($"Unknown event type '{eventType}'.");
        }

        return (IDomainEvent)(JsonSerializer.Deserialize(json, type, JsonOptions)
                              ?? throw new InvalidOperationException($"Event payload for '{eventType}' deserialized to null."));
    }
}
