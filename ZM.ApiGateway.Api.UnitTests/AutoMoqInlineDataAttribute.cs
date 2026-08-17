using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;

namespace ZM.ApiGateway.Api.UnitTests
{
    public class AutoMoqInlineDataAttribute : InlineAutoDataAttribute
    {
        public AutoMoqInlineDataAttribute(params object?[] values) : base(CreateFixture, values!)
        {

        }

        private static IFixture CreateFixture()
        {
            var fixture = new Fixture();

            fixture.Customize(new AutoMoqCustomization
            {
                ConfigureMembers = true,
                GenerateDelegates = true
            });
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
                .ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            fixture.Register(() => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            fixture.Register(() => TimeSpan.FromMinutes(1));

            return fixture;
        }
    }
}
