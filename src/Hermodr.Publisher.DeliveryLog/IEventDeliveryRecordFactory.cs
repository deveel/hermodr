namespace Hermodr;

/// <summary>
/// Provides a mechanism to convert an <see cref="EventDeliveryRecord"/> into a
/// storage-specific record type.
/// </summary>
/// <typeparam name="TTarget">
/// The type of the record that the storage implementation understands.
/// </typeparam>
public interface IEventDeliveryRecordFactory<TTarget>
{
    /// <summary>
    /// Creates a new instance of <typeparamref name="TTarget"/> by copying the
    /// values from the given <paramref name="source"/> record.
    /// </summary>
    /// <param name="source">
    /// The source record to copy values from.
    /// </param>
    /// <returns>
    /// A new <typeparamref name="TTarget"/> instance populated with the source values.
    /// </returns>
    TTarget CreateRecord(EventDeliveryRecord source);
}
