using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.DomainLogic.CardLists.Aggregation
{
    public sealed class CardCoreAggregator : ICardCoreAggregator
    {
        public List<CardCore> Aggregate(IEnumerable<CardCore> cores)
        {
            var byUuid = cores.ToDictionary(c => c.Uuid, StringComparer.OrdinalIgnoreCase);

            var primaryCores = cores.Where(c => string.IsNullOrWhiteSpace(c.Side) || c.Side.Equals("a", StringComparison.OrdinalIgnoreCase)).ToList();

            var results = new List<CardCore>(primaryCores.Count);

            foreach (var core in primaryCores)
            {
                var allKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allTexts = new List<string>();

                void MergeFrom(CardCore source)
                {
                    if (!string.IsNullOrWhiteSpace(source.Keywords))
                    {
                        foreach (var kw in source.Keywords.Split(','))
                        {
                            if (!string.IsNullOrWhiteSpace(kw))
                            {
                                allKeywords.Add(kw.Trim());
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(source.Colors))
                    {
                        foreach (var color in source.Colors.Split(','))
                        {
                            if (!string.IsNullOrWhiteSpace(color))
                            {
                                allColors.Add(color.Trim());
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(source.Types))
                    {
                        foreach (var type in source.Types.Split(','))
                        {
                            if (!string.IsNullOrWhiteSpace(type))
                            {
                                allTypes.Add(type.Trim());
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(source.Text))
                    {
                        allTexts.Add(source.Text.Trim());
                    }
                }

                MergeFrom(core);
                foreach (var otherId in core.OtherFaceIds)
                {
                    if (byUuid.TryGetValue(otherId, out var other))
                    {
                        MergeFrom(other);
                    }
                }
                results.Add(new CardCore
                {
                    Uuid = core.Uuid,
                    Name = core.Name,
                    SetCode = core.SetCode,
                    ManaCost = core.ManaCost,
                    ManaCostRaw = core.ManaCostRaw,
                    Type = core.Type,
                    Types = string.Join(", ", allTypes),
                    SuperTypes = core.SuperTypes,
                    SubTypes = core.SubTypes,
                    Side = core.Side,
                    Rarity = core.Rarity,
                    Finishes = core.Finishes,
                    ManaValue = core.ManaValue,
                    Language = core.Language,
                    OtherFaceIds = core.OtherFaceIds,

                    Keywords = string.Join(",", allKeywords),
                    Colors = string.Join(",", allColors),
                    Text = string.Join(" // ", allTexts)
                });

            }

            return results;
        }

    }
}
