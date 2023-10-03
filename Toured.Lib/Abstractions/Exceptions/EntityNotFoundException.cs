namespace TourEd.Lib.Abstractions.Exceptions;

public class EntityNotFoundException : Exception
{
    public Type EntityType { get; }
    public object Key { get; }

    private EntityNotFoundException(Type entityType, object key)
    {
        EntityType = entityType;
        Key = key;
    }
    public override string Message => $"No {EntityType.Name} with key {Key} found.";
    public static EntityNotFoundException Create<T>(object key) => new(typeof(T), key);
}
