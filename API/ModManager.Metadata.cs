using DedicatedServerMod.API.Metadata;
using DedicatedServerMod.Shared.ModVerification;
using MelonLoader;

namespace DedicatedServerMod.API
{
    public static partial class ModManager
    {
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

        internal static List<ClientModDescriptor> GetLoadedClientModsForVerification(System.Reflection.Assembly coreClientAssembly)
        {
            List<ClientModDescriptor> mods = new List<ClientModDescriptor>();

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
                    string assemblyName = assembly.GetName().Name ?? string.Empty;
                    string displayName = info?.Name ?? melon.MelonTypeName ?? assemblyName;
                    string version = identity?.Version ?? info?.Version ?? assembly.GetName().Version?.ToString() ?? string.Empty;

                    mods.Add(new ClientModDescriptor
                    {
                        ModId = identity?.ModId ?? string.Empty,
                        Version = version,
                        DisplayName = displayName,
                        Author = info?.Author ?? string.Empty,
                        AssemblyName = assemblyName,
                        Sha256 = ClientModHashUtility.TryResolveSha256(melon),
                        IdentityDeclared = identity != null
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
