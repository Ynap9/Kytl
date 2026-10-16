namespace ktl.be.infrastructure.external.Documents.Interfaces
{
    public interface IWordTemplateEngine
    {
        List<string> GetFieldNames(byte[] template);

        byte[] Fill(byte[] template, IReadOnlyDictionary<string, string> values);
    }
}
