using System;
using System.Security.Cryptography;
using System.Text;

namespace DedicatedServerMod.Utils
{
    /// <summary>
    /// Utility class for secure password hashing.
    /// Uses SHA256 to create consistent hashes between client and server.
    /// </summary>
    /// <remarks>
    /// NOTE: This uses SHA256 for password hashing, which is suitable for this use case:
    /// - We are NOT storing user passwords (no database)
    /// - We are comparing a hash transmitted over network vs hash of configured password
    /// - The server password is set by the admin, not by users
    /// - This prevents plaintext password transmission over the network
    /// 
    /// For applications that store user passwords, use bcrypt, scrypt, or Argon2 instead.
    /// </remarks>
    public static class PasswordHasher
    {
        /// <summary>
        /// Generates a SHA256 hash of the provided password.
        /// </summary>
        /// <param name="password">The plaintext password to hash</param>
        /// <returns>A hex-encoded SHA256 hash of the password</returns>
        /// <exception cref="ArgumentNullException">Thrown if password is null</exception>
        public static string HashPassword(string password)
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            using (var sha256 = SHA256.Create())
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                byte[] hashBytes = sha256.ComputeHash(passwordBytes);
                
                // Convert to hex string
                var sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                
                return sb.ToString();
            }
        }

        /// <summary>
        /// Verifies that a password matches a hash.
        /// </summary>
        /// <param name="password">The plaintext password to verify</param>
        /// <param name="hash">The hash to compare against</param>
        /// <returns>True if the password hash matches the provided hash</returns>
        public static bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
            {
                return false;
            }

            string computedHash = HashPassword(password);
            return string.Equals(computedHash, hash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
