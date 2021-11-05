namespace Melanzana.MachO
{
    public abstract class MachBuildVersionBase : MachLoadCommand
    {
        internal static readonly Version EmptyVersion = new Version();

        public abstract MachPlatform Platform { get; }

        public Version MinimumPlatformVersion { get; set; } = EmptyVersion;

        public Version SdkVersion { get; set; } = EmptyVersion;
    }
}