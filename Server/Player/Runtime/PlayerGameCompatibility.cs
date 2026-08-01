using System;
using System.Reflection;
#if IL2CPP
using NetworkConnectionType = Il2CppFishNet.Connection.NetworkConnection;
using PlayerManagerType = Il2CppScheduleOne.PlayerScripts.PlayerManager;
using PlayerType = Il2CppScheduleOne.PlayerScripts.Player;
#else
using NetworkConnectionType = FishNet.Connection.NetworkConnection;
using PlayerManagerType = ScheduleOne.PlayerScripts.PlayerManager;
using PlayerType = ScheduleOne.PlayerScripts.Player;
#endif

namespace DedicatedServerMod.Server.Player.Runtime
{
    internal static class PlayerGameCompatibility
    {
        private const BindingFlags InstanceMemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private const BindingFlags StaticMemberFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly PropertyInfo IntroCompletedProperty =
            typeof(PlayerType).GetProperty("HasCompletedIntro", InstanceMemberFlags)
            ?? typeof(PlayerType).GetProperty("_hasCompletedIntro", InstanceMemberFlags);

        private static readonly FieldInfo IntroCompletedField =
            IntroCompletedProperty == null
                ? typeof(PlayerType).GetField("HasCompletedIntro", InstanceMemberFlags)
                    ?? typeof(PlayerType).GetField("_hasCompletedIntro", InstanceMemberFlags)
                : null;

        private static readonly MethodInfo GetPlayerMethod = ResolveGetPlayerMethod();

        internal static bool GetHasCompletedIntro(PlayerType player)
        {
            if (player == null)
            {
                return false;
            }

            if (IntroCompletedProperty?.GetValue(player) is bool propertyValue)
            {
                return propertyValue;
            }

            if (IntroCompletedField?.GetValue(player) is bool fieldValue)
            {
                return fieldValue;
            }

            throw new MissingMemberException(typeof(PlayerType).FullName, "HasCompletedIntro/_hasCompletedIntro");
        }

        internal static void SetHasCompletedIntro(PlayerType player, bool value)
        {
            if (player == null)
            {
                return;
            }

            if (IntroCompletedProperty?.CanWrite == true)
            {
                IntroCompletedProperty.SetValue(player, value);
                return;
            }

            if (IntroCompletedField != null)
            {
                IntroCompletedField.SetValue(player, value);
                return;
            }

            throw new MissingMemberException(typeof(PlayerType).FullName, "HasCompletedIntro/_hasCompletedIntro");
        }

        internal static PlayerType GetPlayer(NetworkConnectionType connection)
        {
            if (connection == null || GetPlayerMethod == null)
            {
                return null;
            }

            return GetPlayerMethod.Invoke(null, new object[] { connection }) as PlayerType;
        }

        private static MethodInfo ResolveGetPlayerMethod()
        {
            Type[] parameterTypes = { typeof(NetworkConnectionType) };
            return typeof(PlayerManagerType).GetMethod(
                       "GetPlayer",
                       StaticMemberFlags,
                       null,
                       parameterTypes,
                       null)
                   ?? typeof(PlayerType).GetMethod(
                       "GetPlayer",
                       StaticMemberFlags,
                       null,
                       parameterTypes,
                       null);
        }
    }
}
