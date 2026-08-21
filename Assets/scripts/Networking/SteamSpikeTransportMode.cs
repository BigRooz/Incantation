using System;

namespace Incantation.Networking
{
    public enum SteamSpikeTransportMode
    {
        TugboatDevelopment,
        SteamSpike
    }

    public static class SteamSpikeTransportArguments
    {
        private const string TransportArgument = "-incantationTransport";

        public static SteamSpikeTransportMode Resolve(SteamSpikeTransportMode fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (!string.Equals(arguments[i], TransportArgument, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return string.Equals(arguments[i + 1], "steam", StringComparison.OrdinalIgnoreCase)
                    ? SteamSpikeTransportMode.SteamSpike
                    : SteamSpikeTransportMode.TugboatDevelopment;
            }

            return fallback;
        }
    }
}
