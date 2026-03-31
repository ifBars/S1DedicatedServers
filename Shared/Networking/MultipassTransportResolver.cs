using System.Text;
#if IL2CPP
using Il2CppFishNet.Transporting;
using Il2CppFishNet.Transporting.Multipass;
#else
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
#endif

namespace DedicatedServerMod.Shared.Networking
{
    /// <summary>
    /// Resolves the Multipass transport component from the active FishNet transport graph.
    /// </summary>
    internal static class MultipassTransportResolver
    {
        internal static bool TryResolve(Transport transport, out Multipass multipass)
        {
            multipass = transport as Multipass;
            if (multipass != null)
            {
                return true;
            }

            if (transport?.gameObject == null)
            {
                return false;
            }

            multipass = transport.gameObject.GetComponent<Multipass>();
            return multipass != null;
        }

        internal static string Describe(Transport transport)
        {
            if (transport == null)
            {
                return "Active transport: <null>";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("Active transport: ").Append(transport.GetType().FullName ?? transport.GetType().Name);

            if (transport.gameObject == null)
            {
                builder.Append(" | GameObject: <null>");
                return builder.ToString();
            }

            builder.Append(" | GameObject: ").Append(transport.gameObject.name);

            Transport[] attachedTransports = transport.gameObject.GetComponents<Transport>();
            if (attachedTransports.Length == 0)
            {
                builder.Append(" | Attached transports: <none>");
                return builder.ToString();
            }

            builder.Append(" | Attached transports: ");
            for (int i = 0; i < attachedTransports.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                Transport attachedTransport = attachedTransports[i];
                builder.Append(attachedTransport?.GetType().Name ?? "<null>");
            }

            return builder.ToString();
        }
    }
}
