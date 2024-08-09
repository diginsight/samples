using System.Diagnostics.CodeAnalysis;

namespace AzureProvisioning.Configurations;

internal sealed class ArmClientCredentialsCollection
    : Dictionary<string, ArmClientCredentialsOptions>, IReadOnlyDictionary<string, IArmClientCredentialsOptions>
{
    IEnumerable<string> IReadOnlyDictionary<string, IArmClientCredentialsOptions>.Keys => Keys;

    IEnumerable<IArmClientCredentialsOptions> IReadOnlyDictionary<string, IArmClientCredentialsOptions>.Values => Values;

    IArmClientCredentialsOptions IReadOnlyDictionary<string, IArmClientCredentialsOptions>.this[string key] => this[key];

    bool IReadOnlyDictionary<string, IArmClientCredentialsOptions>.TryGetValue(string key, [MaybeNullWhen(false)] out IArmClientCredentialsOptions value)
    {
        if (TryGetValue(key, out ArmClientCredentialsOptions? value0))
        {
            value = value0;
            return true;
        }
        else
        {
            value = null;
            return false;
        }
    }

    IEnumerator<KeyValuePair<string, IArmClientCredentialsOptions>> IEnumerable<KeyValuePair<string, IArmClientCredentialsOptions>>.GetEnumerator()
    {
        return ((IEnumerable<KeyValuePair<string, ArmClientCredentialsOptions>>)this)
            .Select(static kvp => new KeyValuePair<string, IArmClientCredentialsOptions>(kvp.Key, kvp.Value))
            .GetEnumerator();
    }
}
