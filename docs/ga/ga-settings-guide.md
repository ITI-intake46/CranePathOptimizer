# Genetic Algorithm Calibration & Settings Guide

This consolidated guide details parameters, calibrations, and settings of the Genetic Algorithm.

## Consolidated from: GASettings Technical Audit Report (ga/ga-settings-guide.md)


This report provides a detailed property-by-property trace, classification, and architectural review of the `GASettings` configuration class and related parameters.

---

## 1. Property-by-Property Audit & Trace

Below is the trace for every property defined in [GASettings.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs) across the Application, Domain, and UI layers.

### 1. `PopulationSize`
*   **Classification:** Actively Used
*   **Where Read:** [GAEngine.cs:L65](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAEngine.cs#L65), [CraneOptimizerDialogWorkflow.cs:L95](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L95)
*   **Behavioral Impact:** Determines the number of candidate solutions in each generation. Directly affects **solution quality** (higher size maintains diversity and improves coverage) and **execution speed** (directly proportional to population size).
*   **Recommendation:** Keep. Configurable in UI.

### 2. `MaxGenerations`
*   **Classification:** Actively Used
*   **Where Read:** [GAEngine.cs:L73](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAEngine.cs#L73), [CraneOptimizerDialogWorkflow.cs:L96](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L96)
*   **Behavioral Impact:** Sets the absolute generation loop limit. Affects **solution quality** (gives the algorithm enough cycles to find near-optimal placements) and **execution speed** (acts as the maximum runtime cap).
*   **Recommendation:** Keep. Configurable in UI.

### 3. `MutationRate`
*   **Classification:** Actively Used
*   **Where Read:** [GAEngine.cs:L95](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAEngine.cs#L95), [CraneOptimizerDialogWorkflow.cs:L90](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L90)
*   **Behavioral Impact:** Probability of selecting an individual for mutation. Affects **solution quality** and **stability** (prevents local minima traps but causes instability/stagnation if too high).
*   **Recommendation:** Keep. Configurable in UI (Advanced/Dev).

### 4. `MutationSigma`
*   **Classification:** Actively Used
*   **Where Read:** [MutationOperator.cs:L115](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/MutationOperator.cs#L115), [CraneOptimizerDialogWorkflow.cs:L91](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L91)
*   **Behavioral Impact:** Governs the standard deviation of Gaussian coordinate perturbation. Affects **solution quality** (defines the step sizes of position mutations).
*   **Recommendation:** Keep. Move to Advanced Settings.

### 5. `CrossoverRate`
*   **Classification:** Actively Used
*   **Where Read:** [GAEngine.cs:L89](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAEngine.cs#L89), [CraneOptimizerDialogWorkflow.cs:L92](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L92)
*   **Behavioral Impact:** Probability that two parents exchange coordinate setups. Affects **convergence speed** and **solution quality**.
*   **Recommendation:** Keep. Move to Advanced Settings.

### 6. `WireLength`
*   **Classification:** Actively Used
*   **Where Read:** [CraneConfigurationEngine.cs:L58](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/CraneConfiguration/CraneConfigurationEngine.cs#L58), [ElementAssignmentService.cs:L549](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs#L549), [CraneOptimizerDialogWorkflow.cs:L68](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L68)
*   **Behavioral Impact:** Added to hook heights in physical boom length checks. Affects **engineering calculations** (height clearances).
*   **Recommendation:** Keep. Configurable in UI.

### 7. `AdditionalWt`
*   **Classification:** Actively Used
*   **Where Read:** [ElementAssignmentService.cs:L533](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs#L533), [CraneOptimizerDialogWorkflow.cs:L69](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L69)
*   **Behavioral Impact:** Added to element weights to account for rigging weights. Affects **engineering calculations** (load capacity checks).
*   **Recommendation:** Keep. Configurable in UI.

### 8. `DeratingRatio`
*   **Classification:** Actively Used
*   **Where Read:** [CraneConfigurationEngine.cs:L83](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/CraneConfiguration/CraneConfigurationEngine.cs#L83), [OptimizationInputBuilder.cs:L87](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/OptimizationScope/OptimizationInputBuilder.cs#L87), [CraneOptimizerDialogWorkflow.cs:L70](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L70)
*   **Behavioral Impact:** Reduces standard load-chart capacity. Affects **engineering calculations** and **stability**.
*   **Recommendation:** Keep. Configurable in UI.

### 9. `SafetyFactor`
*   **Classification:** Actively Used
*   **Where Read:** [CraneConfigurationEngine.cs:L84](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/CraneConfiguration/CraneConfigurationEngine.cs#L84), [CraneOptimizerDialogWorkflow.cs:L71](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L71)
*   **Behavioral Impact:** Extra user-defined capacity scaling multiplier. Affects **engineering calculations** and **solution feasibility**.
*   **Recommendation:** Keep. Configurable in UI.

### 10. `RiggingLength`
*   **Classification:** Legacy / Unused
*   **Where Read:** [GASettings.cs:L63](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs#L63) (Obsolete property)
*   **Behavioral Impact:** Acts as an alias forwarding getters and setters to `WireLength`. Does not affect any engines.
*   **Recommendation:** Remove. (Clean up the obsolete property).

### 11. `MaxCranePositions`
*   **Classification:** Actively Used
*   **Where Read:** [ConstraintEngine.cs:L162](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/Constraints/ConstraintEngine.cs#L162), [FitnessEvaluator.cs:L164](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L164), [GAEngine.cs:L66](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAEngine.cs#L66), [CraneOptimizerDialogWorkflow.cs:L72](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L72)
*   **Behavioral Impact:** Sets the chromosome capacity limit. Affects **solution quality** (defines relocation setups allowed) and **performance** (shorter chromosomes execute faster).
*   **Recommendation:** Keep. Configurable in UI.

### 12. `RandomSeed`
*   **Classification:** Actively Used
*   **Where Read:** [GAEngine.cs:L59](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAEngine.cs#L59), [CraneOptimizerDialogWorkflow.cs:L93](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L93)
*   **Behavioral Impact:** Initializes random number generator. Affects **stability** and **repeatability** of optimization outputs.
*   **Recommendation:** Keep. Move to Advanced Settings.

### 13. `CoverageWeight`
*   **Classification:** Actively Used
*   **Where Read:** [FitnessEvaluator.cs:L255](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L255), [CraneOptimizerDialogWorkflow.cs:L66](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L66)
*   **Behavioral Impact:** Controls coverage priority in fitness scoring. Affects **solution quality** (tunes setup layout towards maximum coverage).
*   **Recommendation:** Keep. Move to Advanced Settings.

### 13b. `CoverageDominanceMultiplier`
*   **Classification:** Actively Used
*   **Where Read:** [FitnessEvaluator.cs:L255](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L255), [GACsvDiagnosticsService.cs:L754](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/GACsvDiagnosticsService.cs#L754)
*   **Behavioral Impact:** Scaling factor that multiplies `CoverageWeight` to ensure coverage ratio dominance over other penalization metrics.
*   **Recommendation:** Keep.


### 14. `RelocationWeight`
*   **Classification:** Actively Used
*   **Where Read:** [FitnessEvaluator.cs:L161](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L161), [CraneOptimizerDialogWorkflow.cs:L67](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L67)
*   **Behavioral Impact:** Penalizes extra relocation setups. Affects **solution quality** (forces the engine to reduce setup counts).
*   **Recommendation:** Keep. Move to Advanced Settings.

### 15. `RadiusPenaltyWeight`
*   **Classification:** Actively Used
*   **Where Read:** [FitnessEvaluator.cs:L257](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L257), [CraneOptimizerDialogWorkflow.cs:L75](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L75)
*   **Behavioral Impact:** Penalizes elements out of reach. Affects **solution feasibility**.
*   **Recommendation:** Keep. Move to Advanced Settings.

### 16. `RadiusQualityPenaltyWeight`
*   **Classification:** Actively Used
*   **Where Read:** [FitnessEvaluator.cs:L261](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L261), [CraneOptimizerDialogWorkflow.cs:L78](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L78)
*   **Behavioral Impact:** Penalizes long operating radii. Affects **engineering stability** (encourages placements closer to element centers).
*   **Recommendation:** Keep. Move to Advanced Settings.

### 17. `OverweightPenaltyWeight`
*   **Classification:** Actively Used
*   **Where Read:** [FitnessEvaluator.cs:L258](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L258), [CraneOptimizerDialogWorkflow.cs:L80](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L80)
*   **Behavioral Impact:** Penalizes load chart overloads. Enforces **engineering safety**.
*   **Recommendation:** Keep. Move to Advanced Settings.

### 18. `HeightPenaltyWeight`
*   **Classification:** Actively Used
*   **Where Read:** [FitnessEvaluator.cs:L259](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L259), [CraneOptimizerDialogWorkflow.cs:L81](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L81)
*   **Behavioral Impact:** Penalizes clearance height failures. Enforces **engineering calculations**.
*   **Recommendation:** Keep. Move to Advanced Settings.

### 19. `MaxRadiusForNormalisation`
*   **Classification:** Actively Used
*   **Where Read:** [ConstraintEngine.cs:L161](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/Constraints/ConstraintEngine.cs#L161), [FitnessEvaluator.cs:L181](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L181), [FitnessEvaluator.cs:L184](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L184), [FitnessEvaluator.cs:L235](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L235), [FitnessEvaluator.cs:L236](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L236), [CraneOptimizerDialogWorkflow.cs:L89](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L89)
*   **Behavioral Impact:** Converts operating radius meters into normalized [0, 1] range values. Affects **stability** and weights normalization.
*   **Recommendation:** Keep. Hide from UI (auto-calculate based on crane specification limits).

### 20. `GridStep`
*   **Classification:** Actively Used
*   **Where Read:** [GAInitializer.cs:L289](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAInitializer.cs#L289), [MutationOperator.cs:L65](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/MutationOperator.cs#L65), [CraneOptimizerDialogWorkflow.cs:L74](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L74)
*   **Behavioral Impact:** Snaps coordinate mutations and seeds to a regular spacing grid (e.g., 0.5m). Affects **solution layout quality** and discrete placement capabilities.
*   **Recommendation:** Keep. Configurable in UI.

### 21. `EarlyStoppingGenerations`
*   **Classification:** Actively Used
*   **Where Read:** [GAEngine.cs:L109](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAEngine.cs#L109), [CraneOptimizerDialogWorkflow.cs:L73](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L73)
*   **Behavioral Impact:** Number of generations of stagnation before stopping. Affects **execution speed** and convergence.
*   **Recommendation:** Keep. Move to Advanced Settings.

### 22. `ConvergenceThresholdPercent`
*   **Classification:** Actively Used
*   **Where Read:** [GAEngine.cs:L118](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAEngine.cs#L118), [CraneOptimizerDialogWorkflow.cs:L94](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L94)
*   **Behavioral Impact:** Terminate GA run if relative fitness progress stays below this percent threshold. Affects **execution speed**.
*   **Recommendation:** Keep. Move to Advanced Settings.

### 23. `RadiusStdDevPenaltyWeight`
*   **Classification:** Actively Used
*   **Where Read:** [FitnessEvaluator.cs:L237](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L237), [CraneOptimizerDialogWorkflow.cs:L82](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L82)
*   **Behavioral Impact:** Penalizes high variance in lifting operating radii. Affects **solution uniformity**.
*   **Recommendation:** Keep. Move to Advanced Settings.

### 24. `BalancePenaltyWeight`
*   **Classification:** Actively Used
*   **Where Read:** [FitnessEvaluator.cs:L215](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L215), [CraneOptimizerDialogWorkflow.cs:L79](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L79)
*   **Behavioral Impact:** Penalizes uneven partitioning of elements among setups. Affects **logistics balancing**.
*   **Recommendation:** Keep. Move to Advanced Settings.

### 25. `MinimumRelocationDistance`
*   **Classification:** Actively Used
*   **Where Read:** [ConstraintEngine.cs:L82](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/Constraints/ConstraintEngine.cs#L82), [CoverageEngine.cs:L63](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/Coverage/CoverageEngine.cs#L63), [CraneOptimizerDialogWorkflow.cs:L85](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L85)
*   **Behavioral Impact:** Spacing limit to count crane setups as separate. Affects **solution validity** and layouts.
*   **Recommendation:** Keep. Configurable in UI.

### 26. `DuplicatePositionDistance`
*   **Classification:** Actively Used
*   **Where Read:** [CoverageEngine.cs:L62](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/Coverage/CoverageEngine.cs#L62), [CraneOptimizerDialogWorkflow.cs:L86](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L86)
*   **Behavioral Impact:** Distance margin for merging coordinates before generating reports. Affects **diagnostics** and results cleanup.
*   **Recommendation:** Keep. Hide from UI or Advanced Settings.

### 27. `SimilarPositionPenaltyWeight`
*   **Classification:** Actively Used
*   **Where Read:** [FitnessEvaluator.cs:L264](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L264), [CraneOptimizerDialogWorkflow.cs:L83](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L83)
*   **Behavioral Impact:** Penalizes placements that are too close to be operationally distinct. Affects **solution diversity**.
*   **Recommendation:** Keep. Move to Advanced Settings.

### 28. `RelocationDistancePenaltyWeight`
*   **Classification:** Actively Used
*   **Where Read:** [FitnessEvaluator.cs:L265](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L265), [CraneOptimizerDialogWorkflow.cs:L84](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L84)
*   **Behavioral Impact:** Penalizes total travel distance between sequential setup locations. Affects **relocation path quality**.
*   **Recommendation:** Keep. Move to Advanced Settings.

### 29. `MutationExplorationMultiplier`
*   **Classification:** Actively Used
*   **Where Read:** [MutationOperator.cs:L122](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/MutationOperator.cs#L122), [CraneOptimizerDialogWorkflow.cs:L87](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L87)
*   **Behavioral Impact:** Multiplies Gaussian mutations to expand exploration bounds. Affects **solution quality** (diversity).
*   **Recommendation:** Keep. Move to Advanced Settings.

### 30. `EnableAdaptiveSigma`
*   **Classification:** Partially Used (Defaults to false, not mapped in UI)
*   **Where Read:** [MutationOperator.cs:L125](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/MutationOperator.cs#L125)
*   **Behavioral Impact:** Toggles adaptive generation cooling for mutation sigma. Affects **convergence convergence quality**.
*   **Recommendation:** Keep. Move to Advanced Settings / Document only.

### 31. `SigmaCoolingExponent`
*   **Classification:** Partially Used (Defaults to 1.5, not mapped in UI)
*   **Where Read:** [MutationOperator.cs:L130](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/MutationOperator.cs#L130)
*   **Behavioral Impact:** Decay curve exponent for adaptive sigma. Affects **convergence**.
*   **Recommendation:** Keep. Document only.

### 32. `MinimumMutationSigma`
*   **Classification:** Partially Used (Defaults to 0.5, not mapped in UI)
*   **Where Read:** [MutationOperator.cs:L131](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/MutationOperator.cs#L131)
*   **Behavioral Impact:** Decay floor in meters to prevent zero mutation. Affects **stability**.
*   **Recommendation:** Keep. Document only.

### 33. `PositionSamplingAttempts`
*   **Classification:** Actively Used
*   **Where Read:** [GAInitializer.cs:L385](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAInitializer.cs#L385), [MutationOperator.cs:L58](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/MutationOperator.cs#L58), [CraneOptimizerDialogWorkflow.cs:L88](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L88)
*   **Behavioral Impact:** Rejection-sampling limit when generating setup coordinates. Affects **execution speed** and **stability** in constrained sites.
*   **Recommendation:** Keep. Move to Advanced Settings.

### 34. `CraneFootprintRadius`
*   **Classification:** Actively Used
*   **Where Read:** [ConstraintEngine.cs:L51](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/Constraints/ConstraintEngine.cs#L51), [CrossoverOperator.cs:L35](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/CrossoverOperator.cs#L35)
*   **Behavioral Impact:** Spacing limits for allowed/forbidden zones and crossover swaps. Enforces **physical constraints**.
*   **Recommendation:** Keep. Hide from UI (auto-computed by OptimizationInputBuilder).

### 35. `TargetElementTypes`
*   **Classification:** Actively Used
*   **Where Read:** [OptimizationInputBuilder.cs:L37](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/OptimizationScope/OptimizationInputBuilder.cs#L37), [CraneOptimizerDialogWorkflow.cs:L97](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L97)
*   **Behavioral Impact:** Allow-list mapping elements by IFC types. Affects **scope filtering**.
*   **Recommendation:** Keep. Configurable in UI.

### 36. `ProfilingLogDirectory`
*   **Classification:** Actively Used
*   **Where Read:** [CranePlacementEngine.cs:L252](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/PlacementOrchestration/CranePlacementEngine.cs#L252)
*   **Behavioral Impact:** File path output folder for log files. Affects **diagnostics**.
*   **Recommendation:** Keep. Hide from UI.

### 37. `EnableProfiling`
*   **Classification:** Actively Used
*   **Where Read:** [CranePlacementEngine.cs:L43](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/PlacementOrchestration/CranePlacementEngine.cs#L43)
*   **Behavioral Impact:** Toggles high-resolution diagnostic metrics capture. Affects **diagnostics**.
*   **Recommendation:** Keep. Hide from UI.

### 38. `SlewOffsetX`
*   **Classification:** Partially Used (Defaults to 0.0, not mapped in UI)
*   **Where Read:** [ElementAssignmentService.cs:L475](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs#L475), [ElementAssignmentService.cs:L513](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs#L513)
*   **Behavioral Impact:** Offset applied to crane positions to locate the rotation center. Affects **engineering calculations** (reach radius).
*   **Recommendation:** Keep. Hide from UI (should ideally live in CraneSpecification).

### 39. `SlewOffsetY`
*   **Classification:** Partially Used (Defaults to 0.0, not mapped in UI)
*   **Where Read:** [ElementAssignmentService.cs:L476](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs#L476), [ElementAssignmentService.cs:L514](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs#L514)
*   **Behavioral Impact:** Offset applied to crane positions to locate the rotation center. Affects **engineering calculations** (reach radius).
*   **Recommendation:** Keep. Hide from UI.

### 40. `SlewOffsetZ`
*   **Classification:** Partially Used (Defaults to 0.0, not mapped in UI)
*   **Where Read:** [ElementAssignmentService.cs:L477](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs#L477), [ElementAssignmentService.cs:L583](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs#L583)
*   **Behavioral Impact:** Offset applied to crane positions to locate the rotation center. Affects **engineering calculations** (reach height).
*   **Recommendation:** Keep. Hide from UI.

### 41. `BoomHingeOffsetX`
*   **Classification:** Partially Used (Defaults to 0.0, not mapped in UI)
*   **Where Read:** [CraneConfigurationEngine.cs:L49](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/CraneConfiguration/CraneConfigurationEngine.cs#L49), [ElementAssignmentService.cs:L478](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs#L478), [ElementAssignmentService.cs:L541](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs#L541)
*   **Behavioral Impact:** Offset applied to crane positions to locate the boom hinge. Affects **engineering calculations** (boom lengths).
*   **Recommendation:** Keep. Hide from UI (should ideally live in CraneSpecification).

### 42. `BoomHingeOffsetY`
*   **Classification:** Partially Used (Defaults to 0.0, not mapped in UI)
*   **Where Read:** [CraneConfigurationEngine.cs:L50](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/CraneConfiguration/CraneConfigurationEngine.cs#L50), [ElementAssignmentService.cs:L479](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs#L479), [ElementAssignmentService.cs:L542](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs#L542)
*   **Behavioral Impact:** Offset applied to crane positions to locate the boom hinge. Affects **engineering calculations** (boom lengths).
*   **Recommendation:** Keep. Hide from UI.

### 43. `BoomHingeOffsetZ`
*   **Classification:** Partially Used (Defaults to 0.0, not mapped in UI)
*   **Where Read:** [CraneConfigurationEngine.cs:L51](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/CraneConfiguration/CraneConfigurationEngine.cs#L51), [ElementAssignmentService.cs:L480](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs#L480), [ElementAssignmentService.cs:L543](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs#L543)
*   **Behavioral Impact:** Offset applied to crane positions to locate the boom hinge. Affects **engineering calculations** (boom lengths).
*   **Recommendation:** Keep. Hide from UI.

### 44. `EnableConcurrentOptimization`
*   **Classification:** Actively Used (Defaults to true, not mapped in UI)
*   **Where Read:** [CranePlacementEngine.cs:L80](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/PlacementOrchestration/CranePlacementEngine.cs#L80)
*   **Behavioral Impact:** Toggles concurrent GA mode vs greedy set-cover iterations. Affects **solution quality** (concurrency yields better globally coordinated layouts) and **performance** (iterative is simpler).
*   **Recommendation:** Keep. Move to Advanced Settings.

---

## 2. Categorization & Functional Grouping

### Settings Used by Every Run
These are mandatory core inputs that influence candidate evolution and mechanical clearance math:
*   `PopulationSize`
*   `MaxGenerations`
*   `WireLength`
*   `AdditionalWt`
*   `DeratingRatio`
*   `SafetyFactor`
*   `MaxCranePositions`
*   `CrossoverRate`
*   `MutationRate`
*   `MutationSigma`
*   `CraneFootprintRadius`
*   `MinimumRelocationDistance`
*   `PositionSamplingAttempts`
*   `RandomSeed`

### Settings Affecting Only Diagnostics / Debugging
*   `ProfilingLogDirectory`
*   `EnableProfiling`
*   `DuplicatePositionDistance`

### Settings Affecting GA Quality (Fitness Weights & Operators)
These parameters govern selection pressure, exploration/exploitation ratios, and penalty scaling:
*   `CoverageWeight`
*   `RelocationWeight`
*   `RadiusPenaltyWeight`
*   `RadiusQualityPenaltyWeight`
*   `OverweightPenaltyWeight`
*   `HeightPenaltyWeight`
*   `RadiusStdDevPenaltyWeight`
*   `BalancePenaltyWeight`
*   `SimilarPositionPenaltyWeight`
*   `RelocationDistancePenaltyWeight`
*   `MutationExplorationMultiplier`
*   `EnableConcurrentOptimization`

### Settings Affecting Performance Only
*   `EarlyStoppingGenerations`
*   `ConvergenceThresholdPercent`

### Unused Settings & Removal Recommendations
*   `RiggingLength` (obsolete alias for `WireLength`).
    *   **Recommendation:** Remove the property to reduce code noise.

### Duplicate / Overlapping Settings
*   `DuplicatePositionDistance` (default `3.0`m) and `MinimumRelocationDistance` (default `10.0`m).
    *   *Overlap:* Both define spatial separation between crane setup locations. `DuplicatePositionDistance` is used in `CoverageEngine` to merge redundant setups at the end of the run; `MinimumRelocationDistance` is used during constraints checking and mutations to enforce setup spacing.
    *   *Recommendation:* Keep both but document that `DuplicatePositionDistance` must always be smaller than `MinimumRelocationDistance` (otherwise final setups will be merged down to less than the required operational spacing).


---

## Consolidated from: GASettings Developer & Calibration Guide (ga/ga-settings-guide.md)


This guide provides comprehensive developer and maintainer documentation for all active properties of `GASettings` within the crane layout optimization engine. It outlines parameter tuning, classifications, scaling behaviors, and calibration workflows for repeated runs.

---

## 1. Classifications of Settings

To assist developers in navigation and UI layout design, we classify the settings into four functional categories:

```
GASettings
├── High-Impact Settings (Core inputs modified by users on every run)
├── Advanced Settings (Tuning parameters for GA convergence and penalties)
├── Experimental Settings (Non-default cooling schedules and concurrent switches)
└── Diagnostic Settings (Profiling and report file directory targets)
```

### A. High-Impact Settings
These properties represent physical or structural site limits. They must be easily accessible to end-users on every optimization run:
*   `MaxCranePositions`
*   `WireLength`
*   `AdditionalWt`
*   `DeratingRatio`
*   `SafetyFactor`
*   `GridStep`
*   `MinimumRelocationDistance`
*   `TargetElementTypes`

### B. Advanced Settings
Tuning parameters for the GA evolutionary operator behaviors and fitness function weights:
*   `PopulationSize`
*   `MaxGenerations`
*   `EarlyStoppingGenerations`
*   `RandomSeed`
*   `CoverageWeight`
*   `RelocationWeight`
*   `RadiusPenaltyWeight`
*   `OverweightPenaltyWeight`
*   `HeightPenaltyWeight`
*   `RadiusStdDevPenaltyWeight`
*   `BalancePenaltyWeight`
*   `SimilarPositionPenaltyWeight`
*   `RelocationDistancePenaltyWeight`
*   `PositionSamplingAttempts`

### C. Experimental Settings
Settings governing adaptive scaling models and alternative optimization execution branches:
*   `EnableAdaptiveSigma`
*   `SigmaCoolingExponent`
*   `MinimumMutationSigma`
*   `MutationSigma` (when combined with cooling)
*   `MutationExplorationMultiplier`
*   `CrossoverRate`
*   `MutationRate`
*   `ConvergenceThresholdPercent`
*   `EnableConcurrentOptimization`
*   `EnableReachabilitySeeding`
*   `HotSpotSeedingRatio`

### D. Diagnostic Settings
Options managing performance profilers and trace folder targets:
*   `EnableProfiling`
*   `ProfilingLogDirectory`
*   `DuplicatePositionDistance`
*   `MaxRadiusForNormalisation` (used to normalize distance ranges)

---

## 2. Active Parameter Reference Sheets

For every actively used setting, we detail its purpose, default, range, impacts, and risks.

### `PopulationSize`
*   **Purpose:** The number of candidate layouts evolved per generation.
*   **Default:** `100` | **Allowed Range:** `10 - 1000`
*   **Engineering Impact:** None directly on physical metrics.
*   **Optimization Impact:** Higher populations sustain spatial diversity and avoid getting trapped in local minima caused by irregular forbidden zones.
*   **Performance Impact:** Linear scaling $O(P)$. Doubling the size doubles the execution time of each generation.
*   **Risk of Incorrect Values:** Too small ($<30$) leads to loss of diversity and premature convergence. Too large ($>500$) causes performance lag (longer than 5s runs).
*   **Tuning Recommendation:** Use `100` for standard runs. Increase to `250` only for large projects ($E > 500$ elements).

### `MaxGenerations`
*   **Purpose:** The maximum number of evolutionary loops allowed.
*   **Default:** `200` | **Allowed Range:** `50 - 2000`
*   **Engineering Impact:** None.
*   **Optimization Impact:** Sets the cutoff for search depth.
*   **Performance Impact:** Linear scaling $O(G)$.
*   **Risk of Incorrect Values:** Too small ($<50$) cuts off the search before convergence. Too high ($>1000$) causes unnecessary CPU load.
*   **Tuning Recommendation:** Keep at `200`. Rely on early stopping for termination.

### `WireLength`
*   **Purpose:** Vertical length offset for hook slings.
*   **Default:** `2.0`m | **Allowed Range:** `0.0 - 20.0`
*   **Engineering Impact:** Increases the required hook elevation clearance: $Z_{\text{hook}} = Z_{\text{COG}} + H_{\text{half}} + \text{WireLength}$.
*   **Optimization Impact:** Direct impact on solution feasibility. Higher values make tall buildings unreachable for low-hook cranes.
*   **Performance Impact:** None.
*   **Risk of Incorrect Values:** Under-estimation leads to crane boom collisions.
*   **Tuning Recommendation:** Set matching the physical rigging equipment used on site.

### `AdditionalWt`
*   **Purpose:** Weight offset to account for hook block and sling assemblies.
*   **Default:** `0.0` tons | **Allowed Range:** `0.0 - 10.0`
*   **Engineering Impact:** Increases required lifting capacity: $W_{\text{req}} = \text{ElementWT} + \text{AdditionalWt}$.
*   **Optimization Impact:** Directly affects load capacity checks.
*   **Performance Impact:** None.
*   **Risk of Incorrect Values:** Under-estimation leads to crane overloads.
*   **Tuning Recommendation:** Standard default is `0.0` but should match rigging weights.

### `DeratingRatio`
*   **Purpose:** Scale down manufacturer capacity charts for safety margins.
*   **Default:** `0.9` (90% capacity) | **Allowed Range:** `0.5 - 1.0`
*   **Engineering Impact:** Multiplies chart capacity: $\text{Cap}_{\text{effective}} = \text{Cap}_{\text{chart}} \times \text{DeratingRatio} \times \text{SafetyFactor}$.
*   **Optimization Impact:** Directly restricts crane reaches for heavy lifts.
*   **Performance Impact:** None.
*   **Risk of Incorrect Values:** Setting too close to 1.0 reduces safety margins.
*   **Tuning Recommendation:** Standard structural default is `0.9`.

### `SafetyFactor`
*   **Purpose:** Operational safety limit applied after derating.
*   **Default:** `0.95` | **Allowed Range:** `0.5 - 1.0`
*   **Engineering Impact:** Direct multiplier on allowable lifting capacity.
*   **Optimization Impact:** Restricts crane placements.
*   **Performance Impact:** None.
*   **Risk of Incorrect Values:** Too low over-constrains placements.
*   **Tuning Recommendation:** Standard default is `0.95`.

### `MaxCranePositions`
*   **Purpose:** Hard limit on the maximum number of relocation setups per solution.
*   **Default:** `3` | **Allowed Range:** `1 - 10`
*   **Engineering Impact:** Defines maximum mobilization effort.
*   **Optimization Impact:** Directly affects coverage potential.
*   **Performance Impact:** High. Chromosome size scales array sizes in assignment checks.
*   **Risk of Incorrect Values:** Setting too small prevents reaching 100% coverage on large sites.
*   **Tuning Recommendation:** Match layout mobilization targets.

### `EnableReachabilitySeeding`
*   **Purpose:** Toggles pre-computation of reachability hot spots before starting GA.
*   **Default:** `true` | **Allowed Range:** `true` or `false`
*   **Engineering Impact:** None directly.
*   **Optimization Impact:** Prunes search space by seeding initial GA population near coordinates with high coverage.
*   **Performance Impact:** Lightweight pre-computation step (<3s).
*   **Risk of Incorrect Values:** Disabling may cause GA to waste generations on unreachable areas on highly constrained sites.

### `HotSpotSeedingRatio`
*   **Purpose:** Fraction of GA initial population seeded from reachability hot spots.
*   **Default:** `0.30` | **Allowed Range:** `0.0` (disabled) to `1.0` (all)
*   **Engineering Impact:** None.
*   **Optimization Impact:** Balances early exploit pressure (hot spots) with explore diversity (random placements).
*   **Performance Impact:** None.
*   **Risk of Incorrect Values:** Setting to `1.0` reduces genetic diversity, risking local entrapment. Setting to `0.0` disables reachability seeding.

---

## 3. Practical Tuning Profiles

These preset profiles represent balanced configurations for typical project scales:

### A. Small Project Profile (e.g., $<50$ elements, 1-2 setups)
*   `PopulationSize`: `50`
*   `MaxGenerations`: `100`
*   `MaxCranePositions`: `2`
*   `EarlyStoppingGenerations`: `15`
*   `MinimumRelocationDistance`: `8.0`m
*   `PositionSamplingAttempts`: `40`

### B. Medium Project Profile (e.g., $50 - 300$ elements, 2-3 setups)
*   `PopulationSize`: `100`
*   `MaxGenerations`: `200`
*   `MaxCranePositions`: `3`
*   `EarlyStoppingGenerations`: `20`
*   `MinimumRelocationDistance`: `10.0`m
*   `PositionSamplingAttempts`: `80`

### C. Large Project Profile (e.g., $>300$ elements, $3+$ setups)
*   `PopulationSize`: `200`
*   `MaxGenerations`: `400`
*   `MaxCranePositions`: `5`
*   `EarlyStoppingGenerations`: `35`
*   `MinimumRelocationDistance`: `12.0`m
*   `PositionSamplingAttempts`: `120`

---

## 4. Calibration Workflows for Repeated Experiments

When running consecutive optimization experiments to find optimal placements, follow this calibration workflow:

```
[Import IFC & Setup Site]
           │
           ▼
[Step 1: Establish Feasible Baseline]
  - Disable relocation penalties (RelocationWeight = 0).
  - Run GA to locate positions where reach/capacity are 100% satisfied.
           │
           ▼
[Step 2: Calibrate Relocation Density]
  - Increment RelocationWeight (e.g., 2.0 -> 5.0).
  - Run GA to prune setups down to the minimum operational setups needed.
           │
           ▼
[Step 3: Fine-Tune Proximity & Paths]
  - Adjust SimilarPositionPenaltyWeight & MinimumRelocationDistance.
  - Snapshot runs to ga_run_history.csv to compare elapsed time vs coverage.
```

### Diagnostic Validation
Always review the exported diagnostics files located in the project's output reports directory:
*   Verify the cache hit ratio in the `ga_run_history.csv` summary. If the capacity lookup hit ratio falls below 90%, it indicates that coordinate snapping (`GridStep`) should be enabled or set larger to maximize database cache reuse.
*   Audit `ga_failure_breakdown.csv` to see whether failures are dominated by reach (`OutOfRadius`) or load capacity (`InsufficientCapacity`). If capacity failures dominate, adjust `SafetyFactor` or change the selected crane model specification.


---

## Consolidated from: Genetic Algorithm Optimization Reference Guide (domain/ga-settings-guide.md)


This document provides a comprehensive technical reference for the Genetic Algorithm (GA) settings and hardcoded constants within the crane layout optimization engine.

---

## 1. GASettings Parameter Analysis

This section analyzes the properties of [GASettings.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs). For each parameter, we outline its default value, recommended range, engineering impact, performance impact, and risks.

### Population & Generation Parameters

#### 1. `PopulationSize`
*   **Default:** `100` (init) | **Recommended Range:** `50 - 500`
*   **Engineering Impact:** Controls candidate solution diversity. Higher values explore more areas of the site and reduce premature convergence.
*   **Performance Impact:** Linear scaling $O(P)$. Doubling the size doubles the execution time of each generation.
*   **Risks:** Too small ($<30$) leads to loss of diversity and premature convergence on poor local optima. Too large ($>500$) causes performance lag.

#### 2. `MaxGenerations`
*   **Default:** `200` (init) | **Recommended Range:** `100 - 1000`
*   **Engineering Impact:** Defines the absolute search cutoff.
*   **Performance Impact:** Linear scaling $O(G)$. Sets the maximum possible execution duration.
*   **Risks:** Too low ($<50$) cuts off the search before convergence. Too high ($>1000$) wastes CPU cycles unless early stopping triggers.

#### 3. `EarlyStoppingGenerations` (Stagnation Limit)
*   **Default:** `20` | **Recommended Range:** `10 - 50`
*   **Engineering Impact:** Defines how many generations of stagnation on the best solution are tolerated before terminating.
*   **Performance Impact:** Improves average runtimes significantly by terminating search early once convergence is reached.
*   **Risks:** Too small ($<5$) causes premature termination on flat fitness regions. Too large ($>50$) wastes computations.

#### 4. `ConvergenceThresholdPercent`
*   **Default:** `0.0` (disabled) | **Recommended Range:** `0.0 - 1.0`
*   **Engineering Impact:** Relative fitness improvement threshold.
*   **Performance Impact:** Accelerates termination when progress is negligible.
*   **Risks:** Too large ($>2\%$) triggers premature termination on slow convergence phases.

---

### Genetic Operator Rates & Parameters

#### 5. `CrossoverRate`
*   **Default:** `0.8` | **Recommended Range:** `0.6 - 0.95`
*   **Engineering Impact:** Probability of coordinate exchange between two parents. High values promote exploitation of good setups.
*   **Performance Impact:** Minimal. Swap logic is highly optimized.
*   **Risks:** Too low ($<0.4$) prevents recombination, reducing GA to random search with elitism. Too high ($>0.98$) degrades good solutions too fast.

#### 6. `MutationRate`
*   **Default:** `0.15` (init) | **Recommended Range:** `0.05 - 0.3`
*   **Engineering Impact:** Probability of mutating a chromosome. High values prevent local optimum traps.
*   **Performance Impact:** Minimal. Snapping and clipping are fast.
*   **Risks:** Too low ($<0.02$) leads to stagnation. Too high ($>0.4$) disrupts convergence, turning GA into random walk.

#### 7. `MutationSigma`
*   **Default:** `0.05` | **Recommended Range:** `0.01 - 0.2`
*   **Engineering Impact:** Base perturbation radius. Values $\le1.0$ are treated as a ratio of `MaxRadiusForNormalisation`.
*   **Performance Impact:** None.
*   **Risks:** Too small leads to local entrapment. Too large behaves like random coordinates generator.

#### 8. `MutationExplorationMultiplier`
*   **Default:** `2.0` | **Recommended Range:** `1.0 - 5.0`
*   **Engineering Impact:** Scaling multiplier applied to mutation range to push search boundaries.
*   **Performance Impact:** None.
*   **Risks:** Excessive values skip over local adjustments.

#### 9. `EnableAdaptiveSigma`
*   **Default:** `false` | **Recommended Range:** `true` or `false`
*   **Engineering Impact:** Enables generation-dependent cooling schedule for sigma: $\sigma_{\text{gen}} = \sigma \cdot \text{progress}^{\alpha} + \sigma_{\text{floor}}$.
*   **Performance Impact:** None.
*   **Risks:** None, but requires tuning cooling parameters.

#### 10. `SigmaCoolingExponent`
*   **Default:** `1.5` | **Recommended Range:** `1.0 - 3.0`
*   **Engineering Impact:** Controls decay curve shape of adaptive mutation sigma.
*   **Performance Impact:** None.
*   **Risks:** Too high ($>3.0$) decays the search range too early.

#### 11. `MinimumMutationSigma`
*   **Default:** `0.5` | **Recommended Range:** `0.1 - 2.0` (meters)
*   **Engineering Impact:** Sets lower bound floor for adaptive sigma to prevent complete search freeze.
*   **Performance Impact:** None.
*   **Risks:** Too small prevents local adjustments near the end of execution.

#### 12. `PositionSamplingAttempts`
*   **Default:** `80` | **Recommended Range:** `20 - 150`
*   **Engineering Impact:** Maximum attempts to sample a valid, collision-free coordinate.
*   **Performance Impact:** High in dense sites. More attempts increase collision check iterations.
*   **Risks:** Too low ($<10$) fails to find valid spots in highly congested layouts. Too high ($>200$) stalls execution when site is over-constrained.

---

### Mechanical, Physical & Safety Parameters

#### 13. `MaxCranePositions`
*   **Default:** `3` | **Recommended Range:** `1 - 10`
*   **Engineering Impact:** Hard limit on the maximum number of relocation setups per solution.
*   **Performance Impact:** High. Chromosome length scales evaluation arrays.
*   **Risks:** Setting this lower than physically required makes solutions infeasible (coverage cannot reach 100%).

#### 14. `CraneFootprintRadius`
*   **Default:** Set by input builder | **Recommended Range:** Derived from Crane dimensions
*   **Engineering Impact:** Defines outrigger/body clearance. Footprint must fit fully within allowed zones.
*   **Performance Impact:** None.
*   **Risks:** If set too small, outriggers may collide with physical site obstacles.

#### 15. `MinCraneSpacing` (MinimumRelocationDistance)
*   **Default:** `10.0` (meters) | **Recommended Range:** `5 - 30`
*   **Engineering Impact:** Minimum distance to treat setups as separate locations.
*   **Performance Impact:** None.
*   **Risks:** Setting too small ($<3\text{m}$) leads to redundant setups.

#### 16. `SafetyFactor`
*   **Default:** `0.95` | **Recommended Range:** `0.75 - 1.0`
*   **Engineering Impact:** Safe limit applied to chart capacity. Capacity is scaled by `DeratingRatio * SafetyFactor`.
*   **Performance Impact:** None.
*   **Risks:** Too close to 1.0 reduces safety margins. Too low ($<0.7$) makes crane placements conservative and expensive.

#### 17. `DeratingRatio`
*   **Default:** `0.9` | **Recommended Range:** `0.7 - 1.0`
*   **Engineering Impact:** Capacity derating.
*   **Performance Impact:** None.
*   **Risks:** Overriding without analysis risk over-constraining the load.

#### 18. `WireLength`
*   **Default:** `2.0` (meters) | **Recommended Range:** `1.0 - 10.0`
*   **Engineering Impact:** Added rigging wire length. Increases required lift heights.
*   **Performance Impact:** None.
*   **Risks:** Under-estimation leads to crane hook collisions.

#### 19. `AdditionalWt`
*   **Default:** `0.0` (tons) | **Recommended Range:** `0.0 - 5.0`
*   **Engineering Impact:** Adds rigging block/slings mass to the target weights.
*   **Performance Impact:** None.
*   **Risks:** Under-estimation leads to crane overloading.

---

### Offset Parameters

#### 20. `SlewOffsetX` / `SlewOffsetY` / `SlewOffsetZ`
*   **Default:** `0.0` | **Recommended Range:** Crane model dependent
*   **Engineering Impact:** Slew center mechanical offsets from crane base coordinate.
*   **Performance Impact:** None.
*   **Risks:** Mismatch results in incorrect operating radius calculations.

#### 21. `BoomHingeOffsetX` / `BoomHingeOffsetY` / `BoomHingeOffsetZ`
*   **Default:** `0.0` | **Recommended Range:** Crane model dependent
*   **Engineering Impact:** Boom hinge offsets from base coordinate for hoist boom length checks.
*   **Performance Impact:** None.
*   **Risks:** Direct impact on calculated boom length.

---

### Parallel & Optimization Strategy Parameters

#### 22. `EnableConcurrentOptimization`
*   **Default:** `true` | **Recommended Range:** `true` or `false`
*   **Engineering Impact:** When true, runs simultaneous multi-setup GA. When false, runs greedy set-cover iterations.
*   **Performance Impact:** Greedy set-cover iteration runs multiple small GAs sequentially. Concurrent optimization runs a single GA but with longer chromosomes.
*   **Risks:** Greedy set-cover fails to account for coordinated setups, yielding suboptimal global solutions.

#### 23. `EnableProfiling`
*   **Default:** `true` | **Recommended Range:** `true` or `false`
*   **Engineering Impact:** Toggles performance profile tracking via `GAProfiler`.
*   **Performance Impact:** Negligible overhead (ticks accumulation).
*   **Risks:** Disabling removes detailed diagnostic metrics.

#### 24. `RandomSeed`
*   **Default:** `42` | **Recommended Range:** `null` (random) or any integer (deterministic)
*   **Engineering Impact:** Guarantees repeatable optimizations.
*   **Performance Impact:** None.
*   **Risks:** Keeping static prevents discovering alternative placements for identical inputs.

---

## 2. Hardcoded Values Audit & Inventory

This section audits all hardcoded constants identified in the optimization layer. They are classified into **Critical Optimization Parameters**, **Engineering Constants**, and **Magic Numbers** with recommendations.

### A. Critical Optimization Parameters (Should Be Configurable)

These parameters affect solver quality and constraint validations directly. They should be exposed in `GASettings`.

#### 1. Heuristic Seeding Split Ratio (`0.30` / `70%`)
*   **Location:** [GAInitializer.cs Line 52](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAInitializer.cs#L52)
*   **Current State:** `0.30` ratio dictates that 30% of initial populations are seeded using Morton Z-order centroids, and 70% randomly.
*   **Engineering Impact:** Governs initialization quality and diversity. Highly structured sites benefit from heuristic seeds; simple sites benefit from random exploration.
*   **Recommendation:** Move to a new property `GASettings.HeuristicSeedingRatio` (default `0.30`).

#### 2. Crossover Footprint Multiplier (`2.0`)
*   **Location:** [CrossoverOperator.cs Line 36](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/CrossoverOperator.cs#L36)
*   **Current State:** Restricts crossover duplicate points using `2.0 * CraneFootprintRadius` distance.
*   **Engineering Impact:** Directly prevents offspring from retaining overlapping setups.
*   **Recommendation:** Move to `GASettings.CrossoverOverlapDistanceMultiplier` (default `2.0`).

#### 3. Crossover Footprint Fallback (`3.0`)
*   **Location:** [CrossoverOperator.cs Line 37](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/CrossoverOperator.cs#L37)
*   **Current State:** Fallback distance limit of `3.0` meters if footprint is 0.
*   **Engineering Impact:** Governs recombination spacing when footprint data is missing.
*   **Recommendation:** Move to `GASettings.CrossoverOverlapDistanceFallback` (default `3.0`).

#### 4. Mutation Operator Probabilities (`0.80` / `0.90` / `0.10`)
*   **Location:** [MutationOperator.cs Lines 44, 90](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/MutationOperator.cs#L44)
*   **Current State:** Hardcoded 80% perturbation drift, 10% addition (remainder under 90%), and 10% deletion mutation rates.
*   **Engineering Impact:** Controls layout change exploration. High addition rates increase setups; high deletion rates prune them.
*   **Recommendation:** Move to `GASettings.MutationDriftProbability` (default `0.80`), `GASettings.MutationAddProbability` (default `0.10`), and `GASettings.MutationRemoveProbability` (default `0.10`).

#### 5. Elitism Selection Preservation Rate (`0.10`)
*   **Location:** [ReplacementStrategy.cs Line 23](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/ReplacementStrategy.cs#L23)
*   **Current State:** Hardcoded `0.10` (10%) preservation of elite parents during population replacement.
*   **Engineering Impact:** High elitism preserves high-fitness solutions but leads to premature convergence. Low rates risk losing optimal placements.
*   **Recommendation:** Move to `GASettings.ElitismRate` (default `0.10`).

#### 6. Greedy Set-Cover Limits (`maxPoints = 4`, `targetCoverage = 0.95`, `minImprovement = 0.05`)
*   **Location:** [CranePlacementEngine.cs Lines 75-77](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/PlacementOrchestration/CranePlacementEngine.cs#L75-77)
*   **Current State:** Defines greedy set-cover cutoffs.
*   **Engineering Impact:** Governs loop iterations when concurrent engine is disabled.
*   **Recommendation:** Move to `GASettings.GreedyMaxSetups`, `GASettings.GreedyTargetCoverage`, and `GASettings.GreedyMinImprovement`.

---

### B. Engineering Constants (Acceptable as Hardcoded)

These are numerical tolerances or constants derived from mathematics or physical units. They are acceptable as constants.

#### 7. Floating-Point Comparison Epsilon (`1e-12` / `1e-9` / `1e-6`)
*   **Locations:**
    *   `1e-12` in [SpatialMath.PointInPolygon](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/Utilities/SpatialMath.cs#L55) (division protection)
    *   `1e-9` in [ZoneGeometryService.SegmentsIntersect2D](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Services/ZoneGeometryService.cs) (collinearity checks)
    *   `1e-6` in [ZoneGeometryService.ValidateTriangulation](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Services/ZoneGeometryService.cs) (degenerate triangles)
*   **Rationale:** Standard floating-point tolerance offsets. Configurable parameters would introduce unnecessary complexity.
*   **Recommendation:** Remain hardcoded as `private const` or `readonly` constants.

#### 8. Snapping Snug Epsilon Tolerances (`0.001` / `0.005`)
*   **Locations:**
    *   `0.001` point equality tolerance in `ZoneGeometryService`.
    *   `0.005` area mismatch tolerance in `ZoneGeometryService`.
*   **Rationale:** Mesh boundary precision metrics.
*   **Recommendation:** Remain hardcoded.

#### 9. Morton Coordinate Scale Limits (`1023` / 10-bit range)
*   **Location:** [GAInitializer.cs Lines 222-223](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAInitializer.cs#L222-223)
*   **Current State:** Snaps bounding boxes to $[0, 1023]$ range to perform Z-order spatial partitioning.
*   **Rationale:** Integer normalization range required for 20-bit Morton coordinate bit shifting.
*   **Recommendation:** Remain hardcoded but document constraints inside the class.

---

### C. Magic Numbers (Require Review & Justification)

These are values embedded directly in code without explanation or constants mapping.

#### 10. Selection Competitors Count (`3`)
*   **Location:** [TournamentSelection.cs Line 23](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/TournamentSelection.cs#L23)
*   **Current State:** Private constant `TournamentSize = 3` determines selection competitors.
*   **Engineering Impact:** Controls tournament pressure.
*   **Recommendation:** Move to `GASettings.TournamentSize` (default `3`).

#### 11. Special Case Penalty Weight (`1.0`)
*   **Location:** [FitnessEvaluator.cs Line 76](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L76)
*   **Current State:** Deducts 1.0 weight penalty for SpecialCase lift failures.
*   **Rationale:** Hardcoded deduction that behaves as an arbitrary scaling factor.
*   **Recommendation:** Move to `GASettings.SpecialCasePenaltyWeight` (default `1.0`).

#### 12. Normalization Detection Check Tolerance (`0.0001`)
*   **Location:** [OptimizationInputBuilder.cs Line 87](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/OptimizationScope/OptimizationInputBuilder.cs#L87)
*   **Current State:** Checks if `settings.DeratingRatio` is close to the default `0.9` (within `0.0001` epsilon) to auto-apply crane-specific specifications.
*   **Rationale:** Epsilon check used to detect whether settings retain their uninitialized defaults.
*   **Recommendation:** Define as a named constant `DefaultDetectionEpsilon` within the builder.


---

