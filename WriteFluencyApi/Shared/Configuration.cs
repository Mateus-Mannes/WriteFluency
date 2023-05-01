namespace WriteFluencyApi.Shared;

public static class Configuration
{
    public static class ExternalApis  {
        public static class OpenAI  {
            public static string Key { get; set; }
            public static string Url { get; set; }
        }
    }  
}
