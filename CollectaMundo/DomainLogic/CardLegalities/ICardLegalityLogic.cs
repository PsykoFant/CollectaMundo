namespace CollectaMundo.DomainLogic.CardLegalities
{
    public interface ICardLegalityLogic
    {
        List<CardLegalityFormat> CreateFormats(IReadOnlyList<string> formatNames);
        ulong[] CreateMasksBySourceColumn(IReadOnlyList<string> sourceFormatNames, IReadOnlyList<CardLegalityFormat> formats);
        CardLegalityMasks CreateMasks(IReadOnlyList<string?> statuses, IReadOnlyList<ulong> masksByColumn);
        CardLegalityFormat CreateFormat(int bitIndex, string value);
    }
}
