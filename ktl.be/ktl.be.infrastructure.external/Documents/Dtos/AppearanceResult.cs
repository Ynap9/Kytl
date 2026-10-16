namespace ktl.be.infrastructure.external.Documents.Dtos
{
    public sealed class AppearanceResult
    {
        public List<TransplantedObject> Objects { get; init; } = new();

        public int FormObjectNumber { get; init; }
    }
}
