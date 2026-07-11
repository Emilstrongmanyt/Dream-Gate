using System;
using System.Text;

namespace DreamGate.Battlegrounds.Services.Backend
{
    public static class PhoneAuthService
    {
        public static bool TryNormalize(string rawPhone, out string e164, out string error)
        {
            e164 = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(rawPhone))
            {
                error = "Enter your phone number.";
                return false;
            }

            var trimmed = rawPhone.Trim();
            var builder = new StringBuilder(trimmed.Length);
            foreach (var ch in trimmed)
            {
                if (char.IsDigit(ch) || ch == '+')
                {
                    builder.Append(ch);
                }
            }

            var digits = builder.ToString();
            if (string.IsNullOrEmpty(digits))
            {
                error = "Enter a valid phone number.";
                return false;
            }

            if (digits.StartsWith("00", StringComparison.Ordinal))
            {
                digits = "+" + digits.Substring(2);
            }
            else if (!digits.StartsWith("+", StringComparison.Ordinal))
            {
                if (digits.Length == 10)
                {
                    digits = "+1" + digits;
                }
                else if (digits.Length == 11 && digits.StartsWith("1", StringComparison.Ordinal))
                {
                    digits = "+" + digits;
                }
                else
                {
                    error = "Include your country code, e.g. +15551234567.";
                    return false;
                }
            }

            var digitCount = 0;
            for (var i = 0; i < digits.Length; i++)
            {
                if (char.IsDigit(digits[i]))
                {
                    digitCount++;
                }
            }

            if (digitCount < 8 || digitCount > 15)
            {
                error = "Enter a valid phone number with country code.";
                return false;
            }

            e164 = digits;
            return true;
        }

        public static bool IsValidOtp(string rawOtp, out string otp, out string error)
        {
            otp = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(rawOtp))
            {
                error = "Enter the verification code.";
                return false;
            }

            var builder = new StringBuilder(rawOtp.Length);
            foreach (var ch in rawOtp)
            {
                if (char.IsDigit(ch))
                {
                    builder.Append(ch);
                }
            }

            otp = builder.ToString();
            if (otp.Length < 4 || otp.Length > 8)
            {
                error = "Enter the SMS verification code.";
                return false;
            }

            return true;
        }
    }
}