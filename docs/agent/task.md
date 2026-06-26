# Current Task

## Phase 7 Tekla IFC Assembly Mark Extraction & Assembly Lift Unit Grouping

Implemented:

- `ASSEMBLY_POS`, `CAST_UNIT_POS`, `ASSEMBLY_POSITION_CODE`, `CAST_UNIT_POSITION_CODE`, `ASSEMBLY_PREFIX`, `CAST_UNIT_PREFIX`, and `CAST_UNIT_SERIAL_NUMBER` are canonical assembly aliases in both Generic and Tekla IFC profiles.
- `CentralIfcPropertyAliasResolver` now resolves Tekla-style uppercase underscore assembly fields through the existing all-property-set traversal, so detection does not depend on a Tekla exporter header.
- `IfcDiagnosticAuditService.GenerateReportsAsync` writes `Reports/IFC_Audit/debug_ifc_check_assembly_mark_{timestamp}.csv` with raw aliases, resolved assembly mark, group key, member IDs, member/group weight, COG columns, validation warnings, and action.
- `OptimizationInputBuilder` creates synthetic GA lift units for repeated explicit high-confidence assembly marks. Original `PrecastElement` members remain in the scene/project state; grouped GA units carry source member IDs in `ChildGlobalIds`.
- F8 result mapping includes synthetic GA input units when mapping result coordinates, so grouped units display their actual load point instead of falling back to zero.
- The sample IFC `F:\06-ITI\00-Project\tryProjects\project1\Models\IFC\ifc4withAssemplymark.ifc` was audited and produced `F:\06-ITI\00-Project\tryProjects\project1\Reports\IFC_Audit\debug_ifc_check_assembly_mark_20260626_174634.csv`.

Sample observations:

- `01jhsjVXzBy81J1pRn_FyV` resolves `ASSEMBLY_POS=W5102`, `CAST_UNIT_POS=W5102`, and weight about `8810 kg` (`8.81 t`).
- The known `W1` GUIDs resolve and group. The raw IFC also contains an additional `W1` product with missing weight; the audit lists it, while the F8 GA gate still blocks missing/untrusted weight before grouping.

Verified:

- `dotnet build CraneOptimizer.sln` passes with existing warning noise.
- `dotnet test CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj --no-build --filter "FullyQualifiedName~OptimizationInputBuilderTests|FullyQualifiedName~TestResolveCanonicalAssemblyIdentity"` passes: 11 tests.
