using DedicatedServerMod.API.Metadata;
using DedicatedServerMod.Shared.ModVerification;
using MelonLoader;

namespace DedicatedServerMod.API
{
    public static partial class ModManager
    {
        private sealed class DiscoveredClientModEntry
        {
            public ClientModMetadata Metadata { get; set; } = new ClientModMetadata();

            public string Sha256 { get; set; } = string.Empty;
        }

#if SERVER
        internal static List<DeclaredClientCompanionRequirement> GetDeclaredServerCompanions()
        {
            List<DeclaredClientCompanionRequirement> discovered = new List<DeclaredClientCompanionRequirement>();

            try
            {
                IReadOnlyList<MelonBase> melonMods = MelonMod.RegisteredMelons;
                if (melonMods == null)
                {
                    return discovered;
                }

                foreach (MelonBase melon in melonMods)
                {
                    System.Reflection.Assembly assembly = melon?.MelonAssembly?.Assembly;
                    if (!(melon is IServerMod) || assembly == null)
                    {
                        continue;
                    }

                    object[] attributes = assembly.GetCustomAttributes(typeof(S1DSClientCompanionAttribute), false);
                    for (int i = 0; i < attributes.Length; i++)
                    {
                        if (!(attributes[i] is S1DSClientCompanionAttribute attribute))
                        {
                            continue;
                        }

                        discovered.Add(new DeclaredClientCompanionRequirement
                        {
                            ModId = ClientModPolicy.NormalizeValue(attribute.ModId),
                            DisplayName = ClientModPolicy.NormalizeValue(attribute.DisplayName),
                            Required = attribute.Required,
                            MinVersion = ClientModPolicy.NormalizeValue(attribute.MinVersion),
                            PinnedSha256 = (attribute.PinnedSha256 ?? Array.Empty<string>())
                                .Select(ClientModPolicy.NormalizeHash)
                                .Where(value => !string.IsNullOrEmpty(value))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList(),
                            SourceAssemblyName = assembly.GetName().Name ?? string.Empty
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error discovering server companion metadata: {ex.Message}");
            }

            return discovered;
        }
#endif

        internal static List<ClientModMetadata> GetLoadedClientModMetadata(System.Reflection.Assembly coreClientAssembly)
        {
            return DiscoverLoadedClientMods(coreClientAssembly)
                .Select(entry => entry.Metadata)
                .ToList();
        }

        internal static List<ClientModDescriptor> GetLoadedClientModsForVerification(System.Reflection.Assembly coreClientAssembly)
        {
            return DiscoverLoadedClientMods(coreClientAssembly)
                .Select(entry => new ClientModDescriptor
                {
                    ModId = entry.Metadata.ModId,
                    Version = entry.Metadata.Version,
                    DisplayName = entry.Metadata.DisplayName,
                    Author = entry.Metadata.Author,
                    AssemblyName = entry.Metadata.AssemblyName,
                    Sha256 = entry.Sha256,
                    IdentityDeclared = entry.Metadata.IdentityDeclared
                })
                .ToList();
        }

        private static List<DiscoveredClientModEntry> DiscoverLoadedClientMods(System.Reflection.Assembly coreClientAssembly)
        {
            List<DiscoveredClientModEntry> mods = new List<DiscoveredClientModEntry>();

            try
            {
                IReadOnlyList<MelonBase> melonMods = MelonMod.RegisteredMelons;
                if (melonMods == null)
                {
                    return mods;
                }

                foreach (MelonBase melon in melonMods)
                {
                    System.Reflection.Assembly assembly = melon?.MelonAssembly?.Assembly;
                    if (assembly == null)
                    {
                        continue;
                    }

                    if (coreClientAssembly != null && ReferenceEquals(assembly, coreClientAssembly))
                    {
                        continue;
                    }

                    S1DSClientModIdentityAttribute identity = assembly.GetCustomAttributes(typeof(S1DSClientModIdentityAttribute), false)
                        .OfType<S1DSClientModIdentityAttribute>()
                        .FirstOrDefault();

                    MelonInfoAttribute info = melon.Info;
                    string assemblyName = ClientModPolicy.NormalizeValue(assembly.GetName().Name);
                    string displayName = ClientModPolicy.NormalizeValue(info?.Name ?? melon.MelonTypeName ?? assemblyName);
                    string version = ClientModPolicy.NormalizeValue(identity?.Version ?? info?.Version ?? assembly.GetName().Version?.ToString());

                    mods.Add(new DiscoveredClientModEntry
                    {
                        Metadata = new ClientModMetadata
                        {
                            ModId = ClientModPolicy.NormalizeValue(identity?.ModId),
                            DisplayName = displayName,
                            Version = version,
                            Author = ClientModPolicy.NormalizeValue(info?.Author),
                            AssemblyName = assemblyName,
                            IdentityDeclared = identity != null
                        },
                        Sha256 = ClientModPolicy.NormalizeHash(ClientModHashUtility.TryResolveSha256(melon))
                    });
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error discovering client mods for verification: {ex.Message}");
            }

            return mods;
        }
    }
}
