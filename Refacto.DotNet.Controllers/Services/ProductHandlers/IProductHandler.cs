using Refacto.DotNet.Controllers.Entities;

namespace Refacto.DotNet.Controllers.Services.ProductHandlers
{
    public interface IProductHandler
    {
        string SupportedType { get; }

        void Handle(Product product);
    }
}
