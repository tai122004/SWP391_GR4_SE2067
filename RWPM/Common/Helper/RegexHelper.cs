namespace RWPM.Common
{
    public static class RegexHelper
    {
        /// <summary>
        /// Only letters, digits, underscores, or dashes.
        /// </summary>
        public const string CodeRegex = @"^[a-zA-Z0-9_-]+$";
        public const string CodeRegexLocalization = "RegularExpression_CodeRegex";

        /// <summary>
        /// Must start with a letter and contain only letters and digits.
        /// </summary>
        public const string AlphabetAndDigitRegex = @"^[A-Za-z][A-Za-z0-9]*$";
        public const string AlphabetAndDigitRegexLocalization = "RegularExpression_AlphabetAndDigitRegex";

        /// <summary>
        /// Must start with a letter and contain only letters and digits.
        /// </summary>
        public const string UsernameRegex = @"^[A-Za-z][A-Za-z0-9_-]*$";
        public const string UsernameRegexLocalization = "RegularExpression_UsernameRegex";

        /// <summary>
        /// Must be at least 8 characters, and include uppercase, lowercase, and a number.
        /// </summary>
        public const string PasswordRegex = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$";
        public const string PasswordRegexLocalization = "RegularExpression_PasswordRegex";
    }
}
