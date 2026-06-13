using System;
using System.Collections.Generic;

namespace WeThinks.Mcp.Editor
{
    /// <summary>
    /// Typed accessor over a deserialized JSON params object so handlers don't
    /// repeat casting/coercion logic. Missing keys return supplied defaults.
    /// </summary>
    public sealed class CommandParams
    {
        private readonly Dictionary<string, object> _data;

        public CommandParams(Dictionary<string, object> data)
        {
            _data = data ?? new Dictionary<string, object>();
        }

        public bool Has(string key) => _data.ContainsKey(key) && _data[key] != null;

        public object Raw(string key) => _data.TryGetValue(key, out var v) ? v : null;

        public string GetString(string key, string fallback = null)
        {
            return _data.TryGetValue(key, out var v) && v != null ? v.ToString() : fallback;
        }

        public bool GetBool(string key, bool fallback = false)
        {
            if (!_data.TryGetValue(key, out var v) || v == null)
            {
                return fallback;
            }

            if (v is bool b)
            {
                return b;
            }

            return bool.TryParse(v.ToString(), out var parsed) ? parsed : fallback;
        }

        public int GetInt(string key, int fallback = 0)
        {
            if (!_data.TryGetValue(key, out var v) || v == null)
            {
                return fallback;
            }

            switch (v)
            {
                case long l:
                    return (int)l;
                case double d:
                    return (int)d;
                default:
                    return int.TryParse(v.ToString(), out var parsed) ? parsed : fallback;
            }
        }

        public float GetFloat(string key, float fallback = 0f)
        {
            if (!_data.TryGetValue(key, out var v) || v == null)
            {
                return fallback;
            }

            switch (v)
            {
                case double d:
                    return (float)d;
                case long l:
                    return l;
                default:
                    return float.TryParse(v.ToString(), out var parsed) ? parsed : fallback;
            }
        }

        /// <summary>
        /// Reads a 3-element numeric array as a Vector3-like float[3], or null
        /// when the key is absent.
        /// </summary>
        public float[] GetVector3(string key)
        {
            if (!(Raw(key) is List<object> list) || list.Count < 3)
            {
                return null;
            }

            var result = new float[3];
            for (int i = 0; i < 3; i++)
            {
                result[i] = list[i] is double d ? (float)d : list[i] is long l ? l : 0f;
            }

            return result;
        }
    }
}
