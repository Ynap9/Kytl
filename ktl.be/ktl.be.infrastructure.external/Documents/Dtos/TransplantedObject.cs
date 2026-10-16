namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public sealed class TransplantedObject
    {
        public int Number { get; init; }

        public string DictText { get; init; } = string.Empty;

        public byte[]? StreamData { get; init; }
    }
}
