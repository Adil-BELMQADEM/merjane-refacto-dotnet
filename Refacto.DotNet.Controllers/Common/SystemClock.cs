namespace Refacto.DotNet.Controllers.Common
{
    public sealed class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;

        public DateTime Today => DateTime.Today;
    }
}
