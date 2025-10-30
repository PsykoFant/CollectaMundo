namespace CollectaMundo.DomainLogic.Import.Models
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
        Finish
    }

}
