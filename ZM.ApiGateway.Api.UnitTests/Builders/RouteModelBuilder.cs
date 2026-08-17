using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace ZM.ApiGateway.Api.UnitTests.Builders
{
    internal static class RouteModelBuilder
    {
        public static RouteModel WithRouteId(string routeId)
        {
            var config = new RouteConfig
            {
                RouteId = routeId,
                ClusterId = $"{routeId}-cluster",
                Match = new RouteMatch { Path = "/{**catch-all}" }
            };

            return new RouteModel(config, cluster: null, transformer: HttpTransformer.Empty);
        }
    }
}
