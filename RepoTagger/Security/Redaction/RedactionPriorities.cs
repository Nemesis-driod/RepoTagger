namespace RepoTagger.Security.Redaction
{
    public static class RedactionPriorities
    {
        public const int Critical = 200;

        public const int KnownSecret = 100;

        public const int PersonalData = 50;

        public const int Generic = 10;
    }
}
