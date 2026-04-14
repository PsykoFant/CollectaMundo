namespace CollectaMundo.Infrastructure.CardDatabaseManagement.SqlDictionaries
{
    public static class DatabaseIndexSql
    {
        // Central source of truth for all indices
        public static IReadOnlyDictionary<string, string> Statements { get; } = new Dictionary<string, string>
            {
                { "idx_uniquemanasymbols_uniquemanasymbol",
                    "CREATE INDEX IF NOT EXISTS idx_uniquemanasymbols_uniquemanasymbol ON uniqueManaSymbols(uniqueManaSymbol);" },

                { "idx_uniquemanaCostimages_uniquemanaCost",
                    "CREATE INDEX IF NOT EXISTS idx_uniquemanaCostimages_uniquemanaCost ON uniqueManaCostImages(uniqueManaCost);" },

                { "idx_keyruneimages_setcode",
                    "CREATE INDEX IF NOT EXISTS idx_keyruneimages_setcode ON keyruneImages(setCode);" },

                { "idx_cardprices_uuid",
                    "CREATE INDEX IF NOT EXISTS idx_cardprices_uuid ON cardPrices(uuid);" },

                { "idx_cardidentifiers_uuid",
                    "CREATE INDEX IF NOT EXISTS idx_cardidentifiers_uuid ON cardIdentifiers(uuid);" },

                { "idx_cardforeigndata_uuid",
                    "CREATE INDEX IF NOT EXISTS idx_cardforeigndata_uuid ON cardForeignData(uuid);" },

                { "idx_cardlegalities_uuid",
                    "CREATE INDEX IF NOT EXISTS idx_cardlegalities_uuid ON cardLegalities(uuid);" },

                { "idx_cards_uuid",
                    "CREATE INDEX IF NOT EXISTS idx_cards_uuid ON cards(uuid);" },

                { "idx_cards_setcode_name",
                    "CREATE INDEX IF NOT EXISTS idx_cards_setcode_name ON cards(setCode, name);" },

                { "idx_tokens_setcode_name",
                    "CREATE INDEX IF NOT EXISTS idx_tokens_setcode_name ON tokens(setCode, name);" },

                { "idx_cards_keywords",
                    "CREATE INDEX IF NOT EXISTS idx_cards_keywords ON cards(keywords);" },

                { "idx_sets_tokenSetcode",
                    "CREATE INDEX IF NOT EXISTS idx_sets_tokenSetcode ON sets(tokenSetCode);" },

                { "idx_tokenidentifiers_uuid",
                    "CREATE INDEX IF NOT EXISTS idx_tokenidentifiers_uuid ON tokenIdentifiers(uuid);" },

                { "idx_tokens_uuid",
                    "CREATE INDEX IF NOT EXISTS idx_tokens_uuid ON tokens(uuid);" },

                { "idx_tokens_name",
                    "CREATE INDEX IF NOT EXISTS idx_tokens_name ON tokens(name);" },

                { "idx_tokens_facename",
                    "CREATE INDEX IF NOT EXISTS idx_tokens_facename ON tokens(faceName);" },

                { "idx_cards_side_uuid",
                    "CREATE INDEX IF NOT EXISTS idx_cards_side_uuid ON cards(side, uuid);" },

                { "idx_tokens_side_uuid",
                    "CREATE INDEX IF NOT EXISTS idx_tokens_side_uuid ON tokens(side, uuid);" },

                { "idx_sets_code_tokensetcode",
                    "CREATE INDEX IF NOT EXISTS idx_sets_code_tokensetcode ON sets(code, tokenSetCode);" },

                { "idx_cards_setcode_name_type",
                    "CREATE INDEX IF NOT EXISTS idx_cards_setcode_name_type ON cards(setCode, name, type);" },

                { "idx_tokens_setcode_name_type",
                    "CREATE INDEX IF NOT EXISTS idx_tokens_setcode_name_type ON tokens(setCode, name, type);" },

                { "idx_myCollection_uuid",
                    "CREATE INDEX IF NOT EXISTS idx_myCollection_uuid ON myCollection (uuid);" },

                // Identity index (business critical)
                { "ux_myCollection_identity",
                    "CREATE UNIQUE INDEX IF NOT EXISTS ux_myCollection_identity ON myCollection (" +
                    "uuid, language, finish, condition, " +
                    "COALESCE(locationId, -1), " +
                    "COALESCE(comment, '')" +
                    ");" }
            };
    }
}
