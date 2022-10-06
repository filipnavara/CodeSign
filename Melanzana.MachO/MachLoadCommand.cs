namespace Melanzana.MachO
{
    public abstract class MachLoadCommand
    {
        protected MachLoadCommand()
        {
        }

        internal virtual void UpdateLayout(MachObjectFile objectFile)
        {
        }
    }
}