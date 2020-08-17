namespace cdabtesttools.Config
{
    public class GlobalConfiguration
    {
        public GlobalConfiguration()
        {
            QueryTryNumber = 3;
            CountryShapefilePath = "App_Data/TM_WORLD_BORDERS-0.3/TM_WORLD_BORDERS-0.3";
            TestMode = false;
        }

        public int QueryTryNumber { get; set; }
        public string CountryShapefilePath { get; set; }

        public bool TestMode { get; set; }
    }
}