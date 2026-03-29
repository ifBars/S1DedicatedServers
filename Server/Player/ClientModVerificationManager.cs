using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DedicatedServerMod.API;
using DedicatedServerMod.Server.Core;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Shared.ModVerification;
using DedicatedServerMod.Shared.Permissions;
using DedicatedServerMod.Utils;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;

namespace DedicatedServerMod.Server.Player
{
    /// <summary>
    /// Evaluates client mod inventories against the server's active compatibility and security policy.
    /// </summary>
    internal sealed class ClientModVerificationManager
    {
        private readonly MelonLogger.Instance _logger;
        private readonly ClientModPolicyStore _policyStore;
        private readonly List<DeclaredClientCompanionRequirement> _companionRequirements = new List<DeclaredClientCompanionRequirement>();
        private readonly List<KnownRiskyClientModEntry> _knownRiskyClientMods = new List<KnownRiskyClientModEntry>();

        private ClientModPolicy _policy = new ClientModPolicy();
        private string _policyHash = string.Empty;
        private string _policyFilePath = string.Empty;

        internal ClientModVerificationManager(MelonLogger.Instance logger)
        {
            _logger = logger;
            _policyStore = new ClientModPolicyStore(logger);
            SeedKnownRiskyCatalog();
        }

        internal event Action<ConnectedPlayerInfo, ModVerificationEvaluationResult> VerificationCompleted;

        internal string PolicyHash => _policyHash;

        internal void Initialize()
        {
            LoadPolicy();
            DiscoverCompanionRequirements();
            ValidateStrictModeConfiguration();
        }

        internal void Shutdown()
        {
            _companionRequirements.Clear();
        }

        internal bool IsVerificationRequiredForPlayer(ConnectedPlayerInfo playerInfo)
        {
            return ServerConfig.Instance.ModVerificationEnabled &&
                   playerInfo != null &&
                   !playerInfo.IsLoopbackConnection &&
                   !HasClientModPolicyBypass(playerInfo);
        }

        internal ModVerificationResultMessage BypassVerification(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null)
            {
                return new ModVerificationResultMessage
                {
                    Success = true,
                    Message = "Mod verification bypassed"
                };
            }

            playerInfo.IsModVerificationPending = false;
            playerInfo.IsModVerificationComplete = true;
            playerInfo.ModVerificationNonce = string.Empty;
            playerInfo.ModVerificationStartedAtUtc = DateTime.UtcNow;
            playerInfo.LastModVerificationAttemptUtc = DateTime.UtcNow;
            playerInfo.LastModVerificationMessage = "Mod verification not required";

            return new ModVerificationResultMessage
            {
                Success = true,
                Message = playerInfo.LastModVerificationMessage
            };
        }

        internal ModVerificationChallengeMessage CreateChallenge(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null)
            {
                return null;
            }

            playerInfo.IsModVerificationPending = true;
            playerInfo.IsModVerificationComplete = false;
            playerInfo.ModVerificationNonce = Guid.NewGuid().ToString("N");
            playerInfo.ModVerificationStartedAtUtc = DateTime.UtcNow;
            playerInfo.LastModVerificationMessage = "Awaiting client mod inventory";

