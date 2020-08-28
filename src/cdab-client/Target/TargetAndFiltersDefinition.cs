using cdabtesttools.Data;

namespace cdabtesttools.Target
{
    public class TargetAndFiltersDefinition
    {
        private TargetSiteWrapper target;
        private FiltersDefinition filtersDefinition;

        public TargetSiteWrapper Target => target;

        public FiltersDefinition FiltersDefinition => filtersDefinition;

        public TargetAndFiltersDefinition(TargetSiteWrapper target, FiltersDefinition filtersDefinition)
        {
            this.target = target;
            this.filtersDefinition = filtersDefinition;
        }
    }
}