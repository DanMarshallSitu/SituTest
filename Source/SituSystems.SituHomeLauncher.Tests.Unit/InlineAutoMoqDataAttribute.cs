using AutoFixture.Xunit2;

namespace SituSystems.SituHomeLauncher.Tests.Unit
{
    public class InlineAutoMoqDataAttribute : InlineAutoDataAttribute
    {
        public InlineAutoMoqDataAttribute(params object[] objects) : base(new AutoMoqDataAttribute(), objects) { }
    }
}