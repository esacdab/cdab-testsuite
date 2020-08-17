using cdabtesttools.Target;

namespace cdabtesttools.Data
{
    public class CrossCatalogueCoverageFiltersDefinition
    {
        private TargetAndFiltersDefinition target;
        private TargetAndFiltersDefinition reference;

        public CrossCatalogueCoverageFiltersDefinition(TargetAndFiltersDefinition target, TargetAndFiltersDefinition reference)
        {
            this.Target = target;
            this.Reference = reference;
        }

        public TargetAndFiltersDefinition Target { get => target; set => target = value; }
        public TargetAndFiltersDefinition Reference { get => reference; set => reference = value; }
    }
}