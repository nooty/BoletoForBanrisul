using System;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Converters
{
    /// <summary>
    /// Converts JSON property names to snake_case.
    /// </summary>
    public class SnakeCaseLowerNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            return ToSnakeCase(name);
        }

        private string ToSnakeCase(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // Converts the string from PascalCase or camelCase to snake_case.
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                    {
                        result.Append('_');
                    }
                    result.Append(char.ToLower(c));
                }
                else
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }
    }
}
