# Current Task

## Phase 7B -- IFC Schema Contract & Normalized Identity Mapping

Implemented:

- IFC normalized import now keeps `ElementID` as IFC `GlobalId` and sets `ElementName` from raw IFC `IIfcProduct.Name` / preview name instead of letting Tekla assembly marks occupy the name slot.
- Project persistence no longer stores `DisplayName = ElementName ?? ElementMark`; save/reload preserves IFC Name separately from `ElementMark` and `AssemblyMark`.
- F5 Element Management now shows separate identity rows for `IFC GUID`, `IFC Name`, `IFC Type`, and `Material`, plus canonical rows for `Display Label / Lift Mark` and `Assembly Mark / Assembly Position`.
- `debug_ifc_check_assembly_mark_{timestamp}.csv` begins with the Phase 7B identity fields: `GlobalId`, `ElementID`, `ElementName`, `ElementType`, `RawName`, `RawTag`, `RawAssemblyPos`, `RawCastUnitPos`, `NormalizedAssemblyMark`, `ResolvedDisplayMark`, `CanonicalDisplayLabel`, `AssemblyMarkSource`, `AssemblyMarkConfidence`, `IsExplicitAssemblyMark`, `ValidationStatus`, `ValidationWarnings`; existing grouping/weight/COG/exporter/action columns remain appended.
- Added tests covering canonical resolver identity separation, report row identity separation, project persistence round-trip, existing IFC alias resolution, and GA assembly grouping.
- Documented the field ownership matrix, exporter schema map, normalized project contract, screenshot root cause, minimal mapping fixes, audit columns, and deferred Phase 7D/7E work in `docs/domain/ifc.md`.

Root cause fixed:

- The screenshot state `ID = W5102`, `Name = W5102 - Wall`, `Display Label = W5102`, `Assembly Mark = -` came from F5 using a readable-id helper that preferred marks over GUIDs and from persistence filling missing `ElementName` from `ElementMark`. `ASSEMBLY_POS` is now kept as assembly/display metadata and does not replace GUID or IFC Name.

Verified:

- `dotnet build CraneOptimizer.sln` passes with existing warning noise.
- `dotnet test CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj --no-build --filter "FullyQualifiedName~CanonicalMarkResolverTests|FullyQualifiedName~UserFacingReportsTests|FullyQualifiedName~IfcSessionStabilizationTests|FullyQualifiedName~OptimizationInputBuilderTests"` passes: 95 tests.

Carry-forward:

- Phase 7D: add UI/review workflows for low-confidence or ambiguous assembly mark mappings.
- Phase 7E: plan long-term schema cleanup and field-name migration. Do not delete existing identity fields before migration/compatibility work is designed.
