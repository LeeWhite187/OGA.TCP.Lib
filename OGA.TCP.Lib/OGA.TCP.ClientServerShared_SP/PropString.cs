using System;
using Newtonsoft.Json.Linq;

namespace OGA.TCP
{
    /// <summary>
    /// Composer/parser for registration prop strings: the '"key":"value"' fragments carried in a
    ///     ConnRegisterDTO's Props array.
    /// The fragment format is a wire contract shared with the WebSocket library and with fielded v1/v2
    ///     clients, so this class changes HOW fragments are built and read, never WHAT they look like:
    ///     Compose emits byte-identical output to the historical hand-concatenation for values without
    ///     special characters, and TryParse accepts everything the historical parser accepted.
    /// What it fixes over the historical handling (OI-29): values containing colons no longer truncate
    ///     (the old parser split on every colon), values containing quotes or backslashes are properly
    ///     escaped instead of producing malformed fragments, and callers can match keys exactly instead
    ///     of by substring.
    /// </summary>
    static public class PropString
    {
        /// <summary>
        /// Composes a prop fragment from a key and value, with proper string escaping.
        /// For keys and values without characters needing escapes (every value the libraries have
        ///     historically emitted), the output is byte-identical to '"key":"value"' hand-concatenation,
        ///     so older parsers on the far end see exactly what they have always seen.
        /// </summary>
        /// <param name="key">The prop key. Never null; composed as-is (keys are library-controlled literals).</param>
        /// <param name="value">The prop value. Null composes as an empty string value.</param>
        /// <returns></returns>
        static public string Compose(string key, string value)
        {
            // JsonConvert.ToString produces the quoted, escaped json string literal...
            return Newtonsoft.Json.JsonConvert.ToString(key ?? "") + ":" +
                   Newtonsoft.Json.JsonConvert.ToString(value ?? "");
        }

        /// <summary>
        /// Parses a prop fragment into its key and value.
        /// Primary path: the fragment is parsed as the json object property it is ('{fragment}'), which
        ///     preserves colons inside values and honors escaping.
        /// Fallback path: fragments that fail json parsing (possible from hand-built legacy senders) are
        ///     split on the FIRST colon only, with quotes trimmed - everything the historical
        ///     split-on-every-colon parser handled, without its value truncation.
        /// Returns true when a key was recovered; false for null/empty/keyless fragments.
        /// NOTE: Key matching policy belongs to the caller - match recovered keys with exact,
        ///     case-insensitive comparison, never by substring.
        /// </summary>
        /// <param name="fragment">The prop fragment to parse.</param>
        /// <param name="key">The recovered key. Never null on success.</param>
        /// <param name="value">The recovered value. Never null on success; empty when the fragment carried none.</param>
        /// <returns></returns>
        static public bool TryParse(string fragment, out string key, out string value)
        {
            key = null;
            value = null;

            if (string.IsNullOrWhiteSpace(fragment))
                return false;

            // Primary path: the fragment is a json object property...
            try
            {
                var jo = JObject.Parse("{" + fragment + "}");
                foreach (var prop in jo.Properties())
                {
                    // A well-formed fragment holds exactly one property; take the first.
                    // (Json permits an empty property name; a keyless prop is meaningless here, so it
                    //  falls through to the fallback, which rejects it.)
                    var jval = prop.Value as JValue;
                    if (jval != null && prop.Name.Length > 0)
                    {
                        key = prop.Name;
                        // JValue.ToString() yields the raw (unquoted, unescaped) value for strings,
                        //  and the literal text for numbers/booleans a nonstandard sender might emit...
                        value = jval.Type == JTokenType.Null ? "" : jval.ToString();
                        return true;
                    }

                    // A structured value (object/array) is not a legal prop; fall through to the legacy split.
                    break;
                }
            }
            catch (Exception)
            {
                // Not valid json; fall through to the legacy split.
            }

            // Fallback path: first-colon split with quote trimming...
            int idx = fragment.IndexOf(':');
            if (idx < 0)
            {
                // No separator: the whole fragment is a key with no value (historical parsers ignored
                //  these; callers see an empty value).
                key = fragment.Trim().Trim('"');
                value = "";
                return key.Length > 0;
            }

            key = fragment.Substring(0, idx).Trim().Trim('"');
            value = fragment.Substring(idx + 1).Trim().Trim('"');
            return key.Length > 0;
        }
    }
}
