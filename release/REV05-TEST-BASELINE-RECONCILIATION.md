# REV05 test-baseline reconciliation

The REV02 re-review states that 20 test files were deleted, but it identifies only ten of them by filename. REV05 therefore uses two simultaneous acceptance controls rather than pretending the unnamed ten can be reconstructed from the PDF:

1. every deleted file explicitly named by the re-review must exist; and
2. the total static `[Fact]/[Theory]` declaration count must be at least the reviewed pre-deletion baseline of **1,041**, with at least **114** test source files.

## Explicitly named deleted files restored/present

- `IFC/Placement/IfcSessionPlacementTests.cs`
- `IFC/Session/IfcSessionStabilizationTests.cs`
- `IFC/Selection/ViewportSelectionTests.cs`
- `IFC/Selection/ViewportHitTestRefinementTests.cs`
- `IFC/Rendering/RenderPerformanceDiagnosticsTests.cs`
- `IFC/Rendering/RenderPerformanceBenchmark.cs`
- `IFC/Audit/IfcUnitNormalizationTests.cs`
- `IFC/Georeferencing/IfcGeoreferencingExtractorTests.cs`
- `GA/ZoneGeometryTests.cs`
- `ProjectPersistence/GoldenMasterTests.cs`

REV05 also adds acceptance tests for the actual IFC reader mass-unit path, clean-machine IFC workflow, AI endpoint security and catalog lifecycle. `scripts/verify_rev05_acceptance.py` enforces the filename list and declaration/file-count floors so a green result cannot be achieved by deleting failure categories.


## Complete REV05 inventory evidence

`acceptance/test-integrity-manifest.json` hashes **every current test source file** and records its individual `[Fact]/[Theory]` declaration count. The manifest is regenerated and checked by the REV05 gate. This prevents a future submission from silently removing a test file while still satisfying only an aggregate declaration-count check.

The supplied independent review does not disclose the filenames of the other ten files behind its statement that twenty files were removed, and the supplied repository history contains no reviewed REV02 commit from which those names can be recovered. REV05 therefore does not invent identities for evidence that the review does not provide. The reviewer-visible rationale is: all ten named files are restored, every current test file is hash-inventoried, and the full test inventory exceeds both reviewed static floors. If the reviewer supplies the other ten historical filenames, the gate can add them as exact filename requirements without changing this release architecture.
