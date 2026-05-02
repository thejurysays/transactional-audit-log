using System.Text.Json.Nodes;

namespace TransactionalAuditLog.Services;

public sealed class DiffEngine
{
    public string Compute(JsonObject before, JsonObject after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var diff = new JsonObject();

        // Two-pass O(n+m) with no intermediate allocation: Union() would build a HashSet internally;
        // iterating each object's KVPs directly avoids that and uses JsonObject's O(1) key lookup.
        foreach (var (key, oldValue) in before)
        {
            var newValue = after[key];
            if (!JsonNode.DeepEquals(oldValue, newValue))
                diff[key] = new JsonObject { ["old"] = oldValue?.DeepClone(), ["new"] = newValue?.DeepClone() };
        }

        // Pass 2: keys added in after that were not present in before.
        foreach (var (key, newValue) in after)
        {
            if (!before.ContainsKey(key) && !JsonNode.DeepEquals(null, newValue))
                diff[key] = new JsonObject { ["old"] = null, ["new"] = newValue?.DeepClone() };
        }

        return diff.ToJsonString();
    }
}
