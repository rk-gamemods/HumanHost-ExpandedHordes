using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace ExpandedHordes
{
    internal static class TelemetryAccessors
    {
        // Initialization only. Never use ICollection or enumerate an unknown type
        // as a fallback. A not-yet-created authoritative dictionary is unavailable.
        internal static Func<T, int> DictionaryCount<T>(FieldInfo field)
        {
            try
            {
                if (field == null || !field.FieldType.IsGenericType ||
                    field.FieldType.GetGenericTypeDefinition() != typeof(Dictionary<,>)) return null;
                var owner = Expression.Parameter(typeof(T));
                var collection = Expression.Field(owner, field);
                return Expression.Lambda<Func<T, int>>(Expression.Condition(
                    Expression.Equal(collection, Expression.Constant(null, field.FieldType)),
                    Expression.Constant(-1), Expression.Property(collection, "Count")), owner).Compile();
            }
            catch { return null; }
        }
    }

    internal static class LifecycleObservation
    {
        // Count is the no-event sentinel. Hooks supply dictionary membership on
        // both sides of a successfully completed native registration/removal call.
        internal static TelemetryEvent Registration(bool before, bool after, bool restoring) =>
            before || !after ? TelemetryEvent.Count : restoring ? TelemetryEvent.Restored : TelemetryEvent.Fresh;
        internal static TelemetryEvent Removal(bool before, bool after, bool deathNotification) =>
            !before || after ? TelemetryEvent.Count : deathNotification ? TelemetryEvent.Death : TelemetryEvent.OtherRemoval;
    }
}
