using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Shared.Permissions;
using DedicatedServerMod.Utils;
using MelonLoader;
using Newtonsoft.Json;

namespace DedicatedServerMod.Server.Permissions
{
    public sealed partial class ServerPermissionService
    {
        private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
        private static readonly TimeSpan TemporaryGrantSweepInterval = TimeSpan.FromSeconds(30);

        private static readonly IReadOnlyDictionary<string, int> BuiltInGroupPriorities = new Dictionary<string, int>(Comparer)
        {
            [PermissionBuiltIns.Groups.Default] = 0,
            [PermissionBuiltIns.Groups.Support] = 10,
            [PermissionBuiltIns.Groups.Moderator] = 20,
            [PermissionBuiltIns.Groups.Administrator] = 30,
            [PermissionBuiltIns.Groups.Operator] = 40
        };

        private static readonly IReadOnlyDictionary<string, string> BuiltInServerCommandNodes = new Dictionary<string, string>(Comparer)
        {
            ["help"] = PermissionBuiltIns.Nodes.ServerHelp,
            ["serverinfo"] = PermissionBuiltIns.Nodes.ServerInfo,
            ["save"] = PermissionBuiltIns.Nodes.ServerSave,
            ["reloadconfig"] = PermissionBuiltIns.Nodes.ServerReloadConfig,
            ["shutdown"] = PermissionBuiltIns.Nodes.ServerStop,
            ["listplayers"] = PermissionBuiltIns.Nodes.PlayerList,
            ["kick"] = PermissionBuiltIns.Nodes.PlayerKick,
            ["ban"] = PermissionBuiltIns.Nodes.PlayerBan,
            ["unban"] = PermissionBuiltIns.Nodes.PlayerUnban,
            ["reloadpermissions"] = PermissionBuiltIns.Nodes.PermissionsReload,
            ["op"] = PermissionBuiltIns.Nodes.PermissionsGroupAssign,
            ["admin"] = PermissionBuiltIns.Nodes.PermissionsGroupAssign,
            ["deop"] = PermissionBuiltIns.Nodes.PermissionsGroupUnassign,
            ["deadmin"] = PermissionBuiltIns.Nodes.PermissionsGroupUnassign,
            ["listops"] = PermissionBuiltIns.Nodes.PermissionsGroupList,
            ["listadmins"] = PermissionBuiltIns.Nodes.PermissionsGroupList
        };

        private readonly MelonLogger.Instance _logger;
        private readonly PermissionStore _store;
        private readonly PermissionAuditLogger _auditLogger;
        private readonly PermissionMigrationCoordinator _migrationCoordinator;
        private readonly PermissionDefinitionRegistry _registry;
        private readonly HashSet<string> _knownRemoteCommands;

        private PermissionStoreData _data;
        private PlayerManager _playerManager;
        private DateTime _lastTemporarySweepUtc = DateTime.MinValue;

        public ServerPermissionService(MelonLogger.Instance logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _store = new PermissionStore(_logger);
            _auditLogger = new PermissionAuditLogger(_logger);
            _migrationCoordinator = new PermissionMigrationCoordinator(_logger, _store);
            _registry = new PermissionDefinitionRegistry();
            _knownRemoteCommands = new HashSet<string>(Comparer);
            _data = new PermissionStoreData();
        }

        public event Action StateChanged;

        public string FilePath => _store.FilePath;

        public void Initialize()
        {
            RegisterBuiltInDefinitions();
            _data = LoadOrCreatePermissionData();
            _lastTemporarySweepUtc = DateTime.UtcNow;
            _logger.Msg("Server permission service initialized.");
        }

        public void AttachPlayerManager(PlayerManager playerManager)
        {
            if (ReferenceEquals(_playerManager, playerManager))
            {
                return;
            }

            if (_playerManager != null)
            {
                _playerManager.OnPlayerJoined -= HandlePlayerJoined;
            }

            _playerManager = playerManager;
            if (_playerManager != null)
            {
                _playerManager.OnPlayerJoined += HandlePlayerJoined;
                RefreshAllCapabilitySnapshots();
            }
        }

        public void Shutdown()
        {
            if (_playerManager != null)
            {
                _playerManager.OnPlayerJoined -= HandlePlayerJoined;
                _playerManager = null;
            }
        }

        public void Tick()
        {
            if (DateTime.UtcNow - _lastTemporarySweepUtc < TemporaryGrantSweepInterval)
            {
                return;
            }

            _lastTemporarySweepUtc = DateTime.UtcNow;
            if (PruneExpiredEntries(_data))
            {
                PersistAndBroadcast("expired temporary permission entries pruned");
            }
        }

        public bool Reload()
        {
            try
            {
                _data = LoadOrCreatePermissionData();
                RefreshAllCapabilitySnapshots();
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to reload permissions: {ex}");
                return false;
            }
        }

        public void RegisterPermissionDefinitions(string modId, IEnumerable<PermissionDefinition> definitions)
        {
            string normalizedModId = NormalizeModId(modId);
            List<PermissionDefinition> rewrittenDefinitions = new List<PermissionDefinition>();

            foreach (PermissionDefinition definition in definitions ?? Enumerable.Empty<PermissionDefinition>())
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Node))
                {
                    continue;
                }

                string normalizedNode = PermissionNode.Normalize(definition.Node);
                if (!normalizedNode.StartsWith("mod.", StringComparison.OrdinalIgnoreCase) &&
                    !BuiltInServerCommandNodes.ContainsKey(normalizedNode))
                {
                    normalizedNode = $"mod.{normalizedModId}.{normalizedNode}";
                }

