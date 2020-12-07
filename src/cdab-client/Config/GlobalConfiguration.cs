namespace cdabtesttools.Config
{
    /// <summary>
    /// Data object class for the <em>global</em> node in the configuration YAML file.
    /// </summary>
    public class GlobalConfiguration
    {
        public int QueryTryNumber { get; set; }

        public string CountryShapefilePath { get; set; }

        public bool TestMode { get; set; }

        public GlobalConfiguration()
        {
            QueryTryNumber = 3;
            CountryShapefilePath = "App_Data/TM_WORLD_BORDERS-0.3/TM_WORLD_BORDERS-0.3";
            TestMode = false;
        }
    }
}