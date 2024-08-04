using Azure.Security.KeyVault.Secrets;
using System.Text;

namespace Isolatedsample;

public sealed class ComplexKeyVaultSecretNameParser : IKeyVaultSecretNameParser
{
    public static readonly IKeyVaultSecretNameParser Instance = new ComplexKeyVaultSecretNameParser();

    private ComplexKeyVaultSecretNameParser() { }

    public string Parse(KeyVaultSecret secret)
    {
        StringBuilder sb = new ();
        ReadOnlySpan<char> name = secret.Name;

        bool lastWasDash = false;
        while (!name.IsEmpty)
        {
            char input = name[0];
            name = name[1..];

            char? maybeOutput = input switch
            {
                '-' => ParseDash(ref name),
                (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') => input,
                _ => throw new ArgumentException("Unexpected character in secret name"),
            };

            if (maybeOutput is not { } output)
                continue;

            if (output == ':')
            {
                if (lastWasDash)
                {
                    return sb.ToString()[..^1];
                }
                else
                {
                    lastWasDash = true;
                }
            }
            else
            {
                lastWasDash = false;
            }

            sb.Append(output);
        }

        string key = sb.ToString();
        return lastWasDash ? key[..^1] : key;

        static char? ParseDash(ref ReadOnlySpan<char> name)
        {
            if (name.IsEmpty)
                return null;

            char c = name[0];
            name = name[1..];
            return c switch
            {
                '-' => ':',
                'x' or 'X' => ParseHex(ref name, 2),
                'u' or 'U' => ParseHex(ref name, 4),
                _ => null,
            };
        }

        static char? ParseHex(ref ReadOnlySpan<char> name, int length)
        {
            if (name.Length < length)
            {
                name = default;
                return null;
            }

            string str = new (name[..length]);
            name = name[length..];

            try
            {
                return (char)Convert.ToUInt16(str, 16);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