                rewrittenDefinitions.Add(new PermissionDefinition
                {
                    Node = normalizedNode,
                    Category = string.IsNullOrWhiteSpace(definition.Category) ? normalizedModId : definition.Category,
                    Description = definition.Description ?? string.Empty,
                    SuggestedGroups = definition.SuggestedGroups?
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(PermissionNode.NormalizeGroupName)
                        .Distinct(Comparer)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToList() ?? new List<string>()
                });
            }

            _registry.Register(rewrittenDefinitions);
        }

        public IReadOnlyList<PermissionDefinition> GetRegisteredDefinitions()
        {
            return _registry.GetAll();
        }

        public bool HasPermission(string subjectId, string node)
        {
            return Evaluate(subjectId, node).IsGranted;
        }

        public bool HasDominanceOver(string actorId, string targetUserId)
        {
            string normalizedActorId = NormalizeSubjectId(actorId);
            if (string.IsNullOrWhiteSpace(normalizedActorId))
            {
                return true;
            }

            string normalizedTargetId = NormalizeSubjectId(targetUserId);
            return GetHighestPriority(normalizedActorId) > GetHighestPriority(normalizedTargetId);
        }

        public bool CanOpenConsole(string subjectId)
        {
            return HasPermission(subjectId, PermissionBuiltIns.Nodes.ConsoleOpen);
        }

        public bool CanExecuteRemoteConsoleCommand(string subjectId, string commandWord)
        {
            string normalizedCommandWord = (commandWord ?? string.Empty).Trim().ToLowerInvariant();
            if (!CanOpenConsole(subjectId) || string.IsNullOrWhiteSpace(normalizedCommandWord))
            {
                return false;
            }

            RegisterKnownRemoteCommands(new[] { normalizedCommandWord });
            return HasPermission(subjectId, PermissionNode.CreateConsoleCommandNode(normalizedCommandWord));
        }

        public bool IsBanned(string subjectId)
        {
            string normalizedSubjectId = NormalizeSubjectId(subjectId);
            return !string.IsNullOrWhiteSpace(normalizedSubjectId) && _data.Bans.ContainsKey(normalizedSubjectId);
        }

        /// <summary>
        /// Writes an audit log entry for a privileged command attempt.
        /// </summary>
        public void LogCommand(ConnectedPlayerInfo actor, string commandWord, bool succeeded, string details)
        {
            _auditLogger.LogCommand(actor, commandWord, succeeded, details);
        }

        public DedicatedServerMod.Shared.Permissions.PermissionSummary GetSummary()
        {
            return new DedicatedServerMod.Shared.Permissions.PermissionSummary
            {
                TotalGroups = _data.Groups.Count,
                TotalUsers = _data.Users.Count,
                TotalBans = _data.Bans.Count,
                TotalOperators = GetUsersInEffectiveGroup(PermissionBuiltIns.Groups.Operator).Count,
                TotalAdministrators = GetUsersInEffectiveGroup(PermissionBuiltIns.Groups.Administrator).Count
            };
        }

        public PermissionCapabilitySnapshot BuildCapabilitySnapshot(string subjectId)
        {
            string normalizedSubjectId = NormalizeSubjectId(subjectId);
            List<string> allowedRemoteCommands = GetAllowedRemoteCommands(normalizedSubjectId);
            bool canOpenConsole = CanOpenConsole(normalizedSubjectId);

            return new PermissionCapabilitySnapshot
            {
                UserId = normalizedSubjectId,
                CanOpenConsole = canOpenConsole,
                CanUseRemoteConsole = canOpenConsole && allowedRemoteCommands.Count > 0,
                AllowedRemoteCommands = allowedRemoteCommands,
                IssuedAtUtc = DateTime.UtcNow
            };
        }

        public PermissionEvaluationResult Evaluate(string subjectId, string node)
        {
            string normalizedSubjectId = NormalizeSubjectId(subjectId);
            string normalizedNode = PermissionNode.Normalize(node);
            if (string.IsNullOrWhiteSpace(normalizedSubjectId) || string.IsNullOrWhiteSpace(normalizedNode))
            {
                return CreateDeniedResult(normalizedSubjectId, normalizedNode);
            }

            PermissionMatch winningMatch = null;
            PermissionUserRecord user = GetUser(normalizedSubjectId);
            if (user != null)
            {
                AddMatches(user.Allow, normalizedNode, "user", normalizedSubjectId, int.MaxValue, isAllow: true, ref winningMatch);
                AddMatches(user.Deny, normalizedNode, "user", normalizedSubjectId, int.MaxValue, isAllow: false, ref winningMatch);
                AddMatches(user.TemporaryAllow.Select(item => item.Node), normalizedNode, "temp-user", normalizedSubjectId, int.MaxValue, isAllow: true, ref winningMatch);
                AddMatches(user.TemporaryDeny.Select(item => item.Node), normalizedNode, "temp-user", normalizedSubjectId, int.MaxValue, isAllow: false, ref winningMatch);
            }

            foreach (string groupName in GetEffectiveGroups(normalizedSubjectId))
            {
                if (!_data.Groups.TryGetValue(groupName, out PermissionGroupDefinition group))
                {
                    continue;
                }

                AddMatches(group.Allow, normalizedNode, "group", group.Name, group.Priority, isAllow: true, ref winningMatch);
                AddMatches(group.Deny, normalizedNode, "group", group.Name, group.Priority, isAllow: false, ref winningMatch);
            }

            if (winningMatch == null)
            {
                return CreateDeniedResult(normalizedSubjectId, normalizedNode);
            }

            return new PermissionEvaluationResult
            {
                SubjectId = normalizedSubjectId,
                Node = normalizedNode,
                IsGranted = winningMatch.IsAllow,
                MatchedRule = winningMatch.Rule,
                SourceType = winningMatch.SourceType,
                SourceName = winningMatch.SourceName,
                Specificity = winningMatch.Specificity,
                Priority = winningMatch.Priority
            };
        }

        public IReadOnlyList<string> GetEffectiveGroups(string subjectId)
        {
            HashSet<string> effectiveGroups = new HashSet<string>(Comparer);
            CollectEffectiveGroups(NormalizeSubjectId(subjectId), effectiveGroups);
            return effectiveGroups
                .OrderByDescending(GetGroupPriority)
                .ThenBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        public IReadOnlyList<string> GetGroupNames()
        {
            return _data.Groups.Keys.OrderBy(value => value, StringComparer.Ordinal).ToList();
        }

        public IReadOnlyList<string> GetUsersInEffectiveGroup(string groupName)
        {
            string normalizedGroupName = PermissionNode.NormalizeGroupName(groupName);
            return _data.Users.Keys
                .Where(userId => GetEffectiveGroups(userId).Contains(normalizedGroupName, Comparer))
                .OrderBy(userId => userId, StringComparer.Ordinal)
                .ToList();
        }

        public IReadOnlyList<string> GetDirectUsersInGroup(string groupName)
        {
            string normalizedGroupName = PermissionNode.NormalizeGroupName(groupName);
            return _data.Users.Values
                .Where(user => user.Groups.Contains(normalizedGroupName, Comparer))
                .Select(user => user.UserId)
                .OrderBy(userId => userId, StringComparer.Ordinal)
                .ToList();
        }

        public bool GroupExists(string groupName)
        {
            return _data.Groups.ContainsKey(PermissionNode.NormalizeGroupName(groupName));
        }

        public bool UserHasDirectGroup(string subjectId, string groupName)
        {
            PermissionUserRecord user = GetUser(subjectId);
            return user != null && user.Groups.Contains(PermissionNode.NormalizeGroupName(groupName), Comparer);
        }

        public PermissionUserRecord GetUserRecord(string subjectId)
        {
            PermissionUserRecord user = GetUser(subjectId);
            if (user == null)
            {
                return null;
            }

            return new PermissionUserRecord
            {
                UserId = user.UserId,
                Groups = new List<string>(user.Groups),
                Allow = new List<string>(user.Allow),
                Deny = new List<string>(user.Deny),
                TemporaryGroups = user.TemporaryGroups
                    .Select(item => new TemporaryGroupAssignment
                    {
                        Id = item.Id,
                        GroupName = item.GroupName,
                        ExpiresAtUtc = item.ExpiresAtUtc,
                        GrantedBy = item.GrantedBy,
                        Reason = item.Reason
                    })
                    .ToList(),
                TemporaryAllow = user.TemporaryAllow
                    .Select(item => new TemporaryPermissionGrant
                    {
                        Id = item.Id,
                        Node = item.Node,
                        ExpiresAtUtc = item.ExpiresAtUtc,
                        GrantedBy = item.GrantedBy,
                        Reason = item.Reason
                    })
                    .ToList(),
                TemporaryDeny = user.TemporaryDeny
                    .Select(item => new TemporaryPermissionGrant
                    {
                        Id = item.Id,
                        Node = item.Node,
                        ExpiresAtUtc = item.ExpiresAtUtc,
                        GrantedBy = item.GrantedBy,
                        Reason = item.Reason
                    })
                    .ToList()
            };
        }

        public IReadOnlyCollection<BanEntry> GetBanEntries()
        {
            return _data.Bans.Values
                .OrderBy(entry => entry.SubjectId, StringComparer.Ordinal)
                .Select(entry => new BanEntry
                {
                    SubjectId = entry.SubjectId,
                    CreatedAtUtc = entry.CreatedAtUtc,
                    CreatedBy = entry.CreatedBy,
                    Reason = entry.Reason
                })
                .ToList()
                .AsReadOnly();
        }

        public bool AssignGroup(string actorId, string targetUserId, string groupName, string reason = "")
        {
            string normalizedTargetId = NormalizeSubjectId(targetUserId);
            string normalizedGroupName = PermissionNode.NormalizeGroupName(groupName);
            if (string.IsNullOrWhiteSpace(normalizedTargetId) || !GroupExists(normalizedGroupName))
            {
                return false;
            }

            if (!CanManageGroupAssignment(actorId, normalizedTargetId, normalizedGroupName, PermissionBuiltIns.Nodes.PermissionsGroupAssign))
            {
                return false;
            }

            PermissionUserRecord user = EnsureUser(normalizedTargetId);
            if (user.Groups.Contains(normalizedGroupName, Comparer))
            {
                return false;
            }

            user.Groups.Add(normalizedGroupName);
            user.Groups = user.Groups.Distinct(Comparer).OrderBy(value => value, StringComparer.Ordinal).ToList();
            _auditLogger.LogMutation(FormatActor(actorId), "assign_group", normalizedTargetId, $"{normalizedGroupName}|{reason}".TrimEnd('|'));
            PersistAndBroadcast($"group '{normalizedGroupName}' assigned to {normalizedTargetId}");
            return true;
        }

        public bool UnassignGroup(string actorId, string targetUserId, string groupName, string reason = "")
        {
            string normalizedTargetId = NormalizeSubjectId(targetUserId);
            string normalizedGroupName = PermissionNode.NormalizeGroupName(groupName);
            if (string.IsNullOrWhiteSpace(normalizedTargetId) || string.IsNullOrWhiteSpace(normalizedGroupName))
            {
                return false;
            }

            if (!CanManageGroupAssignment(actorId, normalizedTargetId, normalizedGroupName, PermissionBuiltIns.Nodes.PermissionsGroupUnassign))
            {
                return false;
            }

            PermissionUserRecord user = GetUser(normalizedTargetId);
            if (user == null)
            {
                return false;
            }

            int removed = user.Groups.RemoveAll(value => Comparer.Equals(value, normalizedGroupName));
            RemoveMatchingTemporaryGroups(user, normalizedGroupName);
            if (removed == 0)
            {
                return false;
            }

            RemoveUserIfEmpty(user);
            _auditLogger.LogMutation(FormatActor(actorId), "unassign_group", normalizedTargetId, $"{normalizedGroupName}|{reason}".TrimEnd('|'));
            PersistAndBroadcast($"group '{normalizedGroupName}' unassigned from {normalizedTargetId}");
            return true;
        }

        public bool GrantNode(string actorId, string targetUserId, string node, string reason = "")
        {
            return MutateDirectNode(actorId, targetUserId, node, PermissionBuiltIns.Nodes.PermissionsGrant, isGrant: true, reason: reason);
        }

        public bool DenyNode(string actorId, string targetUserId, string node, string reason = "")
        {
            return MutateDirectNode(actorId, targetUserId, node, PermissionBuiltIns.Nodes.PermissionsDeny, isGrant: false, reason: reason);
        }

        public bool RevokeNode(string actorId, string targetUserId, string node, string reason = "")
        {
            string normalizedTargetId = NormalizeSubjectId(targetUserId);
            string normalizedNode = PermissionNode.Normalize(node);
            if (string.IsNullOrWhiteSpace(normalizedTargetId) || string.IsNullOrWhiteSpace(normalizedNode))
            {
                return false;
            }

            if (!CanManageSubject(actorId, normalizedTargetId, PermissionBuiltIns.Nodes.PermissionsRevoke))
            {
                return false;
            }

            PermissionUserRecord user = GetUser(normalizedTargetId);
            if (user == null)
            {
                return false;
            }

            int removed = user.Allow.RemoveAll(value => StringComparer.Ordinal.Equals(value, normalizedNode));
            removed += user.Deny.RemoveAll(value => StringComparer.Ordinal.Equals(value, normalizedNode));
            removed += user.TemporaryAllow.RemoveAll(item => StringComparer.Ordinal.Equals(item.Node, normalizedNode));
            removed += user.TemporaryDeny.RemoveAll(item => StringComparer.Ordinal.Equals(item.Node, normalizedNode));
            if (removed == 0)
            {
                return false;
            }

            RemoveUserIfEmpty(user);
            _auditLogger.LogMutation(FormatActor(actorId), "revoke_node", normalizedTargetId, $"{normalizedNode}|{reason}".TrimEnd('|'));
            PersistAndBroadcast($"node '{normalizedNode}' revoked from {normalizedTargetId}");
            return true;
        }

        public bool GrantTemporaryNode(string actorId, string targetUserId, string node, DateTime expiresAtUtc, string reason = "")
        {
            string normalizedTargetId = NormalizeSubjectId(targetUserId);
            string normalizedNode = PermissionNode.Normalize(node);
            if (string.IsNullOrWhiteSpace(normalizedTargetId) || string.IsNullOrWhiteSpace(normalizedNode) || expiresAtUtc <= DateTime.UtcNow)
            {
                return false;
            }

            if (!CanManageSubject(actorId, normalizedTargetId, PermissionBuiltIns.Nodes.PermissionsTempGrant))
            {
                return false;
            }

            PermissionUserRecord user = EnsureUser(normalizedTargetId);
            user.TemporaryAllow.Add(new TemporaryPermissionGrant
            {
                Id = Guid.NewGuid().ToString("N"),
                Node = normalizedNode,
                ExpiresAtUtc = expiresAtUtc.ToUniversalTime(),
                GrantedBy = FormatActor(actorId),
                Reason = reason?.Trim() ?? string.Empty
            });

            _auditLogger.LogMutation(FormatActor(actorId), "temp_grant", normalizedTargetId, $"{normalizedNode}|{expiresAtUtc.ToUniversalTime():O}|{reason}".TrimEnd('|'));
            PersistAndBroadcast($"temporary node '{normalizedNode}' granted to {normalizedTargetId}");
            return true;
        }

        public bool AddBan(string actorId, string targetUserId, string reason = "")
        {
            string normalizedTargetId = NormalizeSubjectId(targetUserId);
            if (string.IsNullOrWhiteSpace(normalizedTargetId))
            {
                return false;
            }

            if (!CanManageSubject(actorId, normalizedTargetId, PermissionBuiltIns.Nodes.PlayerBan))
            {
                return false;
            }

            if (_data.Bans.ContainsKey(normalizedTargetId))
            {
                return false;
            }

            _data.Bans[normalizedTargetId] = new BanEntry
            {
                SubjectId = normalizedTargetId,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = FormatActor(actorId),
                Reason = reason?.Trim() ?? string.Empty
            };

            _auditLogger.LogMutation(FormatActor(actorId), "ban", normalizedTargetId, reason?.Trim() ?? string.Empty);
            PersistAndBroadcast($"user '{normalizedTargetId}' banned");
            return true;
        }

        public bool RemoveBan(string actorId, string targetUserId, string reason = "")
        {
            string normalizedTargetId = NormalizeSubjectId(targetUserId);
            if (string.IsNullOrWhiteSpace(normalizedTargetId))
            {
                return false;
            }

            if (!RequireActorPermission(actorId, PermissionBuiltIns.Nodes.PlayerUnban))
            {
                return false;
            }

            if (!_data.Bans.Remove(normalizedTargetId))
            {
                return false;
            }

            _auditLogger.LogMutation(FormatActor(actorId), "unban", normalizedTargetId, reason?.Trim() ?? string.Empty);
            PersistAndBroadcast($"user '{normalizedTargetId}' unbanned");
            return true;
        }

        private bool MutateDirectNode(string actorId, string targetUserId, string node, string requiredNode, bool isGrant, string reason)
        {
            string normalizedTargetId = NormalizeSubjectId(targetUserId);
            string normalizedNode = PermissionNode.Normalize(node);
            if (string.IsNullOrWhiteSpace(normalizedTargetId) || string.IsNullOrWhiteSpace(normalizedNode))
            {
                return false;
            }

            if (!CanManageSubject(actorId, normalizedTargetId, requiredNode))
            {
                return false;
            }

            PermissionUserRecord user = EnsureUser(normalizedTargetId);
            List<string> targetList = isGrant ? user.Allow : user.Deny;
            List<string> oppositeList = isGrant ? user.Deny : user.Allow;
            if (targetList.Contains(normalizedNode, StringComparer.Ordinal))
            {
                return false;
            }

            oppositeList.RemoveAll(value => StringComparer.Ordinal.Equals(value, normalizedNode));
            targetList.Add(normalizedNode);
            targetList.Sort(StringComparer.Ordinal);
            _auditLogger.LogMutation(FormatActor(actorId), isGrant ? "grant_node" : "deny_node", normalizedTargetId, $"{normalizedNode}|{reason}".TrimEnd('|'));
            PersistAndBroadcast($"node '{normalizedNode}' {(isGrant ? "granted" : "denied")} for {normalizedTargetId}");
            return true;
        }

        private bool CanManageSubject(string actorId, string targetUserId, string requiredNode)
        {
            return RequireActorPermission(actorId, requiredNode) && HasDominanceOver(actorId, targetUserId);
        }

        private bool CanManageGroupAssignment(string actorId, string targetUserId, string groupName, string requiredNode)
        {
            if (!CanManageSubject(actorId, targetUserId, requiredNode))
            {
                return false;
            }

            string normalizedActorId = NormalizeSubjectId(actorId);
            return string.IsNullOrWhiteSpace(normalizedActorId) || GetHighestPriority(normalizedActorId) > GetGroupPriority(groupName);
        }

        private bool RequireActorPermission(string actorId, string node)
        {
            string normalizedActorId = NormalizeSubjectId(actorId);
            return string.IsNullOrWhiteSpace(normalizedActorId) || HasPermission(normalizedActorId, node);
        }

        private PermissionUserRecord EnsureUser(string subjectId)
        {
            string normalizedSubjectId = NormalizeSubjectId(subjectId);
            if (!_data.Users.TryGetValue(normalizedSubjectId, out PermissionUserRecord user))
            {
                user = new PermissionUserRecord
                {
                    UserId = normalizedSubjectId
                };
                _data.Users[normalizedSubjectId] = user;
            }

            return user;
        }

        private void RemoveMatchingTemporaryGroups(PermissionUserRecord user, string groupName)
        {
            if (user == null)
            {
                return;
            }

            user.TemporaryGroups.RemoveAll(item => Comparer.Equals(item.GroupName, groupName));
        }

        private void RemoveUserIfEmpty(PermissionUserRecord user)
        {
            if (user == null)
            {
                return;
            }

            if (user.Groups.Count > 0 ||
                user.Allow.Count > 0 ||
                user.Deny.Count > 0 ||
                user.TemporaryGroups.Count > 0 ||
                user.TemporaryAllow.Count > 0 ||
                user.TemporaryDeny.Count > 0)
            {
                return;
            }

            _data.Users.Remove(user.UserId);
        }

        private string FormatActor(string actorId)
        {
            return string.IsNullOrWhiteSpace(actorId) ? "system" : NormalizeSubjectId(actorId);
        }

        private void RegisterBuiltInDefinitions()
        {
            RegisterPermissionDefinitions(
                "core",
                new[]
                {
                    CreateDefinition(PermissionBuiltIns.Nodes.ServerHelp, "server", "View help for built-in server commands."),
                    CreateDefinition(PermissionBuiltIns.Nodes.ConsoleOpen, "console", "Open the remote administration console."),
                    CreateDefinition(PermissionBuiltIns.Nodes.ConsoleCommandWildcard, "console", "Execute any relayed game console command."),
                    CreateDefinition(PermissionBuiltIns.Nodes.ClientModPolicyBypass, "clientmods", "Bypass client mod policy verification."),
                    CreateDefinition(PermissionBuiltIns.Nodes.PlayerList, "player", "List connected players."),
                    CreateDefinition(PermissionBuiltIns.Nodes.PlayerKick, "player", "Kick connected players."),
                    CreateDefinition(PermissionBuiltIns.Nodes.PlayerBan, "player", "Ban players from the server."),
                    CreateDefinition(PermissionBuiltIns.Nodes.PlayerUnban, "player", "Remove player bans."),
                    CreateDefinition(PermissionBuiltIns.Nodes.ServerInfo, "server", "View server information."),
                    CreateDefinition(PermissionBuiltIns.Nodes.ServerSave, "server", "Trigger a save."),
                    CreateDefinition(PermissionBuiltIns.Nodes.ServerReloadConfig, "server", "Reload server configuration."),
                    CreateDefinition(PermissionBuiltIns.Nodes.ServerStop, "server", "Stop the dedicated server."),
                    CreateDefinition(PermissionBuiltIns.Nodes.PermissionsReload, "permissions", "Reload permissions from disk."),
                    CreateDefinition(PermissionBuiltIns.Nodes.PermissionsInfo, "permissions", "Inspect permission state."),
                    CreateDefinition(PermissionBuiltIns.Nodes.PermissionsGrant, "permissions", "Grant direct permission nodes."),
                    CreateDefinition(PermissionBuiltIns.Nodes.PermissionsDeny, "permissions", "Deny direct permission nodes."),
                    CreateDefinition(PermissionBuiltIns.Nodes.PermissionsRevoke, "permissions", "Revoke direct permission nodes."),
                    CreateDefinition(PermissionBuiltIns.Nodes.PermissionsTempGrant, "permissions", "Grant temporary permission nodes."),
                    CreateDefinition(PermissionBuiltIns.Nodes.PermissionsGroupList, "permissions", "List permission groups."),
                    CreateDefinition(PermissionBuiltIns.Nodes.PermissionsGroupAssign, "permissions", "Assign permission groups."),
                    CreateDefinition(PermissionBuiltIns.Nodes.PermissionsGroupUnassign, "permissions", "Remove permission groups.")
                });
        }

        private static PermissionDefinition CreateDefinition(string node, string category, string description)
        {
            return new PermissionDefinition
            {
                Node = node,
                Category = category,
                Description = description
            };
        }

        private PermissionStoreData LoadOrCreatePermissionData()
        {
            RegisterKnownRemoteCommands(GetKnownConsoleCommandsFromConstants());

            return NormalizeAndValidate(_migrationCoordinator.LoadOrCreate());
        }

        private bool TryLoadMigrationConfig(string path, out ServerConfig config)
        {
            config = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                config = ServerConfig.LoadConfigSnapshot(path);
                return config != null;
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to load migration config from {path}: {ex.Message}");
                return false;
            }
        }

        private PermissionStoreData CreatePermissionDataFromLegacyConfig(ServerConfig config, string migratedFromPath)
        {
            PermissionStoreData data = CreateDefaultStoreData();
            data.MigrationVersion = 1;
            data.MigratedFrom = migratedFromPath ?? string.Empty;
            data.MigratedAtUtc = DateTime.UtcNow;

            AddLegacyConsoleOpen(config.EnableConsoleForPlayers, PermissionBuiltIns.Groups.Default, data);
            AddLegacyConsoleOpen(config.EnableConsoleForAdmins, PermissionBuiltIns.Groups.Administrator, data);
            AddLegacyConsoleOpen(config.EnableConsoleForOps, PermissionBuiltIns.Groups.Operator, data);

            AddNodesToGroup(data, PermissionBuiltIns.Groups.Default, config.PlayerAllowedCommands, isDeny: false);
            AddNodesToGroup(data, PermissionBuiltIns.Groups.Administrator, config.AllowedCommands, isDeny: false);
            AddNodesToGroup(data, PermissionBuiltIns.Groups.Administrator, config.RestrictedCommands, isDeny: true);
            AddNodesToGroup(data, PermissionBuiltIns.Groups.Operator, config.RestrictedCommands, isDeny: false);

            PermissionGroupDefinition operatorGroup = data.Groups[PermissionBuiltIns.Groups.Operator];
            if (!operatorGroup.Allow.Contains(PermissionBuiltIns.Nodes.ConsoleCommandWildcard, StringComparer.Ordinal))
            {
                operatorGroup.Allow.Add(PermissionBuiltIns.Nodes.ConsoleCommandWildcard);
                operatorGroup.Allow.Sort(StringComparer.Ordinal);
            }

            IReadOnlyList<string> globalDisabledNodes = ExpandLegacyCommandNodes(config.GlobalDisabledCommands).ToList().AsReadOnly();
            foreach (PermissionGroupDefinition group in data.Groups.Values)
            {
                MergeNodes(group.Deny, globalDisabledNodes);
            }

            foreach (string operatorId in config.Operators ?? new HashSet<string>())
            {
                if (!string.IsNullOrWhiteSpace(operatorId))
                {
                    EnsureUserMigration(data, operatorId).Groups.Add(PermissionBuiltIns.Groups.Operator);
                }
            }

            foreach (string adminId in config.Admins ?? new HashSet<string>())
            {
                if (string.IsNullOrWhiteSpace(adminId))
                {
                    continue;
                }

                PermissionUserRecord user = EnsureUserMigration(data, adminId);
                if (!user.Groups.Contains(PermissionBuiltIns.Groups.Operator, Comparer))
                {
                    user.Groups.Add(PermissionBuiltIns.Groups.Administrator);
                }
            }

            foreach (string bannedId in config.BannedPlayers ?? new HashSet<string>())
            {
                string normalizedBannedId = NormalizeSubjectId(bannedId);
                if (string.IsNullOrWhiteSpace(normalizedBannedId))
                {
                    continue;
                }

                data.Bans[normalizedBannedId] = new BanEntry
                {
                    SubjectId = normalizedBannedId,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedBy = "migration",
                    Reason = "Migrated from legacy configuration"
                };
            }

            foreach (PermissionUserRecord user in data.Users.Values)
            {
                user.Groups = user.Groups
                    .Select(PermissionNode.NormalizeGroupName)
                    .Distinct(Comparer)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList();
            }

            return data;
        }

        private static PermissionUserRecord EnsureUserMigration(PermissionStoreData data, string subjectId)
        {
            string normalizedSubjectId = NormalizeSubjectId(subjectId);
            if (!data.Users.TryGetValue(normalizedSubjectId, out PermissionUserRecord user))
            {
                user = new PermissionUserRecord
                {
                    UserId = normalizedSubjectId
                };
                data.Users[normalizedSubjectId] = user;
            }

            return user;
        }

        private static void AddLegacyConsoleOpen(bool enabled, string groupName, PermissionStoreData data)
        {
            if (!enabled || !data.Groups.TryGetValue(groupName, out PermissionGroupDefinition group))
            {
                return;
            }

            if (!group.Allow.Contains(PermissionBuiltIns.Nodes.ConsoleOpen, StringComparer.Ordinal))
            {
                group.Allow.Add(PermissionBuiltIns.Nodes.ConsoleOpen);
                group.Allow.Sort(StringComparer.Ordinal);
            }
        }

        private static IEnumerable<string> ExpandLegacyCommandNodes(IEnumerable<string> commandWords)
        {
            foreach (string commandWord in commandWords ?? Enumerable.Empty<string>())
            {
                string normalizedCommandWord = commandWord?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(normalizedCommandWord))
                {
                    continue;
                }

                if (BuiltInServerCommandNodes.TryGetValue(normalizedCommandWord, out string builtInNode))
                {
                    yield return builtInNode;
                    continue;
                }

                yield return PermissionNode.CreateConsoleCommandNode(normalizedCommandWord);
            }
        }

        private static void AddNodesToGroup(PermissionStoreData data, string groupName, IEnumerable<string> commandWords, bool isDeny)
        {
            if (!data.Groups.TryGetValue(groupName, out PermissionGroupDefinition group))
            {
                return;
            }

            MergeNodes(isDeny ? group.Deny : group.Allow, ExpandLegacyCommandNodes(commandWords));
        }

        private static void MergeNodes(List<string> target, IEnumerable<string> nodes)
        {
            HashSet<string> mergedNodes = new HashSet<string>(target ?? new List<string>(), StringComparer.Ordinal);
            foreach (string node in nodes ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(node))
                {
                    mergedNodes.Add(PermissionNode.Normalize(node));
                }
            }

            target.Clear();
            target.AddRange(mergedNodes.OrderBy(value => value, StringComparer.Ordinal));
        }

        private PermissionStoreData CreateDefaultStoreData()
        {
            PermissionStoreData data = new PermissionStoreData
            {
                SchemaVersion = 1,
                MigrationVersion = 1
            };
            SeedBuiltInGroups(data);
            return data;
        }

        private PermissionStoreData NormalizeAndValidate(PermissionStoreData data)
        {
            PermissionStoreData normalized = data ?? new PermissionStoreData();
            normalized.SchemaVersion = Math.Max(1, normalized.SchemaVersion);
            normalized.MigrationVersion = Math.Max(1, normalized.MigrationVersion);
            normalized.Groups = new Dictionary<string, PermissionGroupDefinition>(normalized.Groups ?? new Dictionary<string, PermissionGroupDefinition>(), Comparer);
            normalized.Users = new Dictionary<string, PermissionUserRecord>(normalized.Users ?? new Dictionary<string, PermissionUserRecord>(), Comparer);
            normalized.Bans = new Dictionary<string, BanEntry>(normalized.Bans ?? new Dictionary<string, BanEntry>(), Comparer);

            SeedBuiltInGroups(normalized);
            NormalizeGroups(normalized);
            NormalizeUsers(normalized);
            NormalizeBans(normalized);
            ValidateGroupReferences(normalized);
            ValidateInheritance(normalized);
            PruneExpiredEntries(normalized);

            return normalized;
        }

        private void SeedBuiltInGroups(PermissionStoreData data)
        {
            EnsureBuiltInGroup(data, PermissionBuiltIns.Groups.Default, 0, Array.Empty<string>(), new[] { PermissionBuiltIns.Nodes.ServerHelp }, Array.Empty<string>());
            EnsureBuiltInGroup(data, PermissionBuiltIns.Groups.Support, 10, new[] { PermissionBuiltIns.Groups.Default }, new[] { PermissionBuiltIns.Nodes.ServerInfo }, Array.Empty<string>());
            EnsureBuiltInGroup(data, PermissionBuiltIns.Groups.Moderator, 20, new[] { PermissionBuiltIns.Groups.Support }, new[]
            {
                PermissionBuiltIns.Nodes.PlayerList,
                PermissionBuiltIns.Nodes.PlayerKick,
                PermissionBuiltIns.Nodes.PlayerBan,
                PermissionBuiltIns.Nodes.PlayerUnban
            }, Array.Empty<string>());
            EnsureBuiltInGroup(data, PermissionBuiltIns.Groups.Administrator, 30, new[] { PermissionBuiltIns.Groups.Moderator }, new[]
            {
                PermissionBuiltIns.Nodes.ServerSave,
                PermissionBuiltIns.Nodes.ServerReloadConfig,
                PermissionBuiltIns.Nodes.PermissionsInfo,
                PermissionBuiltIns.Nodes.PermissionsGroupList
            }, Array.Empty<string>());
            EnsureBuiltInGroup(data, PermissionBuiltIns.Groups.Operator, 40, new[] { PermissionBuiltIns.Groups.Administrator }, new[]
            {
                PermissionBuiltIns.Nodes.ClientModPolicyBypass,
                PermissionBuiltIns.Nodes.ServerStop,
                PermissionBuiltIns.Nodes.PermissionsReload,
                PermissionBuiltIns.Nodes.PermissionsGrant,
                PermissionBuiltIns.Nodes.PermissionsDeny,
                PermissionBuiltIns.Nodes.PermissionsRevoke,
                PermissionBuiltIns.Nodes.PermissionsTempGrant,
                PermissionBuiltIns.Nodes.PermissionsGroupAssign,
                PermissionBuiltIns.Nodes.PermissionsGroupUnassign
            }, Array.Empty<string>());
        }

        private static void EnsureBuiltInGroup(
            PermissionStoreData data,
            string groupName,
            int priority,
            IEnumerable<string> inherits,
            IEnumerable<string> allow,
            IEnumerable<string> deny)
        {
            string normalizedGroupName = PermissionNode.NormalizeGroupName(groupName);
            if (!data.Groups.TryGetValue(normalizedGroupName, out PermissionGroupDefinition group))
            {
                group = new PermissionGroupDefinition
                {
                    Name = normalizedGroupName
                };
                data.Groups[normalizedGroupName] = group;
            }

            group.Name = normalizedGroupName;
            group.Priority = priority;
            group.Inherits = MergeNormalizedStrings(group.Inherits, inherits, isGroupName: true);
            group.Allow = MergeNormalizedStrings(group.Allow, allow, isGroupName: false);
            group.Deny = MergeNormalizedStrings(group.Deny, deny, isGroupName: false);
        }

        private static List<string> MergeNormalizedStrings(IEnumerable<string> first, IEnumerable<string> second, bool isGroupName)
        {
            IEnumerable<string> values = (first ?? Enumerable.Empty<string>())
                .Concat(second ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => isGroupName ? PermissionNode.NormalizeGroupName(value) : PermissionNode.Normalize(value))
                .Where(value => !string.IsNullOrWhiteSpace(value));

            return values
                .Distinct(isGroupName ? Comparer : StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private static void NormalizeGroups(PermissionStoreData data)
        {
            Dictionary<string, PermissionGroupDefinition> normalizedGroups = new Dictionary<string, PermissionGroupDefinition>(Comparer);
            foreach (PermissionGroupDefinition sourceGroup in data.Groups.Values)
            {
                string normalizedGroupName = PermissionNode.NormalizeGroupName(sourceGroup?.Name);
                if (string.IsNullOrWhiteSpace(normalizedGroupName))
                {
                    continue;
                }

                if (!normalizedGroups.TryGetValue(normalizedGroupName, out PermissionGroupDefinition group))
                {
                    group = new PermissionGroupDefinition
                    {
                        Name = normalizedGroupName,
                        Priority = BuiltInGroupPriorities.TryGetValue(normalizedGroupName, out int builtInPriority)
                            ? builtInPriority
                            : sourceGroup?.Priority ?? 0
                    };
                    normalizedGroups[normalizedGroupName] = group;
                }

                group.Priority = BuiltInGroupPriorities.TryGetValue(normalizedGroupName, out int priority)
                    ? priority
                    : sourceGroup?.Priority ?? group.Priority;
                group.Inherits = MergeNormalizedStrings(group.Inherits, sourceGroup?.Inherits, isGroupName: true);
                group.Allow = MergeNormalizedStrings(group.Allow, sourceGroup?.Allow, isGroupName: false);
                group.Deny = MergeNormalizedStrings(group.Deny, sourceGroup?.Deny, isGroupName: false);
            }

            data.Groups = normalizedGroups;
        }

        private static void NormalizeUsers(PermissionStoreData data)
        {
            Dictionary<string, PermissionUserRecord> normalizedUsers = new Dictionary<string, PermissionUserRecord>(Comparer);
            foreach (PermissionUserRecord sourceUser in data.Users.Values)
            {
                string normalizedUserId = NormalizeSubjectId(sourceUser?.UserId);
                if (string.IsNullOrWhiteSpace(normalizedUserId))
                {
                    continue;
                }

                PermissionUserRecord user = new PermissionUserRecord
                {
                    UserId = normalizedUserId,
                    Groups = MergeNormalizedStrings(sourceUser.Groups, Array.Empty<string>(), isGroupName: true),
                    Allow = MergeNormalizedStrings(sourceUser.Allow, Array.Empty<string>(), isGroupName: false),
                    Deny = MergeNormalizedStrings(sourceUser.Deny, Array.Empty<string>(), isGroupName: false),
                    TemporaryGroups = NormalizeTemporaryGroups(sourceUser.TemporaryGroups),
                    TemporaryAllow = NormalizeTemporaryNodes(sourceUser.TemporaryAllow),
                    TemporaryDeny = NormalizeTemporaryNodes(sourceUser.TemporaryDeny)
                };
                normalizedUsers[normalizedUserId] = user;
            }

            data.Users = normalizedUsers;
        }

        private static List<TemporaryGroupAssignment> NormalizeTemporaryGroups(IEnumerable<TemporaryGroupAssignment> assignments)
        {
            return (assignments ?? Enumerable.Empty<TemporaryGroupAssignment>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.GroupName))
                .Select(item => new TemporaryGroupAssignment
                {
                    Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id.Trim(),
                    GroupName = PermissionNode.NormalizeGroupName(item.GroupName),
                    ExpiresAtUtc = item.ExpiresAtUtc.ToUniversalTime(),
                    GrantedBy = item.GrantedBy?.Trim() ?? string.Empty,
                    Reason = item.Reason?.Trim() ?? string.Empty
                })
                .OrderBy(item => item.ExpiresAtUtc)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToList();
        }

        private static List<TemporaryPermissionGrant> NormalizeTemporaryNodes(IEnumerable<TemporaryPermissionGrant> grants)
        {
            return (grants ?? Enumerable.Empty<TemporaryPermissionGrant>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Node))
                .Select(item => new TemporaryPermissionGrant
                {
                    Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id.Trim(),
                    Node = PermissionNode.Normalize(item.Node),
                    ExpiresAtUtc = item.ExpiresAtUtc.ToUniversalTime(),
                    GrantedBy = item.GrantedBy?.Trim() ?? string.Empty,
                    Reason = item.Reason?.Trim() ?? string.Empty
                })
                .OrderBy(item => item.ExpiresAtUtc)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToList();
        }

        private static void NormalizeBans(PermissionStoreData data)
        {
            Dictionary<string, BanEntry> normalizedBans = new Dictionary<string, BanEntry>(Comparer);
            foreach (BanEntry sourceBan in data.Bans.Values)
            {
                string normalizedSubjectId = NormalizeSubjectId(sourceBan?.SubjectId);
                if (string.IsNullOrWhiteSpace(normalizedSubjectId))
                {
                    continue;
                }

                normalizedBans[normalizedSubjectId] = new BanEntry
                {
                    SubjectId = normalizedSubjectId,
                    CreatedAtUtc = sourceBan.CreatedAtUtc == default ? DateTime.UtcNow : sourceBan.CreatedAtUtc.ToUniversalTime(),
                    CreatedBy = sourceBan.CreatedBy?.Trim() ?? string.Empty,
                    Reason = sourceBan.Reason?.Trim() ?? string.Empty
                };
            }

            data.Bans = normalizedBans;
        }

        private void ValidateGroupReferences(PermissionStoreData data)
        {
            foreach (PermissionUserRecord user in data.Users.Values)
            {
                foreach (string groupName in user.Groups.Concat(user.TemporaryGroups.Select(item => item.GroupName)))
                {
                    if (!data.Groups.ContainsKey(groupName))
                    {
                        throw new InvalidOperationException($"User '{user.UserId}' references unknown group '{groupName}'.");
                    }
                }
            }
        }

        private void ValidateInheritance(PermissionStoreData data)
        {
            Dictionary<string, int> visitStates = new Dictionary<string, int>(Comparer);
            foreach (PermissionGroupDefinition group in data.Groups.Values)
            {
                VisitGroup(group.Name);
            }

            void VisitGroup(string groupName)
            {
                string normalizedGroupName = PermissionNode.NormalizeGroupName(groupName);
                if (visitStates.TryGetValue(normalizedGroupName, out int state))
                {
                    if (state == 1)
                    {
                        throw new InvalidOperationException($"Permission group inheritance cycle detected at '{normalizedGroupName}'.");
                    }

                    return;
                }

                if (!data.Groups.TryGetValue(normalizedGroupName, out PermissionGroupDefinition group))
                {
                    throw new InvalidOperationException($"Permission group '{normalizedGroupName}' does not exist.");
                }

                visitStates[normalizedGroupName] = 1;
                foreach (string inheritedGroupName in group.Inherits)
                {
                    if (!data.Groups.ContainsKey(inheritedGroupName))
                    {
                        throw new InvalidOperationException($"Group '{normalizedGroupName}' inherits unknown group '{inheritedGroupName}'.");
                    }

                    VisitGroup(inheritedGroupName);
                }

                visitStates[normalizedGroupName] = 2;
            }
        }

        private bool PruneExpiredEntries(PermissionStoreData data)
        {
            bool changed = false;
            DateTime nowUtc = DateTime.UtcNow;

            foreach (PermissionUserRecord user in data.Users.Values)
            {
                changed |= user.TemporaryGroups.RemoveAll(item => item.ExpiresAtUtc <= nowUtc) > 0;
                changed |= user.TemporaryAllow.RemoveAll(item => item.ExpiresAtUtc <= nowUtc) > 0;
                changed |= user.TemporaryDeny.RemoveAll(item => item.ExpiresAtUtc <= nowUtc) > 0;
            }

            List<string> emptyUsers = data.Users.Values
                .Where(user => user.Groups.Count == 0 &&
                               user.Allow.Count == 0 &&
                               user.Deny.Count == 0 &&
                               user.TemporaryGroups.Count == 0 &&
                               user.TemporaryAllow.Count == 0 &&
                               user.TemporaryDeny.Count == 0)
                .Select(user => user.UserId)
                .ToList();

            foreach (string userId in emptyUsers)
            {
                changed |= data.Users.Remove(userId);
            }

            return changed;
        }

        private static IEnumerable<string> GetKnownConsoleCommandsFromConstants()
        {
            return typeof(Constants.Commands)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => field.GetRawConstantValue() as string)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.ToLowerInvariant())
                .Distinct(Comparer)
                .OrderBy(value => value, StringComparer.Ordinal);
        }

        private static PermissionEvaluationResult CreateDeniedResult(string subjectId, string node)
        {
            return new PermissionEvaluationResult
            {
                SubjectId = subjectId ?? string.Empty,
                Node = node ?? string.Empty,
                IsGranted = false,
                MatchedRule = string.Empty,
                SourceType = string.Empty,
                SourceName = string.Empty,
                Specificity = -1,
                Priority = 0
            };
        }

        private void AddMatches(
            IEnumerable<string> rules,
            string requestedNode,
            string sourceType,
            string sourceName,
            int priority,
            bool isAllow,
            ref PermissionMatch winningMatch)
        {
            foreach (string rule in rules ?? Enumerable.Empty<string>())
            {
                int specificity = PermissionNode.GetSpecificity(rule, requestedNode);
                if (specificity < 0)
                {
                    continue;
                }

                PermissionMatch candidate = new PermissionMatch
                {
                    Rule = rule,
                    IsAllow = isAllow,
                    SourceType = sourceType,
                    SourceName = sourceName,
                    Specificity = specificity,
                    Priority = priority,
                    SourceTier = string.Equals(sourceType, "group", StringComparison.Ordinal) ? 1 : 2
                };

                if (winningMatch == null || CompareMatches(candidate, winningMatch) < 0)
                {
                    winningMatch = candidate;
                }
            }
        }

        private static int CompareMatches(PermissionMatch left, PermissionMatch right)
        {
            int specificity = right.Specificity.CompareTo(left.Specificity);
            if (specificity != 0)
            {
                return specificity;
            }

            int sourceTier = right.SourceTier.CompareTo(left.SourceTier);
            if (sourceTier != 0)
            {
                return sourceTier;
            }

            int priority = right.Priority.CompareTo(left.Priority);
            if (priority != 0)
            {
                return priority;
            }

            if (left.IsAllow != right.IsAllow)
            {
                return left.IsAllow ? 1 : -1;
            }

            return string.CompareOrdinal(left.Rule, right.Rule);
        }

        private PermissionUserRecord GetUser(string subjectId)
        {
            string normalizedSubjectId = NormalizeSubjectId(subjectId);
            return string.IsNullOrWhiteSpace(normalizedSubjectId)
                ? null
                : _data.Users.GetValueOrDefault(normalizedSubjectId);
        }

        private int GetHighestPriority(string subjectId)
        {
            return GetEffectiveGroups(subjectId)
                .Select(GetGroupPriority)
                .DefaultIfEmpty(0)
                .Max();
        }

        private int GetGroupPriority(string groupName)
        {
            string normalizedGroupName = PermissionNode.NormalizeGroupName(groupName);
            if (_data.Groups.TryGetValue(normalizedGroupName, out PermissionGroupDefinition group))
            {
                return group.Priority;
            }

            return BuiltInGroupPriorities.GetValueOrDefault(normalizedGroupName, 0);
        }

        private void CollectEffectiveGroups(string subjectId, ISet<string> effectiveGroups)
        {
            if (effectiveGroups == null)
            {
                return;
            }

            CollectGroupClosure(PermissionBuiltIns.Groups.Default, effectiveGroups);

            PermissionUserRecord user = GetUser(subjectId);
            if (user == null)
            {
                return;
            }

            foreach (string groupName in user.Groups)
            {
                CollectGroupClosure(groupName, effectiveGroups);
            }

            foreach (TemporaryGroupAssignment temporaryGroup in user.TemporaryGroups)
            {
                if (temporaryGroup.ExpiresAtUtc > DateTime.UtcNow)
                {
                    CollectGroupClosure(temporaryGroup.GroupName, effectiveGroups);
                }
            }
        }

        private void CollectGroupClosure(string groupName, ISet<string> effectiveGroups)
        {
            string normalizedGroupName = PermissionNode.NormalizeGroupName(groupName);
            if (string.IsNullOrWhiteSpace(normalizedGroupName) ||
                !_data.Groups.TryGetValue(normalizedGroupName, out PermissionGroupDefinition group) ||
                !effectiveGroups.Add(normalizedGroupName))
            {
                return;
            }

            foreach (string inheritedGroup in group.Inherits)
            {
                CollectGroupClosure(inheritedGroup, effectiveGroups);
            }
        }

        private void PersistAndBroadcast(string reason)
        {
            _data = NormalizeAndValidate(_data);
            _store.Save(_data);
            RefreshAllCapabilitySnapshots();
            StateChanged?.Invoke();

            if (!string.IsNullOrWhiteSpace(reason))
            {
                _logger.Msg($"Permissions updated: {reason}");
            }
        }

        private void RegisterKnownRemoteCommands(IEnumerable<string> commandWords)
        {
            foreach (string commandWord in commandWords ?? Enumerable.Empty<string>())
            {
                string normalizedCommandWord = commandWord?.Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(normalizedCommandWord))
                {
                    _knownRemoteCommands.Add(normalizedCommandWord);
                }
            }

            foreach (string builtInCommandWord in BuiltInServerCommandNodes.Keys)
            {
                _knownRemoteCommands.Add(builtInCommandWord);
            }
        }

        private List<string> GetAllowedRemoteCommands(string subjectId)
        {
            string normalizedSubjectId = NormalizeSubjectId(subjectId);
            if (string.IsNullOrWhiteSpace(normalizedSubjectId))
            {
                return new List<string>();
            }

            List<string> allowedCommands = _knownRemoteCommands
                .Where(commandWord => HasPermission(normalizedSubjectId, PermissionNode.CreateConsoleCommandNode(commandWord)))
                .OrderBy(commandWord => commandWord, StringComparer.Ordinal)
                .ToList();

            if (HasPermission(normalizedSubjectId, PermissionNode.CreateConsoleCommandNode("__permission_probe__")) &&
                !allowedCommands.Contains("*", StringComparer.Ordinal))
            {
                allowedCommands.Insert(0, "*");
            }

            return allowedCommands;
        }

        private void RefreshAllCapabilitySnapshots()
        {
            if (_playerManager == null)
            {
                return;
            }

            foreach (ConnectedPlayerInfo player in _playerManager.GetConnectedPlayers())
            {
                SendCapabilitySnapshot(player);
            }
        }

        private void HandlePlayerJoined(ConnectedPlayerInfo player)
        {
            SendCapabilitySnapshot(player);
        }

        private void SendCapabilitySnapshot(ConnectedPlayerInfo player)
        {
            if (player == null || player.Connection == null || !player.Connection.IsActive || player.IsLoopbackConnection)
            {
                return;
            }

            PermissionCapabilitySnapshot snapshot = BuildCapabilitySnapshot(player.TrustedUniqueId);
            CustomMessaging.SendToClientOrDeferUntilReady(
                player.Connection,
                Constants.Messages.PermissionSnapshot,
                JsonConvert.SerializeObject(snapshot));
        }

        private static string NormalizeSubjectId(string subjectId)
        {
            return (subjectId ?? string.Empty).Trim();
        }

        private static string NormalizeModId(string modId)
        {
            return string.IsNullOrWhiteSpace(modId)
                ? "unknown"
                : string.Concat((modId ?? string.Empty).Trim().ToLowerInvariant().Where(character => char.IsLetterOrDigit(character) || character == '_' || character == '-'));
        }

        private sealed class PermissionMatch
        {
            public string Rule { get; set; }

            public bool IsAllow { get; set; }

            public string SourceType { get; set; }

            public string SourceName { get; set; }

            public int Specificity { get; set; }

            public int Priority { get; set; }

            public int SourceTier { get; set; }
        }
    }
}
