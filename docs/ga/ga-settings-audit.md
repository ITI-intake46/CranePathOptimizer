# GASettings Technical Audit Report

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