            return new ModVerificationChallengeMessage
            {
                Nonce = playerInfo.ModVerificationNonce,
                TimeoutSeconds = ServerConfig.Instance.ModVerificationTimeoutSeconds,
                ProtocolVersion = "1",
                PolicyHash = _policyHash
            };
        }

        internal void SubmitReport(ConnectedPlayerInfo playerInfo, ModVerificationReportMessage report)
        {
            ModVerificationEvaluationResult result;

            if (playerInfo == null)
            {
                result = ModVerificationEvaluationResult.Fail("Client mod verification failed: player not tracked.");
                VerificationCompleted?.Invoke(playerInfo, result);
                return;
            }

            if (report == null)
            {
                result = ModVerificationEvaluationResult.Fail("Client mod verification failed: missing report payload.");
                CompletePlayerVerification(playerInfo, result);
                return;
            }

            if (!playerInfo.IsModVerificationPending)
            {
                result = ModVerificationEvaluationResult.Fail("Client mod verification failed: no active verification challenge.");
                CompletePlayerVerification(playerInfo, result);
                return;
            }

            if (!string.Equals(playerInfo.ModVerificationNonce, report.Nonce ?? string.Empty, StringComparison.Ordinal))
            {
                result = ModVerificationEvaluationResult.Fail("Client mod verification failed: challenge nonce mismatch.");
                CompletePlayerVerification(playerInfo, result);
                return;
            }

            result = EvaluateReport(report);
            CompletePlayerVerification(playerInfo, result);
        }

        private void CompletePlayerVerification(ConnectedPlayerInfo playerInfo, ModVerificationEvaluationResult result)
        {
            if (playerInfo != null)
            {
                playerInfo.IsModVerificationPending = false;
                playerInfo.IsModVerificationComplete = result.Success;
                playerInfo.LastModVerificationAttemptUtc = DateTime.UtcNow;
                playerInfo.LastModVerificationMessage = result.Message ?? string.Empty;
                playerInfo.ModVerificationNonce = string.Empty;
            }

            VerificationCompleted?.Invoke(playerInfo, result);
        }

        private void LoadPolicy()
        {
            _policyFilePath = _policyStore.FilePath;

            try
            {
                _policy = _policyStore.Exists
                    ? _policyStore.Load()
                    : new ClientModPolicy();

                _policy.Normalize();
                _policyHash = BuildPolicyHash();

                if (!_policyStore.Exists)
                {
                    SavePolicy();
                }

                _logger.Msg($"Client mod policy loaded from {_policyFilePath}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load client mod policy: {ex}");
                _policy = new ClientModPolicy();
                _policy.Normalize();
                _policyHash = BuildPolicyHash();
                SavePolicy();
            }
        }

        private void SavePolicy()
        {
            try
            {
                _policyStore.Save(_policy ?? new ClientModPolicy());
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to write default client mod policy: {ex.Message}");
            }
        }

        private void DiscoverCompanionRequirements()
        {
            _companionRequirements.Clear();

            List<DeclaredClientCompanionRequirement> discovered = ModManager.GetDeclaredServerCompanions();
            Dictionary<string, DeclaredClientCompanionRequirement> merged = new Dictionary<string, DeclaredClientCompanionRequirement>(StringComparer.OrdinalIgnoreCase);

            foreach (DeclaredClientCompanionRequirement requirement in discovered)
            {
                if (requirement == null || string.IsNullOrWhiteSpace(requirement.ModId))
                {
                    continue;
                }

                if (!merged.TryGetValue(requirement.ModId, out DeclaredClientCompanionRequirement existing))
                {
                    merged[requirement.ModId] = new DeclaredClientCompanionRequirement
                    {
                        ModId = requirement.ModId,
                        DisplayName = requirement.DisplayName,
                        Required = requirement.Required,
                        MinVersion = requirement.MinVersion,
                        PinnedSha256 = new List<string>(requirement.PinnedSha256),
                        SourceAssemblyName = requirement.SourceAssemblyName
                    };
                    continue;
                }

                existing.Required |= requirement.Required;

                if (string.IsNullOrWhiteSpace(existing.DisplayName) && !string.IsNullOrWhiteSpace(requirement.DisplayName))
                {
                    existing.DisplayName = requirement.DisplayName;
                }

                if (ClientModVersionComparer.Compare(requirement.MinVersion, existing.MinVersion) > 0)
                {
                    existing.MinVersion = requirement.MinVersion;
                }

                existing.PinnedSha256 = existing.PinnedSha256
                    .Concat(requirement.PinnedSha256 ?? Enumerable.Empty<string>())
                    .Select(ClientModPolicy.NormalizeHash)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            _companionRequirements.AddRange(merged.Values.OrderBy(item => item.ModId, StringComparer.OrdinalIgnoreCase));
            _policyHash = BuildPolicyHash();
        }

        private void ValidateStrictModeConfiguration()
        {
            if (!ServerConfig.Instance.StrictClientModMode)
            {
                return;
            }

            foreach (DeclaredClientCompanionRequirement requirement in _companionRequirements)
            {
                if (!requirement.Required)
                {
                    continue;
                }

                List<string> hashes = GetStrictHashesForCompanion(requirement);
                if (hashes.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Strict client mod mode requires pinned hashes for required companion '{requirement.ModId}' ({requirement.DisplayName}).");
                }
            }
        }

        private ModVerificationEvaluationResult EvaluateReport(ModVerificationReportMessage report)
        {
            List<ClientModDescriptor> mods = report.Mods ?? new List<ClientModDescriptor>();
            NormalizeReportedMods(mods);

            Dictionary<string, ClientModDescriptor> declaredMods = new Dictionary<string, ClientModDescriptor>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < mods.Count; i++)
            {
                ClientModDescriptor mod = mods[i];
                if (string.IsNullOrEmpty(mod.ModId))
                {
                    continue;
                }

                if (declaredMods.ContainsKey(mod.ModId))
                {
                    return ModVerificationEvaluationResult.Fail(
                        $"Duplicate client mod identity detected for '{mod.ModId}'.");
                }

                declaredMods[mod.ModId] = mod;
            }

            for (int i = 0; i < mods.Count; i++)
            {
                ClientModDescriptor mod = mods[i];

                string denyReason = TryGetExplicitDenyReason(mod);
                if (!string.IsNullOrEmpty(denyReason))
                {
                    return ModVerificationEvaluationResult.Fail(denyReason);
                }

                if (ServerConfig.Instance.BlockKnownRiskyClientMods &&
                    TryMatchKnownRiskyClientMod(mod, out KnownRiskyClientModEntry riskyEntry))
                {
                    return ModVerificationEvaluationResult.Fail(riskyEntry.Reason);
                }
            }

            HashSet<string> companionIds = new HashSet<string>(_companionRequirements.Select(item => item.ModId), StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _companionRequirements.Count; i++)
            {
                DeclaredClientCompanionRequirement requirement = _companionRequirements[i];
                bool found = declaredMods.TryGetValue(requirement.ModId, out ClientModDescriptor reportedMod);
                if (!found)
                {
                    if (requirement.Required)
                    {
                        return ModVerificationEvaluationResult.Fail(
                            $"Missing required client companion '{requirement.DisplayName}' ({requirement.ModId}).");
                    }

                    continue;
                }

                if (!ClientModVersionComparer.SatisfiesMinimumVersion(reportedMod.Version, requirement.MinVersion))
                {
                    return ModVerificationEvaluationResult.Fail(
                        $"Client companion '{requirement.DisplayName}' is below the minimum version '{requirement.MinVersion}'.");
                }

                if (ServerConfig.Instance.StrictClientModMode)
                {
                    List<string> allowedHashes = GetStrictHashesForCompanion(requirement);
                    if (allowedHashes.Count == 0)
                    {
                        return ModVerificationEvaluationResult.Fail(
                            $"Strict mode cannot validate companion '{requirement.DisplayName}' because no pinned hashes are configured.");
                    }

                    if (!allowedHashes.Contains(reportedMod.Sha256, StringComparer.OrdinalIgnoreCase))
                    {
                        return ModVerificationEvaluationResult.Fail(
                            $"Client companion '{requirement.DisplayName}' does not match a pinned strict-mode hash.");
                    }
                }
            }

            List<ClientModDescriptor> unpairedMods = mods
                .Where(mod => string.IsNullOrEmpty(mod.ModId) || !companionIds.Contains(mod.ModId))
                .ToList();

            if (unpairedMods.Count > 0 && !ServerConfig.Instance.AllowUnpairedClientMods)
            {
                ClientModDescriptor blockedMod = unpairedMods[0];
                return ModVerificationEvaluationResult.Fail(
                    $"Unpaired client-only mod '{blockedMod.DisplayName}' is not allowed on this server.");
            }

            if (ServerConfig.Instance.StrictClientModMode)
            {
                for (int i = 0; i < unpairedMods.Count; i++)
                {
                    ClientModDescriptor mod = unpairedMods[i];
                    ApprovedUnpairedClientModPolicyEntry approvedEntry = FindApprovedUnpairedEntry(mod);
                    if (approvedEntry == null)
                    {
                        return ModVerificationEvaluationResult.Fail(
                            $"Unpaired client-only mod '{mod.DisplayName}' is not approved in strict mode.");
                    }

                    List<string> pinnedHashes = approvedEntry.PinnedSha256
                        .Select(ClientModPolicy.NormalizeHash)
                        .Where(value => !string.IsNullOrEmpty(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (pinnedHashes.Count == 0)
                    {
                        return ModVerificationEvaluationResult.Fail(
                            $"Strict mode cannot validate unpaired client-only mod '{mod.DisplayName}' because no pinned hashes are configured.");
                    }

                    if (!pinnedHashes.Contains(mod.Sha256, StringComparer.OrdinalIgnoreCase))
                    {
                        return ModVerificationEvaluationResult.Fail(
                            $"Unpaired client-only mod '{mod.DisplayName}' does not match a pinned strict-mode hash.");
                    }
                }
            }

            return ModVerificationEvaluationResult.SuccessResult("Client mod verification completed successfully.");
        }

        private static void NormalizeReportedMods(List<ClientModDescriptor> mods)
        {
            for (int i = 0; i < mods.Count; i++)
            {
                ClientModDescriptor mod = mods[i] ?? new ClientModDescriptor();
                mod.ModId = ClientModPolicy.NormalizeValue(mod.ModId);
                mod.Version = ClientModPolicy.NormalizeValue(mod.Version);
                mod.DisplayName = ClientModPolicy.NormalizeValue(mod.DisplayName);
                mod.Author = ClientModPolicy.NormalizeValue(mod.Author);
                mod.AssemblyName = ClientModPolicy.NormalizeValue(mod.AssemblyName);
                mod.Sha256 = ClientModPolicy.NormalizeHash(mod.Sha256);
                mods[i] = mod;
            }
        }

        private string TryGetExplicitDenyReason(ClientModDescriptor mod)
        {
            if (!string.IsNullOrEmpty(mod.Sha256) &&
                _policy.DeniedClientModHashes.Contains(mod.Sha256, StringComparer.OrdinalIgnoreCase))
            {
                return $"Client mod '{mod.DisplayName}' is denied by server hash policy.";
            }

            if (!string.IsNullOrEmpty(mod.ModId) &&
                _policy.DeniedClientModIds.Contains(mod.ModId, StringComparer.OrdinalIgnoreCase))
            {
                return $"Client mod '{mod.DisplayName}' is denied by server mod ID policy.";
            }

            if (_policy.DeniedClientModNames.Contains(mod.DisplayName, StringComparer.OrdinalIgnoreCase) ||
                _policy.DeniedClientModNames.Contains(mod.AssemblyName, StringComparer.OrdinalIgnoreCase))
            {
                return $"Client mod '{mod.DisplayName}' is denied by server name policy.";
            }

            return string.Empty;
        }

        private bool TryMatchKnownRiskyClientMod(ClientModDescriptor mod, out KnownRiskyClientModEntry matchedEntry)
        {
            for (int i = 0; i < _knownRiskyClientMods.Count; i++)
            {
                KnownRiskyClientModEntry entry = _knownRiskyClientMods[i];
                if (!string.IsNullOrEmpty(mod.Sha256) &&
                    entry.KnownHashes.Contains(mod.Sha256, StringComparer.OrdinalIgnoreCase))
                {
                    matchedEntry = entry;
                    return true;
                }

                if (!string.IsNullOrEmpty(mod.ModId) &&
                    entry.KnownModIds.Contains(mod.ModId, StringComparer.OrdinalIgnoreCase))
                {
                    matchedEntry = entry;
                    return true;
                }

                if (entry.KnownNames.Contains(mod.DisplayName, StringComparer.OrdinalIgnoreCase) ||
                    entry.KnownNames.Contains(mod.AssemblyName, StringComparer.OrdinalIgnoreCase))
                {
                    matchedEntry = entry;
                    return true;
                }
            }

            matchedEntry = null;
            return false;
        }

        private List<string> GetStrictHashesForCompanion(DeclaredClientCompanionRequirement requirement)
        {
            List<string> hashes = new List<string>();
            if (requirement?.PinnedSha256 != null)
            {
                hashes.AddRange(requirement.PinnedSha256);
            }

            if (requirement != null &&
                _policy.StrictPinnedCompanionHashes.TryGetValue(requirement.ModId, out List<string> overrideHashes) &&
                overrideHashes != null)
            {
                hashes.AddRange(overrideHashes);
            }

            return hashes
                .Select(ClientModPolicy.NormalizeHash)
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private ApprovedUnpairedClientModPolicyEntry FindApprovedUnpairedEntry(ClientModDescriptor mod)
        {
            for (int i = 0; i < _policy.ApprovedUnpairedClientMods.Count; i++)
            {
                ApprovedUnpairedClientModPolicyEntry entry = _policy.ApprovedUnpairedClientMods[i];
                if (entry == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(mod.ModId) &&
                    !string.IsNullOrEmpty(entry.ModId) &&
                    string.Equals(entry.ModId, mod.ModId, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }

                if (!string.IsNullOrEmpty(entry.DisplayName) &&
                    (string.Equals(entry.DisplayName, mod.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(entry.DisplayName, mod.AssemblyName, StringComparison.OrdinalIgnoreCase)))
                {
                    return entry;
                }
            }

            return null;
        }

        private string BuildPolicyHash()
        {
            try
            {
                string json = JsonConvert.SerializeObject(new
                {
                    ServerConfig.Instance.ModVerificationEnabled,
                    ServerConfig.Instance.StrictClientModMode,
                    ServerConfig.Instance.AllowUnpairedClientMods,
                    ServerConfig.Instance.BlockKnownRiskyClientMods,
                    Policy = _policy,
                    Companions = _companionRequirements
                }, Formatting.None);

                using SHA256 sha256 = SHA256.Create();
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
                return BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }

        private void SeedKnownRiskyCatalog()
        {
            _knownRiskyClientMods.Clear();
            _knownRiskyClientMods.Add(new KnownRiskyClientModEntry
            {
                DisplayName = "UnityExplorer",
                KnownModIds = new List<string> { "unityexplorer", "com.sinai.unityexplorer" },
                KnownNames = new List<string> { "UnityExplorer", "UnityExplorerStandalone" },
                KnownHashes = new List<string>(),
                Reason = "Client mod verification rejected UnityExplorer because it exposes runtime object inspection and mutation capabilities that can be abused on dedicated servers."
            });
        }

        private static bool HasClientModPolicyBypass(ConnectedPlayerInfo playerInfo)
        {
            return !string.IsNullOrWhiteSpace(playerInfo?.TrustedUniqueId) &&
                   ServerBootstrap.Permissions?.HasPermission(playerInfo.TrustedUniqueId, PermissionBuiltIns.Nodes.ClientModPolicyBypass) == true;
        }

        private sealed class KnownRiskyClientModEntry
        {
            public string DisplayName { get; set; } = string.Empty;

            public List<string> KnownModIds { get; set; } = new List<string>();

            public List<string> KnownNames { get; set; } = new List<string>();

            public List<string> KnownHashes { get; set; } = new List<string>();

            public string Reason { get; set; } = string.Empty;
        }
    }

    internal sealed class ModVerificationEvaluationResult
    {
        public bool Success { get; private set; }

        public string Message { get; private set; } = string.Empty;

        public bool ShouldDisconnect => !Success;

        public static ModVerificationEvaluationResult SuccessResult(string message)
        {
            return new ModVerificationEvaluationResult
            {
                Success = true,
                Message = message ?? string.Empty
            };
        }

        public static ModVerificationEvaluationResult Fail(string message)
        {
            return new ModVerificationEvaluationResult
            {
                Success = false,
                Message = message ?? "Client mod verification failed."
            };
        }
    }
}
