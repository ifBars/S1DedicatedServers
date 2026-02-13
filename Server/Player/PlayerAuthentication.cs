using MelonLoader;
using System;
using DedicatedServerMod;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Server.Player
{
    /// <summary>
    /// Handles player authentication including password and Steam ticket validation.
    /// </summary>
    public class PlayerAuthentication
    {
        private readonly MelonLogger.Instance logger;

        public PlayerAuthentication(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
        }

        /// <summary>
        /// Initialize the authentication system
        /// </summary>
        public void Initialize()
        {
            logger.Msg("Player authentication system initialized");
            
            // Log password protection status
            if (!string.IsNullOrEmpty(ServerConfig.Instance.ServerPassword))
            {
                logger.Msg("Server password protection is ENABLED");
            }
            else
            {
                logger.Msg("Server password protection is DISABLED");
            }
        }

        /// <summary>
        /// Authenticate a player using their password hash and Steam ticket
        /// </summary>
        public AuthenticationResult AuthenticatePlayer(ConnectedPlayerInfo playerInfo, string passwordHash = null, string steamTicket = null)
        {
            try
            {
                // Check if player is banned first
                if (!string.IsNullOrEmpty(playerInfo.SteamId) && IsPlayerBanned(playerInfo.SteamId))
                {
                    return new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Player is banned",
                        ShouldDisconnect = true
                    };
                }

                // Check password if server has password protection enabled
                if (RequiresPassword())
                {
                    if (string.IsNullOrEmpty(passwordHash))
                    {
                        return new AuthenticationResult
                        {
                            IsSuccessful = false,
                            Message = "Password required",
                            ShouldDisconnect = true
                        };
                    }

                    if (!ValidatePasswordHash(passwordHash))
                    {
                        logger.Warning($"Invalid password attempt from {playerInfo.DisplayName ?? $"ClientId {playerInfo.ClientId}"}");
                        return new AuthenticationResult
                        {
                            IsSuccessful = false,
                            Message = "Invalid password",
                            ShouldDisconnect = true
                        };
                    }

                    logger.Msg($"Password authentication successful for {playerInfo.DisplayName ?? $"ClientId {playerInfo.ClientId}"}");
                }

                // If Steam authentication is required, validate ticket
                if (ServerConfig.Instance.RequireAuthentication)
                {
                    // TODO: Implement actual Steam ticket validation
                    // This would involve:
                    // 1. Validating the ticket with Steam's servers
                    // 2. Extracting the SteamID from the validated ticket
                    // 3. Checking if the SteamID matches expected values
                    
                    logger.Msg($"TODO: Validate Steam ticket for {playerInfo.DisplayName}");
                }
                
                // Authentication successful
                return new AuthenticationResult
                {
                    IsSuccessful = true,
                    Message = "Authentication successful",
                    ExtractedSteamId = playerInfo.SteamId // Would be extracted from ticket
                };
            }
            catch (Exception ex)
            {
                logger.Error($"Error authenticating player {playerInfo.DisplayName}: {ex}");
                return new AuthenticationResult
                {
                    IsSuccessful = false,
                    Message = $"Authentication error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Check if the server requires password authentication.
        /// </summary>
        /// <returns>True if a password is configured</returns>
        public bool RequiresPassword()
        {
            return !string.IsNullOrEmpty(ServerConfig.Instance.ServerPassword);
        }

        /// <summary>
        /// Validate a password hash against the server's configured password.
        /// </summary>
        /// <param name="clientHash">The password hash provided by the client</param>
        /// <returns>True if the hash matches the server password</returns>
        private bool ValidatePasswordHash(string clientHash)
        {
            if (string.IsNullOrEmpty(clientHash))
            {
                return false;
            }

            var serverPassword = ServerConfig.Instance.ServerPassword;
            if (string.IsNullOrEmpty(serverPassword))
            {
                return false;
            }

            var serverHash = PasswordHasher.HashPassword(serverPassword);
            return string.Equals(clientHash, serverHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if a player should be allowed based on friends requirement
        /// </summary>
        public bool ShouldAllowPlayer(string steamId, string hostSteamId = null)
        {
            try
            {
                // Check if player is banned
                if (IsPlayerBanned(steamId))
                {
                    logger.Msg($"Player {steamId} is banned, denying access");
                    return false;
                }
                
                // For now, allow all non-banned players
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Error checking player allowance for {steamId}: {ex}");
                // Default to allow on error to prevent breaking the game
                return true;
            }
        }

        /// <summary>
        /// Check if a player is banned
        /// </summary>
        public bool IsPlayerBanned(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return false;
                
            return ServerConfig.Instance.BannedPlayers.Contains(steamId);
        }

        /// <summary>
        /// Validate a Steam ticket (placeholder for future implementation)
        /// </summary>
        private bool ValidateSteamTicket(string ticket, out string extractedSteamId)
        {
            extractedSteamId = null;
            
            // TODO: Implement actual Steam ticket validation
            // This would involve:
            // 1. Making a request to Steam's Web API
            // 2. Validating the ticket response
            // 3. Extracting the SteamID from the response
            
            logger.Msg($"TODO: Validate Steam ticket: {ticket?.Substring(0, Math.Min(8, ticket?.Length ?? 0))}...");
            
            return false; // Placeholder
        }
    }

    /// <summary>
    /// Result of a player authentication attempt
    /// </summary>
    public class AuthenticationResult
    {
        /// <summary>
        /// Whether authentication was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Authentication result message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Whether the player should be disconnected due to auth failure
        /// </summary>
        public bool ShouldDisconnect { get; set; }

        /// <summary>
        /// SteamID extracted from the authentication process
        /// </summary>
        public string ExtractedSteamId { get; set; }

        public override string ToString()
        {
            return $"Auth: {(IsSuccessful ? "Success" : "Failed")} - {Message}";
        }
    }
}
