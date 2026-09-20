namespace RepoTagger.AI.Prompts
{
    public class PromptLoader
    {
        public string Load(string fileName)
        {
            var path=  Path.Combine(AppContext.BaseDirectory, "AI", "Prompts", fileName );
            return File.ReadAllText(path );
        }
    }
}
