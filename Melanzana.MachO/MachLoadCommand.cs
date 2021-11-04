namespace Melanzana.MachO
{
    public abstract class MachLoadCommand
    {
        protected MachLoadCommand()
        {
        }

        public abstract MachLoadCommandType GetCommandType(MachObjectFile objectFile);

        public abstract int GetCommandSize(MachObjectFile objectFile);
    }
}