namespace CollectaMundo.DomainLogic.CardLegalities
{
    public readonly record struct CardLegalityMasks(ulong PlayableFormatsMask, ulong RestrictedFormatsMask);
}
