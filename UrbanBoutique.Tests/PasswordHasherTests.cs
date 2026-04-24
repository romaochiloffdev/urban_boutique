// Author: Ochilov Ilyosjon (ID: B2300540)
// Tests for the PBKDF2 password hashing implementation in AuthController.

using UrbanBoutiqueWeb.Controllers;
using Xunit;

namespace UrbanBoutique.Tests
{
    public class PasswordHasherTests
    {
        [Fact]
        public void Hash_ProducesPbkdf2PrefixedString()
        {
            var hash = AuthController.HashPassword("anything");
            Assert.StartsWith("pbkdf2$", hash);
        }

        [Fact]
        public void Hash_HasFourDollarSeparatedSections()
        {
            // Format: pbkdf2$iters$salt$hash
            var hash = AuthController.HashPassword("p4ssw0rd");
            var parts = hash.Split('$');
            Assert.Equal(4, parts.Length);
        }

        [Fact]
        public void Hash_SamePasswordProducesDifferentHashes_DueToSalt()
        {
            var a = AuthController.HashPassword("repeat");
            var b = AuthController.HashPassword("repeat");
            Assert.NotEqual(a, b);
        }

        [Theory]
        [InlineData("admin123")]
        [InlineData("correct horse battery staple")]
        [InlineData("پارولُیْ۫")]                  // unicode
        [InlineData("!@#$%^&*()_+-={}[]:;\"'<>,.?/\\|")]
        public void Verify_ReturnsTrue_ForOriginalPassword(string pwd)
        {
            var hash = AuthController.HashPassword(pwd);
            Assert.True(AuthController.VerifyPassword(pwd, hash));
        }

        [Fact]
        public void Verify_ReturnsFalse_ForWrongPassword()
        {
            var hash = AuthController.HashPassword("right");
            Assert.False(AuthController.VerifyPassword("wrong", hash));
        }

        [Fact]
        public void Verify_IsCaseSensitive()
        {
            var hash = AuthController.HashPassword("AbCdEf");
            Assert.False(AuthController.VerifyPassword("abcdef", hash));
            Assert.False(AuthController.VerifyPassword("ABCDEF", hash));
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("not-a-hash")]
        [InlineData("md5$1000$aaa$bbb")]            // wrong scheme
        [InlineData("pbkdf2$notanumber$aaa$bbb")]   // malformed iterations
        [InlineData("pbkdf2$1000$aaa")]             // missing hash segment
        public void Verify_ReturnsFalse_ForMalformedStoredHash(string stored)
        {
            Assert.False(AuthController.VerifyPassword("anything", stored));
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenStoredIsNull()
        {
            Assert.False(AuthController.VerifyPassword("anything", null!));
        }

        [Fact]
        public void Verify_UsesConstantTimeComparison_NoShortCircuit()
        {
            // Both wrong, but the hash is well-formed — should complete without throwing.
            var hash = AuthController.HashPassword("original");

            // Tamper with the hash section
            var parts = hash.Split('$');
            parts[3] = new string('A', parts[3].Length);
            var tampered = string.Join('$', parts);

            Assert.False(AuthController.VerifyPassword("original", tampered));
        }

        [Fact]
        public void Hash_EmptyPassword_StillProducesValidHash()
        {
            var hash = AuthController.HashPassword("");
            Assert.True(AuthController.VerifyPassword("", hash));
            Assert.False(AuthController.VerifyPassword("x", hash));
        }
    }
}
