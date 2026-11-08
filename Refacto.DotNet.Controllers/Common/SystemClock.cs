namespace Refacto.DotNet.Controllers.Common
{
    public class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
    }
}
