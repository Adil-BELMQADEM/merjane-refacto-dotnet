using Refacto.DotNet.Controllers.Common;

namespace Refacto.DotNet.Controllers.Tests.Fakes
{
    public sealed class FakeClock : IClock
    {
        public FakeClock(DateTime now)
        {
            Now = now;
        }

        public DateTime Now { get; }

        public DateTime Today => Now.Date;
    }
}
