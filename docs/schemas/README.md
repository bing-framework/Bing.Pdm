# PDM JSON format

`pdm-v1.schema.json` documents the project's exported JSON root. Version 1 keeps
the historical root object layout and adds the integer `SchemaVersion` field.
Missing `SchemaVersion` means historical version 0. The reader upgrades version 0
in memory; `PdmJsonMigrator` writes the upgraded JSON while preserving unknown
fields and date text. Future versions and invalid version values fail explicitly.

The `Version` property is the model's business version, independent of
`SchemaVersion` and the PowerDesigner file version. New optional properties do
not require a schema version increase. A change to an existing field's meaning
or shape does require a new migration step. The schema is documentation, not a
runtime validation dependency.

`PdmObjectAddress` values combine `ModelKey` and `PdmId`. They remain in a single
model's JSON export, but resolving that address after loading requires an
explicit workspace manifest and the corresponding local model file. No external
file is opened implicitly by `PdmJsonReader`.
