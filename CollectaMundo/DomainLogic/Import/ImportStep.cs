namespace CollectaMundo.DomainLogic.Import
{
    public enum ImportStep
    {
        Start,
        IdColumnMapping,
        NameAndSetMapping,
        MultipleUuidsSelection,
        AdditionalFieldsMapping,
        ConditionMapping,
        FinishMapping,
        LanguageMapping,
        Summary,
        Success
    }

}
